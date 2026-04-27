using UnityEngine;

public abstract class PlayerActionBase : MonoBehaviour
{
    protected PlayerController controller;
    protected PlayerActionManager manager;

    private float timer;

    public bool IsRunning { get; protected set; }

    public virtual string ActionName => "Base";
    public virtual PlayerActionManager.ActionKind Kind => PlayerActionManager.ActionKind.None;
    public virtual string AnimationName => "";
    public virtual float Duration => 0.1f;
    public virtual float MoveSpeedRate => 0.0f;
    public virtual bool IsHoldAction => false;

    public virtual void Initialize(PlayerController owner, PlayerActionManager actionManager)
    {
        controller = owner;
        manager = actionManager;
    }

    public virtual bool CanStart()
    {
        return manager != null && !manager.IsActing;
    }

    public virtual void StartAction()
    {
        IsRunning = true;
        timer = 0.0f;

        if (manager != null)
        {
            manager.PlayAnimation(AnimationName);
        }

        OnStartEffect();
    }

    public virtual void Tick(float dt)
    {
        if (!IsRunning) return;

        if (IsHoldAction)
        {
            TickHold(dt);
            OnTickEffect(dt);
            return;
        }

        timer += dt;
        OnTickEffect(dt);

        if (timer >= Duration)
        {
            EndAction();
        }
    }

    public virtual void EndAction()
    {
        if (!IsRunning) return;

        OnEndEffect();
        IsRunning = false;
    }

    protected virtual void TickHold(float dt) { }

    // 後から攻撃判定、VFX、SE、ノックバックなどを足す場所
    protected virtual void OnStartEffect() { }
    protected virtual void OnTickEffect(float dt) { }
    protected virtual void OnEndEffect() { }
}
