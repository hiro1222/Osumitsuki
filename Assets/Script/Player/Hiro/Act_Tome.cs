using UnityEngine;

public class Act_Tome : PlayerActionBase
{
    private enum TomePhase
    {
        None,
        Rise,
        Falling,
        LandingResume,
        Finished
    }

    [Header("Tome")]
    [SerializeField] private string animationName = "tome";
    [SerializeField] private float duration = 1.2f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    [Header("Frame Control")]
    [SerializeField] private float animationFps = 60.0f;
    [SerializeField] private int riseEndFrame = 13;
    [SerializeField] private int fallingHoldFrame = 14;

    [Header("Rise Motion")]
    [SerializeField] private float riseHeight = 0.45f;

    [Header("Pre Landing Paint")]
    [SerializeField] private bool enablePreLandingPaint = true;

    [Tooltip("Playerの下にこの距離以内で地面があれば、着地前に塗る")]
    [SerializeField] private float paintTriggerDistance = 0.6f;

    [Tooltip("なぞりと同じ PaintGroundNearPlayer の前方オフセット")]
    [SerializeField] private float paintForwardOffset = 0.0f;

    [SerializeField] private float paintRadius = 0.9f;
    [SerializeField] private byte paintDensity = 180;

    [Tooltip("地面検出用。基本はGround系Layerを指定")]
    [SerializeField] private LayerMask groundCheckMask = ~0;

    [Header("Landing")]
    [SerializeField] private float resumeAfterGroundedTime = 0.0f;

    private TomePhase phase;
    private float actionTimer;
    private float phaseTimer;
    private float groundedTimer;

    private Vector3 riseStartPosition;
    private Vector3 riseTargetPosition;

    private bool preLandingPainted;

    private PlayerInkActionPainter inkPainter;
    private PlayerPaintStatus paintStatus;

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

        paintStatus = owner.GetComponent<PlayerPaintStatus>();
        if (paintStatus == null)
        {
            paintStatus = owner.gameObject.AddComponent<PlayerPaintStatus>();
        }
    }

    public override bool CanStart()
    {
        return !manager.IsActing &&
               !controller.Move.IsGrounded &&
               controller.Stats.HasInk(controller.Stats.tomeInkCost);
    }

    public override void StartAction()
    {
        if (!controller.Stats.ConsumeInk(controller.Stats.tomeInkCost))
        {
            return;
        }

        base.StartAction();

        phase = TomePhase.Rise;
        actionTimer = 0.0f;
        phaseTimer = 0.0f;
        groundedTimer = 0.0f;
        preLandingPainted = false;

        StartRise();
    }

    public override void Tick(float dt)
    {
        if (!IsRunning) return;

        actionTimer += dt;
        phaseTimer += dt;

        switch (phase)
        {
            case TomePhase.Rise:
                TickRise();
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

    private void StartRise()
    {
        phase = TomePhase.Rise;
        phaseTimer = 0.0f;

        riseStartPosition = controller.transform.position;
        riseTargetPosition = riseStartPosition + Vector3.up * riseHeight;

        if (controller.Move != null)
        {
            controller.Move.ClearVerticalVelocity();
            controller.Move.SetExternalGravityEnabled(false);
            controller.Move.SetExternalPositionLock(true, riseStartPosition, false);
        }
    }

    private void TickRise()
    {
        float riseTime = GetFrameTime(riseEndFrame);

        float t = riseTime <= 0.0001f
            ? 1.0f
            : phaseTimer / riseTime;

        t = Mathf.Clamp01(t);

        Vector3 pos = Vector3.Lerp(
            riseStartPosition,
            riseTargetPosition,
            Smooth01(t)
        );

        controller.transform.position = pos;

        if (controller.Move != null)
        {
            controller.Move.SetExternalPositionLock(true, pos, false);
        }

        if (t >= 1.0f)
        {
            StartFalling();
        }
    }

    private void StartFalling()
    {
        phase = TomePhase.Falling;
        phaseTimer = 0.0f;

        if (controller.AnimatorDriver != null)
        {
            controller.AnimatorDriver.PauseCurrentAnimationAt(
                animationName,
                GetFrameTime(fallingHoldFrame)
            );
        }

        if (controller.Move != null)
        {
            controller.Move.SetExternalPositionLock(false, controller.transform.position, false);
            controller.Move.ClearVerticalVelocity();
            controller.Move.SetExternalGravityEnabled(true);
        }
    }

    private void TickFalling()
    {
        TryPreLandingPaint();

        if (controller.Move == null) return;
        if (!controller.Move.IsGrounded) return;

        phase = TomePhase.LandingResume;
        phaseTimer = 0.0f;
        groundedTimer = 0.0f;
    }

    private void TryPreLandingPaint()
    {
        if (preLandingPainted) return;
        if (!enablePreLandingPaint) return;
        if (inkPainter == null) return;

        Vector3 origin = controller.transform.position;
        Ray ray = new Ray(origin, Vector3.down);

        if (!Physics.Raycast(ray, out RaycastHit hit, paintTriggerDistance, groundCheckMask))
        {
            return;
        }

        float scaledRadius = paintRadius;

        if (paintStatus != null)
        {
            scaledRadius = paintStatus.GetPaintRadius(paintRadius);
        }

        inkPainter.PaintGroundNearPlayer(
            controller.transform,
            paintForwardOffset,
            scaledRadius,
            paintDensity
        );

        preLandingPainted = true;
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
        phaseTimer = 0.0f;
    }

    private void TickFinished()
    {
        if (actionTimer >= duration)
        {
            EndAction();
        }
    }

    private float GetFrameTime(int frame)
    {
        if (animationFps <= 0.0001f)
        {
            return 0.0f;
        }

        return frame / animationFps;
    }

    private float Smooth01(float t)
    {
        return t * t * (3.0f - 2.0f * t);
    }

    public override void EndAction()
    {
        if (controller.Move != null)
        {
            controller.Move.SetExternalPositionLock(false, controller.transform.position, false);
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