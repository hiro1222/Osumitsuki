using UnityEngine;
using UnityEngine.UI;

public class PlayerUIManager : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private PlayerStats playerStats;

    [Header("HP")]
    [SerializeField] private Image[] hpImages = new Image[3];
    [SerializeField] private Sprite hpOnSprite;
    [SerializeField] private Sprite hpOffSprite;

    [Header("Stock")]
    [SerializeField] private Image stockTensImage;
    [SerializeField] private Image stockOnesImage;
    [SerializeField] private Sprite[] numberSprites = new Sprite[10];

    [Header("Ink")]
    [SerializeField] private Image inkFillImage;

    private void Update()
    {
        if (playerStats == null) return;

        UpdateHP();
        UpdateStock();
        UpdateInk();
    }

    private void UpdateHP()
    {
        if (hpImages == null) return;

        for (int i = 0; i < hpImages.Length; i++)
        {
            if (hpImages[i] == null) continue;

            bool isOn = i < playerStats.CurrentHP;
            hpImages[i].sprite = isOn ? hpOnSprite : hpOffSprite;
        }
    }

    private void UpdateStock()
    {
        int stock = Mathf.Clamp(playerStats.Stock, 0, 99);

        int tens = stock / 10;
        int ones = stock % 10;

        if (stockTensImage != null &&
            numberSprites != null &&
            numberSprites.Length > tens)
        {
            stockTensImage.sprite = numberSprites[tens];
        }

        if (stockOnesImage != null &&
            numberSprites != null &&
            numberSprites.Length > ones)
        {
            stockOnesImage.sprite = numberSprites[ones];
        }
    }

    private void UpdateInk()
    {
        if (inkFillImage == null) return;

        inkFillImage.fillAmount = playerStats.InkRate;
    }
}