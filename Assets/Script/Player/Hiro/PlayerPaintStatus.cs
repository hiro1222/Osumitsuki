using UnityEngine;

public class PlayerPaintStatus : MonoBehaviour
{
    [Header("Paint Level")]
    [SerializeField, Range(0, 4)] private int paintLevel = 0;
    [SerializeField, Range(1, 4)] private int maxPaintLevel = 4;
    [SerializeField] private float radiusBonusPerLevel = 0.25f;

    [Header("Stock 連動")]
    [Tooltip("ペイントレベルを上げたとき(雑魚攻撃時)に残基を+1するための参照。")]
    [SerializeField] private PlayerStats playerStats;
    [Tooltip("AddPaintLevel時に残基を増やすか。")]
    [SerializeField] private bool addStockOnPaintLevelUp = true;
    [Tooltip("1回のレベルアップで増やす残基量。")]
    [SerializeField] private int stockPerLevelUp = 1;

    public int PaintLevel => paintLevel;
    public int MaxPaintLevel => maxPaintLevel;

    private void Awake()
    {
        // 未割り当てなら親階層から自動取得を試みる
        if (playerStats == null)
            playerStats = GetComponentInParent<PlayerStats>();
    }

    public void AddPaintLevel(int amount = 1)
    {
        // レベルは上限でClampされるが、stockは上限後も増やしたいので
        // レベル変化の有無に関わらず stock を加算する。
        paintLevel = Mathf.Clamp(paintLevel + amount, 0, maxPaintLevel);

        if (addStockOnPaintLevelUp && playerStats != null && amount > 0)
        {
            playerStats.AddStock(stockPerLevelUp * amount);
        }
    }

    public void SubPaintLevel(int amount = 1)
    {
        paintLevel = Mathf.Clamp(paintLevel - amount, 0, maxPaintLevel);
    }

    public void SetPaintLevel(int level)
    {
        paintLevel = Mathf.Clamp(level, 0, maxPaintLevel);
    }

    public void ResetPaintLevel()
    {
        paintLevel = 0;
    }

    public float GetPaintRadius(float baseRadius)
    {
        float rate = 1.0f + radiusBonusPerLevel * paintLevel;
        return baseRadius * rate;
    }
}