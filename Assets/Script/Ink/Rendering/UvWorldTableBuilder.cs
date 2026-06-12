using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// メッシュのUVから「UV→3Dワールド変換テーブル」を構築する処理。
/// PaintableSurface から切り出した重い初期化ロジック（責務分割 P2-Step1）。
///
/// ■ 特徴:
/// - 実行時状態に依存しない純粋なメッシュ依存処理 → 将来のベイク対象(A1)
/// - 結果(cellPositions/cellNormals/cellValid/maxCellDistance)は呼び出し側(PaintableSurface)が保持する
/// </summary>
internal static class UvWorldTableBuilder
{
    // 全インスタンス共通の再利用バッファ（Awakeは順次実行なので競合なし）
    private static List<Vector3> s_verts, s_norms;
    private static List<Vector2> s_uvs;
    private static List<int> s_tris;

    /// <summary>
    /// メッシュサイズと targetCellSize から解像度を自動計算
    /// 例: 10m × 10m のオブジェクトで targetCellSize=0.05m なら
    ///     必要な解像度 = 10 / 0.05 = 200 → NextPowerOfTwo(200) = 256
    /// </summary>
    public static int CalculateResolution(MeshFilter mf, Transform t, float targetCellSize,
                                          int minRes, int maxRes, int fallbackRes, string debugName)
    {
        if (mf == null || mf.sharedMesh == null)
        {
            Debug.LogWarning($"[PaintableSurface] {debugName}: MeshFilterなし、デフォルト解像度を使用");
            return Mathf.Clamp(fallbackRes, minRes, maxRes);
        }

        // メッシュのワールド空間でのサイズ（lossyScale適用）
        Bounds b = mf.sharedMesh.bounds;
        Vector3 worldSize = Vector3.Scale(b.size, t.lossyScale);
        float maxDimension = Mathf.Max(Mathf.Abs(worldSize.x),
                                        Mathf.Abs(worldSize.y),
                                        Mathf.Abs(worldSize.z));

        if (maxDimension < 0.001f || targetCellSize < 0.001f)
        {
            return Mathf.Clamp(fallbackRes, minRes, maxRes);
        }

        // 目標セルサイズから必要な解像度を逆算
        int desired = Mathf.CeilToInt(maxDimension / targetCellSize);

        // 2の累乗に切り上げ（テクスチャ的に効率的）
        int powerOfTwo = Mathf.NextPowerOfTwo(desired);

        // 範囲クランプ
        int clamped = Mathf.Clamp(powerOfTwo, minRes, maxRes);

#if UNITY_EDITOR
        Debug.Log($"[PaintableSurface] {debugName}: " +
                  $"size={maxDimension:F2}m, target={targetCellSize}m → " +
                  $"desired={desired} → resolution={clamped} " +
                  $"(1セル={maxDimension / clamped * 100:F1}cm)");
#endif

        return clamped;
    }

    /// <summary>
    /// UV→3D変換テーブルを構築する。出力配列(gridW*gridH)は内部で確保する。
    /// メッシュが無い / UVが無い場合は空テーブル(cellValid全false, maxCellDistance=0)を返す。
    /// </summary>
    public static void Build(Mesh mesh, int gridW, int gridH, string debugName,
                             out Vector3[] cellPositions, out Vector3[] cellNormals,
                             out bool[] cellValid, out float maxCellDistance)
    {
        cellPositions = new Vector3[gridW * gridH];
        cellNormals = new Vector3[gridW * gridH];
        cellValid = new bool[gridW * gridH];
        maxCellDistance = 0f;

        if (mesh == null) return;

        // 全インスタンス共通の再利用バッファ（Awakeは順次実行なので競合なし）
        s_verts ??= new List<Vector3>();
        s_norms ??= new List<Vector3>();
        s_uvs ??= new List<Vector2>();
        s_tris ??= new List<int>();
        mesh.GetVertices(s_verts);   // ← 既存配列に詰め直すだけ＝ゴミ0
        mesh.GetNormals(s_norms);
        mesh.GetUVs(0, s_uvs);
        mesh.GetTriangles(s_tris, 0);
        var verts = s_verts; var norms = s_norms; var uvs = s_uvs; var tris = s_tris;

        if (uvs.Count == 0)
        {
            Debug.LogError($"[PaintableSurface] {debugName}: メッシュにUVがありません");
            return;
        }

        // 各三角形をUV空間にラスタライズ
        for (int i = 0; i < tris.Count; i += 3)
        {
            int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];

            Vector2 uv0 = uvs[i0], uv1 = uvs[i1], uv2 = uvs[i2];
            Vector3 p0 = verts[i0], p1 = verts[i1], p2 = verts[i2];
            Vector3 n0 = norms[i0], n1 = norms[i1], n2 = norms[i2];

            float minU = Mathf.Min(uv0.x, uv1.x, uv2.x);
            float maxU = Mathf.Max(uv0.x, uv1.x, uv2.x);
            float minV = Mathf.Min(uv0.y, uv1.y, uv2.y);
            float maxV = Mathf.Max(uv0.y, uv1.y, uv2.y);

            int startX = Mathf.Max(0, Mathf.FloorToInt(minU * gridW));
            int endX = Mathf.Min(gridW - 1, Mathf.CeilToInt(maxU * gridW));
            int startY = Mathf.Max(0, Mathf.FloorToInt(minV * gridH));
            int endY = Mathf.Min(gridH - 1, Mathf.CeilToInt(maxV * gridH));

            for (int gy = startY; gy <= endY; gy++)
            {
                for (int gx = startX; gx <= endX; gx++)
                {
                    float cu = (gx + 0.5f) / gridW;
                    float cv = (gy + 0.5f) / gridH;

                    if (BarycentricInTriangle(new Vector2(cu, cv), uv0, uv1, uv2,
                            out float w0, out float w1, out float w2))
                    {
                        int idx = gy * gridW + gx;
                        cellPositions[idx] = p0 * w0 + p1 * w1 + p2 * w2;
                        cellNormals[idx] = (n0 * w0 + n1 * w1 + n2 * w2).normalized;
                        cellValid[idx] = true;
                    }
                }
            }
        }

        // 隣接セル間の平均3D距離を計算（UV島境界の判定閾値）
        float totalDist = 0f;
        int distCount = 0;
        for (int gy = 0; gy < gridH; gy++)
        {
            for (int gx = 0; gx < gridW; gx++)
            {
                int idx = gy * gridW + gx;
                if (!cellValid[idx]) continue;

                if (gx + 1 < gridW && cellValid[idx + 1])
                {
                    totalDist += (cellPositions[idx + 1] - cellPositions[idx]).magnitude;
                    distCount++;
                }
                if (gy + 1 < gridH && cellValid[idx + gridW])
                {
                    totalDist += (cellPositions[idx + gridW] - cellPositions[idx]).magnitude;
                    distCount++;
                }
            }
        }
        float avgDist = distCount > 0 ? totalDist / distCount : 0.1f;
        maxCellDistance = avgDist * 3f;

#if UNITY_EDITOR
        int validCount = 0;
        for (int i = 0; i < cellValid.Length; i++)
            if (cellValid[i]) validCount++;
        Debug.Log($"[PaintableSurface] {debugName}: UV table built. " +
                  $"{validCount}/{gridW * gridH} cells mapped. avgDist={avgDist:F4} maxDist={maxCellDistance:F4}");
#endif
    }

    private static bool BarycentricInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c,
                                              out float w0, out float w1, out float w2)
    {
        Vector2 v0 = b - a, v1 = c - a, v2 = p - a;
        float d00 = Vector2.Dot(v0, v0);
        float d01 = Vector2.Dot(v0, v1);
        float d11 = Vector2.Dot(v1, v1);
        float d20 = Vector2.Dot(v2, v0);
        float d21 = Vector2.Dot(v2, v1);

        float denom = d00 * d11 - d01 * d01;
        if (Mathf.Abs(denom) < 1e-8f)
        {
            w0 = w1 = w2 = 0;
            return false;
        }

        float invDenom = 1f / denom;
        w1 = (d11 * d20 - d01 * d21) * invDenom;
        w2 = (d00 * d21 - d01 * d20) * invDenom;
        w0 = 1f - w1 - w2;

        return w0 >= -0.001f && w1 >= -0.001f && w2 >= -0.001f;
    }
}
