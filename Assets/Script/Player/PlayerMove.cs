using UnityEngine;

public class PlayerMove : MonoBehaviour
{
    private PlayerController controller;
    private CharacterController characterController;
    private Transform tf;

    [Header("Move Settings")]
    [SerializeField] private float rotateSpeed = 12.0f;

    [Header("Camera Reference")]
    [SerializeField] private Transform cameraTransform;

    private Vector3 velocity;

    public bool IsGrounded { get; private set; }
    public float VerticalVelocity => velocity.y;

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        characterController = owner.CharacterController;
        tf = transform;
    }

    public void Tick()
    {
        UpdateMove();
    }

    private void UpdateMove()
    {
        PlayerStats stats = controller.Stats;

        IsGrounded = characterController.isGrounded;

        if (IsGrounded && velocity.y < 0.0f)
        {
            velocity.y = stats.groundedY;
        }

        Vector2 moveInput = controller.InputHandler.MoveInput;

        Vector3 move = Vector3.zero;

        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;

            camForward.y = 0.0f;
            camRight.y = 0.0f;

            if (camForward.sqrMagnitude > 0.0001f) camForward.Normalize();
            if (camRight.sqrMagnitude > 0.0001f) camRight.Normalize();

            move = camForward * moveInput.y + camRight * moveInput.x;
        }
        else
        {
            Vector3 forward = tf.forward;
            Vector3 right = tf.right;

            forward.y = 0.0f;
            right.y = 0.0f;

            if (forward.sqrMagnitude > 0.0001f) forward.Normalize();
            if (right.sqrMagnitude > 0.0001f) right.Normalize();

            move = forward * moveInput.y + right * moveInput.x;
        }

        if (move.sqrMagnitude > 1.0f)
        {
            move.Normalize();
        }

        float speed = controller.InputHandler.SprintHeld ? stats.sprintSpeed : stats.walkSpeed;

        if (controller.ActionManager.IsActing)
        {
            speed *= 0.2f;
        }

        if (move.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
            tf.rotation = Quaternion.Slerp(tf.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }

        if (controller.InputHandler.JumpPressed && IsGrounded && !controller.ActionManager.IsActing)
        {
            velocity.y = stats.jumpPower;
        }

        velocity.y += stats.gravity * Time.deltaTime;

        Vector3 finalMove = move * speed;
        finalMove.y = velocity.y;

        characterController.Move(finalMove * Time.deltaTime);
    }
}