using UnityEngine;

public class Act_Nazori : PlayerActionBase
{
    [Header("Nazori")]
    [SerializeField] private string animationName = "nazori";
    [SerializeField] private string moveAnimationName = "nazori_move";
    [SerializeField] private string idleAnimationName = "nazori_idle";
    [SerializeField] private float animationFadeTime = 0.05f;
    [SerializeField] private float moveSpeedRate = 0.35f;

    [Header("Move Check")]
    [SerializeField] private float movingThreshold = 0.01f;

    [Header("Paint")]
    [SerializeField] private bool enablePaint = true;
    [SerializeField] private float paintInterval = 0.05f;
    [SerializeField] private float paintForwardOffset = 0.65f;
    [SerializeField] private float paintRadius = 0.8f;
    [SerializeField] private byte paintDensity = 180;

    [Header("Paint Effect")]
    [SerializeField] private bool enablePaintEffect = true;
    [SerializeField] private GameObject paintEffectPrefab;
    [SerializeField] private float effectInterval = 0.05f;
    [SerializeField] private float effectForwardOffset = 0.65f;
    [SerializeField] private float effectGroundRayHeight = 1.0f;
    [SerializeField] private float effectGroundRayDistance = 3.0f;
    [SerializeField] private Vector3 effectOffset = Vector3.zero;
    [SerializeField] private float effectLifeTime = 1.5f;
    [SerializeField] private LayerMask effectGroundMask = ~0;

    private PlayerInkActionPainter inkPainter;
    private PlayerPaintStatus paintStatus;
    private Animator animator;

    private float paintTimer;
    private float effectTimer;

    private Vector3 lastInkConsumePosition;
    private Vector3 lastMoveCheckPosition;

    private float nazoriDistanceStock;
    private bool isMoving;
    private string currentAnimation;

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

        animator = owner.GetComponentInChildren<Animator>();
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
        effectTimer = 0.0f;

        lastInkConsumePosition = controller.transform.position;
        lastMoveCheckPosition = controller.transform.position;

        nazoriDistanceStock = 0.0f;
        isMoving = false;
        currentAnimation = "";

        PlayNazoriAnimation(false);

        Paint();
    }

    protected override void OnTickEffect(float dt)
    {
        isMoving = CheckMoving();

        PlayNazoriAnimation(isMoving);

        if (!UpdateInkByDistance())
        {
            EndAction();
            return;
        }

        if (enablePaint)
        {
            paintTimer += dt;

            if (paintTimer >= paintInterval)
            {
                paintTimer = 0.0f;
                Paint();
            }
        }

        // エフェクトは移動中のみ
        if (enablePaintEffect && isMoving)
        {
            effectTimer += dt;

            if (effectTimer >= effectInterval)
            {
                effectTimer = 0.0f;
                SpawnPaintEffect();
            }
        }
        else
        {
            effectTimer = 0.0f;
        }
    }

    protected override void OnEndEffect()
    {
        currentAnimation = "";
    }

    private bool CheckMoving()
    {
        Vector3 current = controller.transform.position;

        Vector3 prev = lastMoveCheckPosition;
        Vector3 now = current;

        prev.y = 0.0f;
        now.y = 0.0f;

        float distance = Vector3.Distance(prev, now);

        lastMoveCheckPosition = current;

        return distance >= movingThreshold;
    }

    private void PlayNazoriAnimation(bool moving)
    {
        if (animator == null) return;

        string nextAnimation = moving ? moveAnimationName : idleAnimationName;

        if (string.IsNullOrEmpty(nextAnimation)) return;
        if (currentAnimation == nextAnimation) return;

        animator.CrossFade(nextAnimation, animationFadeTime, 0);

        currentAnimation = nextAnimation;
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

    private void SpawnPaintEffect()
    {
        if (!enablePaintEffect) return;
        if (paintEffectPrefab == null) return;

        Vector3 basePosition =
            controller.transform.position +
            controller.transform.forward * effectForwardOffset;

        Vector3 rayOrigin =
            basePosition +
            Vector3.up * effectGroundRayHeight;

        if (!Physics.Raycast(
                rayOrigin,
                Vector3.down,
                out RaycastHit hit,
                effectGroundRayDistance,
                effectGroundMask))
        {
            return;
        }

        Quaternion rotation =
            Quaternion.FromToRotation(Vector3.up, hit.normal);

        GameObject effect = Object.Instantiate(
            paintEffectPrefab,
            hit.point + effectOffset,
            rotation);

        if (effectLifeTime > 0.0f)
        {
            Object.Destroy(effect, effectLifeTime);
        }
    }
}