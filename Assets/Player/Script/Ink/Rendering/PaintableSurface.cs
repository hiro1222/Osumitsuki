using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UV方式の統合インクサーフェス（設計書 第3章・第4章）
/// 
/// 1つのdensity配列から描画・判定・コリジョンを全て生成する。
/// メッシュのUV1座標をそのまま使う。
/// 
/// ■ 前提条件:
/// - メッシュのUV展開が重なっていないこと（ユニークUV）
/// - MeshColliderが必要（BoxCollider等ではtextureCoordが取れない）
/// - Unity標準のCube/Sphereは使えない（UVが重なっているため）
/// 
/// ■ セットアップ:
/// 1. カスタムメッシュのオブジェクトにアタッチ
/// 2. MeshColliderが付いていることを確認
/// 3. PaintableSurfaceInk シェーダーのマテリアルを適用
/// </summary>
public class PaintableSurface : MonoBehaviour
{
    // ── Inspector設定 ──
    [Header("Grid settings")]
    [Tooltip("density/コリジョングリッドの解像度（1辺）")]
    [SerializeField] private int gridResolution = 64;
    [Tooltip("通行可能の閾値（0〜255）")]
    [SerializeField] private byte walkThreshold = 50;

    [Header("Collision mesh")]
    [Tooltip("チャンクサイズ（セル数）")]
    [SerializeField] private int chunkSize = 16;
    [Tooltip("コリジョンメッシュの厚み（両面）")]
    [SerializeField] private float meshThickness = 0.15f;

    [Header("Rendering")]
    [Tooltip("描画用テクスチャの解像度")]
    [SerializeField] private int renderResolution = 256;

    // ── 内部データ ──
    // 単一データソース（描画・判定・コリジョン全ての元）
    private byte[] density;
    private byte[] colorId;      // 色番号配列（densityと同じサイズ）
    private int gridW, gridH;

    // UV→3D変換テーブル（Awake時にメッシュから構築）
    private Vector3[] cellPositions;   // 各グリッドセルのローカル3D位置
    private Vector3[] cellNormals;     // 各グリッドセルのローカル法線
    private bool[] cellValid;          // そのセルにメッシュの面があるか
    private float maxCellDistance;     // UV島境界判定用の距離閾値

    // 描画用
    private Texture2D densityTexture;
    private Texture2D colorTexture;     // 色番号を送るテクスチャ
    private MaterialPropertyBlock propBlock;
    private Renderer meshRenderer;
    private bool visualDirty;

    // コリジョン用（子オブジェクトに配置）
    private GameObject collisionChild;
    private MeshCollider inkCollider;
    private Mesh collisionMesh;
    private int chunksX, chunksY;
    private bool[] chunkDirty;

    // ── プロパティ ──
    public int GridW => gridW;
    public int GridH => gridH;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Awake()
    {
        meshRenderer = GetComponent<Renderer>();

        gridW = gridResolution;
        gridH = gridResolution;
        density = new byte[gridW * gridH];
        colorId = new byte[gridW * gridH];

        // MeshColliderの確認
        var mc = GetComponent<MeshCollider>();
        if (mc == null)
        {
            Debug.LogError($"[PaintableSurface] {gameObject.name}: MeshColliderが必要です");
            enabled = false;
            return;
        }

        // ── 親MeshColliderをTriggerにする ──
        // Trigger: CharacterControllerが物理衝突しない（墨がなければ落ちる）
        //          Raycast(QueryTriggerInteraction.Collide)では当たる（塗れる）
        // 非ConvexのままTrigger設定（Unity 2019+で動作）
        mc.isTrigger = true;

        // UV→3D変換テーブルを構築（設計書 3.3）
        BuildUVToWorldTable();

        // チャンク初期化
        chunksX = Mathf.CeilToInt((float)gridW / chunkSize);
        chunksY = Mathf.CeilToInt((float)gridH / chunkSize);
        chunkDirty = new bool[chunksX * chunksY];

        // インクコリジョン用の子オブジェクト（設計書 2.2）
        collisionChild = new GameObject($"{gameObject.name}_InkCollision");
        collisionChild.transform.SetParent(transform, false);

        // レイヤー設定: Player↔PlayerVSObject をON、Player↔Default をOFFにしておくことで
        // CharacterControllerはインクコリジョンにだけ衝突する
        int inkLayer = LayerMask.NameToLayer("PlayerVSObject");
        if (inkLayer >= 0)
        {
            collisionChild.layer = inkLayer;
        }
        else
        {
            Debug.LogWarning($"[PaintableSurface] 'PlayerVSObject' レイヤーが見つかりません。" +
                             "Edit > Project Settings > Tags and Layers で追加してください。");
            collisionChild.layer = gameObject.layer; // フォールバック
        }

        inkCollider = collisionChild.AddComponent<MeshCollider>();
        collisionMesh = new Mesh { name = $"InkCol_{gameObject.name}" };

        // 描画用テクスチャ（density配列をそのまま流し込む）
        densityTexture = new Texture2D(gridW, gridH, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        // 色番号テクスチャ（色番号をそのまま流し込む）
        // Point filterで色番号がブレンドされないようにする
        colorTexture = new Texture2D(gridW, gridH, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        propBlock = new MaterialPropertyBlock();
        visualDirty = false;
    }

    private void OnDestroy()
    {
        if (densityTexture != null) Destroy(densityTexture);
        if (colorTexture != null) Destroy(colorTexture);
        if (collisionMesh != null) Destroy(collisionMesh);
        if (collisionChild != null) Destroy(collisionChild);
    }

    // ====================================================================
    //  UV→3D変換テーブルの構築（設計書 3.3）
    // ====================================================================

    /// <summary>
    /// メッシュの全三角形をUV空間にラスタライズして、
    /// 各グリッドセルの3D位置と法線を記録する。
    /// コリジョンメッシュ生成時に「このUVセルの3D位置はどこか」を引くために使う。
    /// </summary>
    private void BuildUVToWorldTable()
    {
        cellPositions = new Vector3[gridW * gridH];
        cellNormals = new Vector3[gridW * gridH];
        cellValid = new bool[gridW * gridH];

        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return;

        Mesh mesh = mf.sharedMesh;
        Vector3[] verts = mesh.vertices;
        Vector3[] norms = mesh.normals;
        Vector2[] uvs = mesh.uv;
        int[] tris = mesh.triangles;

        if (uvs == null || uvs.Length == 0)
        {
            Debug.LogError($"[PaintableSurface] {gameObject.name}: メッシュにUVがありません");
            return;
        }

        // 各三角形をUV空間にラスタライズ
        for (int i = 0; i < tris.Length; i += 3)
        {
            int i0 = tris[i], i1 = tris[i + 1], i2 = tris[i + 2];

            Vector2 uv0 = uvs[i0], uv1 = uvs[i1], uv2 = uvs[i2];
            Vector3 p0 = verts[i0], p1 = verts[i1], p2 = verts[i2];
            Vector3 n0 = norms[i0], n1 = norms[i1], n2 = norms[i2];

            // この三角形がカバーするグリッドセルの範囲（AABB）
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
                    // グリッドセルの中心UV
                    float cu = (gx + 0.5f) / gridW;
                    float cv = (gy + 0.5f) / gridH;

                    // 重心座標で三角形内判定
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

        // 隣接セル間の平均3D距離を計算（UV島境界の判定閾値に使う）
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
        maxCellDistance = avgDist * 3f; // 平均の3倍を超えたらUV島の境界とみなす

#if UNITY_EDITOR
        int validCount = 0;
        for (int i = 0; i < cellValid.Length; i++)
            if (cellValid[i]) validCount++;
        Debug.Log($"[PaintableSurface] {gameObject.name}: UV table built. " +
                  $"{validCount}/{gridW * gridH} cells mapped. avgDist={avgDist:F4} maxDist={maxCellDistance:F4}");
#endif
    }

    /// <summary>重心座標で点が三角形内にあるか判定</summary>
    private bool BarycentricInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c,
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

    // ====================================================================
    //  Paint（設計書 3.4）
    // ====================================================================

    /// <summary>
    /// RaycastHitから墨を塗る（メイン入口）
    /// hit.textureCoord → UV → grid → density[] 書き込み
    ///                                + テクスチャ更新
    ///                                + コリジョンメッシュ再構築
    /// </summary>
    public void Paint(RaycastHit hit, float radius, byte inkDensity, byte inkColorId = 0)
    {
        // ヒット位置をローカル座標に変換（cellPositionsはローカル座標で保存されているため）
        Vector3 hitLocal = transform.InverseTransformPoint(hit.point);
        Vector2 uv = hit.textureCoord;
        float uvRadius = WorldRadiusToUV(radius);

        // ローカル空間での半径（lossyScaleで補正）
        Vector3 s = transform.lossyScale;
        float avgScale = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
        float localRadius = radius / Mathf.Max(avgScale, 0.0001f);
        float localRadiusSq = localRadius * localRadius;

        PaintInternal(uv, uvRadius, hitLocal, localRadiusSq, inkDensity, inkColorId);
    }

    /// <summary>UV座標のみで塗る（3D距離チェックなし。互換用）</summary>
    public void PaintAtUV(Vector2 uv, float uvRadius, byte inkDensity, byte inkColorId = 0)
    {
        // 3D距離チェックを無効化するためlocalRadiusSqにfloat.MaxValueを渡す
        PaintInternal(uv, uvRadius, Vector3.zero, float.MaxValue, inkDensity, inkColorId);
    }

    /// <summary>
    /// 内部Paint処理
    /// UV距離でブラシサイズを制御 + 3D距離でUVアイランド越えの染み出しを防ぐ
    /// </summary>
    private void PaintInternal(Vector2 uv, float uvRadius,
                               Vector3 hitLocal, float localRadiusSq,
                               byte inkDensity, byte inkColorId)
    {
        int cu = Mathf.FloorToInt(uv.x * gridW);
        int cv = Mathf.FloorToInt(uv.y * gridH);
        int cellRadius = Mathf.CeilToInt(uvRadius * Mathf.Max(gridW, gridH));

        int painted = 0;
        for (int dv = -cellRadius; dv <= cellRadius; dv++)
        {
            for (int du = -cellRadius; du <= cellRadius; du++)
            {
                int gx = cu + du;
                int gy = cv + dv;
                if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) continue;

                // UV空間での距離チェック（ブラシサイズ制御）
                float distU = (float)du / gridW;
                float distV = (float)dv / gridH;
                if (Mathf.Sqrt(distU * distU + distV * distV) > uvRadius) continue;

                int idx = gy * gridW + gx;
                if (!cellValid[idx]) continue;

                // 3D距離チェック: UV上は近くても3Dで遠いセルは別のUVアイランド
                // （例: UVアトラス上で隣接している別パーツへの染み出しを防ぐ）
                float dist3DSq = (cellPositions[idx] - hitLocal).sqrMagnitude;
                if (dist3DSq > localRadiusSq) continue;

                if (inkDensity > density[idx])
                {
                    density[idx] = inkDensity;
                    colorId[idx] = inkColorId;
                    MarkChunkDirty(gx, gy);
                    painted++;
                }
            }
        }

        if (painted > 0)
        {
            RebuildDirtyChunks();
            visualDirty = true;
        }
    }

    /// <summary>ワールド半径をUV空間の半径に概算変換（設計書 3.5）</summary>
    private float WorldRadiusToUV(float worldRadius)
    {
        var mf = GetComponent<MeshFilter>();
        if (mf == null || mf.sharedMesh == null) return 0.1f;

        Bounds b = mf.sharedMesh.bounds;
        Vector3 worldSize = Vector3.Scale(b.size, transform.lossyScale);
        float avgWorldSize = (worldSize.x + worldSize.y + worldSize.z) / 3f;
        if (avgWorldSize < 0.001f) return 0.1f;

        return worldRadius / avgWorldSize;
    }

    // ====================================================================
    //  判定（設計書 3.4）
    // ====================================================================

    /// <summary>RaycastHitから通行可能か判定</summary>
    public bool CanWalk(RaycastHit hit)
    {
        return GetDensityAtUV(hit.textureCoord) >= walkThreshold;
    }

    /// <summary>RaycastHitからdensityがあるか判定</summary>
    public bool HasDensityAt(RaycastHit hit)
    {
        return GetDensityAtUV(hit.textureCoord) > 0;
    }

    /// <summary>RaycastHitからdensity値を取得</summary>
    public byte GetDensity(RaycastHit hit)
    {
        return GetDensityAtUV(hit.textureCoord);
    }

    /// <summary>ワールド座標からdensity取得（Raycast不要の概算版）</summary>
    public byte GetDensityAt(Vector3 worldPos)
    {
        Vector3 local = transform.InverseTransformPoint(worldPos);
        float bestDist = float.MaxValue;
        byte bestDensity = 0;

        // 全セルを探索（重い。頻繁に呼ぶ場合はキャッシュを検討）
        for (int i = 0; i < cellValid.Length; i++)
        {
            if (!cellValid[i]) continue;
            float d = (cellPositions[i] - local).sqrMagnitude;
            if (d < bestDist)
            {
                bestDist = d;
                bestDensity = density[i];
            }
        }
        return bestDensity;
    }

    private byte GetDensityAtUV(Vector2 uv)
    {
        int gx = Mathf.FloorToInt(uv.x * gridW);
        int gy = Mathf.FloorToInt(uv.y * gridH);
        if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) return 0;
        return density[gy * gridW + gx];
    }

    // ====================================================================
    //  描画更新（設計書 1.3 パイプライン）
    // ====================================================================

    private void LateUpdate()
    {
        if (!visualDirty || meshRenderer == null) return;

        // density配列をテクスチャに流し込む
        densityTexture.SetPixelData(density, 0);
        densityTexture.Apply(false);

        // 色番号配列をテクスチャに流し込む
        colorTexture.SetPixelData(colorId, 0);
        colorTexture.Apply(false);

        // シェーダーに送信
        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetTexture("_InkTex", densityTexture);
        propBlock.SetTexture("_InkColorTex", colorTexture);
        propBlock.SetTexture("_InkPalette", InkPalette.GetPaletteTexture());
        meshRenderer.SetPropertyBlock(propBlock);

        visualDirty = false;
    }

    // ====================================================================
    //  コリジョンメッシュ生成（設計書 第4章）
    // ====================================================================

    private void MarkChunkDirty(int gx, int gy)
    {
        int cx = gx / chunkSize;
        int cy = gy / chunkSize;
        if (cx >= 0 && cx < chunksX && cy >= 0 && cy < chunksY)
            chunkDirty[cy * chunksX + cx] = true;
    }

    /// <summary>
    /// 隣接するcellPositionsを直接繋いでコリジョンメッシュを生成する。
    /// tangent/bitangent計算は不要。元メッシュの表面形状に自然に沿う。
    /// UV島の境界（3D距離が大きすぎるペア）はスキップする。
    /// </summary>
    private void RebuildDirtyChunks()
    {
        bool anyDirty = false;
        for (int i = 0; i < chunkDirty.Length; i++)
            if (chunkDirty[i]) { anyDirty = true; break; }
        if (!anyDirty) return;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        float halfThick = meshThickness * 0.5f;
        float maxDistSq = maxCellDistance * maxCellDistance;

        // 2x2セルブロックごとに処理
        for (int gy = 0; gy < gridH - 1; gy++)
        {
            for (int gx = 0; gx < gridW - 1; gx++)
            {
                int i00 = gy * gridW + gx;
                int i10 = i00 + 1;
                int i01 = i00 + gridW;
                int i11 = i01 + 1;

                // 4セルのdensityチェック（1つでも閾値以上なら処理対象）
                bool d00 = cellValid[i00] && density[i00] >= walkThreshold;
                bool d10 = cellValid[i10] && density[i10] >= walkThreshold;
                bool d01 = cellValid[i01] && density[i01] >= walkThreshold;
                bool d11 = cellValid[i11] && density[i11] >= walkThreshold;

                // 4つ全部なければスキップ
                if (!d00 && !d10 && !d01 && !d11) continue;

                // UV島境界チェック: 4辺全ての3D距離を確認
                // 1辺でも大きすぎたらこのquadはUV島をまたいでいるのでスキップ
                bool tooFar = false;
                if (cellValid[i00] && cellValid[i10])
                    tooFar |= (cellPositions[i10] - cellPositions[i00]).sqrMagnitude > maxDistSq;
                if (cellValid[i01] && cellValid[i11])
                    tooFar |= (cellPositions[i11] - cellPositions[i01]).sqrMagnitude > maxDistSq;
                if (cellValid[i00] && cellValid[i01])
                    tooFar |= (cellPositions[i01] - cellPositions[i00]).sqrMagnitude > maxDistSq;
                if (cellValid[i10] && cellValid[i11])
                    tooFar |= (cellPositions[i11] - cellPositions[i10]).sqrMagnitude > maxDistSq;
                if (tooFar) continue;

                // 4つ全部有効ならquad、3つならtriangle
                if (d00 && d10 && d01 && d11)
                {
                    // 4セルの法線の平均でオフセット
                    Vector3 avgNorm = (cellNormals[i00] + cellNormals[i10] +
                                       cellNormals[i01] + cellNormals[i11]).normalized;
                    Vector3 offset = avgNorm * halfThick;

                    // 表面
                    int bi = verts.Count;
                    verts.Add(cellPositions[i00] + offset);
                    verts.Add(cellPositions[i10] + offset);
                    verts.Add(cellPositions[i11] + offset);
                    verts.Add(cellPositions[i01] + offset);
                    tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
                    tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 3);

                    // 裏面
                    bi = verts.Count;
                    verts.Add(cellPositions[i00] - offset);
                    verts.Add(cellPositions[i11] - offset);
                    verts.Add(cellPositions[i10] - offset);
                    verts.Add(cellPositions[i01] - offset);
                    tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
                    tris.Add(bi); tris.Add(bi + 3); tris.Add(bi + 1);
                }
                else
                {
                    // 3セル以上が有効ならtriangleを生成
                    // 有効なセルの位置を集める
                    var validPositions = new List<Vector3>();
                    var validNormals = new List<Vector3>();
                    if (d00) { validPositions.Add(cellPositions[i00]); validNormals.Add(cellNormals[i00]); }
                    if (d10) { validPositions.Add(cellPositions[i10]); validNormals.Add(cellNormals[i10]); }
                    if (d11) { validPositions.Add(cellPositions[i11]); validNormals.Add(cellNormals[i11]); }
                    if (d01) { validPositions.Add(cellPositions[i01]); validNormals.Add(cellNormals[i01]); }

                    if (validPositions.Count >= 3)
                    {
                        Vector3 avgNorm = Vector3.zero;
                        for (int i = 0; i < validNormals.Count; i++)
                            avgNorm += validNormals[i];
                        avgNorm = avgNorm.normalized;
                        Vector3 offset = avgNorm * halfThick;

                        // 最初の3点で三角形
                        int bi = verts.Count;
                        verts.Add(validPositions[0] + offset);
                        verts.Add(validPositions[1] + offset);
                        verts.Add(validPositions[2] + offset);
                        tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);

                        // 裏面
                        bi = verts.Count;
                        verts.Add(validPositions[0] - offset);
                        verts.Add(validPositions[2] - offset);
                        verts.Add(validPositions[1] - offset);
                        tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 2);
                    }
                }
            }
        }

        // MeshCollider更新
        collisionMesh.Clear();
        if (verts.Count > 0)
        {
            collisionMesh.SetVertices(verts);
            collisionMesh.SetTriangles(tris, 0);
            collisionMesh.RecalculateNormals();
            collisionMesh.RecalculateBounds();
        }

        inkCollider.sharedMesh = null;
        inkCollider.sharedMesh = collisionMesh;

        for (int i = 0; i < chunkDirty.Length; i++)
            chunkDirty[i] = false;
    }

    // ====================================================================
    //  ユーティリティ
    // ====================================================================

    /// <summary>全グリッドをクリアしてコリジョンを消す</summary>
    public void ClearAll()
    {
        System.Array.Clear(density, 0, density.Length);
        System.Array.Clear(colorId, 0, colorId.Length);
        for (int i = 0; i < chunkDirty.Length; i++)
            chunkDirty[i] = true;
        RebuildDirtyChunks();
        visualDirty = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (cellValid == null || density == null) return;
        Gizmos.color = new Color(0, 0.5f, 0, 0.3f);
        for (int i = 0; i < cellValid.Length; i += 4)
        {
            if (cellValid[i] && density[i] >= walkThreshold)
            {
                Vector3 world = transform.TransformPoint(cellPositions[i]);
                Gizmos.DrawCube(world, Vector3.one * 0.05f);
            }
        }
    }
#endif
}