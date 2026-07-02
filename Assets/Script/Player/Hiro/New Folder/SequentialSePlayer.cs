using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// インスペクターで設定した順に「待ち時間 → SE再生」を繰り返すだけのプレイヤー。
/// CutsceneSequencer の Action (UnityEvent) から Play() を呼んで使う想定。
/// </summary>
public class SequentialSePlayer : MonoBehaviour
{
    [Serializable]
    public class SeEntry
    {
        [Tooltip("インスペクター識別用ラベル。動作に影響しない。")]
        public string label = "SE";

        [Tooltip("この音を鳴らす前に待つ秒数。1個目は開始からの秒数。")]
        public float delayBefore = 0f;

        [Tooltip("鳴らすWAVファイル (AudioClip)。")]
        public AudioClip clip;

        [Tooltip("この音の音量 (0〜1)。")]
        [Range(0f, 1f)]
        public float volume = 1f;
    }

    [Header("再生に使うAudioSource (未設定なら自動追加)")]
    [SerializeField] private AudioSource audioSource;

    [Header("SE一覧 (上から順に再生)")]
    [SerializeField] private List<SeEntry> entries = new List<SeEntry>();

    private Coroutine _running;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
        audioSource.playOnAwake = false;
    }

    /// <summary>先頭から順番に再生開始。UnityEventから呼ぶ。</summary>
    public void Play()
    {
        Stop();
        _running = StartCoroutine(Run());
    }

    /// <summary>途中で止めたいとき用。</summary>
    public void Stop()
    {
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }
    }

    private IEnumerator Run()
    {
        foreach (var e in entries)
        {
            if (e.delayBefore > 0f)
                yield return new WaitForSeconds(e.delayBefore);

            if (e.clip != null)
                audioSource.PlayOneShot(e.clip, e.volume);
        }
        _running = null;
    }

    [ContextMenu("▶ SEをテスト再生")]
    private void TestPlay()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[SequentialSePlayer] テスト再生はPlay中に行ってください。");
            return;
        }
        Play();
    }
}
