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
    [SerializeField] private string idleAnimationName = "Idle";
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

        // アクション中はアクション側のアニメを優先する
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

    private void PlayBaseAnimation(string stateName)
    {
        PlayAnimationInternal(stateName, baseCrossFade, false);
    }

    private void PlayAnimationInternal(string stateName, float fadeTime, bool forceRestart)
    {
        if (anim == null) return;
        if (string.IsNullOrEmpty(stateName)) return;

        // ここが重要。
        // 同じステートに毎フレームCrossFadeすると、アニメが1フレーム目に戻り続ける。
        if (!forceRestart && currentStateName == stateName)
        {
            return;
        }

        currentStateName = stateName;
        anim.CrossFadeInFixedTime(stateName, fadeTime);
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
