using Unity.VisualScripting;
using UnityEngine;

public class Seesaw_Osumitsuki : Obj_Osumitsuki
{

	[Header("天秤ステータス")]
	[SerializeField] private float rotationLimit;   //回転制限
	[SerializeField] private float activeDist;      //発動距離

	[Header("参照オブジェクト")]
	[SerializeField] private GameObject joint_Center;
	[SerializeField] private GameObject joint_Left;
	[SerializeField] private GameObject joint_Rigth;
	[SerializeField] private Transform playerTarget;
	[SerializeField] private Transform player;


	private float allJoint_rotation;
	private float leftWeight;
	private float rightWeight;

	private int moveVec = 0;
	private bool changeFlg = false;


	private void Start()
	{
		activeDist *= activeDist;
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

		Vector3 dif = player.transform.position - playerTarget.position;
		if (dif.sqrMagnitude > activeDist)
		{
			osumitsukiFlg = false;
			End();
		}
		Action2Update();
	}

	public override void Update_Osumitsuki()
	{
	}

	private void SynchroRotation()
	{
		if (allJoint_rotation > 180)
			allJoint_rotation = allJoint_rotation - 360;

		if (allJoint_rotation < -rotationLimit)
			allJoint_rotation = -rotationLimit;
		if (allJoint_rotation > rotationLimit)
			allJoint_rotation = rotationLimit;

		joint_Center.transform.localRotation = Quaternion.Euler(0, 0, allJoint_rotation);
		joint_Left.transform.localRotation = Quaternion.Euler(0, 0, -allJoint_rotation);
		joint_Rigth.transform.localRotation = Quaternion.Euler(0, 0, -allJoint_rotation);
	}

	private void FixedUpdate()
	{
		if (!osumitsukiTrg)
			return;


		SeesawFunc();
		Vector3 dif = player.transform.position - playerTarget.position;

		if (osumitsukiFlg)
		{
			if (dif.sqrMagnitude > activeDist)
			{
				osumitsukiFlg = false;
				End();
			}
		}
		else if (dif.sqrMagnitude <= activeDist)
		{
			osumitsukiFlg = false;
			endFlg = false;
			SearchOsumitsuki_Obj();
			Mng_Osumitsuki.instance.AddObject(this);
			Debug.Log("傾いていくよ");
			if (GetHelperNum() == 4)
			{
				moveVec = -1;
			}
			else
			{
				moveVec = 1;
			}
			return;
		}

		if (!osumitsukiFlg)
		{
			Debug.Log("傾き直し");
			if (allJoint_rotation < 0)
				moveVec = 1;
			else if (allJoint_rotation > 0)
				moveVec = -1;
		}
	}


	private void SeesawFunc()
	{
		allJoint_rotation += 0.1f * moveVec;
		SynchroRotation();
	}
}
