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

    // B button / LShift
    public bool NazoriPressed { get; private set; }
    public bool NazoriHeld { get; private set; }
    public bool NazoriReleased { get; private set; }

    // LT / Left Click
    public bool HaraiPressed { get; private set; }
    public bool HaraiHeld { get; private set; }
    public bool HaraiReleased { get; private set; }

    // RT / Right Click
    public bool HanePressed { get; private set; }
    public bool HaneHeld { get; private set; }
    public bool HaneReleased { get; private set; }

    public bool TomePressed => NazoriPressed;

    [Header("Gamepad")]
    [SerializeField] private float triggerPressThreshold = 0.5f;
    [SerializeField] private float rightStickLookScale = 15.0f;

    private bool prevLT;
    private bool prevRT;

    public void Initialize(PlayerController owner)
    {
        controller = owner;
    }

    public void Tick()
    {
        ResetInputs();

        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        Gamepad pad = Gamepad.current;

        ReadMove(keyboard, pad);
        ReadLook(mouse, pad);
        ReadJump(keyboard, pad);
        ReadNazori(keyboard, pad);
        ReadHarai(mouse, pad);
        ReadHane(mouse, pad);
    }

    private void ResetInputs()
    {
        MoveInput = Vector2.zero;
        LookInput = Vector2.zero;

        JumpPressed = false;
        JumpHeld = false;
        JumpReleased = false;

        NazoriPressed = false;
        NazoriHeld = false;
        NazoriReleased = false;

        HaraiPressed = false;
        HaraiHeld = false;
        HaraiReleased = false;

        HanePressed = false;
        HaneHeld = false;
        HaneReleased = false;
    }

    private void ReadMove(Keyboard keyboard, Gamepad pad)
    {
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
    }

    private void ReadLook(Mouse mouse, Gamepad pad)
    {
        Vector2 look = Vector2.zero;

        if (mouse != null)
        {
            look += mouse.delta.ReadValue();
        }

        if (pad != null)
        {
            Vector2 stickLook = pad.rightStick.ReadValue();
            if (stickLook.sqrMagnitude > 0.0001f)
            {
                look += stickLook * rightStickLookScale;
            }
        }

        LookInput = look;
    }

    private void ReadJump(Keyboard keyboard, Gamepad pad)
    {
        if (keyboard != null)
        {
            JumpPressed |= keyboard.spaceKey.wasPressedThisFrame;
            JumpHeld |= keyboard.spaceKey.isPressed;
            JumpReleased |= keyboard.spaceKey.wasReleasedThisFrame;
        }

        if (pad != null)
        {
            JumpPressed |= pad.buttonSouth.wasPressedThisFrame; // A
            JumpHeld |= pad.buttonSouth.isPressed;
            JumpReleased |= pad.buttonSouth.wasReleasedThisFrame;
        }
    }

    private void ReadNazori(Keyboard keyboard, Gamepad pad)
    {
        if (keyboard != null)
        {
            NazoriPressed |= keyboard.leftShiftKey.wasPressedThisFrame;
            NazoriHeld |= keyboard.leftShiftKey.isPressed;
            NazoriReleased |= keyboard.leftShiftKey.wasReleasedThisFrame;
        }

        if (pad != null)
        {
            NazoriPressed |= pad.buttonEast.wasPressedThisFrame; // B
            NazoriHeld |= pad.buttonEast.isPressed;
            NazoriReleased |= pad.buttonEast.wasReleasedThisFrame;
        }
    }

    private void ReadHarai(Mouse mouse, Gamepad pad)
    {
        bool held = false;

        if (mouse != null)
        {
            HaraiPressed |= mouse.leftButton.wasPressedThisFrame;
            held |= mouse.leftButton.isPressed;
            HaraiReleased |= mouse.leftButton.wasReleasedThisFrame;
        }

        if (pad != null)
        {
            bool nowLT = pad.leftTrigger.ReadValue() >= triggerPressThreshold;

            HaraiPressed |= nowLT && !prevLT;
            HaraiReleased |= !nowLT && prevLT;
            held |= nowLT;

            prevLT = nowLT;
        }

        HaraiHeld = held;
    }

    private void ReadHane(Mouse mouse, Gamepad pad)
    {
        bool held = false;

        if (mouse != null)
        {
            HanePressed |= mouse.rightButton.wasPressedThisFrame;
            held |= mouse.rightButton.isPressed;
            HaneReleased |= mouse.rightButton.wasReleasedThisFrame;
        }

        if (pad != null)
        {
            bool nowRT = pad.rightTrigger.ReadValue() >= triggerPressThreshold;

            HanePressed |= nowRT && !prevRT;
            HaneReleased |= !nowRT && prevRT;
            held |= nowRT;

            prevRT = nowRT;
        }

        HaneHeld = held;
    }
}
