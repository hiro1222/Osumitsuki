using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ボスエネミー「墨袋ボス（Boss_SB）」
/// IF_Enemyを実装したボスエネミー
///
/// 【状態遷移】
/// Idle（待機）→ Chase（追従）→ Tackle（タックル）→ Stop（停止）→ Chase
/// お墨付き時: Sealed（6秒停止）→ Roar（咆哮・リセット）→ Chase
///
/// 【フェーズ】
/// Phase1 → Phase2 → Phase3（砲弾3発で撃破）
///
/// 【外部から呼ぶ関数】
/// ・ReceiveInk()        : EnemyHitReceiverから呼ぶ
/// ・GetIsAlly()         : 外部からお墨付き状態を確認する
/// ・ReceiveCannonBall() : 砲弾担当から呼ぶ（フェーズ進行）
/// ・StartBossBattle()   : BossAreaTriggerから呼ぶ（ボス戦開始）
/// </summary>
public class Boss_SB : MonoBehaviour, IF_Enemy
{
    // ====================================================================
    //  状態
    // ====================================================================

    private enum BossState
    {
        Idle,    // 待機（ボス戦開始前）
        Chase,   // 追従
        Tackle,  // タックル（急直進）
        Stop,    // 停止（タックル後3秒）
        Sealed,  // お墨付き停止（6秒）
        Roar,    // 咆哮（リセット）
        Defeated // 撃破
    }

    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("プレイヤー参照")]
    [SerializeField] private Transform player;

    [Header("移動")]
    [SerializeField] private float chaseSpeed = 3f;
    [Tooltip("タックル時の速度")]
    [SerializeField] private float tackleSpeed = 12f;
    [Tooltip("タックル開始までの待機時間（秒）最小")]
    [SerializeField] private float tackleDelayMin = 3f;
    [Tooltip("タックル開始までの待機時間（秒）最大")]
    [SerializeField] private float tackleDelayMax = 8f;
    [Tooltip("この距離以内でプレイヤーに衝突したと判定する")]
    [SerializeField] private float collideDistance = 2f;

    [Header("停止（タックル後）")]
    [Tooltip("タックル後の停止時間（秒）")]
    [SerializeField] private float stopDuration = 3f;

    [Header("ステータス（フェーズ共通）")]
    [Tooltip("攻撃力（ノックバック距離 = 攻撃力 × 0.5m）攻撃力:5")]
    [SerializeField] private float attackPower = 5f;
    [Tooltip("上方向のノックバック")]
    [SerializeField] private float knockbackUpForce = 5f;
    [Tooltip("お墨付き停止時間（秒）")]
    [SerializeField] private float sealedDuration = 6f;
    [Tooltip("回復量")]
    [SerializeField] private float inkRecovery = 2f;

    [Header("フェーズ別ステータス")]
    [Tooltip("必要塗り回数（フェーズ1・2・3）")]
    [SerializeField] private int[] requiredInkCounts = { 10, 12, 15 };
    [Tooltip("咆哮量（フェーズ1・2・3）単位:m（直径）")]
    [SerializeField] private float[] roarRanges = { 5f, 7f, 10f };

    [Header("咆哮リセット対象")]
    [Tooltip("咆哮でリセットするPaintableSurface（ボス自身）")]
    [SerializeField] private List<PaintableSurface> bossSurfaces = new List<PaintableSurface>();
    [Tooltip("咆哮でリセットするボスエリア内のオブジェクトのPaintableSurface")]
    [SerializeField] private List<PaintableSurface> areaSurfaces = new List<PaintableSurface>();

    [Header("地面追従")]
    [SerializeField] private float groundFollowSpeed = 10f;

    // ====================================================================
    //  内部状態（全てprivate）
    // ====================================================================

    private BossState state = BossState.Idle;
    private int currentPhase = 0; // 0=フェーズ1, 1=フェーズ2, 2=フェーズ3
    private int inkHitCount = 0;
    private int cannonBallHitCount = 0;
    private bool isAlly = false;
    private int roarCount = 0; // 咆哮回数（最大2回）

    // タックル
    private float tackleTimer = 0f;
    private float tackleDelay = 0f;
    private Vector3 tackleDirection; // タックル開始時の方向（固定）

    // 停止タイマー
    private float stopTimer = 0f;

    // お墨付き停止タイマー
    private float sealedTimer = 0f;

    // ノックバック
    private CharacterController playerController;
    private Vector3 knockbackVelocity;
    private float knockbackTimer;
    private bool isPlayerKnockedBack;
    private float knockbackDuration = 0.3f;

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

        state = BossState.Idle;
    }

    // ====================================================================
    //  毎フレーム
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
            case BossState.Tackle: UpdateTackle(); break;
            case BossState.Stop: UpdateStop(); break;
            case BossState.Sealed: UpdateSealed(); break;
            case BossState.Roar: break; // コルーチンで処理
            case BossState.Defeated: break;
        }
    }

    // ====================================================================
    //  IF_Enemy の実装
    // ====================================================================

    /// <summary>
    /// 墨を塗られたときの処理
    /// EnemyHitReceiverから呼ぶ
    /// </summary>
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
            StartSealed();
        }
    }

    /// <summary>お墨付き状態を返す</summary>
    public bool GetIsAlly() => isAlly;

    // ====================================================================
    //  ボス戦開始（BossAreaTriggerから呼ぶ）
    // ====================================================================

    /// <summary>
    /// ボス戦を開始する
    /// BossAreaTriggerからプレイヤーが侵入したときに呼ぶ
    /// </summary>
    public void StartBossBattle()
    {
        if (state != BossState.Idle) return;

        Debug.Log("[Boss_SB] ボス戦開始！");
        currentPhase = 0;
        inkHitCount = 0;
        cannonBallHitCount = 0;
        roarCount = 0;
        EnterChase();
    }

    // ====================================================================
    //  砲弾被弾（砲弾担当から呼ぶ）
    // ====================================================================

    /// <summary>
    /// 砲弾が当たったときに呼ぶ
    /// 砲弾担当のスクリプトから呼ぶ
    /// </summary>
    public void ReceiveCannonBall()
    {
        if (state != BossState.Sealed) return; // お墨付き停止中のみ有効
        if (state == BossState.Defeated) return;

        cannonBallHitCount++;
        Debug.Log($"[Boss_SB] 砲弾被弾: {cannonBallHitCount} / 3");

        // フェーズ進行
        if (cannonBallHitCount >= 3)
        {
            Defeat();
        }
        else
        {
            currentPhase = Mathf.Min(cannonBallHitCount, 2);
            Debug.Log($"[Boss_SB] フェーズ{currentPhase + 1}へ移行");
        }
    }

    // ====================================================================
    //  追従
    // ====================================================================

    private void EnterChase()
    {
        state = BossState.Chase;
        tackleTimer = 0f;
        tackleDelay = Random.Range(tackleDelayMin, tackleDelayMax);
    }

    private void UpdateChase()
    {
        Vector3 toPlayer = player.position - transform.position;
        toPlayer.y = 0f;

        // タックルタイマー
        tackleTimer += Time.deltaTime;
        if (tackleTimer >= tackleDelay)
        {
            StartTackle();
            return;
        }

        // プレイヤーに向かって移動
        if (toPlayer.magnitude > collideDistance)
        {
            transform.position += toPlayer.normalized * chaseSpeed * Time.deltaTime;
        }

        LookAt(toPlayer);
    }

    // ====================================================================
    //  タックル（急直進）
    // ====================================================================

    private void StartTackle()
    {
        state = BossState.Tackle;

        // タックル開始時のプレイヤー方向を固定する（途中で変えない）
        tackleDirection = (player.position - transform.position).normalized;
        tackleDirection.y = 0f;

        Debug.Log("[Boss_SB] タックル開始！");
    }

    private void UpdateTackle()
    {
        // 固定した方向に急直進（プレイヤーに吸い付かない）
        transform.position += tackleDirection * tackleSpeed * Time.deltaTime;
        LookAt(tackleDirection);

        // プレイヤーとの距離チェック
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist <= collideDistance)
        {
            // 衝突 → ノックバック → 停止
            ApplyKnockbackToPlayer();
            EnterStop();
            return;
        }

        // 一定距離以上離れたらタックル失敗として停止
        float tackleStartDist = Vector3.Distance(
            transform.position,
            player.position - tackleDirection * 20f);
        if (tackleStartDist > 30f)
        {
            EnterStop();
        }
    }

    // ====================================================================
    //  停止（タックル後3秒）
    // ====================================================================

    private void EnterStop()
    {
        state = BossState.Stop;
        stopTimer = 0f;
    }

    private void UpdateStop()
    {
        stopTimer += Time.deltaTime;

        if (stopTimer >= stopDuration)
        {
            EnterChase();
        }
    }

    // ====================================================================
    //  お墨付き停止（6秒）
    // ====================================================================

    private void StartSealed()
    {
        state = BossState.Sealed;
        sealedTimer = 0f;
        Debug.Log("[Boss_SB] お墨付き！6秒停止");
    }

    private void UpdateSealed()
    {
        sealedTimer += Time.deltaTime;

        if (sealedTimer >= sealedDuration)
        {
            StartRoar();
        }
    }

    // ====================================================================
    //  咆哮（リセット）
    // ====================================================================

    private void StartRoar()
    {
        state = BossState.Roar;
        roarCount++;
        Debug.Log($"[Boss_SB] 咆哮！({roarCount}回目)");

        StartCoroutine(RoarCoroutine());
    }

    private IEnumerator RoarCoroutine()
    {
        // 咆哮演出（アーティスト担当と要確認）
        yield return new WaitForSeconds(0.5f);

        // ボス自身の墨をリセット
        foreach (var surface in bossSurfaces)
        {
            if (surface != null) surface.ClearAll();
        }

        // ボスエリア内のオブジェクトの墨をリセット
        foreach (var surface in areaSurfaces)
        {
            if (surface != null) surface.ClearAll();
        }

        // フィールドを円形にリセット（咆哮量に応じた範囲）
        float roarRange = roarRanges[currentPhase];
        ResetFieldInRange(roarRange * 0.5f); // 直径→半径

        Debug.Log($"[Boss_SB] 咆哮リセット完了。範囲: {roarRange}m（直径）");

        yield return new WaitForSeconds(0.5f);

        // 咆哮後Chase再開
        inkHitCount = 0;
        EnterChase();
    }

    /// <summary>ボスを中心に円形範囲のPaintableSurfaceをリセットする</summary>
    private void ResetFieldInRange(float radius)
    {
        // 範囲内のPaintableSurfaceを全て取得してリセット
        Collider[] colliders = Physics.OverlapSphere(
            transform.position, radius, ~0, QueryTriggerInteraction.Collide);

        foreach (var col in colliders)
        {
            var surface = col.GetComponent<PaintableSurface>()
                       ?? col.GetComponentInParent<PaintableSurface>();

            if (surface != null)
            {
                surface.ClearAll();
            }
        }
    }

    // ====================================================================
    //  撃破
    // ====================================================================

    private void Defeat()
    {
        state = BossState.Defeated;
        Debug.Log("[Boss_SB] ボス撃破！");

        // 撃破演出などはここに追加（アーティスト担当と要確認）
        Destroy(gameObject, 1f);
    }

    // ====================================================================
    //  プレイヤーへのノックバック
    // ====================================================================

    private void ApplyKnockbackToPlayer()
    {
        if (isPlayerKnockedBack) return;
        if (playerController == null) return;

        float knockbackDistance = attackPower * 0.5f;
        Vector3 knockDir = (player.position - transform.position).normalized;
        knockDir.y = 0f;

        Vector3 knockbackForce = knockDir * knockbackDistance * 10f
                               + Vector3.up * knockbackUpForce;

        knockbackVelocity = knockbackForce;
        knockbackTimer = 0f;
        isPlayerKnockedBack = true;
    }

    private void UpdatePlayerKnockback()
    {
        if (!isPlayerKnockedBack) return;
        if (playerController == null) return;

        playerController.Move(knockbackVelocity * Time.deltaTime);
        knockbackTimer += Time.deltaTime;

        if (knockbackTimer >= knockbackDuration)
            isPlayerKnockedBack = false;
    }

    // ====================================================================
    //  地面追従
    // ====================================================================

    private void FollowGround()
    {
        if (Physics.Raycast(
            transform.position + Vector3.up * 0.5f,
            Vector3.down,
            out RaycastHit hit,
            10f,
            ~0,
            QueryTriggerInteraction.Collide))
        {
            if (hit.collider.gameObject == gameObject) return;

            float targetY = hit.point.y;
            float newY = Mathf.Lerp(transform.position.y, targetY, groundFollowSpeed * Time.deltaTime);
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
        // 衝突判定
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, collideDistance);

        // 咆哮範囲（現在のフェーズ）
        if (roarRanges != null && roarRanges.Length > 0)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f);
            float range = roarRanges[Mathf.Min(currentPhase, roarRanges.Length - 1)];
            Gizmos.DrawWireSphere(transform.position, range * 0.5f);
        }
    }
}