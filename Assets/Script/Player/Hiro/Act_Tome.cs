using UnityEngine;

public class Act_Tome : PlayerActionBase
{
    private enum TomePhase
    {
        None,
        Freeze,
        Falling,
        LandingResume,
        Finished
    }

    [Header("Tome")]
    [SerializeField] private string animationName = "tome";
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    [Header("Freeze Position")]
    [SerializeField] private float freezeTime = 0.25f;
    [SerializeField] private bool freezePosition = true;
    [SerializeField] private bool freezeHeightOnly = true;

    [Header("Animation Pause")]
    [SerializeField] private float pauseAnimationAtSeconds = 0.30f;
    [SerializeField] private float resumeAfterGroundedTime = 0.0f;

    private TomePhase phase;
    private float timer;
    private float groundedTimer;
    private Vector3 fixedPosition;

    public override string ActionName => "止め";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Tome;
    public override string AnimationName => animationName;
    public override float Duration => duration;
    public override float MoveSpeedRate => moveSpeedRate;

    public override bool CanStart()
    {
        return !manager.IsActing && !controller.Move.IsGrounded;
    }

    public override void StartAction()
    {
        base.StartAction();

        phase = TomePhase.Freeze;
        timer = 0.0f;
        groundedTimer = 0.0f;
        fixedPosition = controller.transform.position;

        if (controller.Move != null)
        {
            controller.Move.ClearVerticalVelocity();
            controller.Move.SetExternalGravityEnabled(false);

            if (freezePosition)
            {
                controller.Move.SetExternalPositionLock(true, fixedPosition, freezeHeightOnly);
            }
        }
    }

    public override void Tick(float dt)
    {
        if (!IsRunning) return;

        timer += dt;

        switch (phase)
        {
            case TomePhase.Freeze:
                TickFreeze();
                break;

            case TomePhase.Falling:
                TickFalling();
                break;

            case TomePhase.LandingResume:
                TickLandingResume(dt);
                break;

            case TomePhase.Finished:
                TickFinished();
                break;
        }
    }

    private void TickFreeze()
    {
        if (timer < freezeTime) return;

        if (controller.AnimatorDriver != null)
        {
            controller.AnimatorDriver.PauseCurrentAnimationAt(animationName, pauseAnimationAtSeconds);
        }

        if (controller.Move != null)
        {
            controller.Move.SetExternalPositionLock(false, fixedPosition, freezeHeightOnly);
            controller.Move.SetExternalGravityEnabled(true);
        }

        phase = TomePhase.Falling;
    }

    private void TickFalling()
    {
        if (!controller.Move.IsGrounded) return;

        groundedTimer = 0.0f;
        phase = TomePhase.LandingResume;
    }

    private void TickLandingResume(float dt)
    {
        groundedTimer += dt;

        if (groundedTimer < resumeAfterGroundedTime) return;

        if (controller.AnimatorDriver != null)
        {
            controller.AnimatorDriver.ResumeAnimation();
        }

        phase = TomePhase.Finished;
    }

    private void TickFinished()
    {
        if (timer >= duration)
        {
            EndAction();
        }
    }

    public override void EndAction()
    {
        if (controller.Move != null)
        {
            controller.Move.SetExternalPositionLock(false, fixedPosition, freezeHeightOnly);
            controller.Move.SetExternalGravityEnabled(true);
        }

        if (controller.AnimatorDriver != null)
        {
            controller.AnimatorDriver.ResumeAnimation();
        }

        phase = TomePhase.None;

        base.EndAction();
    }
}
