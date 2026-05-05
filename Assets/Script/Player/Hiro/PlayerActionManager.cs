using UnityEngine;

public class PlayerActionManager : MonoBehaviour
{
    public enum ActionKind
    {
        None,
        Nazori,
        Harai,
        Hane,
        DerivedHarai,
        DerivedHane,
        Tome
    }

    private PlayerController controller;

    private Act_Nazori actNazori;
    private Act_Harai actHarai;
    private Act_Hane actHane;
    private Act_DerivedHarai actDerivedHarai;
    private Act_DerivedHane actDerivedHane;
    private Act_Tome actTome;

    private PlayerActionBase currentAction;

    [Header("Return Animation")]
    [SerializeField] private string idleAnimationName = "idle";

    [Header("Nazori Reinput Rule")]
    [SerializeField] private bool requireNazoriReleaseAfterAction = true;

    [Header("Paint Level")]
    [SerializeField] private int paintLevel;
    [SerializeField] private int maxPaintLevel = 4;
    [SerializeField] private float paintLevelRadiusBonus = 0.25f;

    private bool nazoriReleaseRequired;

    public bool IsActing { get; private set; }
    public bool IsNazori { get; private set; }
    public ActionKind CurrentAction { get; private set; }

    public int PaintLevel => paintLevel;

    public float CurrentMoveSpeedRate
    {
        get
        {
            if (!IsActing || currentAction == null) return 1.0f;
            return currentAction.MoveSpeedRate;
        }
    }

    public void Initialize(PlayerController owner)
    {
        controller = owner;

        actNazori = GetOrAddAction<Act_Nazori>();
        actHarai = GetOrAddAction<Act_Harai>();
        actHane = GetOrAddAction<Act_Hane>();
        actDerivedHarai = GetOrAddAction<Act_DerivedHarai>();
        actDerivedHane = GetOrAddAction<Act_DerivedHane>();
        actTome = GetOrAddAction<Act_Tome>();

        actNazori.Initialize(controller, this);
        actHarai.Initialize(controller, this);
        actHane.Initialize(controller, this);
        actDerivedHarai.Initialize(controller, this);
        actDerivedHane.Initialize(controller, this);
        actTome.Initialize(controller, this);

        CurrentAction = ActionKind.None;
    }

    public void Tick()
    {
        PlayerInput input = controller.InputHandler;

        if (nazoriReleaseRequired && !input.NazoriHeld)
        {
            nazoriReleaseRequired = false;
        }

        if (currentAction != null && currentAction.IsRunning)
        {
            if (CurrentAction == ActionKind.Nazori)
            {
                if (input.HaraiPressed)
                {
                    StartAction(actDerivedHarai);
                    return;
                }

                if (input.HanePressed)
                {
                    StartAction(actDerivedHane);
                    return;
                }
            }

            currentAction.Tick(Time.deltaTime);

            if (!currentAction.IsRunning)
            {
                FinishAction();
            }

            RefreshFlags();
            return;
        }

        if (!controller.Move.IsGrounded && input.TomePressed)
        {
            TryStartAction(actTome);
            return;
        }

        if (input.HaraiPressed)
        {
            TryStartAction(actHarai);
            return;
        }

        if (input.HanePressed)
        {
            TryStartAction(actHane);
            return;
        }

        if (input.NazoriHeld && !nazoriReleaseRequired)
        {
            TryStartAction(actNazori);
            return;
        }

        RefreshFlags();
    }

    public void AddPaintLevel()
    {
        paintLevel = Mathf.Clamp(paintLevel + 1, 0, maxPaintLevel);
    }

    public void ResetPaintLevel()
    {
        paintLevel = 0;
    }

    public float GetPaintRadius(float baseRadius)
    {
        float rate = 1.0f + paintLevelRadiusBonus * paintLevel;
        return baseRadius * rate;
    }

    private void TryStartAction(PlayerActionBase action)
    {
        if (action == null) return;
        if (!action.CanStart()) return;

        StartAction(action);
    }

    private void StartAction(PlayerActionBase action)
    {
        if (currentAction != null && currentAction.IsRunning)
        {
            currentAction.EndAction();
        }

        currentAction = action;
        CurrentAction = action.Kind;

        // ★ここ変更：フラグ見る
        if (action.FaceCameraOnStart && controller.Move != null)
        {
            controller.Move.FaceCameraDirectionInstant();
        }

        action.StartAction();

        RefreshFlags();
    }

    private void FinishAction()
    {
        ActionKind finishedKind = CurrentAction;

        if (currentAction != null)
        {
            currentAction.EndAction();
        }

        currentAction = null;
        CurrentAction = ActionKind.None;

        IsActing = false;
        IsNazori = false;

        if (requireNazoriReleaseAfterAction &&
            finishedKind != ActionKind.None &&
            finishedKind != ActionKind.Nazori)
        {
            nazoriReleaseRequired = controller.InputHandler.NazoriHeld;
        }

        PlayAnimation(idleAnimationName);
    }

    private void RefreshFlags()
    {
        IsActing = currentAction != null && currentAction.IsRunning;
        IsNazori = IsActing && CurrentAction == ActionKind.Nazori;

        if (!IsActing && CurrentAction != ActionKind.None)
        {
            FinishAction();
        }
    }

    public void PlayAnimation(string animationName)
    {
        if (string.IsNullOrEmpty(animationName)) return;
        if (controller.AnimatorDriver == null) return;

        controller.AnimatorDriver.PlayActionAnimation(animationName);
    }

    private T GetOrAddAction<T>() where T : MonoBehaviour
    {
        T component = GetComponent<T>();

        if (component == null)
        {
            component = gameObject.AddComponent<T>();
        }

        return component;
    }
}