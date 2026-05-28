using UnityEngine;

/// <summary>
/// ボスエリアの侵入判定
/// 2つの空のGameObject（PointA・PointB）で箱型エリアを定義する
/// Playerがエリア内に入ったらボス戦を開始する
///
/// 【セットアップ】
/// ① 空のGameObjectにこのスクリプトをアタッチ
/// ② 空のGameObjectを2つ作り（PointA・PointB）シーンに配置
/// ③ InspectorでPointA・PointB・Bossをドラッグ
/// ④ PointAとPointBをエリアの対角に置く
///
/// 【イメージ】
/// PointA ●───────────┐
///        │　ボスエリア　　│
///        └───────────● PointB
/// </summary>
public class BossAreaTrigger : MonoBehaviour
{
    [Header("エリアの対角2点")]
    [Tooltip("エリアの対角点A（空のGameObject）")]
    [SerializeField] private Transform pointA;
    [Tooltip("エリアの対角点B（空のGameObject）")]
    [SerializeField] private Transform pointB;

    [Header("参照")]
    [SerializeField] private Boss_SB boss;
    [SerializeField] private Transform player;

    private bool battleStarted = false;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Start()
    {
        // Playerが未設定なら自動検索
        if (player == null)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go != null) player = go.transform;
        }
    }

    // ====================================================================
    //  毎フレーム：Playerがエリア内にいるか判定
    // ====================================================================

    private void Update()
    {
        if (battleStarted) return;
        if (player == null) return;
        if (pointA == null || pointB == null) return;
        if (boss == null) return;

        if (IsInsideArea(player.position))
        {
            battleStarted = true;
            boss.StartBossBattle();
            Debug.Log("[BossAreaTrigger] プレイヤーがボスエリアに侵入！ボス戦開始");
        }
    }

    // ====================================================================
    //  エリア内判定
    // ====================================================================

    /// <summary>指定座標がPointAとPointBで定義した箱の中にいるか</summary>
    private bool IsInsideArea(Vector3 pos)
    {
        Vector3 min = Vector3.Min(pointA.position, pointB.position);
        Vector3 max = Vector3.Max(pointA.position, pointB.position);

        return pos.x >= min.x && pos.x <= max.x &&
               pos.y >= min.y && pos.y <= max.y &&
               pos.z >= min.z && pos.z <= max.z;
    }

    // ====================================================================
    //  Gizmos（エディタ上でエリアを可視化）
    // ====================================================================

    private void OnDrawGizmos()
    {
        if (pointA == null || pointB == null) return;

        Vector3 min = Vector3.Min(pointA.position, pointB.position);
        Vector3 max = Vector3.Max(pointA.position, pointB.position);
        Vector3 center = (min + max) * 0.5f;
        Vector3 size = max - min;

        // エリアを黄色のワイヤーフレームで表示
        Gizmos.color = new Color(1f, 1f, 0f, 0.3f);
        Gizmos.DrawCube(center, size);
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(center, size);

        // PointA・PointBを球で表示
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(pointA.position, 0.3f);
        Gizmos.DrawSphere(pointB.position, 0.3f);
    }
}