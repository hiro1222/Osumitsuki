using UnityEngine;
using System.Collections;

public class AnimTest : MonoBehaviour
{
    public enum EndAction
    {
        None,
        Loop,
        ReturnToStartState,
        GoToSpecificState
    }

    [System.Serializable]
    public class TransitionSetting
    {
        public string fromState = "";
        public string toState = "";
        public float duration = 0.1f;
    }

    [System.Serializable]
    public class EffectSetting
    {
        public GameObject effectPrefab;
        public Transform spawnPoint;
        public Vector3 positionOffset = Vector3.zero;
        public Vector3 rotationOffset = Vector3.zero;
        public float spawnDelay = 0f;
    }

    [System.Serializable]
    public class AnimEntry
    {
        [Range(1, 9)]
        public int keyNumber = 1;

        [Header("State Names")]
        public string enterStateName = "";
        public string mainStateName = "";
        public string exitStateName = "";

        [Header("After Main End")]
        public EndAction endAction = EndAction.None;
        public string nextStateName = "";

        [Header("Effects")]
        public EffectSetting startEffect;
        public EffectSetting endEffect;
    }

    [Header("Target")]
    [SerializeField] private Animator targetAnimator;

    [Header("Default State")]
    [SerializeField] private string startStateName = "Idle";

    [Header("Animation Entries")]
    [SerializeField] private AnimEntry[] animEntries = new AnimEntry[9];

    [Header("Transition Settings")]
    [SerializeField] private TransitionSetting[] transitionSettings;

    [Header("Blend")]
    [SerializeField] private bool useCrossFade = true;
    [SerializeField] private float defaultTransitionDuration = 0.12f;
    [SerializeField] private float fixedTimeOffset = 0.0f;

    [Header("Options")]
    [SerializeField] private int layerIndex = 0;
    [SerializeField] private bool logWarnings = true;
    [SerializeField] private bool enableLeftClickPauseToggle = true;

    [Header("Effect Options")]
    [SerializeField] private bool parentEffectToSpawnPoint = true;
    [SerializeField] private float fallbackDestroyTime = 5.0f;

    private string currentStateName = "";
    private bool isPaused = false;

    private enum SequencePhase
    {
        None,
        Enter,
        Main,
        Exit
    }

    private AnimEntry currentEntry = null;
    private SequencePhase currentPhase = SequencePhase.None;
    private bool sequenceActive = false;
    private bool waitingForStateStart = false;

    private void Reset()
    {
        targetAnimator = GetComponent<Animator>();

        animEntries = new AnimEntry[9];
        for (int i = 0; i < animEntries.Length; i++)
        {
            animEntries[i] = new AnimEntry();
            animEntries[i].keyNumber = i + 1;
            animEntries[i].enterStateName = "";
            animEntries[i].mainStateName = "";
            animEntries[i].exitStateName = "";
            animEntries[i].nextStateName = "";
            animEntries[i].endAction = EndAction.None;
            animEntries[i].startEffect = new EffectSetting();
            animEntries[i].endEffect = new EffectSetting();
        }
    }

    private void Awake()
    {
        if (targetAnimator == null)
        {
            targetAnimator = GetComponent<Animator>();
        }
    }

    private void Start()
    {
        if (targetAnimator == null)
        {
            Debug.LogError("[AnimTest] Animator ‚ªŒ©‚Â‚©‚è‚Ü‚¹‚ñ", this);
            enabled = false;
            return;
        }

        targetAnimator.speed = 1.0f;
        isPaused = false;

        if (!string.IsNullOrEmpty(startStateName))
        {
            PlayRawState(startStateName);
        }
    }

    private void Update()
    {
        HandlePauseToggle();
        HandleNumberKeyInput();
        UpdateSequence();
    }

    private void HandlePauseToggle()
    {
        if (!enableLeftClickPauseToggle) return;

        if (Input.GetMouseButtonDown(0))
        {
            isPaused = !isPaused;
            targetAnimator.speed = isPaused ? 0.0f : 1.0f;
        }
    }

    private void HandleNumberKeyInput()
    {
        for (int i = 0; i < animEntries.Length; i++)
        {
            AnimEntry entry = animEntries[i];
            if (entry == null) continue;
            if (entry.keyNumber < 1 || entry.keyNumber > 9) continue;

            if (IsNumberKeyDown(entry.keyNumber))
            {
                StartEntry(entry);
                return;
            }
        }
    }

    private bool IsNumberKeyDown(int number)
    {
        switch (number)
        {
            case 1: return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
            case 2: return Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
            case 3: return Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3);
            case 4: return Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4);
            case 5: return Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5);
            case 6: return Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6);
            case 7: return Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7);
            case 8: return Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8);
            case 9: return Input.GetKeyDown(KeyCode.Alpha9) || Input.GetKeyDown(KeyCode.Keypad9);
        }

        return false;
    }

    private void StartEntry(AnimEntry entry)
    {
        if (entry == null) return;

        currentEntry = entry;
        sequenceActive = true;

        SpawnEffect(entry.startEffect);

        if (!string.IsNullOrEmpty(entry.enterStateName))
        {
            currentPhase = SequencePhase.Enter;
            PlayRawState(entry.enterStateName);
        }
        else if (!string.IsNullOrEmpty(entry.mainStateName))
        {
            currentPhase = SequencePhase.Main;
            PlayRawState(entry.mainStateName);
        }
        else
        {
            if (logWarnings)
            {
                Debug.LogWarning("[AnimTest] mainStateName ‚ª‹ó‚Å‚·", this);
            }

            sequenceActive = false;
            currentPhase = SequencePhase.None;
        }

        waitingForStateStart = true;
    }

    private void UpdateSequence()
    {
        if (!sequenceActive) return;
        if (targetAnimator == null) return;
        if (isPaused) return;

        AnimatorStateInfo stateInfo = targetAnimator.GetCurrentAnimatorStateInfo(layerIndex);

        if (waitingForStateStart)
        {
            if (IsCurrentPhaseStatePlaying(stateInfo))
            {
                waitingForStateStart = false;
            }
            return;
        }

        if (!IsCurrentPhaseStatePlaying(stateInfo))
        {
            return;
        }

        if (stateInfo.loop)
        {
            if (currentPhase == SequencePhase.Main && currentEntry != null && currentEntry.endAction == EndAction.Loop)
            {
                return;
            }
        }

        if (stateInfo.normalizedTime < 1.0f)
        {
            return;
        }

        OnCurrentPhaseFinished();
    }

    private bool IsCurrentPhaseStatePlaying(AnimatorStateInfo stateInfo)
    {
        string targetState = GetCurrentPhaseStateName();
        if (string.IsNullOrEmpty(targetState)) return false;

        int shortHash = Animator.StringToHash(targetState);
        return stateInfo.shortNameHash == shortHash;
    }

    private string GetCurrentPhaseStateName()
    {
        if (currentEntry == null) return "";

        switch (currentPhase)
        {
            case SequencePhase.Enter:
                return currentEntry.enterStateName;
            case SequencePhase.Main:
                return currentEntry.mainStateName;
            case SequencePhase.Exit:
                return currentEntry.exitStateName;
        }

        return "";
    }

    private void OnCurrentPhaseFinished()
    {
        if (currentEntry == null)
        {
            sequenceActive = false;
            currentPhase = SequencePhase.None;
            return;
        }

        if (currentPhase == SequencePhase.Enter)
        {
            if (!string.IsNullOrEmpty(currentEntry.mainStateName))
            {
                currentPhase = SequencePhase.Main;
                PlayRawState(currentEntry.mainStateName);
                waitingForStateStart = true;
                return;
            }

            sequenceActive = false;
            currentPhase = SequencePhase.None;
            return;
        }

        if (currentPhase == SequencePhase.Main)
        {
            SpawnEffect(currentEntry.endEffect);

            if (!string.IsNullOrEmpty(currentEntry.exitStateName))
            {
                currentPhase = SequencePhase.Exit;
                PlayRawState(currentEntry.exitStateName);
                waitingForStateStart = true;
                return;
            }

            HandleEndAction(currentEntry);
            return;
        }

        if (currentPhase == SequencePhase.Exit)
        {
            HandleEndAction(currentEntry);
        }
    }

    private void HandleEndAction(AnimEntry entry)
    {
        sequenceActive = false;
        currentPhase = SequencePhase.None;

        if (entry == null) return;

        switch (entry.endAction)
        {
            case EndAction.None:
                break;

            case EndAction.Loop:
                if (!string.IsNullOrEmpty(entry.mainStateName))
                {
                    currentPhase = SequencePhase.Main;
                    sequenceActive = true;
                    PlayRawState(entry.mainStateName);
                    waitingForStateStart = true;
                }
                break;

            case EndAction.ReturnToStartState:
                if (!string.IsNullOrEmpty(startStateName))
                {
                    PlayRawState(startStateName);
                }
                break;

            case EndAction.GoToSpecificState:
                if (!string.IsNullOrEmpty(entry.nextStateName))
                {
                    PlayRawState(entry.nextStateName);
                }
                else if (logWarnings)
                {
                    Debug.LogWarning("[AnimTest] nextStateName ‚ª‹ó‚Å‚·", this);
                }
                break;
        }
    }

    private void PlayRawState(string stateName)
    {
        if (string.IsNullOrEmpty(stateName)) return;

        int stateHash = Animator.StringToHash(stateName);

        if (!HasStateSafe(layerIndex, stateHash))
        {
            if (logWarnings)
            {
                Debug.LogWarning("[AnimTest] State‚ªŒ©‚Â‚©‚ç‚È‚¢: " + stateName, this);
            }
            return;
        }

        if (useCrossFade)
        {
            float duration = GetTransitionDuration(currentStateName, stateName);
            targetAnimator.CrossFade(stateHash, duration, layerIndex, fixedTimeOffset);
        }
        else
        {
            targetAnimator.Play(stateHash, layerIndex, fixedTimeOffset);
        }

        currentStateName = stateName;
    }

    private float GetTransitionDuration(string fromState, string toState)
    {
        if (transitionSettings != null)
        {
            for (int i = 0; i < transitionSettings.Length; i++)
            {
                TransitionSetting setting = transitionSettings[i];
                if (setting == null) continue;

                if (setting.fromState == fromState && setting.toState == toState)
                {
                    return Mathf.Max(0f, setting.duration);
                }
            }
        }

        return Mathf.Max(0f, defaultTransitionDuration);
    }

    private bool HasStateSafe(int layer, int stateHash)
    {
        if (targetAnimator == null) return false;
        if (layer < 0 || layer >= targetAnimator.layerCount) return false;
        return targetAnimator.HasState(layer, stateHash);
    }

    private void SpawnEffect(EffectSetting effect)
    {
        if (effect == null) return;
        if (effect.effectPrefab == null) return;

        if (effect.spawnDelay > 0f)
        {
            StartCoroutine(SpawnEffectDelayed(effect));
            return;
        }

        SpawnEffectImmediate(effect);
    }

    private IEnumerator SpawnEffectDelayed(EffectSetting effect)
    {
        yield return new WaitForSeconds(effect.spawnDelay);
        SpawnEffectImmediate(effect);
    }

    private void SpawnEffectImmediate(EffectSetting effect)
    {
        if (effect == null) return;
        if (effect.effectPrefab == null) return;

        Transform baseTransform = effect.spawnPoint != null ? effect.spawnPoint : transform;

        Vector3 pos = baseTransform.position + baseTransform.TransformDirection(effect.positionOffset);
        Quaternion rot = baseTransform.rotation * Quaternion.Euler(effect.rotationOffset);

        GameObject obj;

        if (parentEffectToSpawnPoint)
        {
            obj = Instantiate(effect.effectPrefab, pos, rot, baseTransform);
        }
        else
        {
            obj = Instantiate(effect.effectPrefab, pos, rot);
        }

        if (fallbackDestroyTime > 0.0f)
        {
            Destroy(obj, fallbackDestroyTime);
        }
    }
}