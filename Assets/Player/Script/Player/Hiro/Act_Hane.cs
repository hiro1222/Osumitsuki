using UnityEngine;

public class Act_Hane : PlayerActionBase
{
    [Header("Hane")]
    [SerializeField] private string animationName = "hane";
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    public override string ActionName => "はね";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.Hane;
    public override string AnimationName => animationName;
    public override float Duration => duration;
    public override float MoveSpeedRate => moveSpeedRate;

    public override bool CanStart()
    {
        return !manager.IsActing;
    }

    protected override void OnStartEffect()
    {
        // TODO: はね開始時の処理を後から追加
    }

    protected override void OnTickEffect(float dt)
    {
        // TODO: はね中の効果を後から追加
    }

    protected override void OnEndEffect()
    {
        // TODO: はね終了時の処理を後から追加
    }
}
