using UnityEngine;
using UnityEngine.InputSystem;

public class Cannon_Osumitsuki : Obj_Osumitsuki
{

    [Header("大砲ステータス")]
    [SerializeField] private float power;           //発射力
    [SerializeField] private float rotateSpeed_H;   //横振り速度
    [SerializeField] private float rotateSpeed_V;   //縦ぶり速度
    [SerializeField] private float rotateLimit_V;   //縦ぶり限界値
    [SerializeField] private float coolShotTime;        //クールタイム
    [SerializeField] private float activeDist;      //起動する距離

    [Header("発射されるオブジェクトプレハブ")]
    [SerializeField] private GameObject bullet;

    [Header("参照オブジェクト")]
    [SerializeField] private Transform stage;       //つなぎ目
    [SerializeField] private Transform cannon;      //筒の部分
    [SerializeField] private Transform playerTrf;   //プレイヤートランスフォーム

    private bool changeFlg = false;


    private float curAngle_V;   //大砲縦回転角度
    private float curAngle_H;   //大砲横回転角度

    private float lastFireTime = 0;

    private void Start()
    {
        activeDist *= activeDist;

        //回転を180 ～ -180に変換
        curAngle_H = stage.transform.localEulerAngles.y;
        if (curAngle_H > 180f) curAngle_H -= 360f;

        curAngle_V = cannon.transform.localEulerAngles.z;
        if (curAngle_V > 180f) curAngle_V -= 360f;

		if (Mng_Osumitsuki.instance == null)
		{
			Debug.Log("InstanceがNULLです");
		}
		else
		{
			Mng_Osumitsuki.instance.AddAllList(this);
		}
	}


    public override void Action_Osumitsuki()
    {
        changeFlg = true;
        Action2Update();
    }

    public override void Osumitsuki_Tex()
    {
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
        if (Keyboard.current.rightArrowKey.isPressed)
            Rotate_H(new Vector3(0,1,0));
        if (Keyboard.current.leftArrowKey.isPressed)
            Rotate_H(new Vector3(0, -1, 0));

        if (Keyboard.current.upArrowKey.isPressed)
            Rotate_V(new Vector3(0, 0, -1));
        if (Keyboard.current.downArrowKey.isPressed)
            Rotate_V(new Vector3(0, 0, 1));

        if (Keyboard.current.enterKey.wasPressedThisFrame)
            Fire();

        Vector3 dif = transform.position - playerTrf.position;
        if (dif.sqrMagnitude >= activeDist)
        {
            osumitsukiFlg = false;
            End();
        }
    }

    //大砲発射
    private void Fire()
    {
        if (coolShotTime > Time.time - lastFireTime)
            return;

        lastFireTime = Time.time;

        GameObject obj = Instantiate(bullet);
        obj.transform.position = cannon.transform.position + cannon.forward * 1;
        obj.transform.rotation.Equals(cannon);
        Rigidbody rb = obj.GetComponent<Rigidbody>();

        rb.AddForce(-cannon.right * power);

        Debug.Log("打った");
    }

    //横振り
    private void Rotate_H(Vector3 _moveVec)
    {
        curAngle_H += _moveVec.y * rotateSpeed_H * Time.deltaTime;
        stage.localRotation = Quaternion.Euler(0, curAngle_H, 0);
    }
    //縦ぶり
    private void Rotate_V(Vector3 _moveVec)
    {
        curAngle_V += _moveVec.z * rotateSpeed_V * Time.deltaTime;

        if (curAngle_V < rotateLimit_V)
            curAngle_V = rotateLimit_V;
        if (curAngle_V > 10f)
            curAngle_V = 10f;
 
        cannon.localRotation = Quaternion.Euler(0, 0, curAngle_V);
    }


    private void FixedUpdate()
    {
		if (!osumitsukiTrg)
            return;

        if (osumitsukiFlg)
            return;

        Vector3 dif = transform.position - playerTrf.position;
        if (dif.sqrMagnitude < activeDist)
        {
            osumitsukiFlg = false;
            endFlg = false;
            Mng_Osumitsuki.instance.AddObject(this);
        }
    }

}
