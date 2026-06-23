using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// ポーズ画面の制御。
/// - Esc / コントローラ Start で開閉
/// - ポーズ中は Time.timeScale = 0 でゲームを停止
/// - ボタン: 続ける(Resume) / タイトルに戻る(GoToTitle)
///
/// ■ ゲームプレイ側は静的プロパティ PauseMenu.IsPaused を見て入力を止める
///   （SimplePlayer.Update に「if (PauseMenu.IsPaused) return;」を追加済み）。
/// ■ UIボタンを押せるように、シーンに EventSystem が必要
///   （InputSystem 使用プロジェクトなので "InputSystem UI Input Module" 付きのもの）。
///
/// ■ エディタ設定:
///   1. Canvas配下に「ポーズ画面ルート(Panel)」を作り、背景画像と2ボタン(続ける/タイトル)を置く
///   2. 空GameObject "PauseManager" にこのスクリプトを付ける
///   3. pausePanel / resumeButton / titleButton を割り当てる
///   4. titleSceneName にタイトルシーン名を入れ、そのシーンを Build Settings に追加する
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("ポーズ画面のルート。最初は非表示でOK（Awakeで自動的に隠す）")]
    [SerializeField] private GameObject pausePanel;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;   // 続ける
    [SerializeField] private Button titleButton;    // タイトルに戻る

    [Header("Title Scene")]
    [Tooltip("「タイトルに戻る」で読み込むシーン名。Build Settings に追加しておくこと。")]
    [SerializeField] private string titleSceneName = "Title";

    /// <summary>ポーズ中か（ゲームプレイ側はこれを見て入力を止める）。</summary>
    public static bool IsPaused { get; private set; }

    private CursorLockMode prevCursorLock;
    private bool prevCursorVisible;

    private void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;   // 新規ロード時に止まったままを防ぐ保険

        if (pausePanel != null) pausePanel.SetActive(false);
        if (resumeButton != null) resumeButton.onClick.AddListener(Resume);
        if (titleButton != null) titleButton.onClick.AddListener(GoToTitle);
    }

    private void Update()
    {
        if (PausePressedThisFrame())
        {
            if (IsPaused) Resume();
            else Pause();
        }
    }

    private static bool PausePressedThisFrame()
    {
        bool pressed = false;

        Keyboard kb = Keyboard.current;
        if (kb != null) pressed |= kb.escapeKey.wasPressedThisFrame;

        Gamepad pad = Gamepad.current;
        if (pad != null) pressed |= pad.startButton.wasPressedThisFrame;

        return pressed;
    }

    /// <summary>ポーズ開始。</summary>
    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);

        // マウスでボタンを押せるようカーソルを出す（元の状態は覚えておく）
        prevCursorLock = Cursor.lockState;
        prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    /// <summary>ポーズ解除（続ける）。</summary>
    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);

        // カーソルを元に戻す
        Cursor.lockState = prevCursorLock;
        Cursor.visible = prevCursorVisible;
    }

    /// <summary>タイトルシーンへ戻る。</summary>
    public void GoToTitle()
    {
        IsPaused = false;
        Time.timeScale = 1f;   // 次シーンで止まったままにしない

        if (string.IsNullOrEmpty(titleSceneName))
        {
            Debug.LogError("[PauseMenu] titleSceneName が未設定です。" +
                           "Inspectorでタイトルシーン名を入れ、Build Settingsに追加してください。");
            return;
        }
        // ※ チームのローディング機構が完成したら、それ経由の呼び出しに差し替える
        SceneManager.LoadScene(titleSceneName);
    }

    private void OnDestroy()
    {
        // シーン遷移/破棄時に timeScale を戻し損ねないよう保険
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
