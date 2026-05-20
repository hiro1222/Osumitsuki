//using UnityEngine;

///// <summary>
///// 飛行中の斬撃インスタンス（Phase 2: UV方式対応）
///// - 自前で position += velocity * dt（Rigidbody不使用）
///// - Raycastで地面/壁への着弾を検知
///// - 着弾時にInkPaintService.Paint(hit, pattern)を呼ぶ（1行）
/////   → PaintableSurfaceがdensity + テクスチャ + コリジョンを全て処理
/////
///// Phase 1からの変更点:
///// - InkManagerへの直接書き込みを削除
///// - OnImpact/PaintTrailをInkPaintService経由に差替
///// - RaycastHitをそのまま渡す（textureCoordが必要なため）
///// </summary>
//public class FlyingSlash : MonoBehaviour
//{
//    // ── InkSlashSystemから初期化される ──
//    [HideInInspector] public Vector3 velocity;
//    [HideInInspector] public SlashPattern pattern;
//    [HideInInspector] public LayerMask hitMask = ~0;

//    // ── 内部状態 ──
//    private float age;
//    private float distSinceLastTrail;

//    // ── ビジュアル（Phase 1から変更なし） ──
//    private GameObject visualObj;
//    private TrailRenderer trailRenderer;

//    private void Start()
//    {
//        // ビジュアルの初期化はPhase 1と同じ（省略可）
//    }


//    private void Update()
//    {

//        if (pattern == null)
//        {
//            Destroy(gameObject);
//            return;
//        }

//        float dt = Time.deltaTime;
//        age += dt;

//        // 寿命チェック
//        if (age >= pattern.lifetime)
//        {
//            Destroy(gameObject);
//            return;
//        }

//        // 重力
//        velocity.y -= pattern.gravity * dt;

//        // 移動量
//        Vector3 movement = velocity * dt;
//        float moveDist = movement.magnitude;

//        // ── Raycastで着弾チェック（Triggerも当たるようにCollide指定） ──
//        if (Physics.Raycast(transform.position, movement.normalized,
//                            out RaycastHit hit, moveDist, hitMask,
//                            QueryTriggerInteraction.Collide))
//        {
//            // Phase 2: hitをそのまま渡す（1行で完結）
//            OnImpact(hit);
//            return;
//        }

//        // 移動
//        transform.position += movement;

//        // ── 飛行中の墨痕 ──
//        distSinceLastTrail += moveDist;
//        if (distSinceLastTrail >= pattern.trailInterval)
//        {
//            PaintTrail();
//            distSinceLastTrail = 0f;
//        }

//    }

//    /// <summary>
//    /// 着弾処理（Phase 2）
//    /// InkPaintService.Paint()がPaintableSurfaceを見つけて
//    /// density + テクスチャ + コリジョンを全て処理する
//    /// </summary>
//    private void OnImpact(RaycastHit hit)
//    {
//        // まずEnemyHitReceiverを確認
//        var hitReceiver = hit.collider.GetComponent<EnemyHitReceiver>();
//        if (hitReceiver != null)
//        {
//            // Enemyに当たった → お墨付き処理
//            hitReceiver.ReceiveInkHit();
//            Destroy(gameObject);
//            return;
//        }

//        // 地面・壁 → 墨を塗る
//        InkPaintService.Paint(hit, pattern);
//        Destroy(gameObject);
//    }

//    /// <summary>飛行中の墨痕を落とす（Phase 2）</summary>
//    private void PaintTrail()
//    {
//        if (Physics.Raycast(transform.position, Vector3.down,
//                            out RaycastHit groundHit, 20f, hitMask,
//                            QueryTriggerInteraction.Collide))
//        {
//            // Phase 2: hitを渡すだけ。trailDensityはpatternの6割
//            float trailRadius = pattern.trailRadius;
//            byte trailDensity = (byte)(pattern.inkDensity * 0.6f);
//            InkPaintService.Paint(groundHit, trailRadius, trailDensity);
//        }
//    }

//    private void OnDrawGizmos()
//    {
//        if (pattern == null) return;
//        Gizmos.color = Color.black;
//        Gizmos.DrawRay(transform.position, velocity.normalized * 1.5f);
//        Gizmos.color = new Color(0, 0, 0, 0.2f);
//        Gizmos.DrawWireSphere(transform.position, pattern.impactRadius);
//    }
//}

using UnityEngine;

public class FlyingSlash : MonoBehaviour
{
    // ── InkSlashSystemから初期化 ──
    [HideInInspector] public Vector3 velocity;
    [HideInInspector] public SlashPattern pattern;
    [HideInInspector] public LayerMask hitMask = ~0;

    // ── 内部状態 ──
    private float age;
    private GameObject effectObj;
    private float distSinceLastTrail;

    private void Start()
    {
        Debug.Log(
            $"[FlyingSlash START] " +
            $"Pos:{transform.position} " +
            $"Vel:{velocity}"
        );

        if (pattern != null && pattern.effectPrefab != null)
        {
            effectObj = Instantiate(pattern.effectPrefab, transform);

            // 位置
            effectObj.transform.localPosition = pattern.effectOffset;

            // 回転（XYZ 全軸）
            Quaternion baseRot = effectObj.transform.localRotation;

            effectObj.transform.localRotation =
                baseRot *
                Quaternion.Euler(pattern.effectRotation);

            // スケール
            effectObj.transform.localScale = pattern.effectScale;
        }
    }

    private void Update()
    {
        // -------------------------
        // Pattern確認
        // -------------------------
        if (pattern == null)
        {
            Debug.LogError("[FlyingSlash] pattern=NULL");
            Destroy(gameObject);
            return;
        }

        float dt = Time.deltaTime;
        age += dt;

        Debug.Log(
            $"[FlyingSlash] " +
            $"Age:{age:F2} " +
            $"Pos:{transform.position} " +
            $"Vel:{velocity} " +
            $"Speed:{velocity.magnitude:F2}"
        );

        // -------------------------
        // 寿命
        // -------------------------
        if (age >= pattern.lifetime)
        {
            Debug.Log("[FlyingSlash] Lifetime End");
            Destroy(gameObject);
            return;
        }

        // -------------------------
        // 重力
        // -------------------------
        velocity.y -= pattern.gravity * dt;

        // -------------------------
        // 移動量
        // -------------------------
        Vector3 movement = velocity * dt;
        float moveDist = movement.magnitude;

        Debug.Log(
            $"[FlyingSlash] MoveDist:{moveDist:F3}"
        );

        // 移動量が小さすぎる
        if (moveDist < 0.0001f)
        {
            Debug.LogWarning(
                "[FlyingSlash] MoveDistがほぼ0"
            );
            return;
        }

        // -------------------------
        // 衝突判定
        // 少し前からRay開始
        // -------------------------
        Vector3 rayStart =
            transform.position +
            velocity.normalized * 0.2f;

        Debug.DrawRay(
            rayStart,
            movement.normalized * moveDist,
            Color.red,
            1f
        );

        if (Physics.Raycast(
            rayStart,
            movement.normalized,
            out RaycastHit hit,
            moveDist,
            hitMask,
            QueryTriggerInteraction.Collide))
        {
            Debug.Log(
                $"[FlyingSlash] HIT → {hit.collider.name}"
            );

            OnImpact(hit);
            return;
        }

        // -------------------------
        // 移動
        // -------------------------
        transform.position += movement;

        Debug.Log(
            $"[FlyingSlash] Move To:{transform.position}"
        );

        // -------------------------
        // 飛行中の墨痕
        // -------------------------
        distSinceLastTrail += moveDist;

        if (distSinceLastTrail >= pattern.trailInterval)
        {
            Debug.Log(
                "[FlyingSlash] PaintTrail"
            );

            PaintTrail();

            distSinceLastTrail = 0f;
        }
    }

    private void OnImpact(RaycastHit hit)
    {
        Debug.Log(
            $"[FlyingSlash] OnImpact:{hit.collider.name}"
        );

        var hitReceiver =
            hit.collider.GetComponent<EnemyHitReceiver>();

        if (hitReceiver != null)
        {
            Debug.Log(
                "[FlyingSlash] EnemyHit"
            );

            hitReceiver.ReceiveInkHit();

            Destroy(gameObject);
            return;
        }

        Debug.Log(
            "[FlyingSlash] GroundPaint"
        );

        InkPaintService.Paint(hit, pattern);

        Destroy(gameObject);
    }

    private void PaintTrail()
    {
        if (Physics.Raycast(
            transform.position,
            Vector3.down,
            out RaycastHit groundHit,
            20f,
            hitMask,
            QueryTriggerInteraction.Collide))
        {
            float trailRadius =
                pattern.trailRadius;

            byte trailDensity =
                (byte)(pattern.inkDensity * 0.6f);

            Debug.Log(
                $"[FlyingSlash] TrailPaint:{groundHit.collider.name}"
            );

            InkPaintService.Paint(
                groundHit,
                trailRadius,
                trailDensity);
        }
    }

    private void OnDrawGizmos()
    {
        if (pattern == null) return;

        Gizmos.color = Color.black;

        Gizmos.DrawRay(
            transform.position,
            velocity.normalized * 1.5f
        );

        Gizmos.color =
            new Color(0, 0, 0, 0.2f);

        Gizmos.DrawWireSphere(
            transform.position,
            pattern.impactRadius
        );
    }
}