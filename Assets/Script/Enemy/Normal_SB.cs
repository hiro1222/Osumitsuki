using UnityEngine;

/// <summary>
/// 敵対Enemy「墨袋（Normal_SB）」
/// IF_Enemyを実装した基本の敵対Enemy
///
/// 【状態遷移】
/// Free（自由行動）→ Chase（追従）→ Stop（停止）→ Chase or Free
///
/// 【外部から呼ぶ関数】
/// ・ReceiveInk() : EnemyHitReceiverから呼ぶ
/// ・GetIsAlly()  : 外部からお墨付き状態を確認する
/// </summary>
public class Normal_SB : MonoBehaviour, IF_Enemy
{
    // ====================================================================
    //  状態
    // ====================================================================

    private enum EnemyState
    {
        Free,   // 自由行動（接敵前・往復移動）
        Chase,  // 追従（接敵後・プレイヤーを追いかける）
        Stop,   // 停止（衝突後・3秒待機）
    }

    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("プレイヤー参照（仮：後でPlayer担当と要すり合わせ）")]
    [SerializeField] private Transform player;

    [Header("友好EnemyManager参照")]
    [SerializeField] private AllyEnemyManager allyManager;

    [Header("接敵判定")]
    [Tooltip("この距離以内に入ると接敵状態になる")]
    [SerializeField] private float engageDistance = 5f;

    [Header("自由行動（接敵前）")]
    [Tooltip("往復する座標A")]
    [SerializeField] private Transform patrolPointA;
    [Tooltip("往復する座標B")]
    [SerializeField] private Transform patrolPointB;
    [SerializeField] private float patrolSpeed = 2f;

    [Header("追跡（接敵後）")]
    [SerializeField] private float chaseSpeed = 4f;
    [Tooltip("この距離以内でプレイヤーに衝突したと判定する")]
    [SerializeField] private float collideDistance = 1.2f;

    [Header("停止（衝突後）")]
    [Tooltip("衝突後の停止時間（秒）")]
    [SerializeField] private float stopDuration = 3f;

    [Header("ステータス")]
    [Tooltip("攻撃力（ノックバック距離 = 攻撃力 × 0.5m）")]
    [SerializeField] private float attackPower = 1f;
    [Tooltip("上方向のノックバック")]
    [SerializeField] private float knockbackUpForce = 5f; // 上方向の強さ
    [Tooltip("お墨付きに必要な塗り回数")]
    [SerializeField] private int requiredInkCount = 3;
    [Tooltip("お墨付き時にプレイヤーのインクを回復する量")]
    [SerializeField] private float inkRecovery = 2f;

    [Header("跳ねアニメーション")]
    [SerializeField] private float bounceHeight = 1.5f;
    [SerializeField] private float bounceDuration = 0.5f;

    [Header("PaintStatus参照")]
    [SerializeField] private PlayerPaintStatus paintStatus;


    private Rigidbody rb;

    // ====================================================================
    //  内部状態（全てprivate）
    // ====================================================================

    private EnemyState state = EnemyState.Free;
    private Transform currentPatrolTarget;
    private float stopTimer;
    private int inkHitCount;
    private bool isAlly;
    private bool isBouncing;
    private float bounceTimer;
    private Vector3 bounceBasePos;

    // ノックバック
    private CharacterController playerController;
    private Vector3 knockbackVelocity;
    private float knockbackTimer;
    private bool isPlayerKnockedBack;
    private float knockbackDuration = 0.3f;

    private Vector3 patrolPosA;
    private Vector3 patrolPosB;

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

        // ワールド座標を最初に記憶しておく（子オブジェクトでも回転の影響を受けない）
        if (patrolPointA != null) patrolPosA = patrolPointA.position;
        if (patrolPointB != null) patrolPosB = patrolPointB.position;

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
            // InspectorでPlayerが設定されている場合

            playerController = player.GetComponent<CharacterController>();
            if (playerController == null)
                playerController = player.GetComponentInChildren<CharacterController>();
            if (playerController == null)
                playerController = player.GetComponentInParent<CharacterController>();

        }


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

        FollowGround();

        // プレイヤーのノックバック処理
        UpdatePlayerKnockback();

        if (isBouncing)
        {
            UpdateBounce();
            return;
        }

        switch (state)
        {
            case EnemyState.Free: UpdateFree(); break;
            case EnemyState.Chase: UpdateChase(); break;
            case EnemyState.Stop: UpdateStop(); break;
        }
    }

    // ====================================================================
    //  IF_Enemy の実装
    // ====================================================================

    public void ReceiveInk()
    {
        if (isAlly) return;

        inkHitCount++;
        Debug.Log($"[Normal_SB] 塗り回数: {inkHitCount} / {requiredInkCount}");

        if (inkHitCount >= requiredInkCount)
            BecomeAlly();
    }

    public bool GetIsAlly() => isAlly;

    // ====================================================================
    //  自由行動（接敵前）
    // ====================================================================

    private void UpdateFree()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer <= engageDistance)
        {
            state = EnemyState.Chase;
            return;
        }

        if (patrolPointA == null || patrolPointB == null) return;

        Vector3 currentPos = (currentPatrolTarget == patrolPointA) ? patrolPosA : patrolPosB;
        Vector3 toTarget = currentPos - transform.position; ;
        toTarget.y = 0f;

        if (toTarget.magnitude > 0.3f)
        {
            transform.position += toTarget.normalized * patrolSpeed * Time.deltaTime;
        }
        else
        {
            currentPatrolTarget = currentPatrolTarget == patrolPointA
                ? patrolPointB
                : patrolPointA;
        }

        LookAt(toTarget);
    }

    // ====================================================================
    //  追従（接敵後）
    // ====================================================================

    private void UpdateChase()
    {
        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        if (dist > engageDistance)
        {
            state = EnemyState.Free;
            return;
        }

        if (dist > collideDistance)
        {
            transform.position += toPlayer.normalized * chaseSpeed * Time.deltaTime;
        }
        else
        {
            ApplyKnockbackToPlayer();
            state = EnemyState.Stop;
            stopTimer = 0f;
        }

        LookAt(toPlayer);
    }

    // ====================================================================
    //  停止（衝突後・3秒待機）
    // ====================================================================

    private void UpdateStop()
    {
        stopTimer += Time.deltaTime;

        if (stopTimer >= stopDuration)
        {
            float distToPlayer = Vector3.Distance(transform.position, player.position);
            state = distToPlayer <= engageDistance
                ? EnemyState.Chase
                : EnemyState.Free;
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

        // 横方向（プレイヤーから離れる方向）
        Vector3 knockDir = (player.position - transform.position).normalized;
        knockDir.y = 0f; // 横方向だけ取り出す

        // 横方向 + 上方向を別々に足す
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

        // お墨付き時に塗り範囲を1段階上げる
        if (paintStatus == null)
            paintStatus = player.GetComponent<PlayerPaintStatus>();

        if (paintStatus != null)
            paintStatus.AddPaintLevel();  // 1刻みで増加

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
        direction.y = 0;
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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, engageDistance);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, collideDistance);
    }

    private void OnCollisionStay(Collision collision)
    {
        if(collision.gameObject.GetComponent<FlyingSlash>())
        {
            ReceiveInk();
            Debug.Log("TOKUJIN_MOTIO");
        }

        Debug.Log("RYOKUDUKI_NOTO");
    }

    private void FollowGround()
    {
        // isTriggerのコライダー（PaintableSurfaceの床）も検知する
        if (Physics.Raycast(
                transform.position + Vector3.up * 0.5f,
                Vector3.down,
                out RaycastHit hit,
                10f,
                ~0, // 全レイヤー対象
                QueryTriggerInteraction.Collide)) // ← Triggerも当たるように
        {
            // Enemyコライダー自身には当たらないようにする
            if (hit.collider.gameObject == gameObject) return;

            float targetY = hit.point.y;
            float newY = Mathf.Lerp(transform.position.y, targetY, 10f * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }
}