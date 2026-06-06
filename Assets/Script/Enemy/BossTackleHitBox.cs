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

        var woodBox = other.GetComponent<Boss_WoodBox>();
        if (woodBox == null)
            woodBox = other.GetComponentInParent<Boss_WoodBox>();

        if (woodBox == null) return;

        int inkLayer = LayerMask.NameToLayer("PlayerVSObject");
        if (inkLayer >= 0 && other.gameObject.layer == inkLayer)
        {
            // 実体化したコリジョンに当たった → スタン
            Debug.Log("[BossTackleHitbox] 実体化！スタン");
            boss.NotifyHitCrate();
        }
        else
        {
            // 実体化していないが木箱に当たった → スタンせずに停止だけ
            Debug.Log("[BossTackleHitbox] 塗り量不足。停止のみ");
            boss.NotifyHitCrateNoStun();
        }
    }
}