using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 友好EnemyのManager
/// プレイヤーにアタッチする
/// </summary>
public class AllyEnemyManager : MonoBehaviour
{
    [Header("AllyEnemyのPrefab")]
    [SerializeField] private AllyEnemy allyEnemyPrefab;

    [Header("隊列設定（一列縦隊）")]
    [SerializeField] private float followDistance = 2.5f;
    [SerializeField] private float rowSpacing = 1.5f;

    [Header("追従の最大数")]
    [SerializeField] private int maxFollowCount = 4;

    [Header("アンカー参照")]
    [SerializeField] private PlayerActionAnchorProvider anchorProvider;
    [SerializeField] private PlayerActionManager actionManager;

    private readonly List<AllyEnemy> followingAllies = new List<AllyEnemy>();
    private int stockCount = 0;

    private static readonly Dictionary<PlayerActionManager.ActionKind,
        PlayerActionAnchorProvider.ActionAnchorType> anchorMap =
        new Dictionary<PlayerActionManager.ActionKind,
            PlayerActionAnchorProvider.ActionAnchorType>
    {
        { PlayerActionManager.ActionKind.Nazori,       PlayerActionAnchorProvider.ActionAnchorType.Nazori       },
        { PlayerActionManager.ActionKind.Harai,        PlayerActionAnchorProvider.ActionAnchorType.Harai        },
        { PlayerActionManager.ActionKind.Hane,         PlayerActionAnchorProvider.ActionAnchorType.Hane         },
        { PlayerActionManager.ActionKind.DerivedHarai, PlayerActionAnchorProvider.ActionAnchorType.DerivedHarai },
        { PlayerActionManager.ActionKind.DerivedHane,  PlayerActionAnchorProvider.ActionAnchorType.DerivedHane  },
        { PlayerActionManager.ActionKind.Tome,         PlayerActionAnchorProvider.ActionAnchorType.Tome         },
    };

    private void Awake()
    {
        if (anchorProvider == null)
            anchorProvider = GetComponent<PlayerActionAnchorProvider>();

        if (actionManager == null)
            actionManager = GetComponent<PlayerActionManager>();
    }

    private void Update()
    {
        followingAllies.RemoveAll(a => a == null);
        UpdateFormation();
        ReplenishFromStock();
    }

    private void UpdateFormation()
    {
        int count = followingAllies.Count;
        if (count == 0) return;

        bool isActing = actionManager != null && actionManager.IsActing;
        PlayerActionManager.ActionKind currentKind =
            actionManager != null ? actionManager.CurrentAction : PlayerActionManager.ActionKind.None;

        for (int i = 0; i < count; i++)
        {
            if (followingAllies[i] == null) continue;

            // ── アクション中かどうかで分岐 ──
            if (isActing
                && anchorProvider != null
                && anchorMap.TryGetValue(currentKind, out var anchorType))
            {
                // アクション中: アンカー位置に移動
                Transform anchor = anchorProvider.GetAnchor(anchorType, i);
                Vector3 targetPos = anchor != null ? anchor.position : GetFormationPos(i);

                followingAllies[i].SetUseAnchorSpeed(true);
                followingAllies[i].SetFollowTarget(targetPos);
            }
            else
            {
                // 通常時: プレイヤーの真後ろに一列縦隊
                followingAllies[i].SetUseAnchorSpeed(false);
                followingAllies[i].SetFollowTarget(GetFormationPos(i));
            }
        }
    }

    private Vector3 GetFormationPos(int i)
    {
        float distFromPlayer = followDistance + i * rowSpacing;
        Vector3 pos = transform.position - transform.forward * distFromPlayer;
        pos.y = transform.position.y;
        return pos;
    }

    private void ReplenishFromStock()
    {
        while (followingAllies.Count < maxFollowCount && stockCount > 0)
        {
            Vector3 spawnPos = transform.position - transform.forward * followDistance;
            spawnPos.y = transform.position.y;
            SpawnAllyEnemy(spawnPos);
            stockCount--;
            Debug.Log($"[AllyEnemyManager] ストックから補充。残りストック: {stockCount}");
        }
    }

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

    public void OnEnemyBecameAlly(Vector3 spawnPosition, float inkRecovery)
    {
        Vector3 fixedSpawnPos = spawnPosition;
        fixedSpawnPos.y = transform.position.y;

        if (followingAllies.Count < maxFollowCount)
            SpawnAllyEnemy(fixedSpawnPos);
        else
        {
            stockCount++;
            Debug.Log($"[AllyEnemyManager] ストックに追加。ストック: {stockCount}体");
        }
    }

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

    public int GetAllyCount() => followingAllies.Count;
    public int GetStockCount() => stockCount;

    private void OnGUI()
    {
        GUI.Label(new Rect(10, 130, 400, 20),
            $"仲間: {followingAllies.Count}体  ストック: {stockCount}体");

        if (actionManager != null && actionManager.IsActing)
        {
            GUI.Label(new Rect(10, 150, 400, 20),
                $"アクション中: {actionManager.CurrentAction} → アンカー移動");
        }
    }
}