using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// PaintableSurface の重い初期化(EnsureBuilt)を「プレイヤー近傍だけ・1フレーム数枚ずつ」進める常駐ストリーマ。
///
/// ■ 狙い: シーンに大量(例: Stage_Bは135枚)のPaintableSurfaceがあると、全AwakeでBuildすると
///         シーン切り替え時に固まる。Awakeでは作らず(EnsureBuiltに遅延)、ここで近接・順次に構築する。
///
/// ■ 仕様:
/// - RuntimeInitializeOnLoadMethod で自動常駐（各シーンに何も置かなくてよい）。DontDestroyOnLoad。
/// - sceneLoaded で対象シーンの PaintableSurface を再スキャン。
/// - プレイヤーは「CharacterController + "Player"レイヤー」で取得（見つかるまで0.5s毎リトライ）。
/// - 毎フレーム、buildRadius 内の未構築サーフェスを maxBuildsPerFrame 枚まで EnsureBuilt。
/// - 遠距離の斬撃着弾など範囲外は PaintableSurface 側の入口 EnsureBuilt(保険)が拾う。
/// </summary>
public class InkSurfaceStreamer : MonoBehaviour
{
    [Tooltip("プレイヤーからこの距離(m)内のサーフェスを構築する")]
    [SerializeField] private float buildRadius = 30f;
    [Tooltip("1フレームに構築する最大数（固まり防止。低解像度ほど増やせる）")]
    [SerializeField] private int maxBuildsPerFrame = 3;

    public static InkSurfaceStreamer Instance { get; private set; }

    private readonly List<PaintableSurface> _pending = new List<PaintableSurface>();
    private readonly List<PaintableSurface> _priority = new List<PaintableSurface>();   // 斬撃軌道など距離無視の優先構築
    private Transform _player;
    private int _playerLayer = -1;
    private float _nextFindTime;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;
        var go = new GameObject("[InkSurfaceStreamer]");
        DontDestroyOnLoad(go);
        go.AddComponent<InkSurfaceStreamer>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        _playerLayer = LayerMask.NameToLayer("Player");
        SceneManager.sceneLoaded += OnSceneLoaded;
        Rescan();   // 起動時点で読み込まれているシーン分
    }

    private void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Rescan();

    /// <summary>現在読み込まれている全シーンの未構築 PaintableSurface を集め直す。</summary>
    private void Rescan()
    {
        _pending.Clear();
        var all = FindObjectsByType<PaintableSurface>(FindObjectsSortMode.None);
        foreach (var ps in all)
            if (ps != null && ps.enabled && !ps.IsBuilt)
                _pending.Add(ps);

        _player = null;   // シーンが変わったら取り直し
    }

    /// <summary>後から有効化/Instantiateされたサーフェスを登録（PaintableSurface.OnEnableから呼ばれる）。</summary>
    public void Register(PaintableSurface ps)
    {
        if (ps != null && !ps.IsBuilt && !_pending.Contains(ps))
            _pending.Add(ps);
    }

    /// <summary>距離に関係なく優先構築する（斬撃の軌道先読み用）。数フレームに分散して構築される。</summary>
    public void RequestBuild(PaintableSurface ps)
    {
        if (ps != null && !ps.IsBuilt && !_priority.Contains(ps))
            _priority.Add(ps);
    }

    private void Update()
    {
        int built = 0;

        // ① 優先キュー（斬撃軌道など・距離無視）を先に消化
        for (int i = _priority.Count - 1; i >= 0 && built < maxBuildsPerFrame; i--)
        {
            var ps = _priority[i];
            if (ps == null || ps.IsBuilt) { _priority.RemoveAt(i); continue; }
            ps.EnsureBuilt();
            _priority.RemoveAt(i);
            built++;
        }

        if (built >= maxBuildsPerFrame || _pending.Count == 0) return;

        // ② 近接キュー（プレイヤー周辺）
        // プレイヤー取得（CC + "Player"レイヤー。見つかるまで0.5s毎リトライ）
        if (_player == null)
        {
            if (Time.time < _nextFindTime) return;
            _nextFindTime = Time.time + 0.5f;

            foreach (var cc in FindObjectsByType<CharacterController>(FindObjectsSortMode.None))
                if (cc.gameObject.layer == _playerLayer) { _player = cc.transform; break; }

            if (_player == null) return;
        }

        Vector3 p = _player.position;
        float r2 = buildRadius * buildRadius;

        // 後ろから走査して RemoveAt を安全に
        for (int i = _pending.Count - 1; i >= 0 && built < maxBuildsPerFrame; i--)
        {
            var ps = _pending[i];
            if (ps == null || ps.IsBuilt) { _pending.RemoveAt(i); continue; }

            // 広い床対応で transform.position ではなく bounds 距離で判定
            if (ps.SurfaceBounds.SqrDistance(p) <= r2)
            {
                ps.EnsureBuilt();
                _pending.RemoveAt(i);
                built++;
            }
        }
    }
}
