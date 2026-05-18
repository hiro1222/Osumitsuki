using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// InkManagerの動作テスト用
/// クリックした地面に墨を塗り、通行判定を確認できる
/// 
/// ■ セットアップ手順:
/// 1. シーンにPlane（地面）を配置し、Colliderがあることを確認
///    - Planeのスケール: (20, 1, 20) で200m x 200m相当
///    - 位置: (0, 0, 0)
/// 2. 空のGameObjectを作り、InkManager と InkDebugPainter をアタッチ
/// 3. 地面のマテリアルに inkTexture を表示するシェーダーを設定
///    （まずは後述の簡易シェーダーでOK）
/// 4. Playして左クリック → 墨が塗られる
///              右クリック → 通行判定チェック（コンソールに結果表示）
/// </summary>
public class InkDebugPainter : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InkManager inkManager;

    [Header("塗り設定")]
    [SerializeField] private float paintRadius = 1.5f;         // 塗りの半径（メートル）
    [SerializeField] private byte paintDensity = 200;           // 塗る濃さ
    [SerializeField] private bool useFalloff = true;            // 端を薄くするか
    [SerializeField] private byte edgeDensity = 30;             // 端の濃さ

    [Header("テクスチャ送信先")]
    [SerializeField] private Renderer groundRenderer;           // 地面のRenderer
    [SerializeField] private string textureProperty = "_InkTex"; // シェーダーのプロパティ名

    private MaterialPropertyBlock propBlock;

    private void Start()
    {
        if (inkManager == null)
            inkManager = GetComponent<InkManager>();

        propBlock = new MaterialPropertyBlock();

        Debug.Log($"[InkDebugPainter] グリッドサイズ: {InkManager.GRID_W}x{InkManager.GRID_D} " +
                  $"({inkManager.GridWorldWidth}m x {inkManager.GridWorldDepth}m)");
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null) return;

        // 左クリック: 塗る
        if (mouse.leftButton.isPressed)
        {
            if (RaycastGround(out Vector3 hitPoint))
            {
                if (useFalloff)
                {
                    inkManager.PaintCircleWithFalloff(
                        hitPoint.x, hitPoint.z,
                        paintRadius, paintDensity, edgeDensity);
                }
                else
                {
                    inkManager.PaintCircle(
                        hitPoint.x, hitPoint.z,
                        paintRadius, paintDensity);
                }
            }
        }

        // 右クリック: 通行判定テスト
        if (mouse.rightButton.wasPressedThisFrame)
        {
            if (RaycastGround(out Vector3 hitPoint))
            {
                bool canWalk = inkManager.CanWalk(hitPoint.x, hitPoint.z);
                bool unstable = inkManager.IsUnstable(hitPoint.x, hitPoint.z);
                byte d = inkManager.GetDensity(hitPoint.x, hitPoint.z);

                Debug.Log($"[通行判定] pos=({hitPoint.x:F1}, {hitPoint.z:F1}) " +
                          $"density={d} canWalk={canWalk} unstable={unstable}");
            }
        }

        // Cキー: 全クリア
        if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
        {
            inkManager.ClearAll();
            Debug.Log("[InkDebugPainter] グリッドをクリアしました");
        }

        // テクスチャを地面マテリアルに送る
        UpdateGroundTexture();
    }

    private void UpdateGroundTexture()
    {
        if (groundRenderer == null || inkManager.InkTexture == null) return;

        groundRenderer.GetPropertyBlock(propBlock);
        propBlock.SetTexture(textureProperty, inkManager.InkTexture);

        // UV変換用パラメータも送る（シェーダーでワールド座標→UV変換に使う）
        propBlock.SetVector("_InkGridOrigin",
            new Vector4(inkManager.GridOrigin.x, inkManager.GridOrigin.z, 0, 0));
        propBlock.SetVector("_InkGridSize",
            new Vector4(inkManager.GridWorldWidth, inkManager.GridWorldDepth, 0, 0));

        groundRenderer.SetPropertyBlock(propBlock);
    }

    private bool RaycastGround(out Vector3 hitPoint)
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        if (Physics.Raycast(ray, out RaycastHit hit, 500f))
        {
            hitPoint = hit.point;
            return true;
        }
        hitPoint = Vector3.zero;
        return false;
    }

    private void OnGUI()
    {
        // 画面左上に塗り面積を表示
        GUI.Label(new Rect(10, 10, 300, 25),
            $"塗り面積: {inkManager.GetPaintedAreaSqm():F1} m²  " +
            $"({inkManager.GetPaintedArea()} cells)");
        GUI.Label(new Rect(10, 35, 300, 25),
            "左クリック: 塗る / 右クリック: 通行判定 / C: クリア");
    }
}
