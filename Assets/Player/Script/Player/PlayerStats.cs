using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    private PlayerController controller;

    [Header("Move")]
    public float walkSpeed = 5.0f;
    public float rotationSensitivity = 2.0f;
    public float lookSensitivity = 2.0f;

    [Header("Jump / Gravity")]
    public float jumpPower = 8.0f;
    public float gravity = -20.0f;
    public float groundedY = -2.0f;

    [Header("Camera")]
    public float minPitch = -70.0f;
    public float maxPitch = 70.0f;

    [Header("Status")]
    public int maxHP = 100;
    public int currentHP = 100;

    public void Initialize(PlayerController owner)
    {
        controller = owner;
        currentHP = maxHP;
    }
}
