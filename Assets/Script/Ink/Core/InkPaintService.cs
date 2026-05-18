using UnityEditor.Rendering.Universal;
using UnityEngine;

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
/// 
/// 【墨があるか調べる】
///   bool hasInk = InkPaintService.HasInkAt(hit);
///   byte d = InkPaintService.GetDensity(hit);
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
    private static HF_PaintableSurface FindHFSurface(Collider col)
    {
        return col.GetComponent<HF_PaintableSurface>()
            ?? col.GetComponent<HF_PaintableSurface>();
    }
}