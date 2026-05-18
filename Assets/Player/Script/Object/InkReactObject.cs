using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 墨を塗られたときに子オブジェクトのマテリアルを変えてぴょんと跳ねるスクリプト
/// 親オブジェクトに1つアタッチするだけで子オブジェクト全部を管理できる
/// 既存スクリプトは一切変更しない
///
/// 【仕組み】
/// 子オブジェクトのPaintableSurfaceの塗り量合計を毎フレーム監視する
/// 塗り量が増えたらカウントし、規定回数でマテリアル変更＋跳ねる
///
/// 【セットアップ】
/// ① 親オブジェクトにこのスクリプトをアタッチ
/// ② 子オブジェクトそれぞれに MeshCollider + PaintableSurface をアタッチ
/// ③ Inspectorの「子オブジェクトのマテリアル設定」に各子のRenderer・Before・Afterを設定
/// </summary>
public class InkReactObject : MonoBehaviour
{
    // ====================================================================
    //  子オブジェクトごとのマテリアル設定
    // ====================================================================

    [System.Serializable]
    private class RendererMaterialSetting
    {
        [Tooltip("マテリアルを変えたいRenderer")]
        public Renderer targetRenderer;
        [Tooltip("塗られる前のマテリアル")]
        public Material beforeMaterial;
        [Tooltip("塗られた後のマテリアル")]
        public Material afterMaterial;
    }

    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("塗り設定")]
    [Tooltip("何回塗り量が増加したらマテリアルが変わるか")]
    [SerializeField] private int requiredInkCount = 3;
    [Tooltip("1フレームの塗り量変化を検知する閾値")]
    [SerializeField] private byte detectThreshold = 10;

    [Header("子オブジェクトのマテリアル設定")]
    [SerializeField] private List<RendererMaterialSetting> rendererSettings = new List<RendererMaterialSetting>();

    [Header("跳ねアニメーション")]
    [SerializeField] private float bounceHeight = 0.5f;
    [SerializeField] private float bounceDuration = 0.3f;

    // ====================================================================
    //  内部状態（全てprivate）
    // ====================================================================

    // 子オブジェクトのPaintableSurfaceリスト
    private List<PaintableSurface> surfaces = new List<PaintableSurface>();

    private int inkHitCount = 0;
    private bool hasChanged = false;

    // 前フレームの塗り量合計
    private long prevTotalDensity = 0;

    // 跳ねアニメーション
    private bool isBouncing = false;
    private float bounceTimer = 0f;
    private Vector3 bounceBasePos;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Start()
    {
        foreach (var setting in rendererSettings)
        {
            if (setting.targetRenderer != null && setting.beforeMaterial != null)
                setting.targetRenderer.material = setting.beforeMaterial;
        }
    }

    // ====================================================================
    //  毎フレーム
    // ====================================================================

    private void Update()
    {
        if (isBouncing) UpdateBounce();
    }

    // ====================================================================
    //  塗り量の監視
    // ====================================================================

    private void MonitorDensity()
    {
        long totalDensity = GetTotalDensityFromAllSurfaces();

        // 前フレームより増えていたら塗られたとカウント
        if (totalDensity > prevTotalDensity + detectThreshold)
        {
            inkHitCount++;
            Debug.Log($"[InkReactObject] 塗り検知: {inkHitCount} / {requiredInkCount}");

            if (inkHitCount >= requiredInkCount)
            {
                ChangeMaterials();
                StartBounce();
            }
        }

        prevTotalDensity = totalDensity;
    }

    /// <summary>全PaintableSurfaceの塗り量合計を取得</summary>
    private long GetTotalDensityFromAllSurfaces()
    {
        long total = 0;

        foreach (var surface in surfaces)
        {
            if (surface == null) continue;
            total += GetDensityFromSurface(surface);
        }

        return total;
    }

    /// <summary>1つのPaintableSurfaceの塗り量をサンプリングして取得</summary>
    private long GetDensityFromSurface(PaintableSurface surface)
    {
        int w = surface.GridW;
        int h = surface.GridH;
        long total = 0;

        var mf = surface.GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return 0;

        Mesh mesh = mf.sharedMesh;
        Vector2[] uvs = mesh.uv;
        Vector3[] verts = mesh.vertices;

        if (uvs == null || uvs.Length == 0) return 0;

        // サンプリング間隔（負荷軽減のため間引く）
        int step = Mathf.Max(1, w / 16);

        for (int gy = 0; gy < h; gy += step)
        {
            for (int gx = 0; gx < w; gx += step)
            {
                float u = (gx + 0.5f) / w;
                float v = (gy + 0.5f) / h;

                Vector3 worldPos = GetWorldPosFromUV(u, v, uvs, verts, surface.transform);
                if (worldPos == Vector3.zero) continue;

                Ray ray = new Ray(worldPos + Vector3.up * 0.1f, Vector3.down);
                if (Physics.Raycast(ray, out RaycastHit hit, 0.5f,
                    ~0, QueryTriggerInteraction.Collide))
                {
                    if (hit.collider.gameObject == surface.gameObject)
                        total += surface.GetDensity(hit);
                }
            }
        }

        return total;
    }

    /// <summary>UV座標からワールド座標を近似取得する</summary>
    private Vector3 GetWorldPosFromUV(float u, float v, Vector2[] uvs, Vector3[] verts, Transform t)
    {
        Vector2 targetUV = new Vector2(u, v);
        float minDist = float.MaxValue;
        Vector3 bestPos = Vector3.zero;

        for (int i = 0; i < uvs.Length; i++)
        {
            float dist = Vector2.SqrMagnitude(uvs[i] - targetUV);
            if (dist < minDist)
            {
                minDist = dist;
                bestPos = t.TransformPoint(verts[i]);
            }
        }

        return bestPos;
    }

    // ====================================================================
    //  マテリアル変更（全子オブジェクト一括）
    // ====================================================================

    private void ChangeMaterials()
    {
        foreach (var setting in rendererSettings)
        {
            if (setting.targetRenderer == null) continue;
            if (setting.afterMaterial == null)
            {
                Debug.LogWarning($"[InkReactObject] {setting.targetRenderer.name} のAfterMaterialが未設定です");
                continue;
            }

            setting.targetRenderer.material = setting.afterMaterial;
        }

        hasChanged = true;
        Debug.Log("[InkReactObject] 全マテリアル変更完了！");
    }

    // ====================================================================
    //  跳ねアニメーション
    // ====================================================================

    private void StartBounce()
    {
        isBouncing = true;
        bounceTimer = 0f;
        bounceBasePos = transform.position;
    }

    private void UpdateBounce()
    {
        bounceTimer += Time.deltaTime;
        float t = bounceTimer / bounceDuration;

        float yOffset = Mathf.Sin(t * Mathf.PI) * bounceHeight;
        transform.position = bounceBasePos + Vector3.up * yOffset;

        if (bounceTimer >= bounceDuration)
        {
            transform.position = bounceBasePos;
            isBouncing = false;
        }
    }

    /// <summary>マテリアルが変わったかどうかを返す</summary>
    public bool GetHasChanged() => hasChanged;

    public void ReceiveInk()
    {
        if (hasChanged) return;
        inkHitCount++;
        if (inkHitCount >= requiredInkCount)
        {
            ChangeMaterials();
            StartBounce();
        }
    }
}