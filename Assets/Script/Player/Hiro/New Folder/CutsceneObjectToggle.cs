using System.Collections;
using UnityEngine;

/// <summary>
/// 登録した3Dオブジェクトの表示をオンオフする。UIのCutsceneUIToggleのオブジェクト版。
/// SetActive方式(丸ごと)と Renderer方式(見た目だけ)を選べる。
///
/// 使い方:
///   - 管理用オブジェクトにこのコンポーネントを付ける
///   - targets に表示制御したいオブジェクトを登録(複数可)
///   - useRendererOnly: ONならRendererだけ切替(当たり判定や処理は残す)
///                      OFFならGameObjectごとSetActive
///   - hideOnStart: ONならゲーム開始時に隠す
///   - ステップのActionで Show / Hide / ShowForSeconds を呼ぶ
/// </summary>
public class CutsceneObjectToggle : MonoBehaviour
{
    [Header("制御するオブジェクト")]
    [SerializeField] private GameObject[] targets;

    [Header("Rendererだけ切り替えるか (OFFならGameObjectごとSetActive)")]
    [SerializeField] private bool useRendererOnly = false;

    [Header("ゲーム開始時に隠すか")]
    [SerializeField] private bool hideOnStart = false;

    [Header("ShowForSeconds で表示する秒数")]
    [SerializeField] private float defaultShowSeconds = 3.0f;

    private void Start()
    {
        if (hideOnStart) Hide();
    }

    /// <summary>登録オブジェクトを全部表示する。</summary>
    public void Show()
    {
        SetAll(true);
    }

    /// <summary>登録オブジェクトを全部隠す。</summary>
    public void Hide()
    {
        SetAll(false);
    }

    /// <summary>表示状態を反転。</summary>
    public void Toggle()
    {
        if (targets == null) return;
        foreach (var t in targets)
        {
            if (t == null) continue;

            if (useRendererOnly)
            {
                var renderers = t.GetComponentsInChildren<Renderer>(true);
                bool anyVisible = false;
                foreach (var r in renderers)
                    if (r.enabled) { anyVisible = true; break; }
                SetRenderers(t, !anyVisible);
            }
            else
            {
                t.SetActive(!t.activeSelf);
            }
        }
    }

    /// <summary>表示して、defaultShowSeconds 秒後に自動で隠す(UnityEvent用)。</summary>
    public void ShowForSeconds()
    {
        ShowForSeconds(defaultShowSeconds);
    }

    /// <summary>表示して、指定秒後に自動で隠す。</summary>
    public void ShowForSeconds(float seconds)
    {
        StopAllCoroutines();
        StartCoroutine(ShowThenHide(seconds));
    }

    private IEnumerator ShowThenHide(float seconds)
    {
        Show();
        if (seconds > 0f)
            yield return new WaitForSeconds(seconds);
        Hide();
    }

    private void SetAll(bool visible)
    {
        if (targets == null) return;
        foreach (var t in targets)
        {
            if (t == null) continue;

            if (useRendererOnly)
                SetRenderers(t, visible);
            else
                t.SetActive(visible);
        }
    }

    /// <summary>対象とその子の全Rendererのenabledを切り替える。</summary>
    private void SetRenderers(GameObject obj, bool visible)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
            r.enabled = visible;
    }
}