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
/// - 近傍(nearRadius内)は「近い順」に maxNearBuildsPerFrame 枚/frame で即構築（足場確保＝落下防止）。
///   遠方(nearRadius外)は farBuildInterval 間隔で1枚ずつトリクル構築（start時の一括ロード回避＝FPS低下防止）。
///   → 近い順にいずれ全サーフェスが構築される（先読み）。
/// - 斬撃着弾など緊急ぶんは優先キュー(RequestBuild)が距離無視で先に構築。
/// </summary>
public class InkSurfaceStreamer : MonoBehaviour
{
    [Tooltip("この距離(m)内は最優先で即構築（プレイヤー周辺の足場を確保＝落下防止）")]
    [SerializeField] private float nearRadius = 30f;
    [Tooltip("近傍(nearRadius内)を1フレームに構築する最大数（固まり防止）")]
    [SerializeField] private int maxNearBuildsPerFrame = 3;
    [Tooltip("遠方(nearRadius外)を1枚構築する間隔(秒)。start時の一括ロードを避けてゆっくり先読みする。0以下で遠方の先読みOFF")]
    [SerializeField] private float farBuildInterval = 0.3f;

    public static InkSurfaceStreamer Instance { get; private set; }

    private readonly List<PaintableSurface> _pending = new List<PaintableSurface>();
    private readonly List<PaintableSurface> _priority = new List<PaintableSurface>();   // 斬撃軌道など距離無視の優先構築
    private Transform _player;
    private int _playerLayer = -1;
    private float _nextFindTime;
    private float _nextFarBuildTime;

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
        for (int i = _priority.Count - 1; i >= 0 && built < maxNearBuildsPerFrame; i--)
        {
            var ps = _priority[i];
            if (ps == null || ps.IsBuilt) { _priority.RemoveAt(i); continue; }
            ps.EnsureBuilt();
            _priority.RemoveAt(i);
            built++;
        }

        if (built >= maxNearBuildsPerFrame || _pending.Count == 0) return;

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

        // null/ビルド済みを掃除（このあとのSortでnull比較が起きないように）
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            var ps = _pending[i];
            if (ps == null || ps.IsBuilt) _pending.RemoveAt(i);
        }
        if (_pending.Count == 0) return;

        // 近い順に並べ替え（末尾＝最も近い）。広い床対応で position ではなく bounds 距離。
        // ※129枚規模なら毎フレームSortでも誤差。数千枚規模になるなら距離キャッシュ/周期ソートに。
        _pending.Sort((a, b) =>
            b.SurfaceBounds.SqrDistance(p).CompareTo(a.SurfaceBounds.SqrDistance(p)));

        // ② 近傍(nearRadius内)：近い順に maxNearBuildsPerFrame 枚まで即ビルド（落下防止の本命）
        float nearSq = nearRadius * nearRadius;
        for (int i = _pending.Count - 1; i >= 0 && built < maxNearBuildsPerFrame; i--)
        {
            // nearest順（末尾が最近）なので、遠方に当たったら以降は全部遠方 → 抜ける
            if (_pending[i].SurfaceBounds.SqrDistance(p) > nearSq) break;
            _pending[i].EnsureBuilt();
            _pending.RemoveAt(i);
            built++;
        }

        // ③ 遠方(nearRadius外)：近傍が片付いているフレームだけ、時間スロットルで1枚ずつ先読み。
        //    start時に全枚数を一気にビルドして固まるのを防ぐ（ゆっくり全部そろえる）。
        if (built == 0 && farBuildInterval > 0f &&
            Time.time >= _nextFarBuildTime && _pending.Count > 0)
        {
            int nearestFar = _pending.Count - 1;   // 残りの中で最も近い遠方
            _pending[nearestFar].EnsureBuilt();
            _pending.RemoveAt(nearestFar);
            _nextFarBuildTime = Time.time + farBuildInterval;
        }
    }
}
