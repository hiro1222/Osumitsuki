using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

public class TitleManager : MonoBehaviour
{
    [Header("── UI ──")]
    [SerializeField] private Image titleImage;       // タイトル画像
    [SerializeField] private GameObject pressButtonText; // "Press B Button"のテキスト

    [Header("── タイトルロゴアニメーション ──")]
    [SerializeField] private Image titleLogoImage; // ロゴ表示用のImage
    [SerializeField] private Sprite[] titleLogoFrames; // 分割したスプライトを順番に入れる
    [SerializeField] private float frameDuration = 0.05f; // 1フレームあたりの時間
    [SerializeField] private float initialBlankDuration = 0.3f;

    [Header("── 設定 ──")]
    [Tooltip("タイトル画像表示後、テキストが出るまでの時間")]
    [SerializeField] private float delayBeforeText = 1.5f;

    private bool canProceed = false;

    private void Start()
    {
        if (pressButtonText != null)
            pressButtonText.SetActive(false);

        StartCoroutine(PlayTitleLogoAnimation());
        StartCoroutine(ShowSequence());
    }

    private IEnumerator ShowSequence()
    {
        yield return new WaitForSeconds(delayBeforeText);

        if (pressButtonText != null)
            pressButtonText.SetActive(true);

        canProceed = true;
    }

    private IEnumerator PlayTitleLogoAnimation()
    {
        // 最初は完全に透明にする
        Color c = titleLogoImage.color;
        c.a = 0f;
        titleLogoImage.color = c;

        yield return new WaitForSeconds(initialBlankDuration);

        // 透明を解除
        c.a = 1f;
        titleLogoImage.color = c;

        foreach (var frame in titleLogoFrames)
        {
            titleLogoImage.sprite = frame;
            yield return new WaitForSeconds(frameDuration);
        }
    }

    private void Update()
    {
        if (!canProceed) return;

        // XBOXコントローラーのAボタン
        bool gamepadPressed = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

        // デバッグ用：Enterキー
        bool enterPressed = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;

        if (gamepadPressed || enterPressed)
        {
            SceneTransitionData.nextSceneName = "Stage_B_light";
            SceneManager.LoadScene("LoadingScene");
        }
    }
}