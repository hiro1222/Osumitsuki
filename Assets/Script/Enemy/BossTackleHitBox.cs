using UnityEngine;
public class BossTackleHitbox : MonoBehaviour
{
    [SerializeField] private Boss_SB boss;

    private void Awake()
    {
        if (boss == null)
            boss = GetComponentInParent<Boss_SB>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (boss == null) return;

        // 大砲に当たったら
        var cannon = other.GetComponent<Cannon_Osumitsuki>();
        if (cannon == null)
            cannon = other.GetComponentInParent<Cannon_Osumitsuki>();
        if (cannon != null)
        {
            // お墨付き済みの大砲のみ
            var osumi = other.GetComponent<Obj_Osumitsuki>();
            if (osumi == null)
                osumi = other.GetComponentInParent<Obj_Osumitsuki>();

            if (osumi != null && osumi.OsumiTrg)
            {
                var autoAim = other.GetComponent<CannonAutoAim>();
                if (autoAim == null)
                    autoAim = other.GetComponentInParent<CannonAutoAim>();

                var cannonComp = other.GetComponent<Cannon_Osumitsuki>();
                if (cannonComp == null)
                    cannonComp = other.GetComponentInParent<Cannon_Osumitsuki>();

                boss.NotifyHitCannonTackle(autoAim, osumi, cannonComp);
                return;
            }
            else
            {
                boss.NotifyHitCrateNoStun();
            }
            return;
        }

        var woodBox = other.GetComponent<Boss_WoodBox>();
        if (woodBox == null)
            woodBox = other.GetComponentInParent<Boss_WoodBox>();
        if (woodBox == null) return;

        int inkLayer = LayerMask.NameToLayer("PlayerVSObject");
        if (inkLayer >= 0 && other.gameObject.layer == inkLayer)
        {
            Debug.Log("[BossTackleHitbox] 実体化！スタン");
            boss.NotifyHitCrate();
        }
        else
        {
            Debug.Log("[BossTackleHitbox] 塗り量不足。停止のみ");
            boss.NotifyHitCrateNoStun();
        }
    }
}