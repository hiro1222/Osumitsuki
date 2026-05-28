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

    [Header("HP")]
    [SerializeField] private int maxHP = 3;
    [SerializeField] private int currentHP = 3;

    [Header("Stock")]
    [SerializeField] private int stock = 3;
    [SerializeField] private int maxStock = 99;

    [Header("Ink")]
    [SerializeField] private float maxInk = 100.0f;
    [SerializeField] private float currentInk = 100.0f;

    [Header("Ink Recovery")]
    [SerializeField] private float firstRecoverDelay = 5.0f;
    [SerializeField] private float repeatRecoverInterval = 2.0f;
    [SerializeField] private float recoverAmount = 25.0f;

    [Header("Ink Cost")]
    public float nazoriInkCostPerDistance = 0.1f;
    public float nazoriInkCostDistance = 0.1f;
    public float haraiInkCost = 1.0f;
    public float haneInkCost = 1.0f;
    public float derivedHaraiInkCost = 1.0f;
    public float derivedHaneInkCost = 1.0f;
    public float tomeInkCost = 1.0f;

    [Header("ƒŠƒXƒ|[ƒ“")]
    [SerializeField] private float respawnY = -10f;

    private Vector3 spawnPosition;

    private float noInputTimer;
    private float repeatRecoverTimer;
    private bool firstRecovered;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;

    public int Stock => stock;
    public int MaxStock => maxStock;

    public float MaxInk => maxInk;
    public float CurrentInk => currentInk;

    public float HpRate
    {
        get
        {
            if (maxHP <= 0) return 0.0f;
            return (float)currentHP / maxHP;
        }
    }

    public float InkRate
    {
        get
        {
            if (maxInk <= 0.0f) return 0.0f;
            return currentInk / maxInk;
        }
    }

    public void Initialize(PlayerController owner)
    {
        controller = owner;

        maxHP = 3;
        currentHP = Mathf.Clamp(currentHP, 0, maxHP);

        maxStock = 99;
        stock = Mathf.Clamp(stock, 0, maxStock);

        maxInk = 100.0f;
        currentInk = Mathf.Clamp(currentInk, 0.0f, maxInk);

        spawnPosition = transform.position;
    }

    private void Update()
    {
        if (transform.position.y < respawnY)
        {
            Respawn();
        }

        UpdateInkRecoveryByNoInput();
    }

    private void UpdateInkRecoveryByNoInput()
    {
        if (controller == null) return;
        if (controller.InputHandler == null) return;

        bool hasInput =
            controller.InputHandler.MoveInput.sqrMagnitude > 0.01f ||
            controller.InputHandler.JumpPressed ||
            controller.InputHandler.NazoriHeld ||
            controller.InputHandler.HaraiHeld ||
            controller.InputHandler.HaneHeld;

        if (hasInput)
        {
            ResetRecoveryState();
            return;
        }

        if (currentInk >= maxInk)
        {
            ResetRecoveryState();
            return;
        }

        noInputTimer += Time.deltaTime;

        if (!firstRecovered)
        {
            if (noInputTimer >= firstRecoverDelay)
            {
                RecoverInk(recoverAmount);
                firstRecovered = true;
                repeatRecoverTimer = 0.0f;
            }

            return;
        }

        repeatRecoverTimer += Time.deltaTime;

        if (repeatRecoverTimer >= repeatRecoverInterval)
        {
            RecoverInk(recoverAmount);
            repeatRecoverTimer = 0.0f;
        }
    }

    private void ResetRecoveryState()
    {
        noInputTimer = 0.0f;
        repeatRecoverTimer = 0.0f;
        firstRecovered = false;
    }

    public bool HasInk(float cost)
    {
        return currentInk >= cost;
    }

    public bool ConsumeInk(float cost)
    {
        if (cost <= 0.0f) return true;
        if (currentInk < cost) return false;

        currentInk = Mathf.Clamp(currentInk - cost, 0.0f, maxInk);
        ResetRecoveryState();

        return true;
    }

    public void RecoverInk(float amount)
    {
        if (amount <= 0.0f) return;
        currentInk = Mathf.Clamp(currentInk + amount, 0.0f, maxInk);
    }

    public void Damage(int amount)
    {
        if (amount <= 0) return;

        currentHP = Mathf.Clamp(currentHP - amount, 0, maxHP);

        if (currentHP <= 0)
        {
            OnDead();
        }
    }

    public void Heal(int amount)
    {
        if (amount <= 0) return;
        currentHP = Mathf.Clamp(currentHP + amount, 0, maxHP);
    }

    public void AddStock(int amount)
    {
        if (amount <= 0) return;
        stock = Mathf.Clamp(stock + amount, 0, maxStock);
    }

    public void SubStock(int amount)
    {
        if (amount <= 0) return;
        stock = Mathf.Clamp(stock - amount, 0, maxStock);
    }

    private void OnDead()
    {
        if (stock > 0)
        {
            stock--;
            Respawn();
            currentHP = maxHP;
        }
        else
        {
            currentHP = 0;
            Debug.Log("Game Over");
        }
    }

    private void Respawn()
    {
        controller.enabled = false;
        transform.position = spawnPosition;
        controller.enabled = true;

        currentHP = maxHP;
        currentInk = maxInk;

        ResetRecoveryState();
    }

    public void SetspawnPosition(Vector3 position)
    {
        spawnPosition = position;
    }
}