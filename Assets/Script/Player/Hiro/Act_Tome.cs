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

    [Header("Fall Motion")]
    [SerializeField] private float fallGravity = 25.0f;
    [SerializeField] private float maxFallSpeed = 18.0f;
    [SerializeField] private float landingCheckDistance = 0.35f;
    [SerializeField] private float landingSnapHeight = 0.0f;

    [Header("Pre Landing Paint")]
    [SerializeField] private bool enablePreLandingPaint = true;
    [SerializeField] private float paintTriggerDistance = 0.6f;
    [SerializeField] private float paintForwardOffset = 0.0f;
    [SerializeField] private float paintRadius = 0.9f;
    [SerializeField] private byte paintDensity = 180;
    [SerializeField] private LayerMask groundCheckMask = ~0;

    [Header("Landing")]
    [SerializeField] private float resumeAfterGroundedTime = 0.0f;

    [Header("Landing Effect")]
    [SerializeField] private GameObject landingEffectPrefab;
    [SerializeField] private Vector3 landingEffectOffset = Vector3.zero;
    [SerializeField] private float landingEffectLifeTime = 2.0f;

    private TomePhase phase;
    private float actionTimer;
    private float phaseTimer;
    private float groundedTimer;

    private Vector3 riseStartPosition;
    private Vector3 riseTargetPosition;

    private bool preLandingPainted;
    private bool landingEffectPlayed;

    private float fallSpeed;

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
        landingEffectPlayed = false;
        fallSpeed = 0.0f;

        StartRise();
    }

    /// <summary>
    /// インク消費・条件チェックを行わずに強制起動する（リスポーン復帰用）。
    /// </summary>
    public void StartActionForced()
    {
        base.StartAction();

        phase = TomePhase.Rise;
        actionTimer = 0.0f;
        phaseTimer = 0.0f;
        groundedTimer = 0.0f;

        preLandingPainted = false;
        landingEffectPlayed = false;
        fallSpeed = 0.0f;

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
                TickFalling(dt);
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
        fallSpeed = 0.0f;

        if (controller.AnimatorDriver != null)
        {
            controller.AnimatorDriver.PauseCurrentAnimationAt(
                animationName,
                GetFrameTime(fallingHoldFrame)
            );
        }

        if (controller.Move != null)
        {
            controller.Move.ClearVerticalVelocity();
            controller.Move.SetExternalGravityEnabled(false);
            controller.Move.SetExternalPositionLock(true, controller.transform.position, false);
        }
    }

    private void TickFalling(float dt)
    {
        TryPreLandingPaint();

        fallSpeed += fallGravity * dt;
        fallSpeed = Mathf.Min(fallSpeed, maxFallSpeed);

        Vector3 currentPos = controller.transform.position;
        Vector3 nextPos = currentPos + Vector3.down * fallSpeed * dt;

        float rayDistance = Vector3.Distance(currentPos, nextPos) + landingCheckDistance;

        if (Physics.Raycast(currentPos, Vector3.down, out RaycastHit hit, rayDistance, groundCheckMask))
        {
            Vector3 landedPos = controller.transform.position;
            landedPos.y = hit.point.y + landingSnapHeight;

            controller.transform.position = landedPos;

            if (controller.Move != null)
            {
                controller.Move.SetExternalPositionLock(true, landedPos, false);
                controller.Move.ClearVerticalVelocity();
            }

            TryPlayLandingEffect(hit.point, hit.normal);

            phase = TomePhase.LandingResume;
            phaseTimer = 0.0f;
            groundedTimer = 0.0f;
            return;
        }

        controller.transform.position = nextPos;

        if (controller.Move != null)
        {
            controller.Move.SetExternalPositionLock(true, nextPos, false);
        }
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

    private void TryPlayLandingEffect(Vector3 hitPoint, Vector3 hitNormal)
    {
        if (landingEffectPlayed) return;
        if (landingEffectPrefab == null) return;

        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);

        GameObject effect = Object.Instantiate(
            landingEffectPrefab,
            hitPoint + landingEffectOffset,
            rotation
        );

        if (landingEffectLifeTime > 0.0f)
        {
            Object.Destroy(effect, landingEffectLifeTime);
        }

        landingEffectPlayed = true;
    }

    private void TickLandingResume(float dt)
    {
        groundedTimer += dt;

        if (groundedTimer < resumeAfterGroundedTime) return;

        if (controller.AnimatorDriver != null)
        {
            controller.AnimatorDriver.ResumeAnimation();
        }

        if (controller.Move != null)
        {
            controller.Move.SetExternalPositionLock(false, controller.transform.position, false);
            controller.Move.SetExternalGravityEnabled(true);
            controller.Move.ClearVerticalVelocity();
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
            controller.Move.ClearVerticalVelocity();
        }

        if (controller.AnimatorDriver != null)
        {
            controller.AnimatorDriver.ResumeAnimation();
        }

        phase = TomePhase.None;

        base.EndAction();
    }
}