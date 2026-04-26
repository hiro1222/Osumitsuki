using UnityEngine;

/// <summary>
/// 墨グリッドの管理クラス（ロジック層）
/// - ワールド空間を2Dグリッドに分割し、各セルにdensity（墨の濃さ）を保持
/// - 塗り・通行判定・ダメージ判定・面積計算を担当
/// - グリッドデータをTexture2Dに変換してシェーダーに送る
/// </summary>
public class InkManager : MonoBehaviour
{
    // ── グリッド設定 ──
    // 設計書: 1セル=50cm, 400x400 = 200m x 200m
    public const int CELL_SIZE_CM = 50;
    public const int GRID_W = 400;
    public const int GRID_D = 400;

    // ── 閾値 ──
    // 設計書セクション4.2: density >= 50 で通行可能
    public const byte WALK_THRESHOLD = 50;
    public const byte UNSTABLE_MAX = 49;   // 1〜49: 不安定（滑る等）
    public const byte STRONG_INK_MIN = 200; // 200〜255: 濃い墨

    // ── グリッドの原点（ワールド座標） ──
    // グリッドの左下隅のワールド座標。デフォルトでは(-100, 0, -100)で
    // (0,0,0)を中心に200m x 200mをカバーする
    [Header("グリッド原点（ワールド座標）")]
    [SerializeField] private Vector3 gridOrigin = new Vector3(-100f, 0f, -100f);

    [Header("描画先（地面のRenderer）")]
    [SerializeField] private Renderer groundRenderer;
    [SerializeField] private string textureProperty = "_InkTex";

    // ── グリッドデータ ──
    // 設計書: InkCell { density, flags, timestamp }
    // まずはdensityだけで十分。flags/timestampは拡張時に追加
    private byte[] density;

    // ── テクスチャ（GPU送信用） ──
    // 設計書セクション4.4: 400x400 x 1byte (R8) = 160KB
    private Texture2D inkTexture;
    private bool textureDirty;

    // ── 塗り面積キャッシュ ──
    private int paintedCellCount;
    private bool areaDirty;

    // ── マテリアル更新用 ──
    private MaterialPropertyBlock propBlock;

    // ── プロパティ ──
    /// <summary>グリッドのdensityをテクスチャ化したもの。シェーダーに渡す用</summary>
    public Texture2D InkTexture => inkTexture;

    /// <summary>グリッドの原点ワールド座標</summary>
    public Vector3 GridOrigin => gridOrigin;

    /// <summary>グリッド全体のワールドサイズ（メートル）</summary>
    public float GridWorldWidth => GRID_W * CELL_SIZE_CM * 0.01f;  // 200m
    public float GridWorldDepth => GRID_D * CELL_SIZE_CM * 0.01f;  // 200m

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Awake()
    {
        // density配列を確保（全セル0 = 塗られていない）
        density = new byte[GRID_W * GRID_D];

        // R8テクスチャを作成（FilterMode.Bilinearで滑らかに補間）
        inkTexture = new Texture2D(GRID_W, GRID_D, TextureFormat.R8, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        textureDirty = false;
        areaDirty = true;
        paintedCellCount = 0;
        propBlock = new MaterialPropertyBlock();
    }

    private void OnDestroy()
    {
        if (inkTexture != null)
        {
            Destroy(inkTexture);
        }
    }

    // ====================================================================
    //  毎フレーム: テクスチャ更新
    // ====================================================================

    private void LateUpdate()
    {
        if (textureDirty)
        {
            UploadTexture();
            SendToGround();
            textureDirty = false;
        }
    }

    /// <summary>density配列をTexture2Dに書き込み、GPUにアップロード</summary>
    private void UploadTexture()
    {
        inkTexture.SetPixelData(density, 0);
        inkTexture.Apply(false);
    }

    /// <summary>テクスチャとUV変換パラメータをground rendererに送る</summary>
    private void SendToGround()
    {
        if (groundRenderer == null) return;

        groundRenderer.GetPropertyBlock(propBlock);
        propBlock.SetTexture(textureProperty, inkTexture);
        propBlock.SetVector("_InkGridOrigin",
            new Vector4(gridOrigin.x, gridOrigin.z, 0, 0));
        propBlock.SetVector("_InkGridSize",
            new Vector4(GridWorldWidth, GridWorldDepth, 0, 0));
        groundRenderer.SetPropertyBlock(propBlock);
    }

    // ====================================================================
    //  座標変換
    // ====================================================================

    /// <summary>ワールド座標 → グリッド座標</summary>
    public void WorldToGrid(float wx, float wz, out int gx, out int gz)
    {
        // ワールド座標から原点を引いて、セルサイズ(0.5m)で割る
        gx = Mathf.FloorToInt((wx - gridOrigin.x) / (CELL_SIZE_CM * 0.01f));
        gz = Mathf.FloorToInt((wz - gridOrigin.z) / (CELL_SIZE_CM * 0.01f));
    }

    /// <summary>グリッド座標 → ワールド座標（セル中心）</summary>
    public Vector3 GridToWorld(int gx, int gz)
    {
        float cellSize = CELL_SIZE_CM * 0.01f;
        return new Vector3(
            gridOrigin.x + (gx + 0.5f) * cellSize,
            gridOrigin.y,
            gridOrigin.z + (gz + 0.5f) * cellSize
        );
    }

    /// <summary>グリッド範囲内か</summary>
    private bool InBounds(int gx, int gz)
    {
        return gx >= 0 && gx < GRID_W && gz >= 0 && gz < GRID_D;
    }

    /// <summary>1D配列のインデックス</summary>
    private int Index(int gx, int gz)
    {
        return gz * GRID_W + gx;
    }

    // ====================================================================
    //  読み取り
    // ====================================================================

    /// <summary>指定ワールド座標のdensityを取得（範囲外は0）</summary>
    public byte GetDensity(float wx, float wz)
    {
        WorldToGrid(wx, wz, out int gx, out int gz);
        if (!InBounds(gx, gz)) return 0;
        return density[Index(gx, gz)];
    }

    /// <summary>
    /// 通行可能か判定
    /// 設計書セクション4.2: density >= WALK_THRESHOLD(50) なら通れる
    /// </summary>
    public bool CanWalk(float wx, float wz)
    {
        return GetDensity(wx, wz) >= WALK_THRESHOLD;
    }

    /// <summary>不安定エリアか（1〜49: 滑る・遅くなる等）</summary>
    public bool IsUnstable(float wx, float wz)
    {
        byte d = GetDensity(wx, wz);
        return d >= 1 && d <= UNSTABLE_MAX;
    }

    // ====================================================================
    //  塗り処理
    // ====================================================================

    /// <summary>
    /// 円形に塗る（筆塗り・着弾の基本）
    /// 設計書セクション4.3: PaintCircle
    /// </summary>
    /// <param name="wx">中心ワールドX</param>
    /// <param name="wz">中心ワールドZ</param>
    /// <param name="radius">半径（メートル）</param>
    /// <param name="newDensity">書き込むdensity値</param>
    public void PaintCircle(float wx, float wz, float radius, byte newDensity)
    {
        WorldToGrid(wx, wz, out int cx, out int cz);

        float cellSize = CELL_SIZE_CM * 0.01f;
        int cellRadius = Mathf.CeilToInt(radius / cellSize);

        for (int dz = -cellRadius; dz <= cellRadius; dz++)
        {
            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                int gx = cx + dx;
                int gz = cz + dz;
                if (!InBounds(gx, gz)) continue;

                // セル中心までの距離をチェック
                float dist = Mathf.Sqrt(dx * dx + dz * dz) * cellSize;
                if (dist > radius) continue;

                // 既存値より大きい場合のみ上書き（濃い墨が勝つ）
                int idx = Index(gx, gz);
                if (newDensity > density[idx])
                {
                    density[idx] = newDensity;
                    areaDirty = true;
                }
            }
        }

        textureDirty = true;
    }

    /// <summary>
    /// 円形に塗る（Falloff付き: 中心が濃く、端が薄い）
    /// InkSpreadDataのcenterDensity / edgeDensityに対応
    /// </summary>
    public void PaintCircleWithFalloff(float wx, float wz, float radius,
                                        byte centerDensity, byte edgeDensity)
    {
        WorldToGrid(wx, wz, out int cx, out int cz);

        float cellSize = CELL_SIZE_CM * 0.01f;
        int cellRadius = Mathf.CeilToInt(radius / cellSize);

        for (int dz = -cellRadius; dz <= cellRadius; dz++)
        {
            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                int gx = cx + dx;
                int gz = cz + dz;
                if (!InBounds(gx, gz)) continue;

                float dist = Mathf.Sqrt(dx * dx + dz * dz) * cellSize;
                if (dist > radius) continue;

                // 中心→端でdensityを線形補間
                float t = dist / radius;
                byte d = (byte)Mathf.Lerp(centerDensity, edgeDensity, t);

                int idx = Index(gx, gz);
                if (d > density[idx])
                {
                    density[idx] = d;
                    areaDirty = true;
                }
            }
        }

        textureDirty = true;
    }

    /// <summary>
    /// 斬撃形状で塗る（矩形）
    /// 設計書セクション4.3: PaintSlash
    /// </summary>
    /// <param name="wx">起点ワールドX</param>
    /// <param name="wz">起点ワールドZ</param>
    /// <param name="dirX">斬撃方向X（正規化済み）</param>
    /// <param name="dirZ">斬撃方向Z（正規化済み）</param>
    /// <param name="length">斬撃の長さ（メートル）</param>
    /// <param name="width">斬撃の幅（メートル）</param>
    /// <param name="newDensity">書き込むdensity値</param>
    public void PaintSlash(float wx, float wz, float dirX, float dirZ,
                           float length, float width, byte newDensity)
    {
        float cellSize = CELL_SIZE_CM * 0.01f;

        // 斬撃の矩形をカバーするAABBを計算
        float halfWidth = width * 0.5f;
        float maxExtent = Mathf.Max(length, halfWidth);
        int cellExtent = Mathf.CeilToInt(maxExtent / cellSize);

        WorldToGrid(wx, wz, out int cx, out int cz);

        // 斬撃方向の法線（右方向）
        float perpX = -dirZ;
        float perpZ = dirX;

        for (int dz = -cellExtent; dz <= cellExtent; dz++)
        {
            for (int dx = -cellExtent; dx <= cellExtent; dx++)
            {
                int gx = cx + dx;
                int gz = cz + dz;
                if (!InBounds(gx, gz)) continue;

                // セルの中心のワールド座標
                float cellWx = (gx + 0.5f) * cellSize + gridOrigin.x;
                float cellWz = (gz + 0.5f) * cellSize + gridOrigin.z;

                // 起点からの相対位置
                float relX = cellWx - wx;
                float relZ = cellWz - wz;

                // 斬撃方向への射影（前方距離）
                float forward = relX * dirX + relZ * dirZ;
                if (forward < 0 || forward > length) continue;

                // 法線方向への射影（横方向距離）
                float lateral = Mathf.Abs(relX * perpX + relZ * perpZ);
                if (lateral > halfWidth) continue;

                int idx = Index(gx, gz);
                if (newDensity > density[idx])
                {
                    density[idx] = newDensity;
                    areaDirty = true;
                }
            }
        }

        textureDirty = true;
    }

    /// <summary>
    /// 扇形に塗る（横一文字・斜め斬撃用）
    /// 設計書セクション5.3: 扇形の当たり判定
    /// </summary>
    /// <param name="wx">扇の中心ワールドX</param>
    /// <param name="wz">扇の中心ワールドZ</param>
    /// <param name="dirX">中心方向X（正規化済み）</param>
    /// <param name="dirZ">中心方向Z（正規化済み）</param>
    /// <param name="radius">扇の半径（メートル）</param>
    /// <param name="arcAngleDeg">扇の角度（度数法、例:120で左右60°ずつ）</param>
    /// <param name="newDensity">書き込むdensity値</param>
    public void PaintArc(float wx, float wz, float dirX, float dirZ,
                         float radius, float arcAngleDeg, byte newDensity)
    {
        float cellSize = CELL_SIZE_CM * 0.01f;
        int cellRadius = Mathf.CeilToInt(radius / cellSize);
        float halfAngleRad = arcAngleDeg * 0.5f * Mathf.Deg2Rad;
        float cosHalfAngle = Mathf.Cos(halfAngleRad);

        WorldToGrid(wx, wz, out int cx, out int cz);

        for (int dz = -cellRadius; dz <= cellRadius; dz++)
        {
            for (int dx = -cellRadius; dx <= cellRadius; dx++)
            {
                int gx = cx + dx;
                int gz = cz + dz;
                if (!InBounds(gx, gz)) continue;

                float dist = Mathf.Sqrt(dx * dx + dz * dz) * cellSize;
                if (dist > radius || dist < 0.01f) continue;

                // 360°なら角度チェック不要（全方位）
                if (arcAngleDeg < 360f)
                {
                    // セルへの方向とdirの内積で角度チェック
                    float toCellX = dx * cellSize;
                    float toCellZ = dz * cellSize;
                    float invDist = 1f / dist;
                    float normX = toCellX * invDist;
                    float normZ = toCellZ * invDist;

                    float dot = normX * dirX + normZ * dirZ;
                    if (dot < cosHalfAngle) continue;
                }

                // 距離に応じてFalloff（端が薄い）
                float t = dist / radius;
                byte d = (byte)(newDensity * (1f - t * 0.4f)); // 端で60%くらいの濃さ

                int idx = Index(gx, gz);
                if (d > density[idx])
                {
                    density[idx] = d;
                    areaDirty = true;
                }
            }
        }

        textureDirty = true;
    }

    // ====================================================================
    //  統計
    // ====================================================================

    /// <summary>
    /// 塗られたセル数（density >= 1）
    /// 設計書セクション4.3: GetPaintedArea
    /// </summary>
    public int GetPaintedArea()
    {
        if (!areaDirty) return paintedCellCount;

        int count = 0;
        for (int i = 0; i < density.Length; i++)
        {
            if (density[i] > 0) count++;
        }
        paintedCellCount = count;
        areaDirty = false;
        return count;
    }

    /// <summary>塗り面積をワールド単位（平方メートル）で取得</summary>
    public float GetPaintedAreaSqm()
    {
        float cellArea = (CELL_SIZE_CM * 0.01f) * (CELL_SIZE_CM * 0.01f); // 0.25 m²
        return GetPaintedArea() * cellArea;
    }

    // ====================================================================
    //  デバッグ
    // ====================================================================

    /// <summary>グリッド全体をクリア</summary>
    public void ClearAll()
    {
        System.Array.Clear(density, 0, density.Length);
        textureDirty = true;
        areaDirty = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // エディタ上でグリッド範囲を黄色ワイヤーフレームで表示
        Gizmos.color = Color.yellow;
        Vector3 center = gridOrigin + new Vector3(GridWorldWidth * 0.5f, 0, GridWorldDepth * 0.5f);
        Vector3 size = new Vector3(GridWorldWidth, 0.1f, GridWorldDepth);
        Gizmos.DrawWireCube(center, size);
    }
#endif
}