using UnityEngine;
using UnityEngine.Tilemaps;

public class Lift_Osumitsuki : Obj_Osumitsuki
{
    [Header("リフト足場オブジェクト")]
    [SerializeField] private GameObject boardObj;       //足場オブジェクト
    [SerializeField] private Transform upLimitObjTrf;   //足場オブジェクトの上昇限界

    [Header("リフトステータス")]
    [SerializeField] private const float speed = 5;       //移動速度
    [SerializeField] private float activeDist = 5;  //リフト起動

    [Header("プレイヤー座標")]
    [SerializeField] private Transform playerTrf;   //プレイヤートランスフォーム

    private Vector3 targetPos;      //目標座標
    private Vector3 initboardPos;   //足場初期座標

    private bool changeFlg = false;


    private void Start()
    {
        targetPos = upLimitObjTrf.position;
        initboardPos = boardObj.transform.position;
        activeDist *= activeDist;   //二乗の形にしておく

        if (boardObj == null)
            boardObj = gameObject.transform.GetChild(0).gameObject;

		if (Mng_Osumitsuki.instance == null)
		{
			Debug.Log("InstanceがNULLです");
		}
		else
		{
			Mng_Osumitsuki.instance.AddAllList(this);
		}

	}

    private void OnDestroy()
    {
    }

    public override void Action_Osumitsuki()
    {
		changeFlg = true;
		Action2Update();
    }

	public override void Osumitsuki_Tex()
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
		}
	}

    public override void Update_Osumitsuki()
    {
        if (boardObj.transform.position == targetPos)
        {
            Vector3 dif = boardObj.transform.position - playerTrf.position;
            if (dif.sqrMagnitude > activeDist)
            {
                osumitsukiFlg = false;
                End();
            }
            return;
        }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        Debug.Log("経過時間：" + Time.deltaTime + ", スピード：" + speed);
        boardObj.transform.position = Vector3.MoveTowards(boardObj.transform.position, targetPos, speed * dt);
    }

    private void FixedUpdate()
    {

        if (!osumitsukiTrg)
            return;

        if (osumitsukiFlg)
            return;

        if (playerTrf == null)
            playerTrf = GameObject.Find("player_v3").transform;

        if (boardObj.transform.position == initboardPos)
        {
            Vector3 dif = initboardPos - playerTrf.position;
            if (dif.sqrMagnitude < activeDist)
            {
                osumitsukiFlg = false;
                endFlg = false;
                Mng_Osumitsuki.instance.AddObject(this);
            }
        }
        else
        {
            boardObj.transform.position = Vector3.MoveTowards(boardObj.transform.position, initboardPos, speed * Time.deltaTime);
        }
    }

}
