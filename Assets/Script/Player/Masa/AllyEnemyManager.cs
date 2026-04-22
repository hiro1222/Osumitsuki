using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 友好EnemyのManager
/// プレイヤーにアタッチする
///
/// 【役割】
/// ・敵対EnemyのBecomeAlly時に通知を受け取りAllyEnemyを生成
/// ・追従中は最大4体、5体目以降はストック
/// ・消費されたらストックから補充
/// ・追従位置を毎フレーム計算してAllyEnemyに渡す
///
/// 【外部から呼ぶ関数】
/// ・OnEnemyBecameAlly() : 敵対Enemyのお墨付き時に呼ぶ
/// ・ConsumeAlly()       : 身代わり・ギミック援助で消費するときに呼ぶ
/// ・GetAllyCount()      : 追従中の仲間数を返す
/// ・GetStockCount()     : ストック数を返す
/// </summary>
public class AllyEnemyManager : MonoBehaviour
{
    // ====================================================================
    //  設定（Inspector）
    // ====================================================================

    [Header("AllyEnemyのPrefab")]
    [SerializeField] private AllyEnemy allyEnemyPrefab;

    [Header("隊列設定（一列縦隊）")]
    [Tooltip("プレイヤーから1番目の仲間までの距離")]
    [SerializeField] private float followDistance = 2.5f;
    [Tooltip("仲間同士の前後間隔")]
    [SerializeField] private float rowSpacing = 1.5f;

    [Header("追従の最大数")]
    [Tooltip("追従できる最大数（それ以上はストック）")]
    [SerializeField] private int maxFollowCount = 4;

    // ====================================================================
    //  内部状態（全てprivate）
    // ====================================================================

    private readonly List<AllyEnemy> followingAllies = new List<AllyEnemy>();
    private int stockCount = 0;

    // ====================================================================
    //  毎フレーム
    // ====================================================================

    private void Update()
    {
        followingAllies.RemoveAll(a => a == null);
        UpdateFormation();
        ReplenishFromStock();
    }

    // ====================================================================
    //  隊列管理
    // ====================================================================

    private void UpdateFormation()
    {
        int count = followingAllies.Count;
        if (count == 0) return;

        for (int i = 0; i < count; i++)
        {
            if (followingAllies[i] == null) continue;

            // プレイヤーの真後ろに一列縦隊
            float distFromPlayer = followDistance + i * rowSpacing;
            Vector3 pos = transform.position - transform.forward * distFromPlayer;

            // Y座標はプレイヤーと同じ高さに合わせる
            pos.y = transform.position.y;

            followingAllies[i].SetFollowTarget(pos);
        }
    }

    // ====================================================================
    //  ストックから補充
    // ====================================================================

    private void ReplenishFromStock()
    {
        while (followingAllies.Count < maxFollowCount && stockCount > 0)
        {
            // プレイヤーの後ろ・同じ高さに生成
            Vector3 spawnPos = transform.position - transform.forward * followDistance;
            spawnPos.y = transform.position.y;
            SpawnAllyEnemy(spawnPos);
            stockCount--;
            Debug.Log($"[AllyEnemyManager] ストックから補充。残りストック: {stockCount}");
        }
    }

    // ====================================================================
    //  AllyEnemy生成
    // ====================================================================

    private void SpawnAllyEnemy(Vector3 spawnPos)
    {
        if (allyEnemyPrefab == null)
        {
            Debug.LogWarning("[AllyEnemyManager] AllyEnemyPrefabが未設定です");
            return;
        }

        AllyEnemy newAlly = Instantiate(allyEnemyPrefab, spawnPos, Quaternion.identity);
        followingAllies.Add(newAlly);

        Debug.Log($"[AllyEnemyManager] AllyEnemy生成。追従中: {followingAllies.Count}体");
    }

    // ====================================================================
    //  外部から呼ぶ関数
    // ====================================================================

    /// <summary>
    /// 敵対EnemyのBecomeAlly時に呼ぶ
    /// Normal_SBなどから呼ばれる
    /// </summary>
    public void OnEnemyBecameAlly(Vector3 spawnPosition, float inkRecovery)
    {
        // インク回復処理（Player担当と要すり合わせ後に実装）
        // player.GetComponent<PlayerInk>()?.RecoverInk(inkRecovery);

        // 生成位置のY座標をプレイヤーに合わせる
        Vector3 fixedSpawnPos = spawnPosition;
        fixedSpawnPos.y = transform.position.y;

        if (followingAllies.Count < maxFollowCount)
        {
            SpawnAllyEnemy(fixedSpawnPos);
        }
        else
        {
            stockCount++;
            Debug.Log($"[AllyEnemyManager] ストックに追加。ストック: {stockCount}体");
        }
    }

    /// <summary>
    /// 仲間を1体消費する
    /// 身代わり・ギミック援助時に呼ぶ
    /// </summary>
    public void ConsumeAlly()
    {
        for (int i = 0; i < followingAllies.Count; i++)
        {
            if (followingAllies[i] != null)
            {
                followingAllies[i].Consume();
                followingAllies.RemoveAt(i);
                Debug.Log($"[AllyEnemyManager] 仲間消費。残り: {followingAllies.Count}体 ストック: {stockCount}体");
                return;
            }
        }
        Debug.Log("[AllyEnemyManager] 消費できる仲間がいません");
    }

    /// <summary>追従中の仲間数を返す</summary>
    public int GetAllyCount() => followingAllies.Count;

    /// <summary>ストック数を返す</summary>
    public int GetStockCount() => stockCount;

    // ====================================================================
    //  GUI（デバッグ）
    // ====================================================================

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 130, 400, 20),
            $"仲間: {followingAllies.Count}体  ストック: {stockCount}体");
    }
}