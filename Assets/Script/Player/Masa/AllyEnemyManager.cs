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

    [Header("PaintStatus参照")]
    [SerializeField] private PlayerPaintStatus paintStatus;

    [Header("── 仲間化エフェクト（ストック時用） ──")]
    [SerializeField] private GameObject allyEffectPrefab;

    [Header("── Harai中の見た目隠し ──")]
    [Tooltip("Harai/DerivedHarai開始時から見た目を隠す秒数。Haraiが終わってもこの秒数は隠れ続ける。")]
    [SerializeField] private float hideDurationSeconds = 0.5f;

    private readonly List<AllyEnemy> followingAllies = new List<AllyEnemy>();
    private int stockCount = 0;

    // Harai系統の実行で味方の見た目を隠すための状態
    private bool alliesHidden = false;
    private float hideTimer = 0f;
    private bool prevInHaraiAction = false;

    // 累計の味方番号（リセットしない）
    private int allyIndexCounter = 0;

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

        if (paintStatus == null)
            paintStatus = GetComponent<PlayerPaintStatus>();
    }

    private void Update()
    {
        followingAllies.RemoveAll(a => a == null);
        UpdateFormation();
        UpdateAllyVisibility();
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

            if (isActing
                && anchorProvider != null
                && anchorMap.TryGetValue(currentKind, out var anchorType))
            {
                Transform anchor = anchorProvider.GetAnchor(anchorType, i);
                Vector3 targetPos = anchor != null ? anchor.position : GetFormationPos(i);

                followingAllies[i].SetUseAnchorSpeed(true);
                followingAllies[i].SetFollowTarget(targetPos);
            }
            else
            {
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

    /// <summary>
    /// Harai / DerivedHarai を開始した瞬間に見た目を隠し、
    /// hideDurationSeconds 秒が経過したら戻す。
    /// Haraiが途中で終わっても、この秒数の間は隠れ続ける。
    /// 当たり判定や追従処理は止めず、Rendererのみ切り替える。
    /// </summary>
    private void UpdateAllyVisibility()
    {
        bool inHaraiAction = ShouldHideAllies();

        // Harai系に入った「瞬間」を検出して隠し始める
        if (inHaraiAction && !prevInHaraiAction)
        {
            hideTimer = hideDurationSeconds;
            if (!alliesHidden)
            {
                alliesHidden = true;
                ApplyAllyVisibility(false);
            }
        }
        prevInHaraiAction = inHaraiAction;

        // タイマーが動いている間は隠し続ける(Haraiが終わっても継続)
        if (hideTimer > 0f)
        {
            hideTimer -= Time.deltaTime;

            if (hideTimer <= 0f && alliesHidden)
            {
                alliesHidden = false;
                ApplyAllyVisibility(true);
            }
        }
    }

    private bool ShouldHideAllies()
    {
        if (actionManager == null) return false;
        if (!actionManager.IsActing) return false;

        PlayerActionManager.ActionKind kind = actionManager.CurrentAction;

        return kind == PlayerActionManager.ActionKind.Harai
            || kind == PlayerActionManager.ActionKind.DerivedHarai;
    }

    private void ApplyAllyVisibility(bool visible)
    {
        for (int i = 0; i < followingAllies.Count; i++)
        {
            SetAllyVisible(followingAllies[i], visible);
        }
    }

    private void SetAllyVisible(AllyEnemy ally, bool visible)
    {
        if (ally == null) return;

        Renderer[] renderers = ally.GetComponentsInChildren<Renderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            renderers[r].enabled = visible;
        }
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

        // 番号を付ける（累計番号）
        newAlly.SetAllyIndex(allyIndexCounter, this);
        allyIndexCounter++;

        followingAllies.Add(newAlly);

        // 隠れている最中に補充された個体も合わせて隠す
        if (alliesHidden)
        {
            SetAllyVisible(newAlly, false);
        }

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

            // ストック時はモデルなしでエフェクトだけ再生
            PlayAllyEffectOnly(fixedSpawnPos);
        }
    }

    /// <summary>
    /// AllyEnemyが自分自身でConsumeSelf()を呼んだときに通知される
    /// リストから除去して後処理を行う
    /// </summary>
    public void OnAllyConsumedSelf(AllyEnemy ally)
    {
        if (followingAllies.Contains(ally))
        {
            followingAllies.Remove(ally);

            if (paintStatus != null)
                paintStatus.SubPaintLevel();

            Debug.Log($"[AllyEnemyManager] 自己消費通知。番号: {ally.GetAllyIndex()} 残り: {followingAllies.Count}体");
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

                if (paintStatus != null)
                    paintStatus.SubPaintLevel();

                Debug.Log($"[AllyEnemyManager] 仲間消費。残り: {followingAllies.Count}体");
                return;
            }
        }
    }

    public int GetAllyCount() => followingAllies.Count;
    public int GetStockCount() => stockCount;

    public IReadOnlyList<AllyEnemy> GetAllyEnemy()
    {
        return followingAllies;
    }

    /// <summary>仲間が満員のとき、エフェクトだけを再生する</summary>
    private void PlayAllyEffectOnly(Vector3 position)
    {
        if (allyEffectPrefab == null) return;

        GameObject obj = Instantiate(allyEffectPrefab, position, Quaternion.identity);
        var ps = obj.GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in ps)
        {
            p.Clear();
            p.Play(true);
        }

        StartCoroutine(DestroyEffectWhenDone(obj, ps));
    }

    private System.Collections.IEnumerator DestroyEffectWhenDone(GameObject obj, ParticleSystem[] ps)
    {
        bool anyAlive = true;
        while (anyAlive)
        {
            anyAlive = false;
            foreach (var p in ps)
            {
                if (p != null && p.IsAlive())
                {
                    anyAlive = true;
                    break;
                }
            }
            yield return null;
        }

        if (obj != null)
            Destroy(obj);
    }
}