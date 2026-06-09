using UnityEngine;

/// <summary>
/// 大砲のAllyEnemyManagerを自動取得するスクリプト
/// Prefabにアタッチしておく
/// </summary>
public class CannonSetup : MonoBehaviour
{
    private void Start()
    {
        var manager = FindObjectOfType<AllyEnemyManager>();
        if (manager == null)
        {
            Debug.LogWarning("[CannonSetup] AllyEnemyManagerが見つかりません");
            return;
        }

        // ★ Cannon_Osumitsukiから直接取得（継承元のフィールドに設定）
        var osumiList = GetComponentsInChildren<Cannon_Osumitsuki>(true);
        if (osumiList.Length == 0)
        {
            // 自身もチェック
            var self = GetComponent<Cannon_Osumitsuki>();
            if (self != null)
                osumiList = new Cannon_Osumitsuki[] { self };
        }

        foreach (var osumi in osumiList)
        {
            var field = typeof(Obj_Osumitsuki).GetField(
                "allyEnemyManager",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (field != null)
            {
                field.SetValue(osumi, manager);
                Debug.Log($"[CannonSetup] AllyEnemyManager設定完了: {osumi.gameObject.name}");
            }
            else
            {
                Debug.LogWarning("[CannonSetup] allyEnemyManagerフィールドが見つかりません");
            }
        }
    }
}