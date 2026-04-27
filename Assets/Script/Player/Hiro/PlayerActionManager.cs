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
    [SerializeField] private string idleAnimationName = "Idle";

    [Header("Nazori Reinput Rule")]
    [SerializeField] private bool requireNazoriReleaseAfterAction = true;

    private bool nazoriReleaseRequired;

    public bool IsActing { get; private set; }
    public bool IsNazori { get; private set; }
    public ActionKind CurrentAction { get; private set; }

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
            // なぞり中だけ派生を許可
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

        // 空中B/LShiftは止め。通常なぞりより優先。
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

        // なぞりは「押しっぱなし再発動」を禁止。
        // 派生/他アクション後は一度Shift/Bを離してから再入力が必要。
        if (input.NazoriHeld && !nazoriReleaseRequired)
        {
            TryStartAction(actNazori);
            return;
        }

        RefreshFlags();
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

        // 派生/単発アクション後、Shift/B押しっぱなしでなぞりへ戻らないようにする
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
