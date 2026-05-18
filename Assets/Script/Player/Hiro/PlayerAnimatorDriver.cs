using UnityEngine;

public class PlayerAnimatorDriver : MonoBehaviour
{
    private PlayerController controller;
    private Animator anim;

    private readonly int hashSpeed = Animator.StringToHash("Speed");
    private readonly int hashGrounded = Animator.StringToHash("IsGrounded");
    private readonly int hashIsActing = Animator.StringToHash("IsActing");
    private readonly int hashIsNazori = Animator.StringToHash("IsNazori");
    private readonly int hashActionKind = Animator.StringToHash("ActionKind");

    [Header("Animator")]
    [SerializeField] private Animator targetAnimator;
    [SerializeField] private string defaultChildAnimatorName = "nazori";

    [Header("Base Animation Names")]
    [SerializeField] private string idleAnimationName = "idle";
    [SerializeField] private string walkAnimationName = "walk";
    [SerializeField] private string jumpAnimationName = "jump";

    [Header("Cross Fade")]
    [SerializeField] private float baseCrossFade = 0.12f;
    [SerializeField] private float actionCrossFade = 0.08f;

    private string currentStateName = "";

    public void Initialize(PlayerController owner)
    {
        controller = owner;

        if (targetAnimator != null)
        {
            anim = targetAnimator;
            return;
        }

        Transform child = transform.Find(defaultChildAnimatorName);
        if (child != null)
        {
            anim = child.GetComponent<Animator>();
        }

        if (anim == null)
        {
            anim = GetComponentInChildren<Animator>();
        }
    }

    public void Tick()
    {
        if (anim == null) return;

        float speed = controller.InputHandler.MoveInput.magnitude * controller.ActionManager.CurrentMoveSpeedRate;

        SafeSetFloat(hashSpeed, speed);
        SafeSetBool(hashGrounded, controller.Move.IsGrounded);
        SafeSetBool(hashIsActing, controller.ActionManager.IsActing);
        SafeSetBool(hashIsNazori, controller.ActionManager.IsNazori);
        SafeSetInt(hashActionKind, (int)controller.ActionManager.CurrentAction);

        if (controller.ActionManager.IsActing)
        {
            return;
        }

        bool grounded = controller.Move.IsGrounded;
        bool moving = controller.InputHandler.MoveInput.sqrMagnitude > 0.01f;

        if (!grounded)
        {
            PlayBaseAnimation(jumpAnimationName);
            return;
        }

        if (moving)
        {
            PlayBaseAnimation(walkAnimationName);
            return;
        }

        PlayBaseAnimation(idleAnimationName);
    }

    public void PlayActionAnimation(string stateName)
    {
        PlayAnimationInternal(stateName, actionCrossFade, true);
    }

    public void PauseCurrentAnimationAt(string stateName, float seconds)
    {
        if (anim == null) return;
        if (string.IsNullOrEmpty(stateName)) return;

        int hash = Animator.StringToHash(stateName);

        if (!anim.HasState(0, hash))
        {
            Debug.LogWarning("Animator state not found: " + stateName);
            return;
        }

        AnimatorStateInfo info = anim.GetCurrentAnimatorStateInfo(0);
        float length = info.length;

        if (length <= 0.0f)
        {
            length = 1.0f;
        }

        float normalizedTime = Mathf.Clamp01(seconds / length);

        currentStateName = stateName;
        anim.Play(hash, 0, normalizedTime);
        anim.speed = 0.0f;
    }

    public void ResumeAnimation()
    {
        if (anim == null) return;
        anim.speed = 1.0f;
    }

    private void PlayBaseAnimation(string stateName)
    {
        PlayAnimationInternal(stateName, baseCrossFade, false);
    }

    private void PlayAnimationInternal(string stateName, float fadeTime, bool forceRestart)
    {
        if (anim == null) return;
        if (string.IsNullOrEmpty(stateName)) return;

        int hash = Animator.StringToHash(stateName);

        if (!anim.HasState(0, hash))
        {
            Debug.LogWarning("Animator state not found: " + stateName);
            return;
        }

        if (!forceRestart && currentStateName == stateName)
        {
            return;
        }

        currentStateName = stateName;
        anim.CrossFadeInFixedTime(hash, fadeTime, 0);
    }

    private void SafeSetFloat(int hash, float value)
    {
        if (HasParameter(hash)) anim.SetFloat(hash, value);
    }

    private void SafeSetBool(int hash, bool value)
    {
        if (HasParameter(hash)) anim.SetBool(hash, value);
    }

    private void SafeSetInt(int hash, int value)
    {
        if (HasParameter(hash)) anim.SetInteger(hash, value);
    }

    private bool HasParameter(int hash)
    {
        if (anim == null) return false;

        foreach (AnimatorControllerParameter p in anim.parameters)
        {
            if (p.nameHash == hash) return true;
        }

        return false;
    }
}
