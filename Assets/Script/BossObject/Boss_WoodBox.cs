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
    private void OnTriggerEnter(Collider other)
    {
        NotifyBoss(other.gameObject);
    }

    private void OnCollisionEnter(Collision collision)
    {
        NotifyBoss(collision.gameObject);
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