using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 大砲のセットアップを行うスクリプト（親 CANNON_Boss にアタッチ）
///
/// ・playerTrf / allyEnemyManager を自動セット（Inspector未設定対策）
/// ・子メッシュの塗り(PaintableSurfaceGroup)を親の Painted() に橋渡し
/// ・元マテリアルの記録
///
/// 【前提】
/// ・子メッシュ（STAND/YOKO/BODY/TATE/BARREL）には
///   PaintableSurface と MeshCollider を手動でアタッチしておくこと
/// ・親には PaintableSurfaceGroup と、ダミーの PaintableSurface + MeshCollider を付けておくこと
/// </summary>
public class CannonSetup : MonoBehaviour
{
    // 元のマテリアルを記録
    private Dictionary<MeshRenderer, Material> originalMaterials = new Dictionary<MeshRenderer, Material>();

    private void Start()
    {
        var osumi = GetComponent<Cannon_Osumitsuki>();
        if (osumi == null)
        {
            Debug.LogError("[CannonSetup] Cannon_Osumitsuki が同じオブジェクトに見つかりません");
            return;
        }

        // ── ① playerTrf を自動セット（未設定なら） ──
        var playerField = typeof(Cannon_Osumitsuki).GetField(
            "playerTrf",
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);

        if (playerField != null && (Transform)playerField.GetValue(osumi) == null)
        {
            var player = FindObjectOfType<PlayerMove>();
            if (player != null)
            {
                playerField.SetValue(osumi, player.transform);
                Debug.Log("[CannonSetup] playerTrf 自動設定完了");
            }
            else
            {
                Debug.LogWarning("[CannonSetup] PlayerMove が見つかりません");
            }
        }

        // ── ② allyEnemyManager を自動セット ──
        var manager = FindObjectOfType<AllyEnemyManager>();
        if (manager != null)
        {
            var mgrField = typeof(Obj_Osumitsuki).GetField(
                "allyEnemyManager",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance);

            if (mgrField != null)
            {
                mgrField.SetValue(osumi, manager);
                Debug.Log("[CannonSetup] AllyEnemyManager 設定完了");
            }
            else
            {
                Debug.LogWarning("[CannonSetup] allyEnemyManager フィールドが見つかりません");
            }
        }
        else
        {
            Debug.LogWarning("[CannonSetup] AllyEnemyManager が見つかりません");
        }

        // ── ③ 子メッシュの塗りを親の Painted() に橋渡し ──
        var group = GetComponent<PaintableSurfaceGroup>();
        if (group != null)
        {
            group.OnAnyPainted += (source, cells, density) => osumi.Painted(5f);
            Debug.Log($"[CannonSetup] Group購読完了 surfaces={group.SurfaceCount}");
        }
        else
        {
            Debug.LogWarning("[CannonSetup] PaintableSurfaceGroup が見つかりません");
        }

        // ── ④ 元マテリアルを記録 ──
        var renderers = GetComponentsInChildren<MeshRenderer>();
        foreach (var r in renderers)
            originalMaterials[r] = r.material;
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