using UnityEngine;

/// <summary>
/// 敵対Enemy「飛行型墨袋（Wing_SB）」
/// IF_Enemyを実装した空中行動エネミー
///
/// 【状態遷移】
/// Free（空中往復）→ Chase（空中追従）→ HipDrop（急降下）→ Stop（地上待機）→ Rise（上昇）→ Chase or Free
///
/// 【ヒップドロップ仕様】
/// ・プレイヤーの座標的静止を検知してから急降下する
/// ・X・Z座標は固定したまま、Y座標のみ変動する
/// ・プレイヤーに衝突（collideDistance以内）でノックバック発動
///
/// 【外部から呼ぶ関数】
/// ・ReceiveInk() : EnemyHitReceiverから呼ぶ
/// ・GetIsAlly()  : 外部からお墨付き状態を確認する
/// </summary>
public class Wing_SB : MonoBehaviour, IF_Enemy
{
    // ====================================================================
    //  状態
    // ====================================================================

    private enum EnemyState
    {
        Free,       // 自由行動（空中往復）
        Chase,      // 追従（空中・プレイヤーの真上に移動）
        HipDrop,    // 急降下（Y座標のみ変動）
        Stop,       // 停止（地上・2秒待機）
        Rise,       // 上昇（地上→空中に戻る）
    }

    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("プレイヤー参照")]
    [SerializeField] private Transform player;

    [Header("友好EnemyManager参照")]
    [SerializeField] private AllyEnemyManager allyManager;

    [Header("接敵判定")]
    [Tooltip("この距離以内に入ると接敵状態になる（XZ平面での距離）")]
    [SerializeField] private float engageDistance = 6f;

    [Header("自由行動（接敵前・空中往復）")]
    [Tooltip("往復する座標A（子オブジェクト可）")]
    [SerializeField] private Transform patrolPointA;
    [Tooltip("往復する座標B（子オブジェクト可）")]
    [SerializeField] private Transform patrolPointB;
    [SerializeField] private float patrolSpeed = 2f;

    [Header("空中追従（接敵後）")]
    [Tooltip("プレイヤーの真上に浮く高さ（メートル）")]
    [SerializeField] private float hoverHeight = 4f;
    [SerializeField] private float chaseSpeed = 4f;

    [Header("ヒップドロップ")]
    [Tooltip("プレイヤーが何秒間座標を更新しないとヒップドロップ開始するか")]
    [SerializeField] private float stillTimeRequired = 0.8f;
    [Tooltip("急降下の速度（m/s）")]
    [SerializeField] private float hipDropSpeed = 12f;
    [Tooltip("この距離以内でプレイヤーに衝突したと判定する（Y方向）")]
    [SerializeField] private float collideDistance = 0.5f;
    [Tooltip("衝突判定する地面のY座標（ここまで下がっても衝突扱い）")]
    [SerializeField] private float groundY = 0f;

    [Header("停止（衝突後・地上待機）")]
    [Tooltip("衝突後の地上待機時間（秒）")]
    [SerializeField] private float stopDuration = 2f;

    [Header("上昇（地上→空中）")]
    [SerializeField] private float riseSpeed = 4f;

    [Header("ステータス")]
    [Tooltip("攻撃力（ノックバック距離 = 攻撃力 × 0.5m）")]
    [SerializeField] private float attackPower = 2f;
    [Tooltip("上方向のノックバック強さ")]
    [SerializeField] private float knockbackUpForce = 5f;
    [Tooltip("お墨付きに必要な塗り回数")]
    [SerializeField] private int requiredInkCount = 1;
    [Tooltip("お墨付き時にプレイヤーのインクを回復する量")]
    [SerializeField] private float inkRecovery = 3f;

    [Header("跳ねアニメーション（お墨付き時）")]
    [SerializeField] private float bounceHeight = 1.5f;
    [SerializeField] private float bounceDuration = 0.5f;

    [Header("仲間になったときの色")]
    [SerializeField] private Color allyColor = Color.cyan;

    // ====================================================================
    //  内部状態
    // ====================================================================

    private EnemyState state = EnemyState.Free;
    private Transform currentPatrolTarget;
    private float stopTimer;
    private int inkHitCount;
    private bool isAlly;
    private bool isBouncing;
    private float bounceTimer;
    private Vector3 bounceBasePos;

    // パトロール座標（ワールド座標で記憶。子オブジェクトでも回転の影響を受けない）
    private Vector3 patrolPosA;
    private Vector3 patrolPosB;

    // ヒップドロップ関連
    private Vector3 hipDropTargetXZ;   // ヒップドロップ開始時のX・Z固定座標
    private float playerStillTimer;    // プレイヤーの静止継続時間
    private Vector3 playerPrevPos;     // 前フレームのプレイヤー座標
    private bool hipDropReady;         // ヒップドロップ可能状態か

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
        isAlly = false;
        isBouncing = false;
        inkHitCount = 0;
        state = EnemyState.Free;
        currentPatrolTarget = patrolPointA;

        // パトロール座標をワールド座標で記憶
        if (patrolPointA != null) patrolPosA = patrolPointA.position;
        if (patrolPointB != null) patrolPosB = patrolPointB.position;

        // プレイヤー検索
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                player = go.transform;
                playerController = go.GetComponent<CharacterController>();
            }
        }
        else
        {
            playerController = player.GetComponent<CharacterController>();
        }

        if (player != null)
            playerPrevPos = player.position;

        if (allyManager == null)
            allyManager = FindObjectOfType<AllyEnemyManager>();
    }

    // ====================================================================
    //  毎フレーム
    // ====================================================================

    private void Update()
    {
        if (player == null) return;
        if (isAlly) return;

        UpdatePlayerKnockback();

        if (isBouncing)
        {
            UpdateBounce();
            return;
        }

        // プレイヤーの静止検知（HipDrop判定用）
        UpdatePlayerStillDetection();

        switch (state)
        {
            case EnemyState.Free:    UpdateFree();    break;
            case EnemyState.Chase:   UpdateChase();   break;
            case EnemyState.HipDrop: UpdateHipDrop(); break;
            case EnemyState.Stop:    UpdateStop();    break;
            case EnemyState.Rise:    UpdateRise();    break;
        }

        playerPrevPos = player.position;
    }

    // ====================================================================
    //  IF_Enemy の実装
    // ====================================================================

    public void ReceiveInk()
    {
        if (isAlly) return;

        inkHitCount++;
        Debug.Log($"[Wing_SB] 塗り回数: {inkHitCount} / {requiredInkCount}");

        if (inkHitCount >= requiredInkCount)
            BecomeAlly();
    }

    public bool GetIsAlly() => isAlly;

    // ====================================================================
    //  自由行動（空中往復）
    // ====================================================================

    private void UpdateFree()
    {
        // XZ平面でプレイヤーとの距離を判定
        float distXZ = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(player.position.x, 0, player.position.z));

        if (distXZ <= engageDistance)
        {
            state = EnemyState.Chase;
            return;
        }

        if (patrolPointA == null || patrolPointB == null) return;

        Vector3 currentPatrolPos = (currentPatrolTarget == patrolPointA) ? patrolPosA : patrolPosB;
        Vector3 toTarget = currentPatrolPos - transform.position;
        toTarget.y = 0f; // 水平方向のみ（高さはパトロール座標のYをそのまま使う）

        if (toTarget.magnitude > 0.3f)
        {
            // 水平移動 + パトロール座標のYに近づく
            Vector3 fullToTarget = currentPatrolPos - transform.position;
            transform.position += fullToTarget.normalized * patrolSpeed * Time.deltaTime;
        }
        else
        {
            currentPatrolTarget = (currentPatrolTarget == patrolPointA) ? patrolPointB : patrolPointA;
        }

        LookAt(toTarget);
    }

    // ====================================================================
    //  空中追従（接敵後・プレイヤーの真上に浮く）
    // ====================================================================

    private void UpdateChase()
    {
        // 接敵範囲から外れたら自由行動に戻る
        float distXZ = Vector3.Distance(
            new Vector3(transform.position.x, 0, transform.position.z),
            new Vector3(player.position.x, 0, player.position.z));

        if (distXZ > engageDistance)
        {
            state = EnemyState.Free;
            hipDropReady = false;
            return;
        }

        // 目標位置：プレイヤーの真上 hoverHeight メートル
        Vector3 targetPos = new Vector3(
            player.position.x,
            player.position.y + hoverHeight,
            player.position.z);

        Vector3 toTarget = targetPos - transform.position;
        transform.position += toTarget.normalized * chaseSpeed * Time.deltaTime;

        LookAt(new Vector3(player.position.x - transform.position.x, 0,
                           player.position.z - transform.position.z));

        // プレイヤーが静止していたらヒップドロップ開始
        if (hipDropReady)
        {
            // 真上に近い位置まで来ていたらドロップ開始
            float xzDist = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(player.position.x, 0, player.position.z));

            if (xzDist < 1.0f)
            {
                StartHipDrop();
            }
        }
    }

    // ====================================================================
    //  ヒップドロップ（急降下）
    // ====================================================================

    private void StartHipDrop()
    {
        // X・Z座標を固定してドロップ開始
        hipDropTargetXZ = new Vector3(transform.position.x, 0, transform.position.z);
        state = EnemyState.HipDrop;
        hipDropReady = false;
        Debug.Log("[Wing_SB] ヒップドロップ開始");
    }

    private void UpdateHipDrop()
    {
        // X・Z固定、Yのみ下降
        float newY = transform.position.y - hipDropSpeed * Time.deltaTime;
        transform.position = new Vector3(hipDropTargetXZ.x, newY, hipDropTargetXZ.z);

        // プレイヤーとのY距離で衝突判定
        float yDist = Mathf.Abs(transform.position.y - player.position.y);
        bool hitPlayer = yDist <= collideDistance;

        // 地面まで落ちた場合も衝突扱い
        bool hitGround = transform.position.y <= groundY;

        if (hitPlayer || hitGround)
        {
            if (hitPlayer)
            {
                ApplyKnockbackToPlayer();
                Debug.Log("[Wing_SB] プレイヤーに衝突！ノックバック発動");
            }

            // Raycastで実際の地面のY座標を取得
            float landY = groundY; // デフォルトはInspectorのgroundY
            if (Physics.Raycast(
                    new Vector3(hipDropTargetXZ.x, transform.position.y, hipDropTargetXZ.z),
                    Vector3.down,
                    out RaycastHit hit,
                    10f))
            {
                landY = hit.point.y;
            }

            transform.position = new Vector3(hipDropTargetXZ.x, landY, hipDropTargetXZ.z);
            state = EnemyState.Stop;
            stopTimer = 0f;
        }
    }

    // ====================================================================
    //  停止（地上待機）
    // ====================================================================

    private void UpdateStop()
    {
        stopTimer += Time.deltaTime;

        if (stopTimer >= stopDuration)
        {
            state = EnemyState.Rise;
        }
    }

    // ====================================================================
    //  上昇（地上→空中）
    // ====================================================================

    private void UpdateRise()
    {
        float targetY = player.position.y + hoverHeight;
        float newY = transform.position.y + riseSpeed * Time.deltaTime;
        transform.position = new Vector3(transform.position.x, newY, transform.position.z);

        // hoverHeight に達したら Chase か Free に移行
        if (transform.position.y >= targetY)
        {
            float distXZ = Vector3.Distance(
                new Vector3(transform.position.x, 0, transform.position.z),
                new Vector3(player.position.x, 0, player.position.z));

            state = distXZ <= engageDistance ? EnemyState.Chase : EnemyState.Free;
            Debug.Log($"[Wing_SB] 上昇完了 → {state}");
        }
    }

    // ====================================================================
    //  プレイヤーの静止検知
    // ====================================================================

    /// <summary>
    /// プレイヤーが stillTimeRequired 秒間動かなかったら hipDropReady = true にする
    /// Chase 状態のときのみ有効
    /// </summary>
    private void UpdatePlayerStillDetection()
    {
        if (state != EnemyState.Chase)
        {
            playerStillTimer = 0f;
            hipDropReady = false;
            return;
        }

        float movedDist = Vector3.Distance(player.position, playerPrevPos);

        // 移動量がほぼゼロ = 静止している
        if (movedDist < 0.01f)
        {
            playerStillTimer += Time.deltaTime;

            if (playerStillTimer >= stillTimeRequired)
            {
                hipDropReady = true;
            }
        }
        else
        {
            // 動いたらリセット
            playerStillTimer = 0f;
            hipDropReady = false;
        }
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
    //  お墨付き（仲間化）
    // ====================================================================

    private void BecomeAlly()
    {
        if (isAlly) return;
        isAlly = true;

        if (allyManager != null)
            allyManager.OnEnemyBecameAlly(transform.position, inkRecovery);

        Destroy(gameObject);
    }

    // ====================================================================
    //  跳ねアニメーション
    // ====================================================================

    private void StartBounce()
    {
        isBouncing = true;
        bounceTimer = 0f;
        bounceBasePos = transform.position;
    }

    private void UpdateBounce()
    {
        bounceTimer += Time.deltaTime;
        float t = bounceTimer / bounceDuration;

        float yOffset = Mathf.Sin(t * Mathf.PI) * bounceHeight;
        transform.position = bounceBasePos + Vector3.up * yOffset;

        if (bounceTimer >= bounceDuration)
        {
            transform.position = bounceBasePos;
            isBouncing = false;
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
        // 接敵範囲（XZ平面）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, engageDistance);

        // ホバー高さ
        if (player != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(
                new Vector3(player.position.x, player.position.y + hoverHeight, player.position.z),
                0.3f);
        }

        // 衝突判定距離
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, collideDistance);
    }
}
