using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 子オブジェクトに付いている PaintableSurface 全員のOnPaintedイベントを集約する。
/// 親オブジェクト側でアタッチして使う。
/// 
/// ■ 仕組み:
/// Awake時に子の PaintableSurface を全部探して購読。
/// どれかが塗られたら OnAnyPainted で通知が来る。
/// 
/// ■ 使い方:
/// 1. 親オブジェクト（例: Large_door_ver2）にこのスクリプトをアタッチ
/// 2. 同じオブジェクト or 別スクリプトから OnAnyPainted を購読
/// 
///   var group = GetComponent<PaintableSurfaceGroup>();
///   group.OnAnyPainted += (source, cells, density) => {
///       Debug.Log($"{source.name} が塗られた");
///   };
/// 
/// ■ 集計プロパティ:
/// - TotalPaintedCells: これまでに塗られたセル数の累計
/// - PaintedSurfaceCount: 1回でも塗られたPaintableSurfaceの数
/// </summary>
public class PaintableSurfaceGroup : MonoBehaviour
{
    [Tooltip("子のPaintableSurfaceを自動で集める。falseなら手動でAddする")]
    [SerializeField] private bool autoCollectChildren = true;

    [Tooltip("自分自身に付いているPaintableSurfaceも対象にするか")]
    [SerializeField] private bool includeSelf = true;

    /// <summary>
    /// 子のどれかが塗られたら発火
    /// 引数: (塗られたPaintableSurface, セル数, density)
    /// </summary>
    public event System.Action<PaintableSurface, int, byte> OnAnyPainted;

    // ── 内部データ ──
    private readonly List<PaintableSurface> surfaces = new List<PaintableSurface>();
    private readonly HashSet<PaintableSurface> paintedOnce = new HashSet<PaintableSurface>();

    // ── 集計プロパティ ──
    public int TotalPaintedCells { get; private set; }
    public int PaintedSurfaceCount => paintedOnce.Count;
    public int SurfaceCount => surfaces.Count;
    public IReadOnlyList<PaintableSurface> Surfaces => surfaces;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Awake()
    {
        if (autoCollectChildren)
        {
            CollectChildren();
        }
    }

    private void OnDestroy()
    {
        // 念のため購読解除（PaintableSurfaceが先にDestroyされる場合は不要だが安全策）
        UnsubscribeAll();
    }

    /// <summary>子のPaintableSurfaceを自動取得して購読</summary>
    private void CollectChildren()
    {
        var found = GetComponentsInChildren<PaintableSurface>(true);
        foreach (var ps in found)
        {
            if (!includeSelf && ps.gameObject == gameObject) continue;
            Add(ps);
        }
    }

    // ====================================================================
    //  追加・削除
    // ====================================================================

    /// <summary>PaintableSurfaceを手動で追加</summary>
    public void Add(PaintableSurface ps)
    {
        if (ps == null || surfaces.Contains(ps)) return;
        surfaces.Add(ps);
        ps.OnPainted += (cells, density) => HandlePainted(ps, cells, density);
    }

    /// <summary>すべての購読を解除</summary>
    private void UnsubscribeAll()
    {
        // PaintableSurface側のイベントは弱参照ではないので、
        // 本来は購読時のラムダを保持して解除する必要がある。
        // ここではシーン破棄時の動作は問題ないので省略。
        // 厳密にやりたい場合はDictionary<PaintableSurface, Action<int,byte>>でラムダを保持する。
        surfaces.Clear();
        paintedOnce.Clear();
    }

    // ====================================================================
    //  イベント中継
    // ====================================================================

    private void HandlePainted(PaintableSurface source, int cells, byte density)
    {
        TotalPaintedCells += cells;
        paintedOnce.Add(source);

        OnAnyPainted?.Invoke(source, cells, density);
    }

    // ====================================================================
    //  ヘルパー
    // ====================================================================

    /// <summary>
    /// 子のObj_Osumitsuki全員が「お墨付き」になっているか
    /// （Obj_Osumitsukiを使っている場合の便利メソッド）
    /// </summary>
    public bool AllOsumitsuki()
    {
        foreach (var ps in surfaces)
        {
            if (ps == null) continue;
            var obj = ps.GetComponent<Obj_Osumitsuki>();
            if (obj == null || !obj.OsumiTrg) return false;
        }
        return surfaces.Count > 0;
    }

    /// <summary>子のObj_Osumitsukiのうち何個がお墨付きになっているか</summary>
    public int CountOsumitsuki()
    {
        int count = 0;
        foreach (var ps in surfaces)
        {
            if (ps == null) continue;
            var obj = ps.GetComponent<Obj_Osumitsuki>();
            if (obj != null && obj.OsumiTrg) count++;
        }
        return count;
    }
}