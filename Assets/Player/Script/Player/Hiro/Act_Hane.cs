using UnityEngine;

public class Act_Hane : PlayerActionBase
{
    [Header("Hane")]
    [SerializeField] private string animationName = "hane";
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    [Header("Slash Pattern")]
    [SerializeField] private bool enableSlash = true;
    [SerializeField] private int patternIndex = 1;
    [SerializeField] private float spawnForwardOffset = 1.5f;
    [SerializeField] private float spawnHeightOffset = 0.4f;

    private PlayerInkActionPainter inkPainter;

    public override string ActionName => "はね";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Hane;
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
