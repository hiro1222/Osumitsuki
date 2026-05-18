using UnityEngine;

public class Sakura_Osumitsuki : Obj_Osumitsuki
{
    [Header("お墨付き達成時に切り替える子のマテリアル")]
    [Tooltip("空なら何もしない")]
    [SerializeField] private Material woodMaterial;
    [SerializeField] private Material petalMaterial;

    private PaintableSurfaceGroup group;

    private void Start()
    {
        group = GetComponent<PaintableSurfaceGroup>();
        if (group != null)
        {
            group.OnAnyPainted += HandleAnyPainted;
        }
    }


    private void OnDestroy()
    {
        if (group != null)
        {
            group.OnAnyPainted -= HandleAnyPainted;
        }
    }

    private void HandleAnyPainted(PaintableSurface source, int cells, byte density)
    {
        Painted(4f);
    }

    public override void Action_Osumitsuki()
    {
        // 子のMeshRenderer全員のマテリアルを差し替え
        if (woodMaterial != null && petalMaterial != null)
        {
            var renderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var r in renderers)
            {
                // 親自身のRendererは除外（親は見えないダミー）
                if (r.gameObject == gameObject) continue;

                if (r.gameObject.name == "SM_SAKURA_TREE")
                { r.material = woodMaterial; continue; }

                if (r.gameObject.name == "SM_GROUND_FLOWER" ||
                    r.gameObject.name == "SM_SAKURA_FLOWER")
                   { r.material = petalMaterial; continue; }
            }
        }

        End();
    }
}
