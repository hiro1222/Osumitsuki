using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ボスエネミー「鎧墨袋（Boss_SB）」フェーズ1
///
/// 【状態遷移】
/// Idle → Chase → Charge → Tackle → Stop → Chase
/// 木箱衝突時: Stun（10秒・攻撃ヒットでタイマーリセット）
/// お墨付き完了時: Roar（咆哮）→ Chase
///
/// 【仕様変更・追加時のガイド】
/// ・新しい状態を追加 → BossState に追加 → Enter/Update メソッドを追加
/// ・既存の状態を変更 → 対応する Enter/Update メソッドだけ変更
/// ・ステータス変更   → Inspector で調整（[SerializeField]）
///
/// 【外部から呼ぶ関数】
/// ・StartBossBattle()  : BossAreaTriggerから呼ぶ
/// ・ReceiveInk()       : EnemyHitReceiverから呼ぶ（スタン中のみ有効）
/// ・GetIsAlly()        : 外部からお墨付き状態を確認する
/// ・NotifyHitCrate()   : 木箱スクリプトからタックル衝突時に呼ぶ
/// </summary>
public class Boss_SB : MonoBehaviour, IF_Enemy
{
    // ====================================================================
    //  状態定義
    //  ★新しい状態を追加するときはここに追加する
    // ====================================================================

    private enum BossState
    {
        Idle,    // 待機（ボス戦開始前）
        Chase,   // 追従
        Charge,  // チャージ（タックル前の溜め）
        Tackle,  // タックル（急直進）
        Stop,    // 停止（タックル後3秒）
        Stun,    // スタン（木箱衝突・10秒）
        Roar,    // 咆哮
        Defeated // 撃破
    }

    // ====================================================================
    //  設定（Inspector）
    //  ★ステータス変更はここで行う
    // ====================================================================

    [Header("── プレイヤー参照 ──")]
    [SerializeField] private Transform player;

    [Header("── モデル参照 ──")]
    [Tooltip("距離判定の基準にするモデルのTransform（子オブジェクト）")]
    [SerializeField] private Transform bodyTransform;

    [Header("── 移動 ──")]
    [Tooltip("追従速度")]
    [SerializeField] private float chaseSpeed = 3f;
    [Tooltip("タックル速度")]
    [SerializeField] private float tackleSpeed = 12f;
    [Tooltip("追従中の衝突判定距離")]
    [SerializeField] private float collideDistance = 2f;
    [Tooltip("タックル中の衝突判定距離")]
    [SerializeField] private float tackleCollideDistance = 3.5f;
    [Tooltip("タックルの最大移動距離（m）")]
    [SerializeField] private float tackleMaxDistance = 20f;

    [Header("── タックルタイミング ──")]
    [Tooltip("追従開始からタックルまでの待機時間（最小・秒）")]
    [SerializeField] private float tackleDelayMin = 3f;
    [Tooltip("追従開始からタックルまでの待機時間（最大・秒）")]
    [SerializeField] private float tackleDelayMax = 8f;

    [Header("── チャージ ──")]
    [Tooltip("チャージ時間（秒）")]
    [SerializeField] private float chargeDuration = 1f;

    [Header("── 停止（タックル後） ──")]
    [Tooltip("タックル後の停止時間（秒）")]
    [SerializeField] private float stopDuration = 3f;

    [Header("── スタン ──")]
    [Tooltip("スタン時間（秒）")]
    [SerializeField] private float stunDuration = 10f;
    [Tooltip("攻撃ヒット時にタイマーをリセットするか")]
    [SerializeField] private bool resetStunTimerOnHit = true;

    [Header("── ステータス ──")]
    [Tooltip("攻撃力（ノックバック距離 = 攻撃力 × 0.5m）攻撃力:5")]
    [SerializeField] private float attackPower = 5f;
    [Tooltip("上方向のノックバック強さ")]
    [SerializeField] private float knockbackUpForce = 5f;
    [Tooltip("ノックバックの持続時間（秒）")]
    [SerializeField] private float knockbackDuration = 0.8f;
    [Tooltip("お墨付きに必要な塗り回数")]
    [SerializeField] private int requiredInkCount = 10;
    [Tooltip("お墨付き時にプレイヤーのインクを回復する量")]
    [SerializeField] private float inkRecovery = 2f;

    [Header("── 咆哮 ──")]
    [Tooltip("咆哮量・直径（m）")]
    [SerializeField] private float roarRange = 5f;
    [Tooltip("咆哮時にジャンプするフィールド中央の座標")]
    [SerializeField] private Transform fieldCenter;
    [Tooltip("咆哮ジャンプの高さ")]
    [SerializeField] private float roarJumpHeight = 5f;
    [Tooltip("咆哮ジャンプの速度")]
    [SerializeField] private float roarJumpSpeed = 10f;

    [Header("── 咆哮リセット対象 ──")]
    [Tooltip("ボス自身のPaintableSurface")]
    [SerializeField] private List<PaintableSurface> bossSurfaces = new List<PaintableSurface>();
    [Tooltip("ボスエリア内オブジェクトのPaintableSurface")]
    [SerializeField] private List<PaintableSurface> areaSurfaces = new List<PaintableSurface>();

    [Header("── 木箱 ──")]
    [Tooltip("木箱オブジェクト（咆哮で飛ばす対象）")]
    [SerializeField] private List<GameObject> crateObjects = new List<GameObject>();
    [Tooltip("木箱を飛ばす方向と強さ")]
    [SerializeField] private Vector3 crateBlastForce = new Vector3(0f, 10f, 20f);

    [Header("── 地面追従 ──")]
    [SerializeField] private float groundFollowSpeed = 10f;

    // ====================================================================
    //  内部状態（全てprivate）
    // ====================================================================

    private BossState state = BossState.Idle;
    private int inkHitCount = 0;
    private bool isAlly = false;

    // Chase用
    private float tackleTimer = 0f;
    private float tackleDelay = 0f;

    // Charge用
    private float chargeTimer = 0f;
    private Vector3 chargeTargetDir;

    // Tackle用
    private Vector3 tackleDirection;
    private Vector3 tackleStartPos;

    // Stop用
    private float stopTimer = 0f;

    // Stun用
    private float stunTimer = 0f;

    // ノックバック
    private CharacterController playerController;
    private PlayerMove playerMove;
    private Vector3 knockbackVelocity;
    private float knockbackTimer;
    private bool isPlayerKnockedBack;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Start()
    {
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                player = go.transform;
                playerController = go.GetComponent<CharacterController>();
                if (playerController == null)
                    playerController = go.GetComponentInChildren<CharacterController>();
                if (playerController == null)
                    playerController = go.GetComponentInParent<CharacterController>();
            }
        }
        else
        {
            playerController = player.GetComponent<CharacterController>();
            if (playerController == null)
                playerController = player.GetComponentInChildren<CharacterController>();
            if (playerController == null)
                playerController = player.GetComponentInParent<CharacterController>();
        }

        if (player != null)
        {
            playerMove = player.GetComponent<PlayerMove>();
            if (playerMove == null)
                playerMove = player.GetComponentInChildren<PlayerMove>();
            if (playerMove == null)
                playerMove = player.GetComponentInParent<PlayerMove>();
        }

        state = BossState.Idle;
    }

    // ====================================================================
    //  毎フレーム
    //  ★新しい状態を追加したらここにcaseを追加する
    // ====================================================================

    private void Update()
    {
        if (player == null) return;

        FollowGround();
        UpdatePlayerKnockback();

        switch (state)
        {
            case BossState.Idle: break;
            case BossState.Chase: UpdateChase(); break;
            case BossState.Charge: UpdateCharge(); break;
            case BossState.Tackle: UpdateTackle(); break;
            case BossState.Stop: UpdateStop(); break;
            case BossState.Stun: UpdateStun(); break;
            case BossState.Roar: break; // コルーチンで処理
            case BossState.Defeated: break;
        }
    }

    // ====================================================================
    //  外部から呼ぶ関数
    //  ★他スクリプトとの連携はここに追加する
    // ====================================================================

    /// <summary>ボス戦を開始する（BossAreaTriggerから呼ぶ）</summary>
    public void StartBossBattle()
    {
        if (state != BossState.Idle) return;
        inkHitCount = 0;
        Debug.Log("[Boss_SB] ボス戦開始！");
        EnterChase();
    }

    /// <summary>
    /// 墨を塗られたときの処理（EnemyHitReceiverから呼ぶ）
    /// スタン中のみ有効・スタン中以外は規定数-1までしか増えない
    /// </summary>
    public void ReceiveInk()
    {
        if (isAlly) return;
        if (state == BossState.Roar) return;
        if (state == BossState.Defeated) return;

        // スタン中以外は規定数-1までしか増えない
        if (state != BossState.Stun)
        {
            if (inkHitCount >= requiredInkCount - 1) return;
            inkHitCount++;
            Debug.Log($"[Boss_SB] 塗り回数: {inkHitCount} / {requiredInkCount - 1}（スタン外上限）");
            return;
        }

        // スタン中
        inkHitCount++;
        Debug.Log($"[Boss_SB] 塗り回数(スタン中): {inkHitCount} / {requiredInkCount}");

        // スタンタイマーをリセット
        if (resetStunTimerOnHit)
            stunTimer = 0f;

        if (inkHitCount >= requiredInkCount)
        {
            inkHitCount = 0;
            EnterRoar();
        }
    }

    /// <summary>お墨付き状態を返す</summary>
    public bool GetIsAlly() => isAlly;

    /// <summary>
    /// 木箱に衝突したときに呼ぶ（木箱スクリプトから呼ぶ）
    /// タックル中のみスタンに移行する
    /// </summary>
    public void NotifyHitCrate()
    {
        if (state != BossState.Tackle) return;
        Debug.Log("[Boss_SB] 木箱に衝突！スタン開始");
        EnterStun();
    }

    // ====================================================================
    //  Chase（追従）
    // ====================================================================

    private void EnterChase()
    {
        state = BossState.Chase;
        tackleTimer = 0f;
        tackleDelay = Random.Range(tackleDelayMin, tackleDelayMax);
        Debug.Log($"[Boss_SB] Chase開始。タックルまで{tackleDelay:F1}秒");
    }

    private void UpdateChase()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        tackleTimer += Time.deltaTime;
        if (tackleTimer >= tackleDelay)
        {
            EnterCharge();
            return;
        }

        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;
        Vector3 playerPos = player.position;
        bossPos.y = 0f;
        playerPos.y = 0f;
        float dist = Vector3.Distance(bossPos, playerPos);

        if (dist > collideDistance)
            transform.position += toPlayer.normalized * chaseSpeed * Time.deltaTime;
        else
        {
            ApplyKnockbackToPlayer();
            EnterStop();
            return;
        }

        LookAt(toPlayer);
    }

    // ====================================================================
    //  Charge（チャージ）
    // ====================================================================

    private void EnterCharge()
    {
        state = BossState.Charge;
        chargeTimer = 0f;
        chargeTargetDir = (player.position - transform.position).normalized;
        chargeTargetDir.y = 0f;
        Debug.Log("[Boss_SB] チャージ開始！");
    }

    private void UpdateCharge()
    {
        chargeTimer += Time.deltaTime;
        LookAt(chargeTargetDir);

        if (chargeTimer >= chargeDuration)
            EnterTackle();
    }

    // ====================================================================
    //  Tackle（タックル）
    // ====================================================================

    private void EnterTackle()
    {
        state = BossState.Tackle;
        tackleDirection = chargeTargetDir;
        tackleStartPos = transform.position;
        Debug.Log("[Boss_SB] タックル開始！");
    }

    private void UpdateTackle()
    {
        transform.position += tackleDirection * tackleSpeed * Time.deltaTime;
        LookAt(tackleDirection);

        // プレイヤーとの衝突判定
        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;
        Vector3 playerPos = player.position;
        bossPos.y = 0f;
        playerPos.y = 0f;
        float dist = Vector3.Distance(bossPos, playerPos);

        if (dist <= tackleCollideDistance)
        {
            ApplyKnockbackToPlayer();
            EnterStop();
            return;
        }

        // 最大距離で停止
        float travelDist = Vector3.Distance(transform.position, tackleStartPos);
        if (travelDist > tackleMaxDistance)
            EnterStop();
    }

    // ====================================================================
    //  Stop（停止）
    // ====================================================================

    private void EnterStop()
    {
        state = BossState.Stop;
        stopTimer = 0f;
        Debug.Log("[Boss_SB] 停止");
    }

    private void UpdateStop()
    {
        stopTimer += Time.deltaTime;
        if (stopTimer >= stopDuration)
            EnterChase();
    }

    // ====================================================================
    //  Stun（スタン・木箱衝突）
    //  ★スタンの挙動を変えたいときはここを編集
    // ====================================================================

    private void EnterStun()
    {
        state = BossState.Stun;
        stunTimer = 0f;
        Debug.Log("[Boss_SB] スタン！10秒間停止");
    }

    private void UpdateStun()
    {
        stunTimer += Time.deltaTime;

        if (stunTimer >= stunDuration)
        {
            // スタン解除（塗りは引き継ぎ・リセットしない）
            Debug.Log("[Boss_SB] スタン解除。塗り引き継ぎ");
            EnterChase();
        }
    }

    // ====================================================================
    //  Roar（咆哮）
    //  ★咆哮の挙動を変えたいときはここを編集
    // ====================================================================

    private void EnterRoar()
    {
        state = BossState.Roar;
        Debug.Log("[Boss_SB] 咆哮！");
        StartCoroutine(RoarCoroutine());
    }

    private IEnumerator RoarCoroutine()
    {
        // フィールド中央にジャンプ
        if (fieldCenter != null)
        {
            yield return StartCoroutine(JumpToCenter());
        }

        yield return new WaitForSeconds(0.5f);

        // ボス自身の墨をリセット
        foreach (var surface in bossSurfaces)
        {
            if (surface == null || !surface.enabled) continue;
            try { InkPaintService.ClearAll(surface); } catch { }
        }

        // エリア内オブジェクトの墨をリセット
        foreach (var surface in areaSurfaces)
        {
            if (surface == null || !surface.enabled) continue;
            try { InkPaintService.ClearAll(surface); } catch { }
        }

        // フィールドを円形にリセット
        ResetFieldInRange(roarRange * 0.5f);

        // 木箱を飛ばす
        BlastCrates();

        Debug.Log($"[Boss_SB] 咆哮リセット完了。範囲: {roarRange}m（直径）");

        yield return new WaitForSeconds(0.5f);

        // 塗り回数リセット
        inkHitCount = 0;
        EnterChase();
    }

    /// <summary>フィールド中央にジャンプするコルーチン</summary>
    private IEnumerator JumpToCenter()
    {
        Vector3 startPos = transform.position;
        Vector3 endPos = fieldCenter.position;
        float elapsed = 0f;
        float duration = Vector3.Distance(startPos, endPos) / roarJumpSpeed;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            // 放物線を描いてジャンプ
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * roarJumpHeight;
            transform.position = pos;

            yield return null;
        }

        transform.position = endPos;
    }

    /// <summary>木箱を画面外に飛ばす</summary>
    private void BlastCrates()
    {
        foreach (var crate in crateObjects)
        {
            if (crate == null) continue;

            var rb = crate.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Rigidbodyがあれば力を加える
                rb.AddForce(crateBlastForce, ForceMode.Impulse);
            }
            else
            {
                // なければ非表示にする
                crate.SetActive(false);
            }

            Debug.Log($"[Boss_SB] 木箱を飛ばす: {crate.name}");
        }
    }

    private void ResetFieldInRange(float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, radius, ~0, QueryTriggerInteraction.Collide);

        foreach (var col in colliders)
        {
            var surface = col.GetComponent<PaintableSurface>()
                       ?? col.GetComponentInParent<PaintableSurface>();
            if (surface == null || !surface.enabled) continue;
            try { InkPaintService.ClearAll(surface); } catch { }
        }
    }

    // ====================================================================
    //  Defeated（撃破）
    // ====================================================================

    private void EnterDefeated()
    {
        state = BossState.Defeated;
        Debug.Log("[Boss_SB] ボス撃破！");
        Destroy(gameObject, 1f);
    }

    // ====================================================================
    //  ノックバック
    // ====================================================================

    private void ApplyKnockbackToPlayer()
    {
        if (isPlayerKnockedBack) return;
        if (playerController == null) return;

        float knockbackDistance = attackPower * 0.5f;
        Vector3 knockDir = (player.position - transform.position).normalized;
        knockDir.y = 0f;

        knockbackVelocity = knockDir * knockbackDistance * 10f
                          + Vector3.up * knockbackUpForce;
        knockbackTimer = 0f;
        isPlayerKnockedBack = true;

        if (playerMove != null)
        {
            playerMove.SetExternalGravityEnabled(false);
            playerMove.ClearVerticalVelocity();
        }
    }

    private void UpdatePlayerKnockback()
    {
        if (!isPlayerKnockedBack) return;
        if (playerController == null) return;

        knockbackVelocity.y += Physics.gravity.y * Time.deltaTime;
        playerController.Move(knockbackVelocity * Time.deltaTime);
        knockbackTimer += Time.deltaTime;

        if (knockbackTimer >= knockbackDuration)
        {
            isPlayerKnockedBack = false;
            if (playerMove != null)
                playerMove.SetExternalGravityEnabled(true);
        }
    }

    // ====================================================================
    //  地面追従
    // ====================================================================

    private void FollowGround()
    {
        if (Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            Vector3.down, out RaycastHit hit, 10f,
            ~0, QueryTriggerInteraction.Collide))
        {
            if (hit.collider.gameObject == gameObject) return;

            float newY = Mathf.Lerp(
                transform.position.y, hit.point.y, groundFollowSpeed * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    // ====================================================================
    //  ユーティリティ
    // ====================================================================

    private void LookAt(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;
        direction.y = 0f;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            10f * Time.deltaTime);
    }

    // ====================================================================
    //  Gizmos
    // ====================================================================

    private void OnDrawGizmosSelected()
    {
        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bossPos, collideDistance);

        Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
        Gizmos.DrawWireSphere(transform.position, roarRange * 0.5f);
    }
}