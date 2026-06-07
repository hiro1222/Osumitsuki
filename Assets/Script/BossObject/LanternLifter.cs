using UnityEngine;

/// <summary>灯籠を上に持ち上げるスクリプト</summary>
public class LanternLifter : MonoBehaviour
{
    [SerializeField] private Transform liftTarget;
    [SerializeField] private float liftSpeed = 5f;
    [SerializeField] private int requiredAllies = 3;

    private Obj_Osumitsuki osumitsuki;
    private bool isLifting = false;

    private void Start()
    {
        osumitsuki = GetComponent<Obj_Osumitsuki>();
    }

    private void Update()
    {
        if (osumitsuki == null) return;
        if (osumitsuki.OsumiFlg && !isLifting)
        {
            // 友好Enemyが必要数揃ったら持ち上げ開始
            if (osumitsuki.GetHelperNum() >= requiredAllies)
            {
                isLifting = true;
            }
        }

        if (isLifting && liftTarget != null)
        {
            transform.position = Vector3.MoveTowards(
                transform.position,
                liftTarget.position,
                liftSpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, liftTarget.position) < 0.1f)
            {
                isLifting = false;
            }
        }
    }
}