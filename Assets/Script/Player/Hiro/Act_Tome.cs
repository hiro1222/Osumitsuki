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

    [Header("Foot Slash")]
    [SerializeField] private bool enableSlash = true;
    [SerializeField] private SlashPattern footSlashPattern;

    [Tooltip("足元からのローカル位置。Yをマイナスにすると足元寄り")]
    [SerializeField] private Vector3 footLocalPositionOffset = new Vector3(0.0f, -0.6f, 0.0f);

    [Tooltip("球の向き。下方向に撃ちたい場合は X=90 付近で調整")]
    [SerializeField] private Vector3 footLocalEulerOffset = new Vector3(90.0f, 0.0f, 0.0f);

    [SerializeField] private float footForwardOffset = 0.0f;
    [SerializeField] private float footHeightOffset = 0.0f;

    [Header("Landing")]
    [SerializeField] private float resumeAfterGroundedTime = 0.0f;

    private TomePhase phase;
    private float timer;
    private float phaseTimer;
    private float groundedTimer;

    private Vector3 riseStartPosition;
    private Vector3 riseTargetPosition;

    private bool slashSpawned;

    private PlayerInkActionPainter inkPainter;
    private Transform shotAnchor;

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

        GameObject anchorObj = new GameObject("TomeFootSlashAnchor");
        anchorObj.transform.SetParent(owner.transform);
        shotAnchor = anchorObj.transform;
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
        timer = 0.0f;
        phaseTimer = 0.0f;
        groundedTimer = 0.0f;
        slashSpawned = false;

        StartRise();
    }

    public override void Tick(float dt)
    {
        if (!IsRunning) return;

        timer += dt;
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

        SpawnFootSlash();
    }

    private void TickFalling()
    {
        if (controller.Move == null) return;

        if (!controller.Move.IsGrounded) return;

        phase = TomePhase.LandingResume;
        phaseTimer = 0.0f;
        groundedTimer = 0.0f;
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
        if (timer >= duration)
        {
            EndAction();
        }
    }

    private void SpawnFootSlash()
    {
        if (slashSpawned) return;
        if (!enableSlash) return;
        if (inkPainter == null) return;
        if (footSlashPattern == null) return;

        Transform baseTf = controller.transform;

        shotAnchor.position =
            baseTf.position +
            baseTf.right * footLocalPositionOffset.x +
            baseTf.up * footLocalPositionOffset.y +
            baseTf.forward * footLocalPositionOffset.z;

        shotAnchor.rotation =
            baseTf.rotation *
            Quaternion.Euler(footLocalEulerOffset);

        inkPainter.FireSlashPattern(
            shotAnchor,
            footSlashPattern,
            footForwardOffset,
            footHeightOffset
        );

        slashSpawned = true;
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