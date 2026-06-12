using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大砲のAllyEnemyManagerを自動取得するスクリプト
/// Prefabにアタッチしておく
/// </summary>
public class CannonSetup : MonoBehaviour
{
    // 元のマテリアルを記録
    private Dictionary<MeshRenderer, Material> originalMaterials = new Dictionary<MeshRenderer, Material>();


    private void Start()
    {

        // 元のマテリアルを記録
        var renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
            originalMaterials[r] = r.material;

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

    /// <summary>マテリアルを元に戻す</summary>
    public void ResetMaterials()
    {
        foreach (var kvp in originalMaterials)
            if (kvp.Key != null)
                kvp.Key.material = kvp.Value;
        Debug.Log("[CannonSetup] マテリアルリセット完了");
    }
}