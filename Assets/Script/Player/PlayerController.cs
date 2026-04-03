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

    public CharacterController CharacterController => characterController;
    public PlayerInput InputHandler => playerInput;
    public PlayerMove Move => playerMove;
    public PlayerActionManager ActionManager => actionManager;
    public PlayerStats Stats => playerStats;
    public PlayerStateMachine StateMachine => stateMachine;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
        playerInput = GetComponent<PlayerInput>();
        playerMove = GetComponent<PlayerMove>();
        actionManager = GetComponent<PlayerActionManager>();
        playerStats = GetComponent<PlayerStats>();
        stateMachine = GetComponent<PlayerStateMachine>();

        playerInput.Initialize(this);
        playerStats.Initialize(this);
        stateMachine.Initialize(this);
        actionManager.Initialize(this);
        playerMove.Initialize(this);
    }

    private void Update()
    {
        playerInput.Tick();
        stateMachine.Tick();
        actionManager.Tick();
        playerMove.Tick();
    }
}