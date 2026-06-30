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
    [Tooltip("ボタン入力後、SEを聞かせてからシーン遷移するまでの待機時間（秒）")]
    [SerializeField] private float sceneTransitionDelay = 0.5f;

    [Header("── SE ──")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip inkSE;       // Tome.wav（墨の演出）
    [SerializeField] private AudioClip logoStampSE; // Osumi_stamp.wav（ロゴ演出）
    [SerializeField] private AudioClip buttonSE;    // 拍子木(2).mp3（ボタン入力）

    [Header("── SE再生タイミング（フレーム番号） ──")]
    [Tooltip("墨の演出SEを鳴らすフレーム番号（0始まり）")]
    [SerializeField] private int inkSEFrame = 1;
    [Tooltip("墨の演出2SEを鳴らすフレーム番号（0始まり）")]
    [SerializeField] private int inkSEFrame2 = 1;
    [Tooltip("ロゴ演出SEを鳴らすフレーム番号（0始まり）")]
    [SerializeField] private int logoStampSEFrame = 5;

    private bool canProceed = false;
    private bool isTransitioning = false;

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
        Color c = titleLogoImage.color;
        c.a = 0f;
        titleLogoImage.color = c;

        yield return new WaitForSeconds(initialBlankDuration);

        c.a = 1f;
        titleLogoImage.color = c;

        for (int i = 0; i < titleLogoFrames.Length; i++)
        {
            titleLogoImage.sprite = titleLogoFrames[i];

            // 指定フレームでSE再生
            if (i == inkSEFrame)
                PlaySE(inkSE);

            if (i == inkSEFrame2)
                PlaySE(inkSE);

            if (i == logoStampSEFrame)
                PlaySE(logoStampSE);

            yield return new WaitForSeconds(frameDuration);
        }
    }

    private void PlaySE(AudioClip clip)
    {
        if (audioSource != null && clip != null)
            audioSource.PlayOneShot(clip);
    }

    private void Update()
    {
        if (!canProceed) return;
        if (isTransitioning) return;

        // XBOXコントローラーのAボタン
        bool gamepadPressed = Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame;

        // デバッグ用：Enterキー
        bool enterPressed = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;

        if (gamepadPressed || enterPressed)
        {
            isTransitioning = true;
            PlaySE(buttonSE);
            StartCoroutine(TransitionToNextScene());
        }
    }

    private IEnumerator TransitionToNextScene()
    {
        yield return new WaitForSeconds(sceneTransitionDelay);

        SceneTransitionData.nextSceneName = "Stage_B_light";
        SceneManager.LoadScene("LoadingScene");
    }
}