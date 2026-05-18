using UnityEngine;

public class PlayerPaintStatus : MonoBehaviour
{
    [Header("Paint Level")]
    [SerializeField] private int paintLevel;
    [SerializeField] private int maxPaintLevel = 4;
    [SerializeField] private float radiusBonusPerLevel = 0.25f;

    public int PaintLevel => paintLevel;
    public int MaxPaintLevel => maxPaintLevel;

    public void AddPaintLevel(int amount = 1)
    {
        paintLevel = Mathf.Clamp(paintLevel + amount, 0, maxPaintLevel);
    }

    public void SubPaintLevel(int amount = 1)
    {
        paintLevel = Mathf.Clamp(paintLevel - amount, 0, 0);
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