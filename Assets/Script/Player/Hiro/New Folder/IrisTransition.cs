using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シェーダー(UI/IrisShader)を使った円形アイリス演出。
///
/// シーン構成:
///   Canvas (Screen Space - Overlay, Sort Order 大きめ)
///     └ IrisOverlay (Image: 全画面ストレッチ / Material に IrisShader のMaterialを割当)
///
/// _Radius を大きく(=視界全開) ↔ 小さく(=暗転) 補間する。
/// </summary>
public class IrisTransition : MonoBehaviour
{
    [Header("IrisShader の Material を割り当てた Image")]
    [SerializeField] private Image irisImage;

    [Header("半径設定 (シェーダーの_Radius)")]
    [Tooltip("完全に開いた状態。画面全体が見えるよう大きめ(対角線を覆う1.2前後)。")]
    [SerializeField] private float openRadius = 1.2f;
    [Tooltip("完全に閉じた(真っ暗)状態。0でドット消失。")]
    [SerializeField] private float closedRadius = 0f;

    [Header("補間カーブ")]
    [SerializeField] private AnimationCurve ease =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Material _mat;
    private static readonly int RadiusID = Shader.PropertyToID("_Radius");
    private static readonly int AspectID = Shader.PropertyToID("_Aspect");

    void Awake()
    {
        if (irisImage != null)
        {
            // インスタンス化して他と共有しないようにする
            _mat = Instantiate(irisImage.material);
            irisImage.material = _mat;
            UpdateAspect();
        }
    }

    void OnRectTransformDimensionsChange() => UpdateAspect();

    private void UpdateAspect()
    {
        if (_mat == null) return;
        float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
        _mat.SetFloat(AspectID, aspect);
    }

    /// <summary>視界をすぼめて閉じる(アイリスアウト = 暗転)。</summary>
    public IEnumerator IrisOut(float duration)
    {
        yield return Animate(openRadius, closedRadius, duration);
    }

    /// <summary>視界を開く(アイリスイン = 明転)。</summary>
    public IEnumerator IrisIn(float duration)
    {
        yield return Animate(closedRadius, openRadius, duration);
    }

    public void SetClosedImmediate()
    {
        if (_mat != null) _mat.SetFloat(RadiusID, closedRadius);
    }

    public void SetOpenImmediate()
    {
        if (_mat != null) _mat.SetFloat(RadiusID, openRadius);
    }

    private IEnumerator Animate(float from, float to, float duration)
    {
        if (_mat == null)
        {
            Debug.LogWarning("[IrisTransition] Material が未設定です。irisImage を確認してください。");
            yield break;
        }

        UpdateAspect();

        if (duration <= 0f)
        {
            _mat.SetFloat(RadiusID, to);
            yield break;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / duration));
            _mat.SetFloat(RadiusID, Mathf.LerpUnclamped(from, to, k));
            yield return null;
        }
        _mat.SetFloat(RadiusID, to);
    }
}
