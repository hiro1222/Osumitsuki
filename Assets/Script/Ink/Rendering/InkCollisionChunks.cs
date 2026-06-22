using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// インクの塗り判定セルから、チャンク単位のコリジョンメッシュを生成・管理する。
/// PaintableSurface から切り出したコリジョン責務（責務分割 P2-Step2）。
///
/// ■ 設計:
/// - チャンク基盤(GameObject/MeshCollider/Mesh)を所有する
/// - グリッドデータ(density/cellPositions等)は PaintableSurface が保持し、Initで参照を捕捉する
///   （これらの配列は確保後に再代入されないので、参照を持ち続けても安全）
/// - 厚み補正に使う lossyScale 平均は RebuildDirty の引数で受け取る（AvgScaleの単一ソース維持）
/// </summary>
internal class InkCollisionChunks
{
    // ── 所有: チャンク基盤 ──
    private GameObject collisionChild;        // 親（空）
    private MeshCollider[] chunkColliders;    // チャンクごとのMeshCollider
    private Mesh[] chunkMeshes;               // チャンクごとのMesh
    private int chunksX, chunksY;
    private bool[] chunkDirty;
    private bool collidersEnabled = true;   // Enable/DisableAllの状態（遅延生成チャンクへ引き継ぐ）
    private readonly List<Vector3> _chunkVerts = new List<Vector3>();
    private readonly List<int> _chunkTris = new List<int>();

    // ── Initで捕捉する設定・グリッドデータ参照 ──
    private int gridW, gridH, chunkSize;
    private float meshThickness;
    private byte walkThreshold;
    private float maxCellDistance;
    private string ownerName;   // 遅延チャンク生成用
    private int inkLayer;       // 遅延チャンク生成用
    private bool[] cellValid;
    private byte[] density;
    private Vector3[] cellPositions;
    private Vector3[] paintedNormals;
    private Vector3[] cellNormals;

    /// <summary>チャンク基盤を生成し、グリッドデータ参照を捕捉する。</summary>
    public void Init(Transform owner, string ownerName, int inkLayer,
                     int gridW, int gridH, int chunkSize, float meshThickness, byte walkThreshold,
                     float maxCellDistance,
                     bool[] cellValid, byte[] density, Vector3[] cellPositions,
                     Vector3[] paintedNormals, Vector3[] cellNormals)
    {
        this.gridW = gridW; this.gridH = gridH; this.chunkSize = chunkSize;
        this.meshThickness = meshThickness; this.walkThreshold = walkThreshold;
        this.maxCellDistance = maxCellDistance;
        this.cellValid = cellValid; this.density = density;
        this.cellPositions = cellPositions; this.paintedNormals = paintedNormals;
        this.cellNormals = cellNormals;
        this.ownerName = ownerName;
        this.inkLayer = inkLayer;

        chunksX = Mathf.CeilToInt((float)gridW / chunkSize);
        chunksY = Mathf.CeilToInt((float)gridH / chunkSize);
        chunkDirty = new bool[chunksX * chunksY];

        // インクコリジョン用の親オブジェクト（空）
        collisionChild = new GameObject($"{ownerName}_InkCollision");
        collisionChild.transform.SetParent(owner, false);
        collisionChild.layer = inkLayer;

        // ★遅延生成: チャンクのGameObject/MeshCollider/Meshはここでは作らない。
        //   実際に塗られた(=geometryが出る)チャンクだけ RebuildChunk → EnsureChunk で初めて生成する。
        //   こうしないと Awake で最大 chunksX*chunksY 個(例: 512解像度なら1024個)の
        //   GameObject生成が走り、シーン開始/Play移行が固まる。
        int chunkCount = chunksX * chunksY;
        chunkColliders = new MeshCollider[chunkCount];  // 全null（遅延生成）
        chunkMeshes = new Mesh[chunkCount];             // 全null（遅延生成）
    }

    /// <summary>セル(gx,gy)が属するチャンクをdirtyにする。</summary>
    public void MarkDirty(int gx, int gy)
    {
        int cx = gx / chunkSize;
        int cy = gy / chunkSize;
        if (cx >= 0 && cx < chunksX && cy >= 0 && cy < chunksY)
            chunkDirty[cy * chunksX + cx] = true;
    }

    /// <summary>全チャンクをdirtyにする（ClearAll用）。</summary>
    public void MarkAllDirty()
    {
        if (chunkDirty == null) return;
        for (int i = 0; i < chunkDirty.Length; i++) chunkDirty[i] = true;
    }

    /// <summary>
    /// dirtyなチャンクだけ、それぞれのMeshColliderを再生成する。
    /// チャンク単位なので1メッシュの頂点数が抑えられ、65535制限を回避できる。
    /// </summary>
    /// <param name="avgScale">lossyScaleの3軸平均（厚みのローカル補正用）</param>
    public void RebuildDirty(float avgScale)
    {
        // 厚みをローカル空間に変換（lossyScaleで補正）
        // これをしないと、Scaleが大きいオブジェクトでコリジョンが巨大に突き出す
        float halfThick = (meshThickness * 0.5f) / Mathf.Max(avgScale, 0.0001f);
        float maxDistSq = maxCellDistance * maxCellDistance;

        for (int cy = 0; cy < chunksY; cy++)
        {
            for (int cx = 0; cx < chunksX; cx++)
            {
                int chunkIdx = cy * chunksX + cx;
                if (!chunkDirty[chunkIdx]) continue;

                RebuildChunk(cx, cy, chunkIdx, halfThick, maxDistSq);
                chunkDirty[chunkIdx] = false;
            }
        }
    }

    private void RebuildChunk(int cx, int cy, int chunkIdx, float halfThick, float maxDistSq)
    {
        _chunkVerts.Clear();
        _chunkTris.Clear();
        var verts = _chunkVerts;
        var tris = _chunkTris;

        // このチャンクが担当するセル範囲
        int startX = cx * chunkSize;
        int startY = cy * chunkSize;
        int endX = Mathf.Min(startX + chunkSize, gridW - 1);
        int endY = Mathf.Min(startY + chunkSize, gridH - 1);

        for (int gy = startY; gy < endY; gy++)
        {
            for (int gx = startX; gx < endX; gx++)
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

                bool tooFar = false;
                tooFar |= (cellPositions[i10] - cellPositions[i00]).sqrMagnitude > maxDistSq;
                tooFar |= (cellPositions[i11] - cellPositions[i01]).sqrMagnitude > maxDistSq;
                tooFar |= (cellPositions[i01] - cellPositions[i00]).sqrMagnitude > maxDistSq;
                tooFar |= (cellPositions[i11] - cellPositions[i10]).sqrMagnitude > maxDistSq;
                tooFar |= (cellPositions[i11] - cellPositions[i00]).sqrMagnitude > maxDistSq * 2f;
                tooFar |= (cellPositions[i01] - cellPositions[i10]).sqrMagnitude > maxDistSq * 2f;
                if (tooFar) continue;

                Vector3 avgNorm = (paintedNormals[i00] + paintedNormals[i10] +
                                   paintedNormals[i01] + paintedNormals[i11]).normalized;
                if (avgNorm.sqrMagnitude < 0.01f)
                {
                    avgNorm = (cellNormals[i00] + cellNormals[i10] +
                               cellNormals[i01] + cellNormals[i11]).normalized;
                }
                Vector3 offset = avgNorm * halfThick;

                BuildQuadCell(verts, tris,
                    cellPositions[i00], cellPositions[i10],
                    cellPositions[i11], cellPositions[i01],
                    offset);
            }
        }

        if (verts.Count > 0)
        {
            EnsureChunk(chunkIdx);   // 初めてgeometryが出たチャンクだけ生成

            Mesh mesh = chunkMeshes[chunkIdx];
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var mc = chunkColliders[chunkIdx];
            mc.sharedMesh = null;
            mc.sharedMesh = mesh;
        }
        else if (chunkColliders[chunkIdx] != null)
        {
            // 消去等でgeometryが空になった既存チャンク: メッシュを外す
            chunkMeshes[chunkIdx].Clear();
            chunkColliders[chunkIdx].sharedMesh = null;
        }
        // verts==0 かつ 未生成 → 何もしない（生成不要）
    }

    /// <summary>
    /// チャンクのGameObject/MeshCollider/Meshを遅延生成する（初回のみ）。
    /// 塗られてgeometryが出たチャンクだけ生成されるので、Awakeの大量生成を回避できる。
    /// </summary>
    private void EnsureChunk(int chunkIdx)
    {
        if (chunkColliders[chunkIdx] != null) return;

        var chunkObj = new GameObject($"InkChunk_{chunkIdx}");
        chunkObj.transform.SetParent(collisionChild.transform, false);
        chunkObj.layer = inkLayer;

        var mc = chunkObj.AddComponent<MeshCollider>();
        mc.enabled = collidersEnabled;   // Disable中に塗られたチャンクも無効状態を引き継ぐ
        chunkColliders[chunkIdx] = mc;

        // 32bitインデックス対応（チャンク内で65535を超えても安全）
        chunkMeshes[chunkIdx] = new Mesh
        {
            name = $"InkCol_{ownerName}_chunk{chunkIdx}",
            indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
        };
    }

    /// <summary>当たったコライダーが自分のインクチャンクのどれかか判定</summary>
    public bool IsInkChunkCollider(Collider col)
    {
        if (chunkColliders == null || col == null) return false;
        for (int i = 0; i < chunkColliders.Length; i++)
        {
            if (chunkColliders[i] == col) return true;
        }
        return false;
    }

    /// <summary>インクコリジョンを有効化（全チャンク）</summary>
    public void EnableAll()
    {
        collidersEnabled = true;
        if (chunkColliders == null) return;
        for (int i = 0; i < chunkColliders.Length; i++)
            if (chunkColliders[i] != null) chunkColliders[i].enabled = true;
    }

    /// <summary>インクコリジョンを無効化（全チャンク）。塗りデータは残る。</summary>
    public void DisableAll()
    {
        collidersEnabled = false;
        if (chunkColliders == null) return;
        for (int i = 0; i < chunkColliders.Length; i++)
            if (chunkColliders[i] != null) chunkColliders[i].enabled = false;
    }

    /// <summary>チャンクメッシュと親オブジェクトを破棄する（PaintableSurface.OnDestroyから呼ぶ）。</summary>
    public void Dispose()
    {
        if (chunkMeshes != null)
        {
            for (int i = 0; i < chunkMeshes.Length; i++)
                if (chunkMeshes[i] != null) Object.Destroy(chunkMeshes[i]);
        }
        if (collisionChild != null) Object.Destroy(collisionChild);
    }

    /// <summary>4頂点のquadを生成（表+裏+側面）</summary>
    private static void BuildQuadCell(List<Vector3> verts, List<int> tris,
                                      Vector3 p00, Vector3 p10, Vector3 p11, Vector3 p01,
                                      Vector3 offset)
    {
        int bi = verts.Count;

        // 内側
        verts.Add(p00); verts.Add(p10); verts.Add(p11); verts.Add(p01);
        // 外側
        verts.Add(p00 + offset); verts.Add(p10 + offset);
        verts.Add(p11 + offset); verts.Add(p01 + offset);

        // 表面（外）
        tris.Add(bi + 4); tris.Add(bi + 5); tris.Add(bi + 6);
        tris.Add(bi + 4); tris.Add(bi + 6); tris.Add(bi + 7);
        // 内面
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

    /// <summary>3頂点の三角形を生成（表+裏+側面）。現状未使用だが将来用に保持。</summary>
    private static void BuildTriangleCell(List<Vector3> verts, List<int> tris,
                                          Vector3 p0, Vector3 p1, Vector3 p2,
                                          Vector3 offset)
    {
        int bi = verts.Count;

        // 内側
        verts.Add(p0); verts.Add(p1); verts.Add(p2);
        // 外側
        verts.Add(p0 + offset); verts.Add(p1 + offset); verts.Add(p2 + offset);

        // 表面（外）
        tris.Add(bi + 3); tris.Add(bi + 4); tris.Add(bi + 5);
        // 内面（巻き順逆）
        tris.Add(bi); tris.Add(bi + 2); tris.Add(bi + 1);
        // 側面（3辺）
        tris.Add(bi); tris.Add(bi + 1); tris.Add(bi + 4);
        tris.Add(bi); tris.Add(bi + 4); tris.Add(bi + 3);
        tris.Add(bi + 1); tris.Add(bi + 2); tris.Add(bi + 5);
        tris.Add(bi + 1); tris.Add(bi + 5); tris.Add(bi + 4);
        tris.Add(bi + 2); tris.Add(bi); tris.Add(bi + 3);
        tris.Add(bi + 2); tris.Add(bi + 3); tris.Add(bi + 5);
    }
}
