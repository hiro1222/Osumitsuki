using UnityEngine;

/// <summary>
/// フェーズ3専用の敵「大砲お手伝い墨袋」
/// Normal_SBとほぼ同じ挙動だが、仲間にはならず
/// 塗り終わったら大砲の指定アンカー位置へ移動して配置される
/// 大砲を下向きにする手伝いをする（カウント用）
///
/// 【外部から呼ぶ関数】
/// ・ReceiveInk() : EnemyHitReceiverから呼ぶ
/// ・GetIsAlly()  : 外部からお墨付き状態を確認する（互換性のため残す）
/// </summary>
public class CannonHelper_SB : MonoBehaviour, IF_Enemy
{
    private enum EnemyState
    {
        Free,
        Chase,
        Stop,
        MovingToAnchor, // ★ 大砲のアンカーへ移動中
        Placed,         // ★ 配置完了
    }

    [Header("プレイヤー参照")]
    [SerializeField] private Transform player;

    [Header("大砲参照（移動先のアンカーを管理）")]
    [SerializeField] private CannonAutoAim targetCannon;

    [Header("接敵判定")]
    [SerializeField] private float engageDistance = 5f;

    [Header("自由行動（接敵前）")]
    [SerializeField] private Transform patrolPointA;
    [SerializeField] private Transform patrolPointB;
    [SerializeField] private float patrolSpeed = 2f;

    [Header("追跡（接敵後）")]
    [SerializeField] private float chaseSpeed = 4f;
    [SerializeField] private float collideDistance = 1.2f;

    [Header("停止（衝突後）")]
    [SerializeField] private float stopDuration = 3f;

    [Header("大砲への移動")]
    [Tooltip("アンカーへ移動する速度")]
    [SerializeField] private float moveToAnchorSpeed = 6f;
    [Tooltip("アンカーに到達したとみなす距離")]
    [SerializeField] private float anchorArriveDistance = 0.2f;

    [Header("モデル参照")]
    [SerializeField] private Transform bodyTransform;

    [Header("── ヒットエフェクト ──")]
    [SerializeField] private HitEffectPlayer hitEffectPlayer;

    [Header("── オーラエフェクト ──")]
    [SerializeField] private AuraEffectPlayer auraEffectPlayer;

    [Header("ステータス")]
    [SerializeField] private float attackPower = 1f;
    [SerializeField] private float knockbackUpForce = 5f;
    [Tooltip("お墨付きに必要な塗り回数")]
    [SerializeField] private int requiredInkCount = 3;
    [SerializeField] private int damageAmount = 1;

    [Header("跳ねアニメーション")]
    [SerializeField] private float bounceHeight = 1.5f;
    [SerializeField] private float bounceDuration = 0.5f;

    private PlayerStats playerStats;
    private CharacterController playerController;
    private PlayerMove playerMove;

    private EnemyState state = EnemyState.Free;
    private Transform currentPatrolTarget;
    private float stopTimer;
    private int inkHitCount;
    private bool isAlly; // ★ 互換性のため残すが「配置済み」の意味で使う
    private bool isBouncing;
    private float bounceTimer;
    private Vector3 bounceBasePos;

    private Vector3 patrolPosA;
    private Vector3 patrolPosB;
    private Transform assignedAnchor; // ★ 割り当てられたアンカー位置

    private void Start()
    {
        if (hitEffectPlayer == null)
            hitEffectPlayer = GetComponent<HitEffectPlayer>();
        if (auraEffectPlayer == null)
            auraEffectPlayer = GetComponent<AuraEffectPlayer>();

        isAlly = false;
        isBouncing = false;
        inkHitCount = 0;
        state = EnemyState.Free;
        currentPatrolTarget = patrolPointA;

        if (patrolPointA != null) patrolPosA = patrolPointA.position;
        if (patrolPointB != null) patrolPosB = patrolPointB.position;

        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null)
            {
                player = go.transform;
                playerController = go.GetComponent<CharacterController>();
                playerMove = go.GetComponent<PlayerMove>();
                if (playerMove == null)
                    playerMove = go.GetComponentInChildren<PlayerMove>();
                playerStats = go.GetComponent<PlayerStats>();
                if (playerStats == null)
                    playerStats = go.GetComponentInChildren<PlayerStats>();
            }
        }

        if (targetCannon == null)
            targetCannon = FindObjectOfType<CannonAutoAim>();
    }

    private void Update()
    {
        if (player == null) return;
        if (isAlly) return;

        // アンカーへ移動中は地面追従しない
        if (state != EnemyState.MovingToAnchor)
            FollowGround();

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
            case EnemyState.MovingToAnchor: UpdateMovingToAnchor(); break;
        }
    }
    public void ReceiveInk()
    {
        if (isAlly) return;

        inkHitCount++;
        Debug.Log($"[CannonHelper_SB] 塗り回数: {inkHitCount} / {requiredInkCount}");

        if (inkHitCount >= requiredInkCount)
            BecomeHelper();
    }

    public bool GetIsAlly() => isAlly;

    private void UpdateFree()
    {
        float distToPlayer = Vector3.Distance(transform.position, player.position);
        if (distToPlayer <= engageDistance)
        {
            StartBounce();
            state = EnemyState.Chase;
            if (auraEffectPlayer != null)
                auraEffectPlayer.PlayAura();
            return;
        }

        if (patrolPointA == null || patrolPointB == null) return;

        Vector3 currentPos = (currentPatrolTarget == patrolPointA) ? patrolPosA : patrolPosB;
        Vector3 toTarget = currentPos - transform.position;
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

    private void UpdateChase()
    {
        Vector3 toPlayer = player.position - transform.position;
        float dist = toPlayer.magnitude;

        if (bodyTransform != null)
        {
            Vector3 pos1 = bodyTransform.position;
            Vector3 pos2 = player.position;
            pos1.y = 0f;
            pos2.y = 0f;
            dist = Vector3.Distance(pos1, pos2);
        }

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

    /// <summary>塗り終わったら、大砲のアンカーへ移動を開始する（仲間化の代わり）</summary>
    private void BecomeHelper()
    {
        if (state == EnemyState.MovingToAnchor || isAlly) return;

        if (auraEffectPlayer != null)
            auraEffectPlayer.StopAura();

        if (targetCannon == null)
        {
            Debug.LogWarning("[CannonHelper_SB] targetCannonが未設定。大砲が見つかりません");
            isAlly = true;
            Destroy(gameObject, 1.5f);
            return;
        }

        // ★ 大砲側にアンカーを要求して割り当ててもらう
        //assignedAnchor = targetCannon.RequestAnchor(this);

        if (assignedAnchor == null)
        {
            Debug.LogWarning("[CannonHelper_SB] アンカーが満員。配置できません");
            isAlly = true;
            Destroy(gameObject, 1.5f);
            return;
        }

        state = EnemyState.MovingToAnchor;
        Debug.Log($"[CannonHelper_SB] アンカーへ移動開始: {assignedAnchor.name}");
    }

    private void UpdateMovingToAnchor()
    {
        if (assignedAnchor == null) return;

        Vector3 toAnchor = assignedAnchor.position - transform.position;
        float dist = toAnchor.magnitude;

        if (dist > anchorArriveDistance)
        {
            transform.position += toAnchor.normalized * moveToAnchorSpeed * Time.deltaTime;
            LookAt(toAnchor);
        }
        else
        {
            // ★ アンカーに到着。配置完了を大砲に通知してカウントアップ
            transform.position = assignedAnchor.position;
            isAlly = true;
            state = EnemyState.Placed;

            if (targetCannon != null)
               // targetCannon.NotifyHelperPlaced();

            Debug.Log("[CannonHelper_SB] アンカーに配置完了。大砲カウント+1");
        }
    }

    private void ApplyKnockbackToPlayer()
    {
        if (playerMove == null) return;

        if (hitEffectPlayer != null)
            hitEffectPlayer.PlayHitEffect();

        Vector3 knockDir = player.position - transform.position;
        knockDir.y = 0f;
        knockDir.Normalize();

        Vector3 knockbackVelocity = knockDir * attackPower * 10f
                                  + Vector3.up * knockbackUpForce;

        playerMove.ApplyKnockback(knockbackVelocity, 0.3f);

        if (playerStats != null)
            playerStats.Damage(damageAmount);
    }

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

    private void LookAt(Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.01f) return;
        direction.y = 0;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(direction),
            10f * Time.deltaTime);
    }

    private void OnCollisionStay(Collision collision)
    {
        if (collision.gameObject.GetComponent<FlyingSlash>())
        {
            ReceiveInk();
        }
    }

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
            float newY = Mathf.Lerp(transform.position.y, targetY, 10f * Time.deltaTime);
            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, engageDistance);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, collideDistance);
    }
}