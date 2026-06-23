using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("── UI ──")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private UnityEngine.UI.Image tutorialImage;

    [Header("── 設定 ──")]
    [Tooltip("表示直後、入力を受け付けない時間（秒）")]
    [SerializeField] private float inputDelay = 1f;

    private UnityEngine.UI.Image currentImage;
    private bool canClose = false;
    private bool isShowing = false;

    private void Awake()
    {
        Instance = this;
        if (tutorialPanel != null)
            tutorialPanel.SetActive(false);
    }

    private void Update()
    {
        if (!isShowing) return;
        if (!canClose) return;

        // XBOXコントローラーのAボタン（Gamepad.buttonSouth）
        if (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
        {
            CloseTutorial();
        }
    }

    public void ShowTutorial(Sprite tutorialSprite)
    {
        if (isShowing) return;
        if (tutorialPanel == null) return;

        isShowing = true;
        canClose = false;

        // ★ currentImageの取得処理を削除し、tutorialImageを直接使う
        if (tutorialImage != null)
            tutorialImage.sprite = tutorialSprite;

        tutorialPanel.SetActive(true);
        Time.timeScale = 0f;

        StartCoroutine(EnableCloseAfterDelay());
    }

    private IEnumerator EnableCloseAfterDelay()
    {
        yield return new WaitForSecondsRealtime(inputDelay);
        canClose = true;
    }

    private void CloseTutorial()
    {
        isShowing = false;
        tutorialPanel.SetActive(false);
        Time.timeScale = 1f;
    }
}