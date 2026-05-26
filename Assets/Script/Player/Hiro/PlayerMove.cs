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

    [Header("Jump Delay")]
    [SerializeField] private bool enableJumpDelay = true;
    [SerializeField] private float jumpDelay = 0.15f;
    [SerializeField] private string jumpAnimationName = "jump";

    [Header("Auto Climb")]
    [SerializeField] private bool enableAutoClimb = true;
    [SerializeField] private float maxClimbHeight = 0.8f;
    [SerializeField] private float climbForwardDistance = 0.45f;
    [SerializeField] private float climbDuration = 0.18f;
    [SerializeField] private LayerMask climbMask = ~0;

    [Header("Ground Grace By Ray")]
    [SerializeField] private bool enableGroundGrace = true;
    [SerializeField] private float groundRayDistance = 0.35f;
    [SerializeField] private LayerMask groundRayMask = ~0;

    private Vector3 velocity;
    private bool jumpRequestedThisFrame;

    private bool waitingJump;
    private float jumpTimer;

    private bool externalPositionLock;
    private bool externalGravityEnabled = true;
    private bool lockHeightOnly;
    private Vector3 lockedPosition;

    private bool isClimbing;
    private float climbTimer;
    private Vector3 climbStartPos;
    private Vector3 climbTargetPos;

    public bool IsGrounded { get; private set; }
    public bool IsRealGrounded { get; private set; }

    public bool IsJumpDelayWaiting => waitingJump;
    public bool HasJumpDelayStarted { get; private set; }
    public string JumpAnimationName => jumpAnimationName;

    public float VerticalVelocity => velocity.y;

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        characterController = owner.CharacterController;
        tf = transform;
    }

    public void Tick()
    {
        if (isClimbing)
        {
            TickClimbInputRotation();
            TickClimb();
            return;
        }

        UpdateMove();
    }

    public void FaceCameraDirectionInstant()
    {
        if (cameraTransform == null) return;

        Vector3 camForward = cameraTransform.forward;
        camForward.y = 0.0f;

        if (camForward.sqrMagnitude <= 0.0001f) return;

        camForward.Normalize();
        tf.rotation = Quaternion.LookRotation(camForward, Vector3.up);
    }

    public void SetExternalPositionLock(bool enabled, Vector3 position, bool heightOnly)
    {
        externalPositionLock = enabled;
        lockedPosition = position;
        lockHeightOnly = heightOnly;
    }

    public void SetExternalGravityEnabled(bool enabled)
    {
        externalGravityEnabled = enabled;
    }

    public void ClearVerticalVelocity()
    {
        velocity.y = 0.0f;
    }

    private void UpdateMove()
    {
        PlayerStats stats = controller.Stats;

        jumpRequestedThisFrame = false;

        IsRealGrounded = characterController.isGrounded;
        IsGrounded = IsRealGrounded || CheckGroundGrace();

        if (IsRealGrounded && velocity.y < 0.0f)
        {
            velocity.y = stats.groundedY;

            if (!waitingJump)
            {
                HasJumpDelayStarted = false;
            }
        }

        Vector2 moveInput = controller.InputHandler.MoveInput;
        Vector3 move = BuildCameraRelativeMove(moveInput);

        if (move.sqrMagnitude > 1.0f)
        {
            move.Normalize();
        }

        float speed = stats.walkSpeed * controller.ActionManager.CurrentMoveSpeedRate;

        if (move.sqrMagnitude > 0.0001f &&
            controller.ActionManager.CurrentMoveSpeedRate > 0.0f &&
            (!controller.ActionManager.IsActing ||
              controller.ActionManager.IsNazori))
        {
            Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
            tf.rotation = Quaternion.Slerp(tf.rotation, targetRot, rotateSpeed * Time.deltaTime);
        }

        if (controller.InputHandler.JumpPressed &&
            IsRealGrounded &&
            !controller.ActionManager.IsActing &&
            !waitingJump)
        {
            StartJumpRequest();
        }

        if (waitingJump)
        {
            TickJumpDelay();
        }

        if (enableAutoClimb &&
            !jumpRequestedThisFrame &&
            !waitingJump &&
            IsRealGrounded &&
            move.sqrMagnitude > 0.0001f &&
            TryStartAutoClimb(move.normalized))
        {
            return;
        }

        if (externalGravityEnabled)
        {
            velocity.y += stats.gravity * Time.deltaTime;
        }
        else
        {
            velocity.y = 0.0f;
        }

        Vector3 finalMove = move * speed;
        finalMove.y = velocity.y;

        characterController.Move(finalMove * Time.deltaTime);

        if (externalPositionLock)
        {
            Vector3 pos = transform.position;

            if (lockHeightOnly)
            {
                pos.y = lockedPosition.y;
            }
            else
            {
                pos = lockedPosition;
            }

            transform.position = pos;
        }

        if (!jumpRequestedThisFrame)
        {
            IsRealGrounded = characterController.isGrounded;
            IsGrounded = IsRealGrounded || CheckGroundGrace();
        }
    }

    private void StartJumpRequest()
    {
        HasJumpDelayStarted = true;

        if (enableJumpDelay)
        {
            waitingJump = true;
            jumpTimer = jumpDelay;
            velocity.y = controller.Stats.groundedY;
            return;
        }

        DoJump();
    }

    private void TickJumpDelay()
    {
        jumpTimer -= Time.deltaTime;

        if (!IsRealGrounded)
        {
            waitingJump = false;
            return;
        }

        if (jumpTimer <= 0.0f)
        {
            waitingJump = false;
            DoJump();
        }
    }

    private void DoJump()
    {
        PlayerStats stats = controller.Stats;

        velocity.y = stats.jumpPower;
        jumpRequestedThisFrame = true;

        IsGrounded = false;
        IsRealGrounded = false;
    }

    private bool TryStartAutoClimb(Vector3 moveDirection)
    {
        Vector3 feetPos = GetFeetPosition();

        Vector3 wallRayOrigin = feetPos + Vector3.up * 0.15f;

        bool wallHit = Physics.Raycast(
            wallRayOrigin,
            moveDirection,
            out RaycastHit wallHitInfo,
            characterController.radius + climbForwardDistance,
            climbMask,
            QueryTriggerInteraction.Ignore);

        if (!wallHit) return false;

        Vector3 topCheckOrigin =
            feetPos +
            moveDirection * (characterController.radius + climbForwardDistance) +
            Vector3.up * (maxClimbHeight + 0.2f);

        bool groundHit = Physics.Raycast(
            topCheckOrigin,
            Vector3.down,
            out RaycastHit topHitInfo,
            maxClimbHeight + 0.4f,
            climbMask,
            QueryTriggerInteraction.Ignore);

        if (!groundHit) return false;

        float climbHeight = topHitInfo.point.y - feetPos.y;

        if (climbHeight <= 0.05f) return false;
        if (climbHeight > maxClimbHeight) return false;

        Vector3 targetFeetPos =
            topHitInfo.point +
            moveDirection * 0.1f;

        Vector3 currentFeetPos = GetFeetPosition();
        Vector3 offset = tf.position - currentFeetPos;

        climbStartPos = tf.position;
        climbTargetPos = targetFeetPos + offset;

        if (!HasSpaceAtPosition(climbTargetPos)) return false;

        isClimbing = true;
        climbTimer = 0.0f;
        velocity.y = 0.0f;
        waitingJump = false;
        HasJumpDelayStarted = false;

        characterController.enabled = false;

        return true;
    }

    private void TickClimb()
    {
        climbTimer += Time.deltaTime;

        float t = climbTimer / Mathf.Max(0.01f, climbDuration);
        t = Mathf.Clamp01(t);

        float smoothT = t * t * (3.0f - 2.0f * t);

        tf.position = Vector3.Lerp(climbStartPos, climbTargetPos, smoothT);

        IsGrounded = true;
        IsRealGrounded = true;

        if (t >= 1.0f)
        {
            tf.position = climbTargetPos;
            characterController.enabled = true;

            isClimbing = false;
            velocity.y = controller.Stats.groundedY;
        }
    }

    private bool CheckGroundGrace()
    {
        if (!enableGroundGrace) return false;
        if (jumpRequestedThisFrame) return false;
        if (waitingJump) return true;

        Vector3 origin = GetFeetPosition() + Vector3.up * 0.05f;

        return Physics.Raycast(
            origin,
            Vector3.down,
            groundRayDistance,
            groundRayMask,
            QueryTriggerInteraction.Ignore);
    }

    private bool HasSpaceAtPosition(Vector3 targetPosition)
    {
        Vector3 center = targetPosition + characterController.center;

        float radius = characterController.radius * 0.9f;
        float height = characterController.height;

        Vector3 bottom = center + Vector3.down * (height * 0.5f - radius);
        Vector3 top = center + Vector3.up * (height * 0.5f - radius);

        bool blocked = Physics.CheckCapsule(
            bottom,
            top,
            radius,
            climbMask,
            QueryTriggerInteraction.Ignore);

        return !blocked;
    }

    private Vector3 GetFeetPosition()
    {
        return tf.position +
               characterController.center -
               Vector3.up * (characterController.height * 0.5f);
    }

    private Vector3 BuildCameraRelativeMove(Vector2 moveInput)
    {
        Transform reference = cameraTransform != null ? cameraTransform : tf;

        Vector3 forward = reference.forward;
        Vector3 right = reference.right;

        forward.y = 0.0f;
        right.y = 0.0f;

        if (forward.sqrMagnitude > 0.0001f) forward.Normalize();
        if (right.sqrMagnitude > 0.0001f) right.Normalize();

        return forward * moveInput.y + right * moveInput.x;
    }

    private void TickClimbInputRotation()
    {
        Vector2 moveInput = controller.InputHandler.MoveInput;
        Vector3 move = BuildCameraRelativeMove(moveInput);

        if (move.sqrMagnitude <= 0.0001f) return;

        if (move.sqrMagnitude > 1.0f)
        {
            move.Normalize();
        }

        if (controller.ActionManager.CurrentMoveSpeedRate <= 0.0f) return;

        if (controller.ActionManager.IsActing &&
            !controller.ActionManager.IsNazori)
        {
            return;
        }

        Quaternion targetRot = Quaternion.LookRotation(move, Vector3.up);
        tf.rotation = Quaternion.Slerp(tf.rotation, targetRot, rotateSpeed * Time.deltaTime);
    }
}