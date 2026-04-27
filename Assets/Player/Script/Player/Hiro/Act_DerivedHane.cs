using UnityEngine;

public class Act_DerivedHane : PlayerActionBase
{
    [Header("Derived Hane")]
    [SerializeField] private string animationName = "derived_hane";
    [SerializeField] private float duration = 0.55f;
    [SerializeField] private float moveSpeedRate = 0.0f;

    public override string ActionName => "派生はね";
    public override PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.DerivedHane;
    public override string AnimationName => animationName;
    public override float Duration => duration;
    public override float MoveSpeedRate => moveSpeedRate;

    public override bool CanStart()
    {
        return manager.CurrentAction == PlayerActionManager.ActionKind.Nazori;
    }

    protected override void OnStartEffect()
    {
        // TODO: 派生はね開始時の処理を後から追加
    }

    protected override void OnTickEffect(float dt)
    {
        // TODO: 派生はね中の効果を後から追加
    }

    protected override void OnEndEffect()
    {
        // TODO: 派生はね終了時の処理を後から追加
    }
}
