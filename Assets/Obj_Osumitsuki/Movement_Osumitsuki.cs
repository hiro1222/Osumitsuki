using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;



//このクラスは継承することを前提で作成、機能だけ実装してる
public class Movement_Osumitsuki : Obj_Osumitsuki
{
    [Header("移動ステータス")]
    [SerializeField] private List<GameObject> targets;  //自信が移動する目的地
    [SerializeField] private float turnSpd = 60;    //回転速度(１～１００)
    [SerializeField] private float moveSpd = 10;    //移動速度
    [SerializeField] private bool Y_lock = true;

    private Vector3 targetPos;          //目的地
    private int curTargetIndex = 0;     //現在ターゲットの
    private bool state = false;         //オブジェクトの状態 (true：回転 | false：移動)

	private BoxCollider restrictedArea;


	//初期化時１回だけ、派生クラスのStart()で呼び出し予定
	protected void SetupBaseData()
    {
        if (targets.Count <= 0)
        {
            curTargetIndex = -1;
            return;
        }

        curTargetIndex = 0;
        targetPos = targets[curTargetIndex].transform.position;
        state = true;

		Transform[] children = GetComponentsInChildren<Transform>();

		foreach (Transform child in children)
		{
			if (child.name == "RestrictedArea")
			{
				restrictedArea = child.gameObject.GetComponent<BoxCollider>();
                restrictedArea.enabled = false;
                Debug.Log("立ち入り禁止エリアを非アクティブに");
			}
		}
	}

    //移動処理
    private void Move()
    {
        Vector3 target = targetPos;
        if (Y_lock)
        {
            target.y = transform.position.y;
        }
        Vector3 newPos = Vector3.MoveTowards(transform.position, target, moveSpd * Time.deltaTime);

        transform.position = newPos;
        
        Vector3 difPos = target - transform.position;

        if (difPos.sqrMagnitude < 0.01f)
            ChangeTarget();

    }

    //目標変更
    private void ChangeTarget()
    {
        curTargetIndex++;
        if (curTargetIndex >= targets.Count)
        {
            curTargetIndex = -1;
            return;
        }
        targetPos = targets[curTargetIndex].transform.position;
        Switch_State();
    }



    //回転処理
    private void Rotate()
    {
        //ターゲットの向き
        Vector3 dir = (targetPos - transform.position).normalized;
        //上下の位置関係を無視
        dir.y = 0;
        dir *= -1;
        dir = dir.normalized;

        //方向がゼロでないか確認
        if (dir.sqrMagnitude < 0.0001f)
            Switch_State();

        //現在の向きからターゲットの方向へ向く(補間アリ)
        Quaternion targetRot = Quaternion.LookRotation(dir);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRot,
            turnSpd * Time.deltaTime
        );

        if (Quaternion.Angle(transform.rotation, targetRot) < 0.1f)
            Switch_State();
    }

    //回転と移動切り替え
    protected void Update_RotateMove()
    {
        if (restrictedArea != null)
        {
            restrictedArea.enabled = true;
            Debug.Log("立ち入り禁止エリアをアクティブに");
        }
		//ターゲットがいなければ終了
		if (curTargetIndex == -1)
        {
            if (restrictedArea != null)
            {
                restrictedArea.enabled = false;
                Debug.Log("立ち入り禁止エリアを非アクティブに");
            }
			End();
            return;
        }

        if (state)
        {
            Rotate();
        }
        else
        {
            Move();
        }
    }

    private void Switch_State()
    {
        state = !state;
    }

}
