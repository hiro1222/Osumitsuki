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

    [Header("Damage / Invincible")]
    [SerializeField] private float invincibleTime = 1.0f;
    [SerializeField] private bool debugDamageLog = true;

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

    [Header("Respawn")]
    [SerializeField] private float respawnY = -10f;
    [SerializeField] private float fallRespawnHeightOffset = 1.0f;
    [SerializeField] private float safePositionUpdateInterval = 0.1f;

    private Vector3 spawnPosition;
    private Vector3 lastSafeGroundedPosition;

    private float safePositionTimer;

    private float noInputTimer;
    private float repeatRecoverTimer;
    private bool firstRecovered;

    private float invincibleTimer;

    private bool baseStatsCached;

    private float baseWalkSpeed;
    private float baseRotationSensitivity;
    private float baseLookSensitivity;
    private float baseJumpPower;
    private float baseGravity;
    private int baseMaxHP;
    private float baseMaxInk;
    private float baseRecoverAmount;

    private float baseNazoriInkCostPerDistance;
    private float baseNazoriInkCostDistance;
    private float baseHaraiInkCost;
    private float baseHaneInkCost;
    private float baseDerivedHaraiInkCost;
    private float baseDerivedHaneInkCost;
    private float baseTomeInkCost;

    private float powerBoostMultiplier = 1.0f;

    public int MaxHP => maxHP;
    public int CurrentHP => currentHP;

    public int Stock => stock;
    public int MaxStock => maxStock;

    public float MaxInk => maxInk;
    public float CurrentInk => currentInk;

    public bool IsInvincible => invincibleTimer > 0.0f;
    public float PowerBoostMultiplier => powerBoostMultiplier;

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

    private void Awake()
    {
        CacheBaseStatsIfNeeded();
    }

    public void Initialize(PlayerController owner)
    {
        controller = owner;

        CacheBaseStatsIfNeeded();
        ApplyPowerBoostValues();

        currentHP = Mathf.Clamp(currentHP, 0, maxHP);
        stock = Mathf.Clamp(stock, 0, maxStock);
        currentInk = Mathf.Clamp(currentInk, 0.0f, maxInk);

        spawnPosition = transform.position;
        lastSafeGroundedPosition = transform.position;

        invincibleTimer = 0.0f;
        safePositionTimer = 0.0f;
    }

    private void CacheBaseStatsIfNeeded()
    {
        if (baseStatsCached) return;

        baseWalkSpeed = walkSpeed;
        baseRotationSensitivity = rotationSensitivity;
        baseLookSensitivity = lookSensitivity;
        baseJumpPower = jumpPower;
        baseGravity = gravity;
        baseMaxHP = maxHP;
        baseMaxInk = maxInk;
        baseRecoverAmount = recoverAmount;

        baseNazoriInkCostPerDistance = nazoriInkCostPerDistance;
        baseNazoriInkCostDistance = nazoriInkCostDistance;
        baseHaraiInkCost = haraiInkCost;
        baseHaneInkCost = haneInkCost;
        baseDerivedHaraiInkCost = derivedHaraiInkCost;
        baseDerivedHaneInkCost = derivedHaneInkCost;
        baseTomeInkCost = tomeInkCost;

        baseStatsCached = true;
    }

    public void SetPowerBoostMultiplier(float multiplier)
    {
        CacheBaseStatsIfNeeded();

        if (multiplier < 1.0f)
        {
            multiplier = 1.0f;
        }

        powerBoostMultiplier = multiplier;

        ApplyPowerBoostValues();

        if (powerBoostMultiplier > 1.0f)
        {
            currentHP = maxHP;
            currentInk = maxInk;
        }
        else
        {
            currentHP = Mathf.Clamp(currentHP, 0, maxHP);
            currentInk = Mathf.Clamp(currentInk, 0.0f, maxInk);
        }

        Debug.Log(
            "[PlayerStats] Boost x" +
            powerBoostMultiplier +
            " / Speed:" +
            walkSpeed +
            " / Jump:" +
            jumpPower +
            " / MaxHP:" +
            maxHP +
            " / MaxInk:" +
            maxInk
        );
    }

    private void ApplyPowerBoostValues()
    {
        walkSpeed = baseWalkSpeed * powerBoostMultiplier;
        rotationSensitivity = baseRotationSensitivity * powerBoostMultiplier;
        lookSensitivity = baseLookSensitivity * powerBoostMultiplier;
        jumpPower = baseJumpPower * powerBoostMultiplier;

        gravity = baseGravity * powerBoostMultiplier;

        maxHP = Mathf.RoundToInt(baseMaxHP * powerBoostMultiplier);
        maxInk = baseMaxInk * powerBoostMultiplier;
        recoverAmount = baseRecoverAmount * powerBoostMultiplier;

        nazoriInkCostPerDistance = baseNazoriInkCostPerDistance / powerBoostMultiplier;
        nazoriInkCostDistance = baseNazoriInkCostDistance;
        haraiInkCost = baseHaraiInkCost / powerBoostMultiplier;
        haneInkCost = baseHaneInkCost / powerBoostMultiplier;
        derivedHaraiInkCost = baseDerivedHaraiInkCost / powerBoostMultiplier;
        derivedHaneInkCost = baseDerivedHaneInkCost / powerBoostMultiplier;
        tomeInkCost = baseTomeInkCost / powerBoostMultiplier;
    }

    private void Update()
    {
        UpdateInvincibleTimer();
        UpdateLastSafeGroundedPosition();

        if (transform.position.y < respawnY)
        {
            FallRespawn();
            return;
        }

        UpdateInkRecoveryByNoInput();
    }

    private void UpdateInvincibleTimer()
    {
        if (invincibleTimer <= 0.0f) return;

        invincibleTimer -= Time.deltaTime;

        if (invincibleTimer < 0.0f)
        {
            invincibleTimer = 0.0f;
        }
    }

    private void UpdateLastSafeGroundedPosition()
    {
        if (controller == null) return;
        if (controller.Move == null) return;
        if (!controller.Move.IsGrounded) return;

        safePositionTimer += Time.deltaTime;

        if (safePositionTimer < safePositionUpdateInterval) return;

        safePositionTimer = 0.0f;
        lastSafeGroundedPosition = transform.position;
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
        if (invincibleTimer > 0.0f) return;

        currentHP = Mathf.Clamp(currentHP - amount, 0, maxHP);
        invincibleTimer = invincibleTime;

        if (debugDamageLog)
        {
            Debug.Log("[PlayerStats] Damage : " + amount + " / HP : " + currentHP);
        }

        if (currentHP <= 0)
        {
            DeathRespawn();
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

    private void DeathRespawn()
    {
        MoveToRespawnPosition(spawnPosition);

        currentHP = maxHP;
        currentInk = maxInk;
        invincibleTimer = invincibleTime;

        ResetRecoveryState();

        if (debugDamageLog)
        {
            Debug.Log("[PlayerStats] Death Respawn");
        }
    }

    private void FallRespawn()
    {
        SubStock(1);

        Vector3 respawnPos = lastSafeGroundedPosition + Vector3.up * fallRespawnHeightOffset;
        MoveToRespawnPosition(respawnPos);

        currentHP = maxHP;
        currentInk = maxInk;
        invincibleTimer = invincibleTime;

        ResetRecoveryState();

        if (debugDamageLog)
        {
            Debug.Log("[PlayerStats] Fall Respawn / Stock : " + stock);
        }
    }

    private void MoveToRespawnPosition(Vector3 position)
    {
        CharacterController cc = GetComponent<CharacterController>();

        if (cc != null)
        {
            cc.enabled = false;
        }

        transform.position = position;

        if (cc != null)
        {
            cc.enabled = true;
        }
    }

    public void SetspawnPosition(Vector3 position)
    {
        spawnPosition = position;
    }

    public void SetSpawnPosition(Vector3 position)
    {
        spawnPosition = position;
    }
}