using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UIのImageでスプライトシートのコマ送りアニメをする。
/// クリアロゴなどを表示と同時にアニメさせたいとき用。
///
/// 使い方:
///   - アニメさせたいUIのImageに付ける(または別オブジェクトでtargetImageを指定)
///   - frames に スライスしたSpriteを順番に登録
///   - Play() で再生。ループ/一回再生を選べる
///   - CutsceneUIToggleのShowと組み合わせるなら、表示時にPlay()を呼ぶ
/// </summary>
[RequireComponent(typeof(Image))]
public class UISpriteAnimation : MonoBehaviour
{
    [Header("対象Image (未指定なら自分のImage)")]
    [SerializeField] private Image targetImage;

    [Header("コマ (スライスしたSpriteを順番に)")]
    [SerializeField] private Sprite[] frames;

    [Header("1秒あたりのコマ数")]
    [SerializeField] private float fps = 12f;

    [Header("ループ再生するか")]
    [SerializeField] private bool loop = true;

    [Header("有効化時に自動再生するか")]
    [SerializeField] private bool playOnEnable = true;

    private Coroutine _routine;

    void Awake()
    {
        if (targetImage == null)
            targetImage = GetComponent<Image>();
    }

    void OnEnable()
    {
        if (playOnEnable) Play();
    }

    void OnDisable()
    {
        Stop();
    }

    /// <summary>最初から再生。</summary>
    public void Play()
    {
        Stop();
        _routine = StartCoroutine(Animate());
    }

    /// <summary>停止。</summary>
    public void Stop()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }
    }

    private IEnumerator Animate()
    {
        if (frames == null || frames.Length == 0 || targetImage == null)
            yield break;

        float interval = (fps > 0f) ? 1f / fps : 0.1f;
        int i = 0;

        while (true)
        {
            targetImage.sprite = frames[i];
            i++;

            if (i >= frames.Length)
            {
                if (loop)
                {
                    i = 0;
                }
                else
                {
                    break; // 最後のコマで停止
                }
            }

            yield return new WaitForSeconds(interval);
        }
    }
}