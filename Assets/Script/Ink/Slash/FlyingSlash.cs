using UnityEngine;

/// <summary>
/// 飛行中の斬撃インスタンス（UV方式）
/// - 自前で position += velocity * dt（Rigidbody不使用）
/// - Raycastで地面/壁/敵への着弾を検知
/// - 着弾時に InkPaintService 経由で塗る（PaintableSurfaceがdensity+テクスチャ+コリジョンを処理）
/// </summary>
public class FlyingSlash : MonoBehaviour
{
    // ── InkSlashSystemから初期化 ──
    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public SlashPattern pattern;
    [HideInInspector] public LayerMask hitMask = ~0;
    [HideInInspector] public bool spawnEffect = true;

    // ── 内部状態 ──
    private float age;
    private GameObject effectObj;
    private float distSinceLastTrail;

    private void Start()
    {
        if (spawnEffect && pattern != null && pattern.effectPrefab != null)
        {
            effectObj = Instantiate(pattern.effectPrefab, transform);
            effectObj.transform.localPosition = pattern.effectOffset;

            // Z軸 = 飛行方向
            Vector3 slashZAxis = velocity.normalized;
            if (slashZAxis.sqrMagnitude < 0.0001f)
                slashZAxis = transform.forward;

            // X軸 = 飛行方向に対する水平交差軸
            Vector3 slashXAxis = Vector3.Cross(Vector3.up, slashZAxis);
            if (slashXAxis.sqrMagnitude < 0.0001f)
                slashXAxis = transform.right;
            slashXAxis.Normalize();

            // Y軸 = 鉛直交差軸
            Vector3 slashYAxis = Vector3.Cross(slashZAxis, slashXAxis).normalized;

            Quaternion slashBasis = Quaternion.LookRotation(slashZAxis, slashYAxis);
            effectObj.transform.rotation = slashBasis * Quaternion.Euler(pattern.effectRotation);
            effectObj.transform.localScale = pattern.effectScale;
        }
    }

    private void Update()
    {
        if (pattern == null)
        {
            Debug.LogError("[FlyingSlash] pattern=NULL");
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        age += dt;

        // 寿命
        if (age >= pattern.lifetime)
        {
            Destroy(gameObject);
            return;
        }

        // 重力
        velocity.y -= pattern.gravity * dt;

        // 移動量
        Vector3 movement = velocity * dt;
        float moveDist = movement.magnitude;
        if (moveDist < 0.0001f) return;

        // 衝突判定（少し前からRay開始。Triggerも当たるようにCollide指定）
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
            Destroy(gameObject);
            return;
        }

        // 地面・壁 → 墨を塗る（着弾点+隣接）＋飛沫だけ追加（着弾点の二重塗りを回避）
        InkPaintService.PaintArea(hit, pattern);
        InkPaintService.Splatter(hit, pattern);

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

    private void OnDrawGizmos()
    {
        if (pattern == null) return;
        Gizmos.color = Color.black;
        Gizmos.DrawRay(transform.position, velocity.normalized * 1.5f);
        Gizmos.color = new Color(0, 0, 0, 0.2f);
        Gizmos.DrawWireSphere(transform.position, pattern.impactRadius);
    }
}
