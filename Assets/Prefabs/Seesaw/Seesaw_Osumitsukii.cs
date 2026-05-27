using UnityEngine;

public class NewEmptyCSharpScript : Obj_Osumitsuki
{

    [Header("天秤ステータス")]
    [SerializeField] private float impactLevel; //影響度

    [Header("参照オブジェクト")]
    [SerializeField] private GameObject joint1;
    [SerializeField] private GameObject joint2;
    [SerializeField] private GameObject joint3;


    private float leftWeight;
    private float rightWeight;
	private bool changeFlg = false;
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
		if (myMaterial != null && !changeFlg)
		{
			var renderers = GetComponentsInChildren<MeshRenderer>();
			foreach (var r in renderers)
			{
				// 親自身のRendererは除外（親は見えないダミー）
				if (r.gameObject == gameObject) continue;

				r.material = myMaterial;
			}
			changeFlg = true;
		}

		Action2Update();
	}

	public override void Update_Osumitsuki()
	{
		base.Update_Osumitsuki();
	}
}
