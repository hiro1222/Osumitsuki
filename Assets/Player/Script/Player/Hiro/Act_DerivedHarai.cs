using UnityEngine;

public class Act_DerivedHarai : PlayerActionBase
{
    [Header("Derived Harai")]
    [SerializeField] private string animationName = "derived_harai";
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    public override string ActionName => "派生はらい";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.DerivedHarai;
    public override string AnimationName => animationName;
    public override float Duration => duration;
    public override float MoveSpeedRate => moveSpeedRate;

    public override bool CanStart()
    {
        return manager.CurrentAction == PlayerActionManager.ActionKind.Nazori;
    }

    protected override void OnStartEffect()
    {
        // TODO: 派生はらい開始時の処理を後から追加
    }

    protected override void OnTickEffect(float dt)
    {
        // TODO: 派生はらい中の効果を後から追加
    }

    protected override void OnEndEffect()
    {
        // TODO: 派生はらい終了時の処理を後から追加
    }
}
