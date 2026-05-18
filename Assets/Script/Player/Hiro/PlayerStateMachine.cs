using UnityEngine;

public class PlayerStateMachine : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Move,
        Jump,
        Fall,
        Action
    }

    private PlayerController controller;

    public PlayerState CurrentState { get; private set; }

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        CurrentState = PlayerState.Idle;
    }

    public void Tick()
    {
        if (controller.ActionManager.IsActing)
        {
            ChangeState(PlayerState.Action);
            return;
        }

        bool grounded = controller.Move.IsGrounded;
        bool moving = controller.InputHandler.MoveInput.sqrMagnitude > 0.01f;
        float verticalVelocity = controller.Move.VerticalVelocity;

        if (!grounded)
        {
            ChangeState(verticalVelocity > 0.1f ? PlayerState.Jump : PlayerState.Fall);
            return;
        }

        ChangeState(moving ? PlayerState.Move : PlayerState.Idle);
    }

    private void ChangeState(PlayerState nextState)
    {
        if (CurrentState == nextState) return;
        CurrentState = nextState;
    }
}
