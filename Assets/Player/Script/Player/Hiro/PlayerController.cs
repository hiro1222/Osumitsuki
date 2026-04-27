using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerActionManager))]
[RequireComponent(typeof(PlayerStats))]
[RequireComponent(typeof(PlayerStateMachine))]
public class PlayerController : MonoBehaviour
{
    private CharacterController characterController;
    private PlayerInput playerInput;
    private PlayerMove playerMove;
    private PlayerActionManager actionManager;
    private PlayerStats playerStats;
    private PlayerStateMachine stateMachine;
    private PlayerAnimatorDriver animatorDriver;

    public CharacterController CharacterController => characterController;
    public PlayerInput InputHandler => playerInput;
    public PlayerMove Move => playerMove;
    public PlayerActionManager ActionManager => actionManager;
    public PlayerStats Stats => playerStats;
    public PlayerStateMachine StateMachine => stateMachine;
    public PlayerAnimatorDriver AnimatorDriver => animatorDriver;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerMove = GetComponent<PlayerMove>();
        actionManager = GetComponent<PlayerActionManager>();
        playerStats = GetComponent<PlayerStats>();
        stateMachine = GetComponent<PlayerStateMachine>();
        animatorDriver = GetComponent<PlayerAnimatorDriver>();

        playerInput.Initialize(this);
        playerStats.Initialize(this);
        playerMove.Initialize(this);
        actionManager.Initialize(this);
        stateMachine.Initialize(this);

        if (animatorDriver != null)
        {
            animatorDriver.Initialize(this);
        }
    }

    private void Update()
    {
        playerInput.Tick();

        // アクションを先に更新して、その結果を移動/ジャンプ制御に反映する
        actionManager.Tick();
        playerMove.Tick();
        stateMachine.Tick();

        if (animatorDriver != null)
        {
            animatorDriver.Tick();
        }
    }
}
