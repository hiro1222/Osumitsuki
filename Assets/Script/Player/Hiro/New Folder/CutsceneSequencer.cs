using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 演出をステップ単位で順番に実行するシーケンサー。
/// 各ステップの「前の待ち時間」をインスペクターで数値設定できる。
///
/// カメラは CutsceneCamera で「指定座標へ移動/固定」する方式(Cinemachine不要)。
/// </summary>
public class CutsceneSequencer : MonoBehaviour
{
    public enum StepKind
    {
        Action,     // UnityEvent を1回呼ぶ(アニメ/スカイボックス/花火など)
        IrisOut,    // 視界を閉じる
        IrisIn,     // 視界を開く
        CameraSnap, // カメラを指定ターゲットへ瞬間移動(カット)
        CameraMove, // カメラを指定ターゲットへなめらか移動
        WaitOnly,   // 何もせず待つだけ
    }

    [Serializable]
    public class Step
    {
        [Tooltip("インスペクター識別用ラベル。動作に影響しない。")]
        public string label = "Step";

        [Tooltip("このステップ開始前に待つ秒数。ここで合間を調整する。")]
        public float delayBefore = 0f;

        [Tooltip("ステップの種類。")]
        public StepKind kind = StepKind.Action;

        [Tooltip("kind=Action のとき実行する内容。")]
        public UnityEvent action;

        [Tooltip("kind=IrisOut/IrisIn のときの演出時間(秒)。")]
        public float irisDuration = 0.6f;

        [Tooltip("kind=CameraSnap/CameraMove のときの移動先ターゲット。")]
        public Transform cameraTarget;

        [Tooltip("kind=CameraMove のときの移動時間(秒)。")]
        public float cameraMoveDuration = 1.0f;

        [Tooltip("このステップ実行後の余韻待ち(秒)。")]
        public float holdAfter = 0f;
    }

    [Header("演出要素")]
    [SerializeField] private IrisTransition iris;
    [SerializeField] private CutsceneCamera cutsceneCamera;

    [Header("演出開始時に視界を閉じた状態から始めるか")]
    [SerializeField] private bool startClosed = false;

    [Header("ステップ一覧 (上から順に実行)")]
    [SerializeField] private List<Step> steps = new List<Step>();

    [Header("多重発火を防ぐ")]
    [SerializeField] private bool playOnce = true;

    [Header("コールバック")]
    public UnityEvent onSequenceStart;
    public UnityEvent onSequenceComplete;

    private bool _isPlaying;
    private bool _hasPlayed;

    public bool IsPlaying => _isPlaying;

    public void Play()
    {
        if (_isPlaying) return;
        if (playOnce && _hasPlayed) return;
        StartCoroutine(Run());
    }

    public void ResetSequence()
    {
        StopAllCoroutines();
        _isPlaying = false;
        _hasPlayed = false;
    }

    /// <summary>
    /// テスト用: Inspectorの「⋮」メニューから呼べる。Play中に実行すること。
    /// playOnceで再生済みでも強制的にもう一度流す。
    /// </summary>
    [ContextMenu("▶ 演出をテスト実行")]
    private void TestPlay()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CutsceneSequencer] テスト実行はPlay中に行ってください。");
            return;
        }
        _isPlaying = false;
        _hasPlayed = false;
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        _isPlaying = true;
        _hasPlayed = true;

        if (startClosed && iris != null)
            iris.SetClosedImmediate();

        onSequenceStart?.Invoke();

        foreach (var step in steps)
        {
            if (step.delayBefore > 0f)
                yield return new WaitForSeconds(step.delayBefore);

            switch (step.kind)
            {
                case StepKind.Action:
                    step.action?.Invoke();
                    break;

                case StepKind.IrisOut:
                    if (iris != null)
                        yield return iris.IrisOut(step.irisDuration);
                    break;

                case StepKind.IrisIn:
                    if (iris != null)
                        yield return iris.IrisIn(step.irisDuration);
                    break;

                case StepKind.CameraSnap:
                    if (cutsceneCamera != null)
                        cutsceneCamera.SnapTo(step.cameraTarget);
                    break;

                case StepKind.CameraMove:
                    if (cutsceneCamera != null)
                        yield return cutsceneCamera.MoveTo(step.cameraTarget, step.cameraMoveDuration);
                    break;

                case StepKind.WaitOnly:
                    break;
            }

            if (step.holdAfter > 0f)
                yield return new WaitForSeconds(step.holdAfter);
        }

        onSequenceComplete?.Invoke();
        _isPlaying = false;
    }
}
