using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ボスエネミー「墨袋ボス（Boss_SB）」
///
/// 【状態遷移】
/// Idle → Chase → Charge（溜め）→ Tackle（急直進）→ Stop → Chase
/// お墨付き時: Sealed（6秒停止）→ Roar（咆哮）→ Chase
///
/// 【仕様変更・追加時のガイド】
/// ・新しい状態を追加 → BossState に追加 → Enter/Update メソッドを追加
/// ・既存の状態を変更 → 対応する Enter/Update メソッドだけ変更
/// ・ステータス変更   → Inspector で調整（[SerializeField]）
/// ・他スクリプトとの連携 → 外部から呼ぶ関数セクションに追加
///
/// 【外部から呼ぶ関数】
/// ・StartBossBattle()   : BossAreaTriggerから呼ぶ
/// ・ReceiveInk()        : EnemyHitReceiverから呼ぶ
/// ・GetIsAlly()         : 外部からお墨付き状態を確認する
/// ・ReceiveCannonBall() : 砲弾担当から呼ぶ
/// </summary>
public class Boss_SB : MonoBehaviour, IF_Enemy
{
    // ====================================================================
    //  状態定義
    //  新しい状態を追加するときはここに追加する
    // ====================================================================

    private enum BossState
    {
        Idle,    // 待機（ボス戦開始前）
        Chase,   // 追従（プレイヤーを追いかける）
        Charge,  // チャージ（タックル前の溜め・プレイヤー方向を固定）
        Tackle,  // タックル（急直進・方向は固定）
        Stop,    // 停止（タックル後）
        Sealed,  // お墨付き停止
        Roar,    // 咆哮（リセット）
        Defeated // 撃破
    }

    // ====================================================================
    //  設定（Inspector）
    //  ★ステータス変更はここで行う
    // ====================================================================

    [Header("── プレイヤー参照 ──")]
    [SerializeField] private Transform player;

    [Header("── 移動 ──")]
    [Tooltip("追従速度")]
    [SerializeField] private float chaseSpeed = 3f;
    [Tooltip("タックル速度")]
    [SerializeField] private float tackleSpeed = 12f;
    [Tooltip("追従中の衝突判定距離")]
    [SerializeField] private float collideDistance = 2f;
    [Tooltip("タックル中の衝突判定距離")]
    [SerializeField] private float tackleCollideDistance = 3.5f;

    [Header("── モデル参照 ──")]
    [Tooltip("距離判定の基準にするモデルのTransform（子オブジェクト）")]
    [SerializeField] private Transform bodyTransform;

    [Header("── タックルタイミング ──")]
    [Tooltip("追従開始からタックルまでの待機時間（最小・秒）")]
    [SerializeField] private float tackleDelayMin = 3f;
    [Tooltip("追従開始からタックルまでの待機時間（最大・秒）")]
    [SerializeField] private float tackleDelayMax = 8f;

    [Header("── チャージ ──")]
    [Tooltip("チャージ時間（秒）この間プレイヤー方向を向いて溜める")]
    [SerializeField] private float chargeDuration = 1f;

    [Header("── 停止（タックル後） ──")]
    [Tooltip("タックル後の停止時間（秒）")]
    [SerializeField] private float stopDuration = 3f;

    [Header("── ステータス ──")]
    [Tooltip("攻撃力（ノックバック距離 = 攻撃力 × 0.5m）攻撃力:5")]
    [SerializeField] private float attackPower = 5f;
    [Tooltip("上方向のノックバック強さ")]
    [SerializeField] private float knockbackUpForce = 5f;
    [Tooltip("お墨付き停止時間（秒）")]
    [SerializeField] private float sealedDuration = 6f;
    [Tooltip("回復量")]
    [SerializeField] private float inkRecovery = 2f;

    [Header("── フェーズ別ステータス ──")]
    [Tooltip("必要塗り回数（フェーズ1・2・3）")]
    [SerializeField] private int[] requiredInkCounts = { 10, 12, 15 };
    [Tooltip("咆哮量・直径（フェーズ1・2・3）単位:m")]
    [SerializeField] private float[] roarRanges = { 5f, 7f, 10f };

    [Header("── 咆哮リセット対象 ──")]
    [Tooltip("ボス自身のPaintableSurface")]
    [SerializeField] private List<PaintableSurface> bossSurfaces = new List<PaintableSurface>();
    [Tooltip("ボスエリア内オブジェクトのPaintableSurface")]
    [SerializeField] private List<PaintableSurface> areaSurfaces = new List<PaintableSurface>();

    [Header("── 地面追従 ──")]
    [SerializeField] private float groundFollowSpeed = 10f;

    // ====================================================================
    //  内部状態（全てprivate）
    // ====================================================================

    private BossState state = BossState.Idle;
    private int currentPhase = 0;
    private int inkHitCount = 0;
    private int cannonBallHitCount = 0;
    private bool isAlly = false;
    private int roarCount = 0;

    // Chase用
    private float tackleTimer = 0f;
    private float tackleDelay = 0f;

    // Charge用
    private float chargeTimer = 0f;
    private Vector3 chargeTargetDir; // チャージ中に固定するプレイヤー方向

    // Tackle用
    private Vector3 tackleDirection; // タックル開始時に固定した方向
    private Vector3 tackleStartPos; // タックル開始位置

    // Stop用
    private float stopTimer = 0f;

    // Sealed用
    private float sealedTimer = 0f;

    // ノックバック
    private CharacterController playerController;
    private Vector3 knockbackVelocity;
    private float knockbackTimer;
    private bool isPlayerKnockedBack;
    private float knockbackDuration = 0.8f;

    private PlayerMove playerMove;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Start()
    {
        Debug.Log($"[Boss_SB] Start() player={player}");

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            Debug.Log($"[Boss_SB] FindGameObjectWithTag Player={go}");
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

        playerMove = player.GetComponent<PlayerMove>();
        if (playerMove == null)
            playerMove = player.GetComponentInChildren<PlayerMove>();
        if (playerMove == null)
            playerMove = player.GetComponentInParent<PlayerMove>();

        Debug.Log($"[Boss_SB] PlayerMove={playerMove}");

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
            case BossState.Sealed: UpdateSealed(); break;
            case BossState.Roar: break; // コルーチンで処理
            case BossState.Defeated: break;
        }
    }

    // ====================================================================
    //  外部から呼ぶ関数
    //  他スクリプトとの連携はここに追加する
    // ====================================================================

    /// <summary>ボス戦を開始する（BossAreaTriggerから呼ぶ）</summary>
    public void StartBossBattle()
    {
        if (state != BossState.Idle) return;

        currentPhase = 0;
        inkHitCount = 0;
        cannonBallHitCount = 0;
        roarCount = 0;

        Debug.Log("[Boss_SB] ボス戦開始！");
        EnterChase();
    }

    /// <summary>墨を塗られたときの処理（EnemyHitReceiverから呼ぶ）</summary>
    public void ReceiveInk()
    {
        if (isAlly) return;
        if (state == BossState.Sealed) return;
        if (state == BossState.Roar) return;
        if (state == BossState.Defeated) return;

        inkHitCount++;
        int required = requiredInkCounts[currentPhase];
        Debug.Log($"[Boss_SB] 塗り回数: {inkHitCount} / {required} (フェーズ{currentPhase + 1})");

        if (inkHitCount >= required)
        {
            inkHitCount = 0;
            EnterSealed();
        }
    }

    /// <summary>お墨付き状態を返す</summary>
    public bool GetIsAlly() => isAlly;

    /// <summary>砲弾が当たったときに呼ぶ（砲弾担当から呼ぶ）</summary>
    public void ReceiveCannonBall()
    {
        if (state != BossState.Sealed) return;
        if (state == BossState.Defeated) return;

        cannonBallHitCount++;
        Debug.Log($"[Boss_SB] 砲弾被弾: {cannonBallHitCount} / 3");

        if (cannonBallHitCount >= 3)
        {
            EnterDefeated();
        }
        else
        {
            currentPhase = Mathf.Min(cannonBallHitCount, 2);
            Debug.Log($"[Boss_SB] フェーズ{currentPhase + 1}へ移行");
        }
    }

    // ====================================================================
    //  Chase（追従）
    //  追従の挙動を変えたいときはここを編集
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

        // タックルタイマー
        tackleTimer += Time.deltaTime;
        if (tackleTimer >= tackleDelay)
        {
            EnterCharge(); // Chargeへ移行
            return;
        }

        // プレイヤーを追いかける
        if (toPlayer.magnitude > collideDistance)
        {
            transform.position += toPlayer.normalized * chaseSpeed * Time.deltaTime;
        }
        else
        {
            ApplyKnockbackToPlayer();
            EnterStop();
            return;
        }
        LookAt(toPlayer);
    }

    // ====================================================================
    //  Charge（チャージ・溜め）
    //  チャージの挙動を変えたいときはここを編集
    // ====================================================================

    private void EnterCharge()
    {
        state = BossState.Charge;
        chargeTimer = 0f;

        // チャージ開始時にプレイヤー方向を固定する
        chargeTargetDir = (player.position - transform.position).normalized;
        chargeTargetDir.y = 0f;

        Debug.Log("[Boss_SB] チャージ開始！");
    }

    private void UpdateCharge()
    {
        chargeTimer += Time.deltaTime;

        // チャージ中はプレイヤーの方向を向いて止まる
        LookAt(chargeTargetDir);

        // チャージ完了でタックルへ
        if (chargeTimer >= chargeDuration)
        {
            EnterTackle();
        }
    }

    // ====================================================================
    //  Tackle（タックル・急直進）
    //  タックルの挙動を変えたいときはここを編集
    // ====================================================================

    private void EnterTackle()
    {
        state = BossState.Tackle;

        // チャージ中に固定した方向でタックル（プレイヤーに吸い付かない）
        tackleDirection = chargeTargetDir;
        tackleStartPos = transform.position;
        Debug.Log("[Boss_SB] タックル開始！");
    }

    private void UpdateTackle()
    {
        // 固定した方向に急直進
        transform.position += tackleDirection * tackleSpeed * Time.deltaTime;
        LookAt(tackleDirection);

        // プレイヤーとの距離チェック（衝突判定）
        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;
        float dist = Vector3.Distance(bossPos, player.position);

        Debug.Log($"[Boss_SB] Tackle中 dist={dist:F2} collideDistance={collideDistance}");
        Debug.Log($"[Boss_SB] bossPos={bossPos} playerPos={player.position} dist={dist:F2}");
        if (dist <= tackleCollideDistance)
        {
            Debug.Log("[Boss_SB] 衝突判定！ノックバック発生");
            ApplyKnockbackToPlayer();
            EnterStop();
            return;
        }

        // 開始位置からの移動距離で判定（シンプル）
        float travelDist = Vector3.Distance(transform.position, tackleStartPos);
        if (travelDist > 20f) // ← 最大タックル距離
        {
            EnterStop();
        }
    }

    // ====================================================================
    //  Stop（停止）
    //  停止の挙動を変えたいときはここを編集
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
    //  Sealed（お墨付き停止）
    //  お墨付きの挙動を変えたいときはここを編集
    // ====================================================================

    private void EnterSealed()
    {
        state = BossState.Sealed;
        sealedTimer = 0f;

        // お墨付き回数を加算
        cannonBallHitCount++;
        Debug.Log($"[Boss_SB] お墨付き！{cannonBallHitCount}回目 / 3");

        // 3回お墨付きされたら撃破
        if (cannonBallHitCount >= 3)
        {
            EnterDefeated();
            return;
        }

        // フェーズ移行
        currentPhase = Mathf.Min(cannonBallHitCount, 2);
        Debug.Log($"[Boss_SB] フェーズ{currentPhase + 1}へ移行");
    }

    private void UpdateSealed()
    {
        sealedTimer += Time.deltaTime;

        if (sealedTimer >= sealedDuration)
        {
            // 咆哮は二回まで
            if(roarCount < 2)
            {
                EnterRoar();
            }
            else
            {
                EnterChase();
            }
        }
    }

    // ====================================================================
    //  Roar（咆哮・リセット）
    //  咆哮の挙動を変えたいときはここを編集
    // ====================================================================

    private void EnterRoar()
    {
        state = BossState.Roar;
        roarCount++;
        Debug.Log($"[Boss_SB] 咆哮！({roarCount}回目)");
        StartCoroutine(RoarCoroutine());
    }

    private IEnumerator RoarCoroutine()
    {
        yield return new WaitForSeconds(0.5f);

        foreach (var surface in bossSurfaces)
        {
            if (surface == null || !surface.enabled) continue;
            try { InkPaintService.ClearAll(surface); }
            catch { }
        }

        foreach (var surface in areaSurfaces)
        {
            if (surface == null || !surface.enabled) continue;
            try { InkPaintService.ClearAll(surface); }
            catch { }
        }

        float roarRange = roarRanges[currentPhase];
        ResetFieldInRange(roarRange * 0.5f);

        yield return new WaitForSeconds(0.5f);

        inkHitCount = 0;
        EnterChase();
    }

    private void ResetFieldInRange(float radius)
    {
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, radius, ~0, QueryTriggerInteraction.Collide);

        foreach (var col in colliders)
        {
            var surface = col.GetComponent<PaintableSurface>()
                       ?? col.GetComponentInParent<PaintableSurface>();
            if (surface == null) continue;
            if (!surface.enabled) continue;
            InkPaintService.ClearAll(surface);
        }
    }

    // ====================================================================
    //  Defeated（撃破）
    //  撃破演出を変えたいときはここを編集
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

        // PlayerMoveの重力をOFFにする
        if (playerMove != null)
        {
            playerMove.SetExternalGravityEnabled(false);
            playerMove.ClearVerticalVelocity();
            Debug.Log("[Boss_SB] 重力OFF");
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

            // PlayerMoveの重力をONに戻す
            if (playerMove != null)
            {
                playerMove.SetExternalGravityEnabled(true);
                Debug.Log("[Boss_SB] 重力ON");
            }
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
        // bodyTransformがあればその位置を基準にする
        Vector3 bossPos = bodyTransform != null ? bodyTransform.position : transform.position;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bossPos, collideDistance);

        if (roarRanges != null && roarRanges.Length > 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            float range = roarRanges[Mathf.Min(currentPhase, roarRanges.Length - 1)];
            Gizmos.DrawWireSphere(transform.position, range * 0.5f);
        }
    }
}