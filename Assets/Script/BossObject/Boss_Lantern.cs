using UnityEngine;

/// <summary>
/// 灯籠の体部分にアタッチするスクリプト
/// ボスのゴロゴロが実体化したPaintableSurfaceに当たったときに
/// Boss_LanternSetに通知する
///
/// 【セットアップ】
/// ① 灯籠の体部分のオブジェクトにアタッチ
/// ② InspectorにBoss_LanternSetをドラッグ
/// ③ PaintableSurfaceが同じオブジェクトについていること
/// </summary>
public class Boss_Lantern : MonoBehaviour
{
    [SerializeField] private Boss_LanternSet lanternSet;

    private float lastHitTime = -999f;
    private float hitCooldown = 3f;

    // 起動時にキャッシュ
    private int inkLayer;
    private Collider[] cachedColliders;

    private void Awake()
    {
        inkLayer = LayerMask.NameToLayer("PlayerVSObject");
        cachedColliders = GetComponentsInChildren<Collider>();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (Time.time - lastHitTime < hitCooldown) return;
        if (lanternSet == null) return;

        var boss = other.GetComponent<Boss_SB>();
        if (boss == null)
            boss = other.GetComponentInParent<Boss_SB>();
        if (boss == null) return;

        if (boss.GetCurrentPhase() != 1) return;
        if (!boss.GetIsRolling()) return;
        if (!IsBodySurfacePainted()) return;

        lastHitTime = Time.time;

        Debug.Log("[Boss_Lantern] ボスが灯籠に当たった！");
        Vector3 bossDir = boss.GetRollDirection();
        lanternSet.NotifyBossHit(boss.transform.position, bossDir);
    }

    private bool IsBodySurfacePainted()
    {
        if (inkLayer < 0) return false;

        // キャッシュ済みのコライダーを使う
        foreach (var col in cachedColliders)
        {
            if (col.gameObject.layer == inkLayer && col.enabled)
                return true;
        }
        return false;
    }
}