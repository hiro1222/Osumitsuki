using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;

/// <summary>
/// ポーズ画面（選択式）。
/// - Esc / コントローラ Start で開閉
/// - ポーズ中は Time.timeScale = 0 でゲーム停止
/// - 操作は「カーソルで選択 → 決定」方式（マウスクリックは使わない）
///     上下: ↑↓ / W S / Dパッド上下 / 左スティック上下
///     決定: Enter / Space / A(South)
///     戻る(続ける): B(East)（Esc/Startでも閉じる）
/// - 選択肢 options[] の順番がそのまま動作: [0]=続ける, [1]=タイトルへ
///
/// ■ クリックしないので Button も EventSystem も不要。
/// ■ カーソルはワールド座標で選択肢へ合わせる → Canvasの位置/スケールを変えてもズレない。
///   cursor と options の親が別でも動く。
/// ■ ゲームプレイ側は PauseMenu.IsPaused を見て入力を止める（SimplePlayerに追加済み）。
/// </summary>
public class PauseMenu : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("ポーズ画面のルート。最初は非表示でOK（Awakeで自動的に隠す）")]
    [SerializeField] private GameObject pausePanel;

    [Tooltip("選択カーソルの画像(RectTransform)。options と同じ親の子にすること")]
    [SerializeField] private RectTransform cursor;

    [Tooltip("選択肢を上から順に。順番=決定時の動作 [0]=続ける [1]=タイトルへ")]
    [SerializeField] private RectTransform[] options;

    [Tooltip("選択肢に対するカーソルのズラし（選択肢ローカル空間）。0,0=文字のど真ん中に重なる")]
    [SerializeField] private Vector2 cursorOffset = Vector2.zero;

    [Header("Title Scene")]
    [Tooltip("「タイトルへ」で読み込むシーン名。Build Settings に追加しておくこと。")]
    [SerializeField] private string titleSceneName = "Title";

    [Header("Input")]
    [Tooltip("スティックを「1回倒した」と判定する閾値")]
    [SerializeField] private float stickThreshold = 0.5f;

    /// <summary>ポーズ中か（ゲームプレイ側はこれを見て入力を止める）。</summary>
    public static bool IsPaused { get; private set; }

    private int selectedIndex;
    private bool stickArmed = true;   // スティック連続移動防止（ニュートラル復帰で再武装）
    private CursorLockMode prevCursorLock;
    private bool prevCursorVisible;

    private void Awake()
    {
        IsPaused = false;
        Time.timeScale = 1f;   // 新規ロード時に止まったままを防ぐ保険
        if (pausePanel != null) pausePanel.SetActive(false);

        // 設定ミス検出（null/空はカーソルが動かない・決定が無反応の原因になる）
        if (options == null || options.Length == 0)
        {
            Debug.LogWarning("[PauseMenu] options が未設定です。", this);
        }
        else
        {
            for (int i = 0; i < options.Length; i++)
                if (options[i] == null)
                    Debug.LogWarning($"[PauseMenu] options[{i}] が未設定(null)です。", this);
        }
    }

    private void Update()
    {
        // 開閉
        if (TogglePressed())
        {
            if (IsPaused) Resume();
            else Pause();
            return;
        }

        if (!IsPaused) return;

        // 選択移動
        int dir = NavDir();
        if (dir != 0) MoveSelection(dir);

        // 決定 / 戻る
        if (ConfirmPressed()) Confirm();
        else if (CancelPressed()) Resume();
    }

    // ====================================================================
    //  入力（キーボード + ゲームパッド）
    // ====================================================================

    private static bool TogglePressed()
    {
        bool p = false;
        Keyboard kb = Keyboard.current;
        if (kb != null) p |= kb.escapeKey.wasPressedThisFrame;
        Gamepad pad = Gamepad.current;
        if (pad != null) p |= pad.startButton.wasPressedThisFrame;
        return p;
    }

    /// <summary>-1=上, +1=下, 0=なし</summary>
    private int NavDir()
    {
        Keyboard kb = Keyboard.current;
        Gamepad pad = Gamepad.current;

        bool up = false, down = false;

        if (kb != null)
        {
            up   |= kb.upArrowKey.wasPressedThisFrame   || kb.wKey.wasPressedThisFrame;
            down |= kb.downArrowKey.wasPressedThisFrame || kb.sKey.wasPressedThisFrame;
        }
        if (pad != null)
        {
            up   |= pad.dpad.up.wasPressedThisFrame;
            down |= pad.dpad.down.wasPressedThisFrame;
        }
        if (up) return -1;
        if (down) return 1;

        // 左スティック（押しっぱで飛ばないよう1回ずつ）
        if (pad != null)
        {
            float y = pad.leftStick.ReadValue().y;
            if (stickArmed && Mathf.Abs(y) >= stickThreshold)
            {
                stickArmed = false;
                return y > 0f ? -1 : 1;
            }
            if (Mathf.Abs(y) < stickThreshold * 0.6f) stickArmed = true;
        }
        return 0;
    }

    private static bool ConfirmPressed()
    {
        bool p = false;
        Keyboard kb = Keyboard.current;
        if (kb != null) p |= kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame;
        Gamepad pad = Gamepad.current;
        if (pad != null) p |= pad.buttonSouth.wasPressedThisFrame;   // A
        return p;
    }

    private static bool CancelPressed()
    {
        Gamepad pad = Gamepad.current;
        return pad != null && pad.buttonEast.wasPressedThisFrame;    // B
    }

    // ====================================================================
    //  選択・カーソル
    // ====================================================================

    private void MoveSelection(int dir)
    {
        if (options == null || options.Length == 0) return;
        selectedIndex = (selectedIndex + dir + options.Length) % options.Length;   // 上下ラップ
        UpdateCursor();
    }

    private void UpdateCursor()
    {
        if (cursor == null || options == null || options.Length == 0) return;
        int i = Mathf.Clamp(selectedIndex, 0, options.Length - 1);
        RectTransform o = options[i];
        if (o == null) return;

        // 選択肢の「見た目の中心」をワールドで求める（pivot に依存しない）。
        // そこ + cursorOffset(選択肢ローカル) に、カーソルの「見た目の中心」が来るよう移動する。
        // → 選択肢/カーソルどちらの pivot でも、Canvas を動かしても、中心がピタッと合う。
        Vector3 target = o.TransformPoint(o.rect.center.x + cursorOffset.x,
                                          o.rect.center.y + cursorOffset.y, 0f);
        Vector3 cursorCenter = cursor.TransformPoint(cursor.rect.center.x, cursor.rect.center.y, 0f);
        cursor.position += target - cursorCenter;
    }

    // ====================================================================
    //  動作
    // ====================================================================

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;
        if (pausePanel != null) pausePanel.SetActive(true);

        selectedIndex = 0;
        stickArmed = false;   // 開いた直後、スティックを倒したままでも誤移動しない（ニュートラル復帰まで再武装しない）
        UpdateCursor();

        prevCursorLock = Cursor.lockState;
        prevCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        if (pausePanel != null) pausePanel.SetActive(false);

        Cursor.lockState = prevCursorLock;
        Cursor.visible = prevCursorVisible;
    }

    private void Confirm()
    {
        switch (selectedIndex)
        {
            case 0: Resume();    break;   // 続ける
            case 1: GoToTitle(); break;   // タイトルへ
            default:
                Debug.LogWarning($"[PauseMenu] selectedIndex={selectedIndex} に対応する動作がありません。" +
                                 "options を増やしたら Confirm() に分岐を追加してください。");
                break;
        }
    }

    public void GoToTitle()
    {
        // 状態を変える前にチェック（空のまま IsPaused/timeScale を戻すとパネルが開いたまま矛盾するため）
        if (string.IsNullOrEmpty(titleSceneName))
        {
            Debug.LogError("[PauseMenu] titleSceneName が未設定です。" +
                           "Inspectorでタイトルシーン名を入れ、Build Settingsに追加してください。");
            return;   // ポーズ状態は維持（パネル表示と IsPaused が食い違わない）
        }

        IsPaused = false;
        Time.timeScale = 1f;

        SceneTransitionData.nextSceneName = titleSceneName;
        SceneManager.LoadScene("LoadingScene");
    }

    private void OnDestroy()
    {
        if (IsPaused)
        {
            IsPaused = false;
            Time.timeScale = 1f;
        }
    }
}
