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
    [Tooltip("ONにすると、メッシュサイズと目標セルサイズから自動で解像度を計算する")]
    [SerializeField] private bool autoGridResolution = true;
    [Tooltip("自動計算時の目標セルサイズ（メートル）。小さいほど細かく塗れる。デフォルト=5cm")]
    [SerializeField] private float targetCellSize = 0.05f;
    [Tooltip("自動計算時の最小/最大解像度（メモリ節約のための上限）")]
    [SerializeField] private int minGridResolution = 32;
    [SerializeField] private int maxGridResolution = 512;
    [Tooltip("固定モード時の解像度（autoGridResolution=OFFのときに使う）")]
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

    // 描画（テクスチャ生成・アップロードは InkSurfaceRenderer に委譲）
    private InkSurfaceRenderer inkRenderer;
    private bool visualDirty;

    // コリジョン（チャンク管理は InkCollisionChunks に委譲）
    private InkCollisionChunks chunks;

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
        // 解像度を決定（自動 or 固定）
        int finalResolution = autoGridResolution
            ? CalculateAutoResolution()
            : gridResolution;

        gridW = finalResolution;
        gridH = finalResolution;
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

        // 親メッシュを SumiVSObject レイヤーに入れる
        // （Player↔SumiVSObject の衝突をOFFにしておくことで、isTriggerなしでも
        //   CharacterControllerは親メッシュをすり抜ける。凹メッシュはisTriggerにできないため）
        int sumiLayer = LayerMask.NameToLayer("SumiVSObject");
        if (sumiLayer >= 0)
        {
            gameObject.layer = sumiLayer;
        }
        else
        {
            Debug.LogWarning($"[PaintableSurface] 'SumiVSObject' レイヤーが見つかりません。" +
                             "Edit > Project Settings > Tags and Layers で追加してください。");
        }

        BuildUVToWorldTable();

        // チャンク初期化（コリジョン生成は InkCollisionChunks に委譲）
        int inkLayer = LayerMask.NameToLayer("PlayerVSObject");
        if (inkLayer < 0)
        {
            Debug.LogWarning($"[PaintableSurface] 'PlayerVSObject' レイヤーが見つかりません。" +
                             "Edit > Project Settings > Tags and Layers で追加してください。");
            inkLayer = gameObject.layer;
        }
        chunks = new InkCollisionChunks();
        chunks.Init(transform, gameObject.name, inkLayer,
                    gridW, gridH, chunkSize, meshThickness, walkThreshold, maxCellDistance,
                    cellValid, density, cellPositions, paintedNormals, cellNormals);

        // 描画（テクスチャ生成は InkSurfaceRenderer に委譲）
        inkRenderer = new InkSurfaceRenderer();
        inkRenderer.Init(GetComponent<Renderer>(), gridW, gridH);
        visualDirty = false;

        // ── 旧互換: Obj_Osumitsuki への直接通知用 ──
        // 新しいコードは OnPainted イベントを使ってください
#pragma warning disable CS0618
        obj_osumi = GetComponent<Obj_Osumitsuki>();
#pragma warning restore CS0618
    }

    private void OnDestroy()
    {
        inkRenderer?.Dispose();
        chunks?.Dispose();
    }

    // ====================================================================
    //  UV→3D変換テーブルの構築
    // ====================================================================

    private void BuildUVToWorldTable()
    {
        var mf = GetComponent<MeshFilter>();
        Mesh mesh = (mf != null) ? mf.sharedMesh : null;
        UvWorldTableBuilder.Build(mesh, gridW, gridH, gameObject.name,
                                  out cellPositions, out cellNormals,
                                  out cellValid, out maxCellDistance);
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
        // インクコリジョン子オブジェクトに当たったRaycastHitは弾く
        // （InkCol_xxxメッシュにはUVがないのでtextureCoordが読めずエラーになる）
        if (IsInkChunkCollider(hit.collider))
        {
            return;
        }

        Vector3 hitLocal = transform.InverseTransformPoint(hit.point);
        Vector3 hitNormalLocal = transform.InverseTransformDirection(hit.normal).normalized;
        Vector2 uv = hit.textureCoord;
        float uvRadius = WorldRadiusToUV(radius);

        float avgScale = AvgScale();
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

    /// <summary>
    /// 隣接Plane用のPaint（PaintAreaから呼ばれる）
    /// 3D距離チェックを無効化することで、ヒット地点から少し離れていても
    /// そのサーフェスの該当UVエリア全体を塗れるようにする
    /// hit.normalは使ってpaintedNormalsに記録（表/裏判定は機能する）
    /// </summary>
    public void PaintNeighbor(RaycastHit hit, float radius, byte inkDensity, byte inkColorId = 0)
    {
        if (IsInkChunkCollider(hit.collider)) return;

        Vector3 hitLocal = transform.InverseTransformPoint(hit.point);
        Vector3 hitNormalLocal = transform.InverseTransformDirection(hit.normal).normalized;
        Vector2 uv = hit.textureCoord;
        float uvRadius = WorldRadiusToUV(radius);

        // 3D距離チェック無効化（float.MaxValue を渡す）
        PaintInternal(uv, uvRadius, hitLocal, hitNormalLocal, float.MaxValue, inkDensity, inkColorId);
    }

    private void PaintInternal(Vector2 uv, float uvRadius,
                               Vector3 hitLocal, Vector3 hitNormalLocal,
                               float localRadiusSq,
                               byte inkDensity, byte inkColorId)
    {
        ApplyBrush(uv, uvRadius, hitLocal, hitNormalLocal, localRadiusSq,
                   inkDensity, inkColorId, erase: false);
    }

    /// <summary>
    /// ブラシ範囲のセルを走査し、塗り(erase=false) or 消し(erase=true)を適用する共通処理。
    /// - 走査条件は両者共通（UV円形 + 有効セル + 3D距離）
    /// - 塗り: density加算(255飽和) + color上書き + OnPainted発火
    /// - 消し: density/color/normal を 0 に戻す
    /// ※ デリゲートを使わずbool分岐にしているのは、毎フレーム呼ばれるためGCゴミを出さないため
    /// </summary>
    private void ApplyBrush(Vector2 uv, float uvRadius,
                            Vector3 hitLocal, Vector3 hitNormalLocal,
                            float localRadiusSq,
                            byte inkDensity, byte inkColorId, bool erase)
    {
        int cu = Mathf.FloorToInt(uv.x * gridW);
        int cv = Mathf.FloorToInt(uv.y * gridH);
        int cellRadius = Mathf.CeilToInt(uvRadius * Mathf.Max(gridW, gridH));

        bool useHitNormal = !erase && hitNormalLocal.sqrMagnitude > 0.01f;

        int changed = 0;       // 実際に変化したセル数（メッシュ再構築判定用）
        int hitCells = 0;      // ブラシ範囲内にヒットしたセル数（飽和済みも含む。OnPainted用）

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

                if (erase)
                {
                    if (density[idx] == 0) continue;
                    density[idx] = 0;
                    colorId[idx] = 0;
                    paintedNormals[idx] = Vector3.zero;
                    MarkChunkDirty(gx, gy);
                    changed++;
                }
                else
                {
                    hitCells++;

                    int newDensity = density[idx] + inkDensity;
                    if (newDensity > 255) newDensity = 255;

                    if (newDensity != density[idx] || inkColorId != colorId[idx])
                    {
                        density[idx] = (byte)newDensity;
                        colorId[idx] = inkColorId;
                        paintedNormals[idx] = useHitNormal ? hitNormalLocal : cellNormals[idx];
                        MarkChunkDirty(gx, gy);
                        changed++;
                    }
                }
            }
        }

        if (changed > 0)
        {
            RebuildDirtyChunks();
            visualDirty = true;
        }

        if (!erase && hitCells > 0)
        {
            OnPainted?.Invoke(hitCells, inkDensity);                        // per-surface（後方互換）
            InkPaintService.RaisePainted(gameObject, hitCells, inkDensity); // Service経由グローバル
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

    /// <summary>lossyScaleの3軸平均（ローカル半径・厚みの換算に使用）</summary>
    private float AvgScale()
    {
        Vector3 s = transform.lossyScale;
        return (Mathf.Abs(s.x) + Mathf.Abs(s.y) + Mathf.Abs(s.z)) / 3f;
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
        if (IsInkChunkCollider(hit.collider)) return;

        Vector3 hitLocal = transform.InverseTransformPoint(hit.point);
        Vector2 uv = hit.textureCoord;
        float uvRadius = WorldRadiusToUV(radius);

        float avgScale = AvgScale();
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

        float avgScale = AvgScale();
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
        ApplyBrush(uv, uvRadius, hitLocal, Vector3.zero, localRadiusSq,
                   0, 0, erase: true);
    }

    // ====================================================================
    //  判定
    // ====================================================================

    public bool CanWalk(RaycastHit hit)
    {
        if (IsInkChunkCollider(hit.collider)) return false;
        return GetDensityAtUV(hit.textureCoord) >= walkThreshold;
    }

    public bool HasDensityAt(RaycastHit hit)
    {
        if (IsInkChunkCollider(hit.collider)) return false;
        return GetDensityAtUV(hit.textureCoord) > 0;
    }

    public byte GetDensity(RaycastHit hit)
    {
        if (IsInkChunkCollider(hit.collider)) return 0;
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
        if (!visualDirty) return;
        inkRenderer?.Upload(density, colorId);
        visualDirty = false;
    }

    // ====================================================================
    //  コリジョンメッシュ生成
    // ====================================================================

    private void MarkChunkDirty(int gx, int gy) => chunks?.MarkDirty(gx, gy);

    private void RebuildDirtyChunks() => chunks?.RebuildDirty(AvgScale());

    /// <summary>当たったコライダーが自分のインクチャンクのどれかか判定</summary>
    private bool IsInkChunkCollider(Collider col) => chunks != null && chunks.IsInkChunkCollider(col);

    // ====================================================================
    //  解像度の自動計算
    // ====================================================================

    /// <summary>
    /// メッシュサイズと targetCellSize から解像度を自動計算
    /// 例: 10m × 10m のオブジェクトで targetCellSize=0.05m なら
    ///     必要な解像度 = 10 / 0.05 = 200 → NextPowerOfTwo(200) = 256
    /// </summary>
    private int CalculateAutoResolution()
    {
        return UvWorldTableBuilder.CalculateResolution(
            GetComponent<MeshFilter>(), transform, targetCellSize,
            minGridResolution, maxGridResolution, gridResolution, gameObject.name);
    }

    // ====================================================================
    //  コリジョン制御
    // ====================================================================

    /// <summary>インクコリジョンを有効化（全チャンク）</summary>
    public void EnableInkCollider() => chunks?.EnableAll();

    /// <summary>
    /// インクコリジョンを無効化（全チャンク）
    /// 塗りデータ自体は残るが、プレイヤーは塗った場所を通り抜けるようになる
    /// </summary>
    public void DisableInkCollider() => chunks?.DisableAll();

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
        chunks.MarkAllDirty();
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