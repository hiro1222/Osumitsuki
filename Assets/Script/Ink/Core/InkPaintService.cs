using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 墨システムの唯一の公開API（安定版）
/// 
/// ■ このクラスのルール:
/// 1. public メソッドのシグネチャは変更しない
/// 2. 新しい機能は既存メソッドのオプショナルパラメータで吸収する
/// 3. 内部実装（PaintableSurface等）の詳細は一切公開しない
/// 
/// ■ チームメンバーへ:
/// 墨に関わる全ての操作はこのクラスを通してください。
/// PaintableSurfaceを直接触る必要はありません。
/// 
/// ■ 使い方:
/// 
/// 【墨を塗る】
///   InkPaintService.Paint(hit, radius, density);
///   InkPaintService.Paint(hit, pattern);
///   InkPaintService.PaintArea(hit, radius, density);  // 隣接サーフェスも巻き込む
/// 
/// 【墨を消す】
///   InkPaintService.Erase(hit, radius);
///   InkPaintService.EraseAt(surface, worldCenter, radius);
///   InkPaintService.EraseArea(hit, radius);  // 隣接サーフェスも巻き込む
///   InkPaintService.ClearAll(surface);
/// 
/// 【墨があるか調べる】
///   bool hasInk = InkPaintService.HasInkAt(hit);
///   byte d = InkPaintService.GetDensity(hit);
/// 
/// 【コリジョン制御】
///   InkPaintService.EnableInkCollider(surface);
///   InkPaintService.DisableInkCollider(surface);
/// 
/// 【Raycastの注意】
///   全てのRaycastに QueryTriggerInteraction.Collide を付けてください。
///   親のMeshColliderがTriggerなので、これがないとRaycastが当たりません。
///   ヘルパーメソッド InkPaintService.Raycast() を使えば自動で付きます。
/// </summary>
public static class InkPaintService
{
    // ================================================================
    //  墨を塗る
    // ================================================================

    /// <summary>
    /// RaycastHitの位置に墨を塗る
    /// </summary>
    /// <param name="hit">Physics.Raycastの結果</param>
    /// <param name="radius">塗りの半径（ワールド単位・メートル）</param>
    /// <param name="inkDensity">墨の濃さ（0〜255）</param>
    /// <param name="colorId">色番号。省略時は黒墨。InkPalette.ID_*を使用</param>
    public static void Paint(RaycastHit hit, float radius, byte inkDensity, byte colorId = 0)
    {
        var surface = FindSurface(hit.collider);
        var hfSurface = FindHFSurface(hit.collider);
        if (surface != null)
            surface.Paint(hit, radius, inkDensity, colorId);
    }

    /// <summary>
    /// SlashPatternに基づいて墨を塗る
    /// patternに色番号が設定されていればそれを使用
    /// </summary>
    public static void Paint(RaycastHit hit, SlashPattern pattern)
    {
        var surface = FindSurface(hit.collider);
        if (surface != null)
            surface.Paint(hit, pattern.impactRadius,
                          (byte)pattern.inkDensity,
                          pattern.inkColorId);
    }

    // ================================================================
    //  範囲塗り（隣接サーフェス対応）
    // ================================================================

    /// <summary>
    /// ヒット地点周辺の全PaintableSurfaceに塗る
    /// オブジェクトの境界（隣接した別オブジェクト）を撃ったときに
    /// 両方のサーフェスが塗られる
    /// </summary>
    public static void PaintArea(RaycastHit hit, float radius, byte inkDensity, byte colorId = 0)
    {
        var mainSurface = FindSurface(hit.collider);
        if (mainSurface != null)
            mainSurface.Paint(hit, radius, inkDensity, colorId);

        PaintNeighborSurfaces(hit.point, radius, mainSurface,
            (subHit, surface) => surface.Paint(subHit, radius, inkDensity, colorId));
    }

    /// <summary>SlashPatternベースの範囲塗り</summary>
    public static void PaintArea(RaycastHit hit, SlashPattern pattern)
    {
        var mainSurface = FindSurface(hit.collider);
        if (mainSurface != null)
            mainSurface.Paint(hit, pattern.impactRadius,
                              (byte)pattern.inkDensity, pattern.inkColorId);

        PaintNeighborSurfaces(hit.point, pattern.impactRadius, mainSurface,
            (subHit, surface) => surface.Paint(subHit, pattern.impactRadius,
                                                (byte)pattern.inkDensity, pattern.inkColorId));
    }

    // ================================================================
    //  墨の状態を調べる
    // ================================================================

    /// <summary>
    /// 指定位置に墨があるか（density > 0）
    /// </summary>
    public static bool HasInkAt(RaycastHit hit)
    {
        var surface = FindSurface(hit.collider);
        return surface != null && surface.HasDensityAt(hit);
    }

    /// <summary>
    /// 指定位置のdensity値を取得（0〜255）
    /// 墨がない場合は0を返す
    /// </summary>
    public static byte GetDensity(RaycastHit hit)
    {
        var surface = FindSurface(hit.collider);
        return surface != null ? surface.GetDensity(hit) : (byte)0;
    }

    /// <summary>
    /// 指定位置が通行可能か（density >= walkThreshold）
    /// </summary>
    public static bool CanWalk(RaycastHit hit)
    {
        var surface = FindSurface(hit.collider);
        return surface != null && surface.CanWalk(hit);
    }

    // ================================================================
    //  Raycastヘルパー
    //  （QueryTriggerInteraction.Collide を自動で付ける）
    // ================================================================

    /// <summary>
    /// 墨システム用のRaycast（Trigger対応済み）
    /// Physics.Raycastの代わりにこれを使えば
    /// QueryTriggerInteraction.Collideの指定を忘れる心配がない
    /// </summary>
    public static bool Raycast(Vector3 origin, Vector3 direction,
                               out RaycastHit hit, float maxDistance,
                               int layerMask = ~0)
    {
        return Physics.Raycast(origin, direction, out hit, maxDistance,
                               layerMask, QueryTriggerInteraction.Collide);
    }

    /// <summary>
    /// Ray版のRaycast（Trigger対応済み）
    /// </summary>
    public static bool Raycast(Ray ray, out RaycastHit hit,
                               float maxDistance = Mathf.Infinity,
                               int layerMask = ~0)
    {
        return Physics.Raycast(ray, out hit, maxDistance,
                               layerMask, QueryTriggerInteraction.Collide);
    }

    // ================================================================
    //  消去（Erase）
    // ================================================================

    /// <summary>
    /// RaycastHitの位置を中心に塗りを消す
    /// 範囲内の色とコリジョンが両方消える
    /// </summary>
    public static void Erase(RaycastHit hit, float radius)
    {
        var surface = FindSurface(hit.collider);
        if (surface != null) surface.Erase(hit, radius);
    }

    /// <summary>
    /// ヒット地点周辺の全PaintableSurfaceから塗りを消す
    /// 隣接したサーフェスもまとめて消す
    /// </summary>
    public static void EraseArea(RaycastHit hit, float radius)
    {
        var mainSurface = FindSurface(hit.collider);
        if (mainSurface != null)
            mainSurface.Erase(hit, radius);

        var processed = new HashSet<PaintableSurface>();
        if (mainSurface != null) processed.Add(mainSurface);

        Collider[] colliders = Physics.OverlapSphere(
            hit.point, radius, ~0, QueryTriggerInteraction.Collide);

        foreach (var col in colliders)
        {
            var surface = FindSurface(col);
            if (surface == null || processed.Contains(surface)) continue;
            processed.Add(surface);

            // EraseAtはワールド座標+半径なのでRaycast不要
            surface.EraseAt(hit.point, radius);
        }
    }

    /// <summary>
    /// ワールド座標を中心に塗りを消す（Raycast不要）
    /// 指定オブジェクトのPaintableSurfaceから直接消す
    /// </summary>
    public static void EraseAt(PaintableSurface surface, Vector3 worldCenter, float radius)
    {
        if (surface != null) surface.EraseAt(worldCenter, radius);
    }

    /// <summary>指定オブジェクトの塗りを全消去（デバッグ用）</summary>
    public static void ClearAll(PaintableSurface surface)
    {
        if (surface != null) surface.ClearAll();
    }

    // ================================================================
    //  コリジョン制御
    // ================================================================

    /// <summary>
    /// 指定したPaintableSurfaceのインクコリジョンを有効化
    /// </summary>
    public static void EnableInkCollider(PaintableSurface surface)
    {
        if (surface != null) surface.EnableInkCollider();
    }

    /// <summary>
    /// 指定したPaintableSurfaceのインクコリジョンを無効化
    /// 塗りの見た目は残るがプレイヤーは通り抜けられるようになる
    /// </summary>
    public static void DisableInkCollider(PaintableSurface surface)
    {
        if (surface != null) surface.DisableInkCollider();
    }

    /// <summary>RaycastHitの先のオブジェクトのインクコリジョンを有効化</summary>
    public static void EnableInkCollider(RaycastHit hit)
    {
        var surface = FindSurface(hit.collider);
        if (surface != null) surface.EnableInkCollider();
    }

    /// <summary>RaycastHitの先のオブジェクトのインクコリジョンを無効化</summary>
    public static void DisableInkCollider(RaycastHit hit)
    {
        var surface = FindSurface(hit.collider);
        if (surface != null) surface.DisableInkCollider();
    }

    // ================================================================
    //  内部実装（このセクションはチームメンバーが触る必要なし）
    // ================================================================

    /// <summary>
    /// コライダーからPaintableSurfaceを探す
    /// インクコリジョンは子オブジェクトなので親も検索する
    /// </summary>
    private static PaintableSurface FindSurface(Collider col)
    {
        return col.GetComponent<PaintableSurface>()
            ?? col.GetComponentInParent<PaintableSurface>();
    }

    /// <summary>
    /// HF_PaintableSurfaceを探す（チームメンバー追加の別塗りシステム用）
    /// 注意: 現在の実装は GetComponent を2回呼んでいる可能性あり（要確認）
    /// </summary>
    private static HF_PaintableSurface FindHFSurface(Collider col)
    {
        return col.GetComponent<HF_PaintableSurface>()
            ?? col.GetComponent<HF_PaintableSurface>();
    }

    /// <summary>
    /// ヒット地点を中心に近傍のPaintableSurfaceを探し、
    /// 各々に対して最寄り点へのRaycastを撃ってコールバックを呼ぶ
    /// (隣接サーフェスへの範囲塗り用)
    /// </summary>
    private static void PaintNeighborSurfaces(Vector3 hitPoint, float radius,
        PaintableSurface excludeSurface,
        System.Action<RaycastHit, PaintableSurface> onSurfaceFound)
    {
        // 近傍のコライダーを取得
        Collider[] colliders = Physics.OverlapSphere(
            hitPoint, radius, ~0, QueryTriggerInteraction.Collide);

        Debug.Log($"[PaintArea] hitPoint={hitPoint} radius={radius} → {colliders.Length}個検出");

        // 重複処理防止
        var processed = new HashSet<PaintableSurface>();
        if (excludeSurface != null) processed.Add(excludeSurface);

        foreach (var col in colliders)
        {
            var surface = FindSurface(col);
            if (surface == null)
            {
                Debug.Log($"[PaintArea]   {col.name}: PaintableSurfaceなし → スキップ");
                continue;
            }
            if (processed.Contains(surface))
            {
                Debug.Log($"[PaintArea]   {surface.name}: 処理済み(メイン or 重複) → スキップ");
                continue;
            }
            processed.Add(surface);

            // このコライダーへの最寄り点を求める
            Vector3 closestPoint = col.ClosestPoint(hitPoint);

            // 同一点(コライダー内部)の場合、textureCoordが取れないのでスキップ
            Vector3 toClosest = closestPoint - hitPoint;
            if (toClosest.sqrMagnitude < 0.0001f)
            {
                Debug.Log($"[PaintArea]   {surface.name}: 最寄り点が同一(内部) → スキップ");
                continue;
            }

            Vector3 direction = toClosest.normalized;

            // 最寄り点の少し手前から、最寄り点を通り過ぎる方向にRaycast
            Vector3 rayOrigin = closestPoint - direction * 0.05f;
            float rayDistance = 0.1f;

            Debug.Log($"[PaintArea]   {surface.name}: closest={closestPoint} へRaycast");

            if (Physics.Raycast(rayOrigin, direction, out RaycastHit subHit,
                rayDistance, ~0, QueryTriggerInteraction.Collide))
            {
                var subSurface = FindSurface(subHit.collider);
                if (subSurface == surface)
                {
                    Debug.Log($"[PaintArea]   {surface.name}: 塗り成功 uv={subHit.textureCoord}");
                    onSurfaceFound(subHit, surface);
                }
                else
                {
                    Debug.Log($"[PaintArea]   {surface.name}: Raycastが別物({subHit.collider.name})にヒット");
                }
            }
            else
            {
                Debug.Log($"[PaintArea]   {surface.name}: Raycast当たらず");
            }
        }
    }
}