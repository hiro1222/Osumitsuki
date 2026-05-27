using UnityEngine;

public class Act_Nazori : PlayerActionBase
{
    [Header("Nazori")]
    [SerializeField] private string animationName = "nazori";
    [SerializeField] private float moveSpeedRate = 0.35f;

    [Header("Paint")]
    [SerializeField] private bool enablePaint = true;
    [SerializeField] private float paintInterval = 0.05f;
    [SerializeField] private float paintForwardOffset = 0.65f;
    [SerializeField] private float paintRadius = 0.8f;
    [SerializeField] private byte paintDensity = 180;

    private PlayerInkActionPainter inkPainter;
    private PlayerPaintStatus paintStatus;
    private float paintTimer;

    private Vector3 lastInkConsumePosition;
    private float nazoriDistanceStock;

    public override string ActionName => "なぞり";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Nazori;
    public override string AnimationName => animationName;
    public override float MoveSpeedRate => moveSpeedRate;
    public override bool IsHoldAction => true;

    public override bool FaceCameraOnStart => false;

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
               controller.Move.IsGrounded &&
               controller.Stats.HasInk(controller.Stats.nazoriInkCostPerDistance);
    }

    protected override void TickHold(float dt)
    {
        if (!controller.InputHandler.NazoriHeld)
        {
            EndAction();
            return;
        }
    }

    protected override void OnStartEffect()
    {
        paintTimer = 0.0f;

        lastInkConsumePosition = controller.transform.position;
        nazoriDistanceStock = 0.0f;

        Paint();
    }

    protected override void OnTickEffect(float dt)
    {
        if (!UpdateInkByDistance())
        {
            EndAction();
            return;
        }

        if (!enablePaint) return;

        paintTimer += dt;

        if (paintTimer >= paintInterval)
        {
            paintTimer = 0.0f;
            Paint();
        }
    }

    protected override void OnEndEffect()
    {
    }

    private bool UpdateInkByDistance()
    {
        Vector3 currentPos = controller.transform.position;

        Vector3 prev = lastInkConsumePosition;
        Vector3 now = currentPos;

        prev.y = 0.0f;
        now.y = 0.0f;

        float movedDistance = Vector3.Distance(prev, now);

        lastInkConsumePosition = currentPos;

        if (movedDistance <= 0.0f)
        {
            return true;
        }

        nazoriDistanceStock += movedDistance;

        float costDistance = controller.Stats.nazoriInkCostDistance;
        float cost = controller.Stats.nazoriInkCostPerDistance;

        if (costDistance <= 0.0f)
        {
            return true;
        }

        while (nazoriDistanceStock >= costDistance)
        {
            if (!controller.Stats.ConsumeInk(cost))
            {
                return false;
            }

            nazoriDistanceStock -= costDistance;
        }

        return true;
    }

    private void Paint()
    {
        if (!enablePaint) return;
        if (inkPainter == null) return;

        float scaledRadius = paintRadius;

        if (paintStatus != null)
        {
            scaledRadius = paintStatus.GetPaintRadius(paintRadius);
        }

        inkPainter.PaintGroundNearPlayer(
            controller.transform,
            paintForwardOffset,
            scaledRadius,
            paintDensity);
    }
}