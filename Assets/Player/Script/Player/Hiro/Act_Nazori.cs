using UnityEngine;

public class Act_Nazori : PlayerActionBase
{
    [Header("Nazori")]
    [SerializeField] private string animationName = "nazori";
    [SerializeField] private float moveSpeedRate = 0.35f;

    public override string ActionName => "なぞり";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Nazori;
    public override string AnimationName => animationName;
    public override float MoveSpeedRate => moveSpeedRate;
    public override bool IsHoldAction => true;

    public override bool CanStart()
    {
        return !manager.IsActing && controller.Move.IsGrounded;
    }

    protected override void TickHold(float dt)
    {
        if (!controller.InputHandler.NazoriHeld)
        {
            EndAction();
        }
    }

    protected override void OnStartEffect()
    {
        // TODO: なぞり開始時の処理を後から追加
    }

    protected override void OnTickEffect(float dt)
    {
        // TODO: なぞり継続中の処理を後から追加
    }

    protected override void OnEndEffect()
    {
        // TODO: なぞり終了時の処理を後から追加
    }
}
