using UnityEngine;

/// <summary>
/// 飛行中の斬撃インスタンス（UV方式）
/// - 自前で position += velocity * dt（Rigidbody不使用）
/// - Raycastで地面/壁/敵への着弾を検知
/// - 着弾時に InkPaintService 経由で塗る
/// - 着弾後は stayDuration 秒その場に留まってから消滅（仲間追従用）
/// </summary>
public class FlyingSlash : MonoBehaviour
{
    // ── InkSlashSystemから初期化 ──
    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public SlashPattern pattern;
    [HideInInspector] public LayerMask hitMask = ~0;
    [HideInInspector] public bool spawnEffect = true;

    [Header("着弾後の滞在")]
    [Tooltip("着弾後、その場に留まる秒数。仲間が乗る場合の停止時間。")]
    public float stayDuration = 1.0f;

    // ── 外部参照用の状態 ──
    /// <summary>着弾して停止したか。仲間追従の判定に使う。</summary>
    public bool HasImpacted { get; private set; }
    /// <summary>消滅したか(Destroy予定含む)。</summary>
    public bool IsFinished { get; private set; }

    // ── 内部状態 ──
    private float age;
    private GameObject effectObj;
    private float distSinceLastTrail;
    private float stayTimer;

    private void Start()
    {
        if (spawnEffect && pattern != null && pattern.effectPrefab != null)
        {
            effectObj = Instantiate(pattern.effectPrefab, transform);
            effectObj.transform.localPosition = pattern.effectOffset;

            Vector3 slashZAxis = velocity.normalized;
            if (slashZAxis.sqrMagnitude < 0.0001f)
                slashZAxis = transform.forward;

            Vector3 slashXAxis = Vector3.Cross(Vector3.up, slashZAxis);
            if (slashXAxis.sqrMagnitude < 0.0001f)
                slashXAxis = transform.right;
            slashXAxis.Normalize();

            Vector3 slashYAxis = Vector3.Cross(slashZAxis, slashXAxis).normalized;

            Quaternion slashBasis = Quaternion.LookRotation(slashZAxis, slashYAxis);
            effectObj.transform.rotation = slashBasis * Quaternion.Euler(pattern.effectRotation);
            effectObj.transform.localScale = pattern.effectScale;
        }

        PrebuildAlongPath();
    }

    private void Update()
    {
        if (pattern == null)
        {
            Debug.LogError("[FlyingSlash] pattern=NULL");
            FinishAndDestroy();
            return;
        }

        // 着弾済みなら滞在タイマーを進めるだけ(移動しない)
        if (HasImpacted)
        {
            stayTimer -= Time.deltaTime;
            if (stayTimer <= 0f)
            {
                FinishAndDestroy();
            }
            return;
        }

        float dt = Time.deltaTime;
        age += dt;

        // 寿命(空中のまま寿命が来たら、その場で滞在に移行)
        if (age >= pattern.lifetime)
        {
            EnterImpactedState();
            return;
        }

        // 重力
        velocity.y -= pattern.gravity * dt;

        // 移動量
        Vector3 movement = velocity * dt;
        float moveDist = movement.magnitude;
        if (moveDist < 0.0001f) return;

        // 衝突判定
        Vector3 rayStart = transform.position + velocity.normalized * 0.2f;
        if (InkPaintService.Raycast(rayStart, movement.normalized, out RaycastHit hit,
                                    moveDist, hitMask))
        {
            OnImpact(hit);
            return;
        }

        // 移動
        transform.position += movement;

        // 飛行中の墨痕
        distSinceLastTrail += moveDist;
        if (distSinceLastTrail >= pattern.trailInterval)
        {
            PaintTrail();
            distSinceLastTrail = 0f;
        }
    }

    private void OnImpact(RaycastHit hit)
    {
        // まず敵への着弾を確認
        var hitReceiver = hit.collider.GetComponent<EnemyHitReceiver>();
        if (hitReceiver != null)
        {
            hitReceiver.ReceiveInkHit();
            // 敵に当たった場合は滞在せず即消滅(仲間は隊列に戻る)
            FinishAndDestroy();
            return;
        }

        // 地面・壁 → 墨を塗る
        InkPaintService.PaintArea(hit, pattern);
        InkPaintService.Splatter(hit, pattern);

        // 着弾位置に座って滞在状態へ
        transform.position = hit.point;
        EnterImpactedState();
    }

    /// <summary>着弾(または寿命)で移動を止め、その場に滞在する状態へ。</summary>
    private void EnterImpactedState()
    {
        if (HasImpacted) return;
        HasImpacted = true;
        velocity = Vector3.zero;
        stayTimer = stayDuration;
    }

    private void FinishAndDestroy()
    {
        IsFinished = true;
        Destroy(gameObject);
    }

    private void PaintTrail()
    {
        if (InkPaintService.Raycast(transform.position, Vector3.down, out RaycastHit groundHit,
                                    20f, hitMask))
        {
            float trailRadius = pattern.trailRadius;
            byte trailDensity = (byte)(pattern.inkDensity * 0.6f);
            InkPaintService.PaintArea(groundHit, trailRadius, trailDensity);
        }
    }

    private void PrebuildAlongPath()
    {
        var streamer = InkSurfaceStreamer.Instance;
        if (streamer == null || pattern == null) return;

        Vector3 pos = transform.position;
        Vector3 vel = velocity;
        const float dt = 0.05f;
        for (float t = 0f; t < pattern.lifetime; t += dt)
        {
            if (InkPaintService.Raycast(pos, Vector3.down, out RaycastHit hit, 20f, hitMask))
            {
                var ps = hit.collider.GetComponentInParent<PaintableSurface>();
                if (ps != null) streamer.RequestBuild(ps);
            }
            vel.y -= pattern.gravity * dt;
            pos += vel * dt;
        }
    }

    private void OnDrawGizmos()
    {
        if (pattern == null) return;
        Gizmos.color = Color.black;
        Gizmos.DrawRay(transform.position, velocity.normalized * 1.5f);
        Gizmos.color = new Color(0, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, pattern.impactRadius);
    }
}