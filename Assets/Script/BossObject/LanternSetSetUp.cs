using UnityEngine;

/// <summary>
/// LanternSetのプレハブ化対応
/// AllyEnemyManagerを自動で取得してObj_Osumitsukiに設定する
/// プレイヤー側を変えずにAllyEnemyManagerを参照できる
/// </summary>
public class LanternSetSetup : MonoBehaviour
{
    private void Start()
    {
        // AllyEnemyManagerを自動取得
        var manager = FindObjectOfType<AllyEnemyManager>();
        if (manager == null)
        {
            Debug.LogWarning("[LanternSetSetup] AllyEnemyManagerが見つかりません");
            return;
        }

        var lanternSet = GetComponentInChildren<Boss_LanternSet>();
        if (lanternSet == null) return;

        // 子オブジェクトのObj_Osumitsukiに設定
        var objList = GetComponentsInChildren<Obj_Osumitsuki>();

        Debug.Log($"[LanternSetSetup] 取得したObj_Osumitsuki数: {objList.Length}");
        foreach (var obj in objList)
        {
            Debug.Log($"[LanternSetSetup] 対象: {obj.gameObject.name}");
            // SerializeFieldなのでReflectionで設定
            var field = typeof(Obj_Osumitsuki).GetField(
                "allyEnemyManager",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(obj, manager);
                Debug.Log($"[LanternSetSetup] AllyEnemyManager設定完了: {obj.gameObject.name}");
            }
        }
    }
}