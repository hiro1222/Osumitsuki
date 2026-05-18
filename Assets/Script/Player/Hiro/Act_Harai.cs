using UnityEngine;

public class Act_Harai : PlayerActionBase
{
    [Header("Harai")]
    [SerializeField] private string animationName = "harai";
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    [Header("Slash Pattern")]
    [SerializeField] private bool enableSlash = true;
    [SerializeField] private int patternIndex = 0;
    [SerializeField] private float spawnForwardOffset = 1.5f;
    [SerializeField] private float spawnHeightOffset = 0.4f;

    private PlayerInkActionPainter inkPainter;

    public override string ActionName => "はらい";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Harai;
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
        return !manager.IsActing;
    }

    protected override void OnStartEffect()
    {
        if (!enableSlash) return;
        if (inkPainter == null) return;

        inkPainter.FireSlashPattern(controller.transform, patternIndex, spawnForwardOffset, spawnHeightOffset);
    }

    protected override void OnTickEffect(float dt) { }
    protected override void OnEndEffect() { }
}
