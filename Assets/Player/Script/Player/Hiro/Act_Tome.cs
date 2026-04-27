using UnityEngine;

public class Act_Tome : PlayerActionBase
{
    [Header("Tome")]
    [SerializeField] private string animationName = "tome";
    [SerializeField] private float duration = 0.45f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    public override string ActionName => "止め";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Tome;
    public override string AnimationName => animationName;
    public override float Duration => duration;
    public override float MoveSpeedRate => moveSpeedRate;

    public override bool CanStart()
    {
        return !manager.IsActing && !controller.Move.IsGrounded;
    }

    protected override void OnStartEffect()
    {
        // TODO: 止め開始時の処理を後から追加
    }

    protected override void OnTickEffect(float dt)
    {
        // TODO: 止め中の効果を後から追加
    }

    protected override void OnEndEffect()
    {
        // TODO: 止め終了時の処理を後から追加
    }
}
