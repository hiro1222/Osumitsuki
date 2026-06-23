using UnityEngine;

/// <summary>
/// プレイヤーの動作（足音・ジャンプ・各アクション・被弾）に
/// Unity標準のAudioSourceでSEを再生するコンポーネント。
///
/// ・3Dサウンド（定位／距離減衰）対応：spatialBlend・minDistance・maxDistance・rolloffModeで調整
/// ・足音(SE_001)は複数クリップをランダム or 順番で再生
/// ・なぞり(SE_002)は専用のループ用AudioSourceでループ再生（開始/停止）
///
/// 内部で単発用(sfxSource)とループ用(loopSource)の2つのAudioSourceを自動生成する。
/// 各クリップはInspectorから割り当てる（未割り当てのものは自動スキップ）。
/// </summary>
[DisallowMultipleComponent]
public class PlayerSound : MonoBehaviour
{
    private PlayerController controller;

    [Header("足音 SE_001 (Walk)")]
    [Tooltip("地上移動中に一定間隔で再生。複数入れるとバリエーション再生される")]
    [SerializeField] private AudioClip[] footstepClips;

    [Tooltip("true=ランダム再生 / false=順番(ラウンドロビン)再生")]
    [SerializeField] private bool footstepRandom = true;

    [Tooltip("足音の間隔(秒)。移動速度倍率で自動的に伸縮する")]
    [SerializeField] private float footstepInterval = 0.4f;

    [Tooltip("足音を鳴らす最小入力量(これ未満の微移動では鳴らさない)")]
    [SerializeField] private float footstepMoveThreshold = 0.1f;

    [SerializeField, Range(0f, 1f)] private float footstepVolume = 1.0f;

    [Header("なぞり SE_002 (Nazori / ループ)")]
    [Tooltip("なぞり中ループ再生する本体")]
    [SerializeField] private AudioClip nazoriLoopClip;

    [Tooltip("なぞり開始時に1回だけ鳴らす音(任意)")]
    [SerializeField] private AudioClip nazoriStartClip;

    [SerializeField, Range(0f, 1f)] private float nazoriVolume = 1.0f;

    [Header("ジャンプ SE_003")]
    [SerializeField] private AudioClip jumpClip;

    [Header("アクション SE_004-008")]
    [Tooltip("はね SE_004")]            [SerializeField] private AudioClip haneClip;
    [Tooltip("派生はね SE_005")]        [SerializeField] private AudioClip derivedHaneClip;
    [Tooltip("はらい SE_006")]          [SerializeField] private AudioClip haraiClip;
    [Tooltip("派生はらい SE_007")]      [SerializeField] private AudioClip derivedHaraiClip;
    [Tooltip("とめ SE_008")]            [SerializeField] private AudioClip tomeClip;

    [Header("被弾 SE_011 (OnHit)")]
    [Tooltip("軽いヒット(通常ノックバック)")]   [SerializeField] private AudioClip onHitSmallClip;
    [Tooltip("重いヒット")]                       [SerializeField] private AudioClip onHitBigClip;

    [Header("3Dサウンド設定")]
    [Tooltip("1=完全3D(定位あり) / 0=2D。仕様では3Dサウンド指定")]
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1.0f;

    [Tooltip("この距離までは減衰しない")]
    [SerializeField] private float minDistance = 1.0f;

    [Tooltip("この距離で最小音量になる")]
    [SerializeField] private float maxDistance = 25.0f;

    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Logarithmic;

    [Tooltip("全体音量(各SEに掛かる)")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1.0f;

    private AudioSource sfxSource;   // 単発(PlayOneShot)用
    private AudioSource loopSource;  // なぞり等のループ用

    private float footstepTimer;
    private int footstepIndex;
    private bool wasGrounded;

    public void Initialize(PlayerController owner)
    {
        controller = owner;

        sfxSource = CreateSource("PlayerSound_SFX", false);
        loopSource = CreateSource("PlayerSound_Loop", true);

        wasGrounded = controller != null && controller.Move != null && controller.Move.IsGrounded;
        footstepTimer = 0.0f;
        footstepIndex = 0;
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        GameObject go = new GameObject(sourceName);
        go.transform.SetParent(transform, false);

        AudioSource src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.loop = loop;
        src.spatialBlend = spatialBlend;
        src.minDistance = minDistance;
        src.maxDistance = maxDistance;
        src.rolloffMode = rolloffMode;
        src.dopplerLevel = 0.0f;
        return src;
    }

    public void Tick()
    {
        if (controller == null) return;

        UpdateJump();
        UpdateFootsteps();
        UpdateNazoriLoop();
    }

    /// <summary>
    /// なぞり中は「移動中だけ」ループ音を鳴らす。
    /// なぞり状態でない、または止まっている間は停止する。
    /// </summary>
    private void UpdateNazoriLoop()
    {
        if (loopSource == null) return;

        bool isNazori = controller.ActionManager != null && controller.ActionManager.IsNazori;
        PlayerInput input = controller.InputHandler;
        bool moving = input != null &&
                      input.MoveInput.sqrMagnitude > footstepMoveThreshold * footstepMoveThreshold;

        if (isNazori && moving && nazoriLoopClip != null)
        {
            if (!loopSource.isPlaying || loopSource.clip != nazoriLoopClip)
            {
                loopSource.clip = nazoriLoopClip;
                loopSource.volume = masterVolume * nazoriVolume;
                loopSource.Play();
            }
        }
        else
        {
            if (loopSource.isPlaying)
            {
                loopSource.Stop();
            }
        }
    }

    /// <summary>接地→空中(上向き速度)への遷移をジャンプ踏み切りとして検出。</summary>
    private void UpdateJump()
    {
        PlayerMove move = controller.Move;
        if (move == null) return;

        bool grounded = move.IsGrounded;

        if (wasGrounded && !grounded && move.VerticalVelocity > 0.0f)
        {
            PlayOneShot(jumpClip);
        }

        wasGrounded = grounded;
    }

    /// <summary>地上移動中、一定間隔で足音を鳴らす。</summary>
    private void UpdateFootsteps()
    {
        PlayerMove move = controller.Move;
        PlayerInput input = controller.InputHandler;
        if (move == null || input == null) return;
        if (footstepClips == null || footstepClips.Length == 0) return;

        bool moving = input.MoveInput.sqrMagnitude >
                      footstepMoveThreshold * footstepMoveThreshold;

        float rate = controller.ActionManager != null
            ? controller.ActionManager.CurrentMoveSpeedRate
            : 1.0f;

        if (!move.IsGrounded || !moving || rate <= 0.0f)
        {
            footstepTimer = 0.0f;
            return;
        }

        footstepTimer -= Time.deltaTime;

        if (footstepTimer <= 0.0f)
        {
            PlayFootstep();
            footstepTimer = footstepInterval / Mathf.Max(0.1f, rate);
        }
    }

    private void PlayFootstep()
    {
        AudioClip clip;
        if (footstepRandom)
        {
            clip = footstepClips[Random.Range(0, footstepClips.Length)];
        }
        else
        {
            clip = footstepClips[footstepIndex % footstepClips.Length];
            footstepIndex++;
        }
        PlayOneShot(clip, footstepVolume);
    }

    /// <summary>
    /// アクション開始時のSEを再生。PlayerActionManager.StartActionから呼ばれる。
    /// なぞりはループ開始、それ以外は単発＋ループ停止。
    /// </summary>
    public void PlayActionSound(PlayerActionManager.ActionKind kind)
    {
        // なぞり以外に遷移したらループは止める
        if (kind != PlayerActionManager.ActionKind.Nazori)
        {
            StopActionLoop();
        }

        switch (kind)
        {
            case PlayerActionManager.ActionKind.Nazori:
                // ループ本体はTickのUpdateNazoriLoopが移動中だけ自動制御する
                PlayOneShot(nazoriStartClip);
                break;
            case PlayerActionManager.ActionKind.Hane:
                PlayOneShot(haneClip);
                break;
            case PlayerActionManager.ActionKind.DerivedHane:
                PlayOneShot(derivedHaneClip);
                break;
            case PlayerActionManager.ActionKind.Harai:
                PlayOneShot(haraiClip);
                break;
            case PlayerActionManager.ActionKind.DerivedHarai:
                PlayOneShot(derivedHaraiClip);
                break;
            case PlayerActionManager.ActionKind.Tome:
                PlayOneShot(tomeClip);
                break;
        }
    }

    /// <summary>ループ音(なぞり)を停止。アクション終了時に呼ぶ。</summary>
    public void StopActionLoop()
    {
        if (loopSource != null && loopSource.isPlaying)
        {
            loopSource.Stop();
            loopSource.clip = null;
        }
    }

    /// <summary>被弾SEを再生。PlayerStateMachineから呼ばれる。</summary>
    public void PlayDamage(bool heavy = false)
    {
        PlayOneShot(heavy ? onHitBigClip : onHitSmallClip);
    }

    private void StartLoop(AudioClip clip, float volume)
    {
        if (clip == null || loopSource == null) return;
        loopSource.clip = clip;
        loopSource.volume = masterVolume * volume;
        loopSource.Play();
    }

    private void PlayOneShot(AudioClip clip, float volume = 1.0f)
    {
        if (clip == null || sfxSource == null) return;
        sfxSource.PlayOneShot(clip, masterVolume * volume);
    }
}
