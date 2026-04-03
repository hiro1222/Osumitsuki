using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInput : MonoBehaviour
{
    private PlayerController controller;

    public Vector2 MoveInput { get; private set; }
    public Vector2 LookInput { get; private set; }

    public bool JumpPressed { get; private set; }
    public bool JumpHeld { get; private set; }
    public bool JumpReleased { get; private set; }

    public bool ActionPressed { get; private set; }
    public bool ActionHeld { get; private set; }
    public bool ActionReleased { get; private set; }

    public bool SprintHeld { get; private set; }

    public void Initialize(PlayerController owner)
    {
        controller = owner;
    }

    public void Tick()
    {
        JumpPressed = false;
        JumpReleased = false;
        ActionPressed = false;
        ActionReleased = false;

        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;
        JumpHeld = false;
        ActionHeld = false;
        SprintHeld = false;

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;

        // =========================
        // Move
        // =========================
        Vector2 move = Vector2.zero;

        if (keyboard != null)
        {
            if (keyboard.wKey.isPressed) move.y += 1.0f;
            if (keyboard.sKey.isPressed) move.y -= 1.0f;
            if (keyboard.dKey.isPressed) move.x += 1.0f;
            if (keyboard.aKey.isPressed) move.x -= 1.0f;
        }

        if (pad != null)
        {
            Vector2 stick = pad.leftStick.ReadValue();

            if (stick.sqrMagnitude > move.sqrMagnitude)
            {
                move = stick;
            }
        }

        MoveInput = move.sqrMagnitude > 1.0f ? move.normalized : move;

        // =========================
        // Look
        // =========================
        Vector2 look = Vector2.zero;

        if (mouse != null)
        {
            look += mouse.delta.ReadValue();
        }

        if (pad != null)
        {
            // 右スティックも視点に使う場合
            Vector2 stickLook = pad.rightStick.ReadValue();

            // マウスdeltaは大きい値、スティックは小さい値なので少し補正
            if (stickLook.sqrMagnitude > 0.0001f)
            {
                look += stickLook * 15.0f;
            }
        }

        LookInput = look;

        // =========================
        // Jump
        // =========================
        if (keyboard != null)
        {
            JumpPressed |= keyboard.spaceKey.wasPressedThisFrame;
            JumpHeld |= keyboard.spaceKey.isPressed;
            JumpReleased |= keyboard.spaceKey.wasReleasedThisFrame;
        }

        if (pad != null)
        {
            JumpPressed |= pad.buttonSouth.wasPressedThisFrame;   // Aボタン / Cross
            JumpHeld |= pad.buttonSouth.isPressed;
            JumpReleased |= pad.buttonSouth.wasReleasedThisFrame;
        }

        // =========================
        // Action
        // =========================
        if (keyboard != null)
        {
            ActionPressed |= keyboard.eKey.wasPressedThisFrame;
            ActionHeld |= keyboard.eKey.isPressed;
            ActionReleased |= keyboard.eKey.wasReleasedThisFrame;
        }

        if (pad != null)
        {
            ActionPressed |= pad.buttonWest.wasPressedThisFrame;  // Xボタン / Square
            ActionHeld |= pad.buttonWest.isPressed;
            ActionReleased |= pad.buttonWest.wasReleasedThisFrame;
        }

        // =========================
        // Sprint
        // =========================
        if (keyboard != null)
        {
            SprintHeld |= keyboard.leftShiftKey.isPressed;
        }

        if (pad != null)
        {
            SprintHeld |= pad.leftStickButton.isPressed;
        }
    }
}