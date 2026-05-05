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

    [Header("Paint")]
    [SerializeField] private bool enablePaint = true;
    [SerializeField] private float paintForwardOffset = 0.5f;
    [SerializeField] private float paintRadius = 0.9f;
    [SerializeField] private byte paintDensity = 200;

    private TomePhase phase;
    private float timer;
    private float groundedTimer;
    private Vector3 fixedPosition;
    private PlayerInkActionPainter inkPainter;

    public override string ActionName => "止め";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Tome;
    public override string AnimationName => animationName;
    public override float Duration => duration;
    public override float MoveSpeedRate => moveSpeedRate;

    public override void Initialize(PlayerController owner, PlayerActionManager actionManager)
    {
        base.Initialize(owner, actionManager);

        inkPainter = owner.GetComponent<PlayerInkActionPainter>();
        if (inkPainter == null)
        {
            inkPainter = owner.gameObject.AddComponent<PlayerInkActionPainter>();
        }
    }

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

        PaintTome();

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
        PaintTome();

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

    private void PaintTome()
    {
        if (!enablePaint) return;
        if (inkPainter == null) return;

        float radius = controller.ActionManager.GetPaintRadius(paintRadius);

        inkPainter.PaintGroundNearPlayer(
            controller.transform,
            paintForwardOffset,
            radius,
            paintDensity);
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