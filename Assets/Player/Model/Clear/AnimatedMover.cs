using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 指定したアニメーションを再生しつつ、開始から一定時間だけ前方へ移動させる演出用スクリプト。
/// CutsceneSequencer の Action (UnityEvent) から Play() を呼んで使う想定。
/// </summary>
public class AnimatedMover : MonoBehaviour
{
    [Header("動かす対象 (未設定なら自分自身)")]
    [SerializeField] private Transform target;

    [Header("アニメーション")]
    [Tooltip("対象のAnimator (未設定なら target から自動取得)。")]
    [SerializeField] private Animator animator;

    [Tooltip("再生するステート名 (Animator Controller内のステート名)。空なら再生しない。")]
    [SerializeField] private string stateName = "";

    [Tooltip("差し替えたいAnimator Controller。未設定なら今のControllerをそのまま使う。")]
    [SerializeField] private RuntimeAnimatorController controllerOverride;

    [Header("移動")]
    [Tooltip("開始から何秒間、前方へ進むか。")]
    [SerializeField] private float moveDuration = 2f;

    [Tooltip("進むスピード (m/秒)。マイナスで後退。")]
    [SerializeField] private float moveSpeed = 1f;

    [Tooltip("移動方向。targetのローカル前方(transform.forward)を使う。")]
    [SerializeField] private bool useLocalForward = true;

    [Tooltip("useLocalForward=false のときのワールド方向。")]
    [SerializeField] private Vector3 worldDirection = Vector3.forward;

    private Coroutine _running;

    private void Awake()
    {
        if (target == null) target = transform;
        if (animator == null) animator = target.GetComponent<Animator>();
    }

    /// <summary>アニメ再生+移動を開始。UnityEventから呼ぶ。</summary>
    public void Play()
    {
        Stop();
        _running = StartCoroutine(Run());
    }

    /// <summary>途中で止めたいとき用 (アニメは止めず、移動だけ止まる)。</summary>
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
        // アニメーション開始
        if (animator != null)
        {
            if (controllerOverride != null)
                animator.runtimeAnimatorController = controllerOverride;

            if (!string.IsNullOrEmpty(stateName))
                animator.Play(stateName, 0, 0f);
        }

        // 開始から moveDuration 秒間、前方へ移動
        float elapsed = 0f;
        while (elapsed < moveDuration)
        {
            Vector3 dir = useLocalForward ? target.forward : worldDirection.normalized;
            target.position += dir * moveSpeed * Time.deltaTime;

            elapsed += Time.deltaTime;
            yield return null;
        }

        _running = null;
    }

    [ContextMenu("▶ テスト実行")]
    private void TestPlay()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[AnimatedMover] テスト実行はPlay中に行ってください。");
            return;
        }
        Play();
    }
}
