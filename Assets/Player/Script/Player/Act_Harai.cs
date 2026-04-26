using UnityEngine;

public class Act_Harai : PlayerActionBase
{
    [Header("Harai")]
    [SerializeField] private string animationName = "harai";
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    public override string ActionName => "はらい";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Harai;
    public override string AnimationName => animationName;
    public override float Duration => duration;
    public override float MoveSpeedRate => moveSpeedRate;

    public override bool CanStart()
    {
        return !manager.IsActing;
    }

    protected override void OnStartEffect()
    {
        // TODO: はらい開始時の処理を後から追加
    }

    protected override void OnTickEffect(float dt)
    {
        // TODO: はらい中の当たり判定などを後から追加
    }

    protected override void OnEndEffect()
    {
        // TODO: はらい終了時の処理を後から追加
    }
}
