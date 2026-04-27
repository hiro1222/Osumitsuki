using NUnit.Framework;
using UnityEngine;
using System.Collections.Generic;
using Unity.VisualScripting;



//このクラスは継承することを前提で作成、機能だけ実装してる
public class Movement_Osumitsuki : Obj_Osumitsuki
{

    [SerializeField] private List<GameObject> targets;  //自信が移動する目的地
    [SerializeField] private float turnSpd = 60;    //回転速度(１～１００)
    [SerializeField] private float moveSpd = 10;    //移動速度

    private Vector3 targetPos;          //目的地
    private int curTargetIndex = 0;     //現在ターゲットの
    private bool state = false;         //オブジェクトの状態 (true：回転 | false：移動)


    //初期化時１回だけ、派生クラスのStart()で呼び出し予定
    protected void SetupBaseData()
    {
        curTargetIndex = 0;
        targetPos = targets[curTargetIndex].transform.position;
        state = true;
    }

    //移動処理
    private void Move()
    {
        Vector3 newPos = Vector3.MoveTowards(transform.position, targetPos, moveSpd * Time.deltaTime);
        transform.position = newPos;

        if (targetPos == transform.position)
            ChangeTarget();

    }
    
    //回転処理
    private void Rotate()
    {
        //ターゲットの向き
        Vector3 dir = (targetPos - transform.position).normalized;
        //上下の位置関係を無視
        dir.y = 0;

        //方向がゼロでないか確認
        if (dir.sqrMagnitude < 0.0001f)
            return;

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

    //目標変更
    private void ChangeTarget()
    {
        targetPos = targets[curTargetIndex].transform.position;

        curTargetIndex++;
        if (curTargetIndex >= targets.Count)
        {
            curTargetIndex = -1;
            return;
        }
        targetPos = targets[curTargetIndex].transform.position;
        Switch_State();
    }

    //回転と移動切り替え
    protected void Update_RotateMove()
    {
        if (curTargetIndex == -1)
            return;

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
