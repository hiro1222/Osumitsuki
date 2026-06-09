using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ボス戦フェーズ2「灯籠セット」管理スクリプト
/// 3つの灯籠で三角形を作り、ボスのゴロゴロが内面・外面どちらに当たったか判定する
///
/// 【セットアップ】
/// ① 空のGameObjectを作り Boss_LanternSet.cs をアタッチ
/// ② InspectorにLanternA・B・Cをドラッグ
/// ③ 各灯籠の体部分のオブジェクトに Boss_Lantern.cs をアタッチ
/// ④ Boss_LanternSetをInspectorにドラッグ
///
/// 【外部から呼ぶ関数】
/// ・NotifyBossHit() : Boss_LanternからボスがPlayerVSObjectに当たったときに呼ぶ
/// </summary>
public class Boss_LanternSet : MonoBehaviour
{
    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("── 灯籠3つ ──")]
    [SerializeField] private Transform lanternA;
    [SerializeField] private Transform lanternB;
    [SerializeField] private Transform lanternC;

    [Header("── 内面判定拡張 ──")]
    [Tooltip("三角形の判定を広げる量（大きいほど内面判定が広くなる）")]
    [SerializeField] private float innerExpand = 2f;

    [Header("── ボス参照 ──")]
    [SerializeField] private Boss_SB boss;

    private List<Transform> bouncedLanterns = new List<Transform>();

    // ====================================================================
    //  外部から呼ぶ関数
    // ====================================================================

    private void Start()
    {
        // シーン上のBoss_SBを自動取得
        if (boss == null)
            boss = FindObjectOfType<Boss_SB>();

        if (boss == null)
            Debug.LogError("[Boss_LanternSet] Boss_SBが見つかりません！");
        else
            Debug.Log($"[Boss_LanternSet] ボス取得: {boss.gameObject.name}");
    }

    /// <summary>
    /// ボスが灯籠に当たったときに呼ぶ
    /// Boss_Lanternから呼ばれる
    /// </summary>
    public void NotifyBossHit(Vector3 bossPosition, Vector3 bossDirection)
    {
        if (boss == null) return;

        bool isInner = IsInnerSide(bossPosition);
        Vector3 newDirection = CalculateBounceDirection(bossPosition, bossDirection, isInner);

        // 方向計算後に記録（計算中に除外されないように）
        Transform nearest = GetNearestLantern(bossPosition);
        bouncedLanterns.Add(nearest);
        Debug.Log($"[Boss_LanternSet] バウンド灯籠記録: {nearest?.name} 記録数:{bouncedLanterns.Count}");

        boss.NotifyLanternBounce(isInner, newDirection);
    }

    // ====================================================================
    //  内面・外面判定
    // ====================================================================

    /// <summary>
    /// ボスの位置が三角形の内側かどうか判定する
    /// 三角形の内側 = 内面（バウンドフラグ成立）
    /// 三角形の外側 = 外面（フラグ不成立）
    /// </summary>
    private bool IsInnerSide(Vector3 bossPosition)
    {
        if (lanternA == null || lanternB == null || lanternC == null) return false;

        Vector3 a = lanternA.position;
        Vector3 b = lanternB.position;
        Vector3 c = lanternC.position;

        a.y = 0f; b.y = 0f; c.y = 0f;
        Vector3 pos = bossPosition;
        pos.y = 0f;

        // 三角形の中心を求める
        Vector3 center = (a + b + c) / 3f;

        // 各頂点を中心から外側に広げる
        a = center + (a - center).normalized * ((a - center).magnitude + innerExpand);
        b = center + (b - center).normalized * ((b - center).magnitude + innerExpand);
        c = center + (c - center).normalized * ((c - center).magnitude + innerExpand);

        return IsPointInTriangle(pos, a, b, c);
    }

    /// <summary>点が三角形の内側にあるか判定（2D）</summary>
    private bool IsPointInTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        float d1 = Sign(p, a, b);
        float d2 = Sign(p, b, c);
        float d3 = Sign(p, c, a);

        bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
        bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);

        return !(hasNeg && hasPos);
    }

    private float Sign(Vector3 p1, Vector3 p2, Vector3 p3)
    {
        return (p1.x - p3.x) * (p2.z - p3.z) - (p2.x - p3.x) * (p1.z - p3.z);
    }

    // ====================================================================
    //  バウンド後の方向計算
    // ====================================================================

    /// <summary>
    /// バウンド後の方向を計算する
    /// 内面バウンドの場合は一番近い別の灯籠方向へ
    /// 外面バウンドの場合は反射方向へ
    /// </summary>
    private Vector3 CalculateBounceDirection(Vector3 bossPosition, Vector3 bossDirection, bool isInner)
    {
        if (isInner)
        {
            // 内面バウンド → 一番近い別の灯籠へ向かう
            return GetNearestOtherLanternDirection(bossPosition);
        }
        else
        {
            // 外面バウンド → 当たった灯籠の法線で反射
            Vector3 normal = GetNearestLanternNormal(bossPosition, bossDirection);
            return Vector3.Reflect(bossDirection, normal).normalized;
        }
    }

    /// <summary>一番近い灯籠以外の灯籠の中で一番近い方向を返す</summary>
    private Vector3 GetNearestOtherLanternDirection(Vector3 bossPosition)
    {
        Transform nearest = GetNearestLantern(bossPosition);
        Transform[] others = GetOtherLanterns(nearest);

        Transform target = null;
        float minDist = float.MaxValue;

        foreach (var lantern in others)
        {
            if (lantern == null) continue;
            // 当たった灯籠（nearest含む）を全部除外
            if (bouncedLanterns.Contains(lantern)) continue;

            float dist = Vector3.Distance(bossPosition, lantern.position);
            if (dist < minDist)
            {
                minDist = dist;
                target = lantern;
            }
        }

        // 全部除外された場合はbouncedLangternsをリセットして再探索
        if (target == null)
        {
            Debug.Log("[Boss_LanternSet] 全灯籠バウンド済み。履歴リセット");
            bouncedLanterns.Clear();
            foreach (var lantern in others)
            {
                if (lantern == null) continue;
                float dist = Vector3.Distance(bossPosition, lantern.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    target = lantern;
                }
            }
        }

        if (target == null) return bossPosition;
        return (target.position - bossPosition).normalized;
    }

    /// <summary>一番近い灯籠の法線方向を返す（反射用）</summary>
    private Vector3 GetNearestLanternNormal(Vector3 bossPosition, Vector3 bossDirection)
    {
        Transform nearest = GetNearestLantern(bossPosition);
        if (nearest == null) return -bossDirection;

        // 灯籠からボスへの方向を法線とする
        Vector3 normal = (bossPosition - nearest.position).normalized;
        normal.y = 0f;
        return normal;
    }

    /// <summary>一番近い灯籠を返す</summary>
    private Transform GetNearestLantern(Vector3 bossPosition)
    {
        Transform nearest = null;
        float minDist = float.MaxValue;

        foreach (var lantern in new[] { lanternA, lanternB, lanternC })
        {
            if (lantern == null) continue;
            float dist = Vector3.Distance(bossPosition, lantern.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = lantern;
            }
        }

        return nearest;
    }

    /// <summary>指定した灯籠以外の灯籠を返す</summary>
    private Transform[] GetOtherLanterns(Transform exclude)
    {
        var result = new System.Collections.Generic.List<Transform>();
        foreach (var lantern in new[] { lanternA, lanternB, lanternC })
        {
            if (lantern != null && lantern != exclude)
                result.Add(lantern);
        }
        return result.ToArray();
    }

    /// <summary>バウンド履歴をリセットする（スタン後・ボス戦開始時に呼ぶ）</summary>
    public void ResetBounceHistory()
    {
        bouncedLanterns.Clear();
        Debug.Log("[Boss_LanternSet] バウンド履歴リセット");
    }

    /// <summary>灯籠登録をリセットする（フェーズ移行時に呼ぶ）</summary>
    public void ResetLanterns()
    {
        lanternA = null;
        lanternB = null;
        lanternC = null;
        bouncedLanterns.Clear();
        Debug.Log("[Boss_LanternSet] 灯籠リセット");
    }

    // ====================================================================
    //  Gizmos（エディタ上で三角形を可視化）
    // ====================================================================
    private void OnDrawGizmos()
    {
        if (lanternA == null || lanternB == null || lanternC == null) return;

        Vector3 a = lanternA.position;
        Vector3 b = lanternB.position;
        Vector3 c = lanternC.position;
        a.y = 0f; b.y = 0f; c.y = 0f;

        // Expandを反映
        Vector3 center = (a + b + c) / 3f;
        a = center + (a - center).normalized * ((a - center).magnitude + innerExpand);
        b = center + (b - center).normalized * ((b - center).magnitude + innerExpand);
        c = center + (c - center).normalized * ((c - center).magnitude + innerExpand);

        // 三角形の辺を表示
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawLine(b, c);
        Gizmos.DrawLine(c, a);

        // 各灯籠を球で表示（元の位置）
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(lanternA.position, 0.5f);
        Gizmos.DrawWireSphere(lanternB.position, 0.5f);
        Gizmos.DrawWireSphere(lanternC.position, 0.5f);
    }
}
