using UnityEngine;

/// <summary>
/// ボスのタックル当たり判定
/// ボスの子オブジェクトにアタッチする
///
/// 【セットアップ】
/// ① BossEnemyの子オブジェクト（空のGameObject）を作る
/// ② このスクリプトをアタッチ
/// ③ CapsuleCollider をアタッチして Is Trigger ON にする
/// ④ InspectorでBossをドラッグ
/// </summary>
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

        // Boss_WoodBoxがついているオブジェクトに当たったとき
        var woodBox = other.GetComponent<Boss_WoodBox>();
        if (woodBox == null)
            woodBox = other.GetComponentInParent<Boss_WoodBox>();

        if (woodBox != null)
        {
            boss.NotifyHitCrate();
        }
    }
}