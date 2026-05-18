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

    public override string ActionName => "なぞり";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Nazori;
    public override string AnimationName => animationName;
    public override float MoveSpeedRate => moveSpeedRate;
    public override bool IsHoldAction => true;

    // なぞりは開始時にカメラ方向へ強制回転しない
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
        return !manager.IsActing && controller.Move.IsGrounded;
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
        Paint();
    }

    protected override void OnTickEffect(float dt)
    {
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