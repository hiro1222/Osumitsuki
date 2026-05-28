using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// UV方式の統合インクサーフェス
/// 
/// 1つのdensity配列から描画・判定・コリジョンを全て生成する。
/// メッシュのUV1座標をそのまま使う。
/// 
/// ■ 主な機能:
/// - UV方式で塗り（textureCoord使用）
/// - density重ね塗り（255で飽和）
/// - 色は最新のものに上書き
/// - UV島境界の3D距離チェック（別パーツへの染み出し防止）
/// - 4セル揃ったときだけコリジョン生成（境界の飛び出し抑制）
/// - OnPaintedイベントで外部に塗り通知
/// - Erase（範囲消去）、ClearAll（全消去）
/// - EnableInkCollider/DisableInkCollider（コリジョン制御）
/// 
/// ■ 前提条件:
/// - メッシュのUV展開が重なっていないこと（ユニークUV）
/// - MeshColliderが必要（BoxCollider等ではtextureCoordが取れない）
/// - レイヤー "PlayerVSObject" を作成しておく
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
    [Tooltip("コリジョンメッシュの厚み")]
    [SerializeField] private float meshThickness = 0.15f;

    [Header("Rendering")]
    [Tooltip("描画用テクスチャの解像度")]
    [SerializeField] private int renderResolution = 256;

    // ── 内部データ ──
    private byte[] density;
    private byte[] colorId;
    private Vector3[] paintedNormals;
    private int gridW, gridH;

    // UV→3D変換テーブル
    private Vector3[] cellPositions;
    private Vector3[] cellNormals;
    private bool[] cellValid;
    private float maxCellDistance;

    // 描画用
    private Texture2D densityTexture;
    private Texture2D colorTexture;
    private MaterialPropertyBlock propBlock;
    private Renderer meshRenderer;
    private bool visualDirty;

    // コリジョン用
    private GameObject collisionChild;
    private MeshCollider inkCollider;
    private Mesh collisionMesh;
    private int chunksX, chunksY;
    private bool[] chunkDirty;

    // ── 旧互換用（DEPRECATED: 将来削除予定）──
    // 新しいコードでは OnPainted イベントを購読してください
    // 既存の Obj_Osumitsuki ベースのコードへの影響を避けるため一時的に残しています
    [System.Obsolete("OnPainted イベントを購読する形に移行してください")]
    private Obj_Osumitsuki obj_osumi;

    // ── プロパティ ──
    public int GridW => gridW;
    public int GridH => gridH;
    public bool VisualDirty => visualDirty;

    // ====================================================================
    //  イベント
    // ====================================================================

    /// <summary>
    /// 塗られたときに発火するイベント
    /// 引数: (ヒットしたセル数, 加算しようとしたdensity値)
    /// 飽和済みでもヒットすれば発火する（重ね塗り対応）
    /// 
    /// ■ 購読例:
    ///   ps.OnPainted += (cells, density) => Painted(0.5f);
    /// 
    /// ■ 親オブジェクトで集約したい場合は PaintableSurfaceGroup を使う
    /// </summary>
    public event System.Action<int, byte> OnPainted;

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
        paintedNormals = new Vector3[gridW * gridH];

        // MeshColliderチェック
        var mc = GetComponent<MeshCollider>();
        if (mc == null)
        {
            Debug.LogError($"[PaintableSurface] {gameObject.name}: MeshColliderが必要です");
            enabled = false;
            return;
        }

        // 親MeshColliderをTrigger化（CharacterControllerが物理衝突しない、Raycastは当たる）
        mc.isTrigger = true;

        BuildUVToWorldTable();

        // チャンク初期化
        chunksX = Mathf.CeilToInt((float)gridW / chunkSize);
        chunksY = Mathf.CeilToInt((float)gridH / chunkSize);
        chunkDirty = new bool[chunksX * chunksY];

        // インクコリジョン用の子オブジェクト
        collisionChild = new GameObject($"{gameObject.name}_InkCollision");
        collisionChild.transform.SetParent(transform, false);

        // レイヤー: Player↔PlayerVSObject = ON、Player↔Default = OFF
        int inkLayer = LayerMask.NameToLayer("PlayerVSObject");
        if (inkLayer >= 0)
        {
            collisionChild.layer = inkLayer;
        }
        else
        {
            Debug.LogWarning($"[PaintableSurface] 'PlayerVSObject' レイヤーが見つかりません。" +
                             "Edit > Project Settings > Tags and Layers で追加してください。");
            collisionChild.layer = gameObject.layer;
        }

        inkCollider = collisionChild.AddComponent<MeshCollider>();
        collisionMesh = new Mesh { name = $"InkCol_{gameObject.name}" };

        // 描画用テクスチャ
        densityTexture = new Texture2D(gridW, gridH, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        colorTexture = new Texture2D(gridW, gridH, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };

        propBlock = new MaterialPropertyBlock();
        visualDirty = false;

        // ── 旧互換: Obj_Osumitsuki への直接通知用 ──
        // 新しいコードは OnPainted イベントを使ってください
#pragma warning disable CS0618
        obj_osumi = GetComponent<Obj_Osumitsuki>();
#pragma warning restore CS0618
    }

    private void OnDestroy()
    {
        if (densityTexture != null) Destroy(densityTexture);
        if (colorTexture != null) Destroy(colorTexture);
        if (collisionMesh != null) Destroy(collisionMesh);
        if (collisionChild != null) Destroy(collisionChild);
    }

    // ====================================================================
    //  UV→3D変換テーブルの構築
    // ====================================================================

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
        Debug.Log($"[PaintableSurface] {gameObject.name}: UV table built. " +
                  $"{validCount}/{gridW * gridH} cells mapped. avgDist={avgDist:F4} maxDist={maxCellDistance:F4}");
#endif
    }

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
    //  Paint
    // ====================================================================

    /// <summary>
    /// RaycastHitから墨を塗る（メイン入口）
    /// 重ね塗り: density は加算（255飽和）、colorは最新のものに上書き
    /// </summary>
    public void Paint(RaycastHit hit, float radius, byte inkDensity, byte inkColorId = 0)
    {
        Vector3 hitLocal = transform.InverseTransformPoint(hit.point);
        Vector3 hitNormalLocal = transform.InverseTransformDirection(hit.normal).normalized;
        Vector2 uv = hit.textureCoord;
        float uvRadius = WorldRadiusToUV(radius);

        Vector3 s = transform.lossyScale;
        float avgScale = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
        float localRadius = radius / Mathf.Max(avgScale, 0.0001f);
        float localRadiusSq = localRadius * localRadius;

        // ── 旧互換: Obj_Osumitsuki への直接通知 ──
        // DEPRECATED: 新コードは OnPainted イベントを使うこと
#pragma warning disable CS0618
        if (obj_osumi != null)
        {
            float power = Mathf.Sqrt(localRadiusSq);
            obj_osumi.Painted(power);
        }
#pragma warning restore CS0618

        PaintInternal(uv, uvRadius, hitLocal, hitNormalLocal, localRadiusSq, inkDensity, inkColorId);
    }

    /// <summary>UV座標のみで塗る（3D距離チェックなし。互換用）</summary>
    public void PaintAtUV(Vector2 uv, float uvRadius, byte inkDensity, byte inkColorId = 0)
    {
        PaintInternal(uv, uvRadius, Vector3.zero, Vector3.zero, float.MaxValue, inkDensity, inkColorId);
    }

    private void PaintInternal(Vector2 uv, float uvRadius,
                               Vector3 hitLocal, Vector3 hitNormalLocal,
                               float localRadiusSq,
                               byte inkDensity, byte inkColorId)
    {
        int cu = Mathf.FloorToInt(uv.x * gridW);
        int cv = Mathf.FloorToInt(uv.y * gridH);
        int cellRadius = Mathf.CeilToInt(uvRadius * Mathf.Max(gridW, gridH));

        bool useHitNormal = hitNormalLocal.sqrMagnitude > 0.01f;

        int painted = 0;       // 実際に変化したセル数（メッシュ再構築判定用）
        int hitCells = 0;      // ブラシ範囲内にヒットしたセル数（飽和済みも含む）
        for (int dv = -cellRadius; dv <= cellRadius; dv++)
        {
            for (int du = -cellRadius; du <= cellRadius; du++)
            {
                int gx = cu + du;
                int gy = cv + dv;
                if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) continue;

                float distU = (float)du / gridW;
                float distV = (float)dv / gridH;
                if (Mathf.Sqrt(distU * distU + distV * distV) > uvRadius) continue;

                int idx = gy * gridW + gx;
                if (!cellValid[idx]) continue;

                float dist3DSq = (cellPositions[idx] - hitLocal).sqrMagnitude;
                if (dist3DSq > localRadiusSq) continue;

                hitCells++;

                int newDensity = density[idx] + inkDensity;
                if (newDensity > 255) newDensity = 255;

                if (newDensity != density[idx] || inkColorId != colorId[idx])
                {
                    density[idx] = (byte)newDensity;
                    colorId[idx] = inkColorId;
                    paintedNormals[idx] = useHitNormal ? hitNormalLocal : cellNormals[idx];
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

        if (hitCells > 0)
        {
            OnPainted?.Invoke(hitCells, inkDensity);
        }
    }

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
    //  Erase（消去）
    // ====================================================================

    /// <summary>
    /// RaycastHitの位置を中心に塗りを消す
    /// density と color と paintedNormal を 0 に戻す
    /// コリジョンメッシュも自動的に消える
    /// </summary>
    public void Erase(RaycastHit hit, float radius)
    {
        Vector3 hitLocal = transform.InverseTransformPoint(hit.point);
        Vector2 uv = hit.textureCoord;
        float uvRadius = WorldRadiusToUV(radius);

        Vector3 s = transform.lossyScale;
        float avgScale = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
        float localRadius = radius / Mathf.Max(avgScale, 0.0001f);
        float localRadiusSq = localRadius * localRadius;

        EraseInternal(uv, uvRadius, hitLocal, localRadiusSq);
    }

    /// <summary>
    /// ワールド座標を中心に塗りを消す（Raycast不要）
    /// 範囲内のセルを全探索するので少し重い
    /// </summary>
    public void EraseAt(Vector3 worldCenter, float radius)
    {
        Vector3 localCenter = transform.InverseTransformPoint(worldCenter);

        Vector3 s = transform.lossyScale;
        float avgScale = (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
        float localRadius = radius / Mathf.Max(avgScale, 0.0001f);
        float localRadiusSq = localRadius * localRadius;

        int erased = 0;
        for (int i = 0; i < cellValid.Length; i++)
        {
            if (!cellValid[i]) continue;
            if (density[i] == 0) continue;

            float dist3DSq = (cellPositions[i] - localCenter).sqrMagnitude;
            if (dist3DSq > localRadiusSq) continue;

            density[i] = 0;
            colorId[i] = 0;
            paintedNormals[i] = Vector3.zero;

            int gx = i % gridW;
            int gy = i / gridW;
            MarkChunkDirty(gx, gy);
            erased++;
        }

        if (erased > 0)
        {
            RebuildDirtyChunks();
            visualDirty = true;
        }
    }

    private void EraseInternal(Vector2 uv, float uvRadius,
                               Vector3 hitLocal, float localRadiusSq)
    {
        int cu = Mathf.FloorToInt(uv.x * gridW);
        int cv = Mathf.FloorToInt(uv.y * gridH);
        int cellRadius = Mathf.CeilToInt(uvRadius * Mathf.Max(gridW, gridH));

        int erased = 0;
        for (int dv = -cellRadius; dv <= cellRadius; dv++)
        {
            for (int du = -cellRadius; du <= cellRadius; du++)
            {
                int gx = cu + du;
                int gy = cv + dv;
                if (gx < 0 || gx >= gridW || gy < 0 || gy >= gridH) continue;

                float distU = (float)du / gridW;
                float distV = (float)dv / gridH;
                if (Mathf.Sqrt(distU * distU + distV * distV) > uvRadius) continue;

                int idx = gy * gridW + gx;
                if (!cellValid[idx]) continue;
                if (density[idx] == 0) continue;

                float dist3DSq = (cellPositions[idx] - hitLocal).sqrMagnitude;
                if (dist3DSq > localRadiusSq) continue;

                density[idx] = 0;
                colorId[idx] = 0;
                paintedNormals[idx] = Vector3.zero;
                MarkChunkDirty(gx, gy);
                erased++;
            }
        }

        if (erased > 0)
        {
            RebuildDirtyChunks();
            visualDirty = true;
        }
    }

    // ====================================================================
    //  判定
    // ====================================================================

    public bool CanWalk(RaycastHit hit)
    {
        return GetDensityAtUV(hit.textureCoord) >= walkThreshold;
    }

    public bool HasDensityAt(RaycastHit hit)
    {
        return GetDensityAtUV(hit.textureCoord) > 0;
    }

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
    //  描画更新
    // ====================================================================

    private void LateUpdate()
    {
        if (!visualDirty || meshRenderer == null) return;

        densityTexture.SetPixelData(density, 0);
        densityTexture.Apply(false);

        colorTexture.SetPixelData(colorId, 0);
        colorTexture.Apply(false);

        meshRenderer.GetPropertyBlock(propBlock);
        propBlock.SetTexture("_InkTex", densityTexture);
        propBlock.SetTexture("_InkColorTex", colorTexture);
        propBlock.SetTexture("_InkPalette", InkPalette.GetPaletteTexture());
        meshRenderer.SetPropertyBlock(propBlock);

        visualDirty = false;
    }

    // ====================================================================
    //  コリジョンメッシュ生成
    // ====================================================================

    private void MarkChunkDirty(int gx, int gy)
    {
        int cx = gx / chunkSize;
        int cy = gy / chunkSize;
        if (cx >= 0 && cx < chunksX && cy >= 0 && cy < chunksY)
            chunkDirty[cy * chunksX + cx] = true;
    }

    /// <summary>
    /// 隣接するcellPositionsを直接繋いでコリジョンメッシュを生成
    /// 4セル全てがwalkThreshold以上のブロックのみquad化
    /// UV島の境界（3D距離が大きすぎるペア）はスキップ
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

        for (int gy = 0; gy < gridH - 1; gy++)
        {
            for (int gx = 0; gx < gridW - 1; gx++)
            {
                int i00 = gy * gridW + gx;
                int i10 = i00 + 1;
                int i01 = i00 + gridW;
                int i11 = i01 + 1;

                bool v00 = cellValid[i00] && density[i00] >= walkThreshold;
                bool v10 = cellValid[i10] && density[i10] >= walkThreshold;
                bool v01 = cellValid[i01] && density[i01] >= walkThreshold;
                bool v11 = cellValid[i11] && density[i11] >= walkThreshold;

                if (!(v00 && v10 && v01 && v11)) continue;

                // UV島境界チェック: 4辺 + 2本の対角線
                bool tooFar = false;
                tooFar |= (cellPositions[i10] - cellPositions[i00]).sqrMagnitude > maxDistSq;
                tooFar |= (cellPositions[i11] - cellPositions[i01]).sqrMagnitude > maxDistSq;
                tooFar |= (cellPositions[i01] - cellPositions[i00]).sqrMagnitude > maxDistSq;
                tooFar |= (cellPositions[i11] - cellPositions[i10]).sqrMagnitude > maxDistSq;
                tooFar |= (cellPositions[i11] - cellPositions[i00]).sqrMagnitude > maxDistSq * 2f;
                tooFar |= (cellPositions[i01] - cellPositions[i10]).sqrMagnitude > maxDistSq * 2f;
                if (tooFar) continue;

                // 塗ったときの法線の平均（表/裏判定）
                Vector3 avgNorm = (paintedNormals[i00] + paintedNormals[i10] +
                                   paintedNormals[i01] + paintedNormals[i11]).normalized;
                if (avgNorm.sqrMagnitude < 0.01f)
                {
                    avgNorm = (cellNormals[i00] + cellNormals[i10] +
                               cellNormals[i01] + cellNormals[i11]).normalized;
                }
                Vector3 offset = avgNorm * halfThick;

                int bi = verts.Count;

                // 内側の層
                verts.Add(cellPositions[i00]);
                verts.Add(cellPositions[i10]);
                verts.Add(cellPositions[i11]);
                verts.Add(cellPositions[i01]);

                // 外側の層
                verts.Add(cellPositions[i00] + offset);
                verts.Add(cellPositions[i10] + offset);
                verts.Add(cellPositions[i11] + offset);
                verts.Add(cellPositions[i01] + offset);

                // 表面（外側）
                tris.Add(bi + 4); tris.Add(bi + 5); tris.Add(bi + 6);
                tris.Add(bi + 4); tris.Add(bi + 6); tris.Add(bi + 7);

                // 内側
                tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1);
                tris.Add(bi); tris.Add(bi + 3); tris.Add(bi + 2);

                // 側面
                tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 5);
                tris.Add(bi); tris.Add(bi + 5); tris.Add(bi + 4);
                tris.Add(bi + 1); tris.Add(bi + 2); tris.Add(bi + 6);
                tris.Add(bi + 1); tris.Add(bi + 6); tris.Add(bi + 5);
                tris.Add(bi + 2); tris.Add(bi + 3); tris.Add(bi + 7);
                tris.Add(bi + 2); tris.Add(bi + 7); tris.Add(bi + 6);
                tris.Add(bi + 3); tris.Add(bi); tris.Add(bi + 4);
                tris.Add(bi + 3); tris.Add(bi + 4); tris.Add(bi + 7);
            }
        }

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
    //  コリジョン制御
    // ====================================================================

    /// <summary>インクコリジョンを有効化</summary>
    public void EnableInkCollider()
    {
        if (inkCollider != null)
            inkCollider.enabled = true;
    }

    /// <summary>
    /// インクコリジョンを無効化
    /// 塗りデータ自体は残るが、プレイヤーは塗った場所を通り抜けるようになる
    /// </summary>
    public void DisableInkCollider()
    {
        if (inkCollider != null)
            inkCollider.enabled = false;
    }

    // ====================================================================
    //  全消去
    // ====================================================================

    /// <summary>全グリッドをクリアしてコリジョンを消す（デバッグ用にも使える）</summary>
    public void ClearAll()
    {
        System.Array.Clear(density, 0, density.Length);
        System.Array.Clear(colorId, 0, colorId.Length);
        for (int i = 0; i < paintedNormals.Length; i++)
            paintedNormals[i] = Vector3.zero;
        for (int i = 0; i < chunkDirty.Length; i++)
            chunkDirty[i] = true;
        RebuildDirtyChunks();
        visualDirty = true;
    }

    // ====================================================================
    //  デバッグ
    // ====================================================================

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