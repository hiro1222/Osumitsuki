using UnityEngine;

/// <summary>
/// ボス戦用木箱
/// ボスのタックルが当たったときにBoss_SBにスタンを通知する
///
/// OnTriggerEnter: ボス側のコライダーがIsTrigger ONの場合
/// OnCollisionEnter: ボス側のコライダーがIsTrigger OFFの場合
/// </summary>
public class Boss_WoodBox : MonoBehaviour
{
    [Header("── デバッグ用 ──")]
    [Tooltip("ONにすると最初から塗られた状態（実体化済み）として配置する")]
    [SerializeField] private bool startAsPainted = false;

    private bool hasAppliedPaintedState = false;
    private Material cachedMyMaterial;

    private void Start()
    {
        if (startAsPainted)
        {
            int inkLayer = LayerMask.NameToLayer("PlayerVSObject");
            if (inkLayer >= 0)
            {
                gameObject.layer = inkLayer;

                var children = GetComponentsInChildren<Transform>();
                foreach (var child in children)
                    child.gameObject.layer = inkLayer;

                var osumi = GetComponent<Obj_Osumitsuki>();
                if (osumi != null)
                {
                    var materialField = typeof(Obj_Osumitsuki).GetField(
                        "myMaterial",
                        System.Reflection.BindingFlags.NonPublic |
                        System.Reflection.BindingFlags.Instance);

                    cachedMyMaterial = materialField?.GetValue(osumi) as Material;
                }
            }
        }
    }
    private void LateUpdate()
    {
        if (startAsPainted && !hasAppliedPaintedState && cachedMyMaterial != null)
        {
            var renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                if (r.gameObject == gameObject) continue;
                r.material = cachedMyMaterial;
            }

            hasAppliedPaintedState = true; // 一度だけ適用
            Debug.Log($"[Boss_WoodBox] LateUpdateでマテリアル強制適用: {cachedMyMaterial.name}");
        }
    }

    private void NotifyBoss(GameObject other)
    {
        var boss = other.GetComponent<Boss_SB>();
        if (boss == null)
            boss = other.GetComponentInParent<Boss_SB>();
        if (boss != null)
        {
            boss.NotifyHitCrate();
        }
        else
        {
            Debug.Log($"[Boss_WoodBox] Boss_SBが見つからない: {other.name}");
        }
    }
}