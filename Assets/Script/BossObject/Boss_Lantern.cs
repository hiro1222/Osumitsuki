using UnityEngine;

public class Boss_Lantern : MonoBehaviour
{
    [SerializeField] private Boss_LanternSet lanternSet;
    private float lastHitTime = -999f;
    private float hitCooldown = 0.5f;
    private int inkLayer;
    private Collider[] cachedColliders;

    private void Awake()
    {
        inkLayer = LayerMask.NameToLayer("PlayerVSObject");
    }

    private void Start()
    {
        cachedColliders = GetComponentsInChildren<Collider>();

        // ★ lanternSetを自動取得
        if (lanternSet == null)
            lanternSet = GetComponentInParent<Boss_LanternSet>();
        if (lanternSet == null)
            lanternSet = FindObjectOfType<Boss_LanternSet>();

        Debug.Log($"[Boss_Lantern] lanternSet={lanternSet?.gameObject.name}");
    }

    private void OnTriggerEnter(Collider other)
{
        if (Time.time - lastHitTime < hitCooldown) return;
        Debug.Log($"[Boss_Lantern] lanternSet={lanternSet}");
        if (lanternSet == null) return;

         var boss = other.GetComponent<Boss_SB>();
        if (boss == null)
            boss = other.GetComponentInParent<Boss_SB>();
        if (boss == null) return;
        if (boss.GetCurrentPhase() != 1) return;
        if (!boss.GetIsRolling()) return;

        // ★ 塗り判定の結果をログ
         bool isPainted = IsBodySurfacePainted();
         Debug.Log($"[Boss_Lantern] 当たった！塗り判定={isPainted} オブジェクト={gameObject.name}");

        if (!isPainted) return;

        lastHitTime = Time.time;
        Debug.Log("[Boss_Lantern] ボスが灯籠に当たった！");
        Vector3 bossDir = boss.GetRollDirection();
        lanternSet.NotifyBossHit(boss.transform.position, bossDir);
    }

    private bool IsBodySurfacePainted()
    {
        var osumi = GetComponent<Obj_Osumitsuki>();
        if (osumi == null)
            osumi = GetComponentInParent<Obj_Osumitsuki>();
        if (osumi == null)
            osumi = GetComponentInChildren<Obj_Osumitsuki>();

        Debug.Log($"[Boss_Lantern] osumi={osumi?.gameObject.name} OsumiTrg={osumi?.OsumiTrg}");

        if (osumi != null)
            return osumi.OsumiTrg;

        return false;
    }
}