using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// PaintableSurface (UV方式) のテスト用
/// - 左クリック: 墨を塗る
/// - 右クリック: density確認
/// - Cキー: 全クリア
///
/// ■ 注意: 塗りたいオブジェクトにはMeshColliderが必要
///   （BoxCollider/SphereColliderではUV座標が取れない）
/// </summary>
public class PaintableSurfaceTest : MonoBehaviour
{
    [Header("塗り設定")]
    [SerializeField] private float paintRadius = 1.0f;
    [SerializeField] private byte paintDensity = 200;

    private void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null) return;

        // 左クリック: 塗る
        if (mouse.leftButton.isPressed)
        {
            if (RaycastSurface(out RaycastHit hit))
            {
                var surface = hit.collider.GetComponent<PaintableSurface>()
                           ?? hit.collider.GetComponentInParent<PaintableSurface>();

                if (surface != null)
                {
                    surface.Paint(hit, paintRadius, paintDensity);
                }
                else if (mouse.leftButton.wasPressedThisFrame)
                {
                    Debug.LogWarning($"[PaintTest] Hit {hit.collider.gameObject.name} " +
                                     "but no PaintableSurface found");
                }
            }
        }

        // 右クリック: density確認
        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (RaycastSurface(out RaycastHit hit))
            {
                var surface = hit.collider.GetComponent<PaintableSurface>()
                           ?? hit.collider.GetComponentInParent<PaintableSurface>();

                if (surface != null)
                {
                    byte d = surface.GetDensity(hit);
                    bool canWalk = surface.CanWalk(hit);
                    Debug.Log($"[PaintTest] {hit.collider.gameObject.name} " +
                              $"uv=({hit.textureCoord.x:F2},{hit.textureCoord.y:F2}) " +
                              $"density={d} canWalk={canWalk}");
                }
                else
                {
                    Debug.Log($"[PaintTest] No PaintableSurface on " +
                              hit.collider.gameObject.name);
                }
            }
        }

        // Cキー: 全クリア
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
        {
            foreach (var s in FindObjectsOfType<PaintableSurface>())
                s.ClearAll();
            Debug.Log("[PaintTest] All surfaces cleared");
        }
    }

    private bool RaycastSurface(out RaycastHit hit)
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        return Physics.Raycast(ray, out hit, 500f, ~0, QueryTriggerInteraction.Collide);
    }

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 10, 500, 25),
            $"左クリック: 塗る (radius={paintRadius:F1}) / 右クリック: density / C: クリア");
        GUI.Label(new Rect(10, 30, 500, 25),
            "※ MeshCollider + ユニークUVメッシュのみ対応");
    }
}