using UnityEngine;

public class PlayerPowerBoost : MonoBehaviour
{
    [Header("Command")]
    [SerializeField]
    private KeyCode[] command =
    {
        KeyCode.UpArrow,
        KeyCode.UpArrow,
        KeyCode.DownArrow,
        KeyCode.DownArrow,
        KeyCode.LeftArrow,
        KeyCode.RightArrow,
        KeyCode.LeftArrow,
        KeyCode.RightArrow,
        KeyCode.B,
        KeyCode.A
    };

    [SerializeField] private float commandResetTime = 2.0f;

    [Header("Boost")]
    [SerializeField] private float statusMultiplier = 10.0f;
    [SerializeField] private bool toggleMode = true;
    [SerializeField] private float boostDuration = 10.0f;

    [Header("Debug")]
    [SerializeField] private bool debugLog = true;

    [Header("Gaming Color")]
    [SerializeField] private Renderer[] targetRenderers;
    [SerializeField] private float colorSpeed = 2.0f;
    [SerializeField] private float emissionIntensity = 3.0f;

    private PlayerStats stats;

    private int commandIndex;
    private float commandTimer;

    private bool boosted;
    private float boostTimer;

    private Material[] runtimeMaterials;
    private Color[] originalColors;
    private Color[] originalEmissionColors;

    public bool IsBoosted
    {
        get { return boosted; }
    }

    public float StatusMultiplier
    {
        get { return boosted ? statusMultiplier : 1.0f; }
    }

    private void Awake()
    {
        stats = GetComponent<PlayerStats>();
        CacheMaterials();
    }

    private void Start()
    {
        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }

        if (stats != null)
        {
            stats.SetPowerBoostMultiplier(1.0f);
        }
    }

    private void Update()
    {
        UpdateCommandInput();
        UpdateBoostTimer();
        UpdateGamingColor();
    }

    private void UpdateCommandInput()
    {
        if (command == null || command.Length == 0) return;

        if (commandIndex > 0)
        {
            commandTimer += Time.deltaTime;

            if (commandTimer >= commandResetTime)
            {
                ResetCommand();
            }
        }

        if (!Input.anyKeyDown) return;

        KeyCode expectedKey = command[commandIndex];

        if (Input.GetKeyDown(expectedKey))
        {
            commandIndex++;
            commandTimer = 0.0f;

            if (commandIndex >= command.Length)
            {
                ActivateCommand();
                ResetCommand();
            }

            return;
        }

        ResetCommand();
    }

    private void ActivateCommand()
    {
        if (toggleMode)
        {
            SetBoosted(!boosted);
        }
        else
        {
            boostTimer = boostDuration;
            SetBoosted(true);
        }
    }

    private void SetBoosted(bool value)
    {
        if (boosted == value) return;

        boosted = value;

        if (stats == null)
        {
            stats = GetComponent<PlayerStats>();
        }

        if (stats != null)
        {
            stats.SetPowerBoostMultiplier(StatusMultiplier);
        }

        if (!boosted)
        {
            RestoreColors();
        }

        if (debugLog)
        {
            Debug.Log("[PlayerPowerBoost] Boosted : " + boosted + " / Multiplier : " + StatusMultiplier);
        }
    }

    private void UpdateBoostTimer()
    {
        if (toggleMode) return;
        if (!boosted) return;

        boostTimer -= Time.deltaTime;

        if (boostTimer <= 0.0f)
        {
            SetBoosted(false);
        }
    }

    private void ResetCommand()
    {
        commandIndex = 0;
        commandTimer = 0.0f;
    }

    private void CacheMaterials()
    {
        if (targetRenderers == null || targetRenderers.Length == 0)
        {
            targetRenderers = GetComponentsInChildren<Renderer>();
        }

        runtimeMaterials = new Material[targetRenderers.Length];
        originalColors = new Color[targetRenderers.Length];
        originalEmissionColors = new Color[targetRenderers.Length];

        for (int i = 0; i < targetRenderers.Length; i++)
        {
            if (targetRenderers[i] == null) continue;

            runtimeMaterials[i] = targetRenderers[i].material;

            if (runtimeMaterials[i].HasProperty("_Color"))
            {
                originalColors[i] = runtimeMaterials[i].color;
            }

            if (runtimeMaterials[i].HasProperty("_EmissionColor"))
            {
                originalEmissionColors[i] = runtimeMaterials[i].GetColor("_EmissionColor");
                runtimeMaterials[i].EnableKeyword("_EMISSION");
            }
        }
    }

    private void UpdateGamingColor()
    {
        if (!boosted) return;
        if (runtimeMaterials == null) return;

        float hue = Mathf.Repeat(Time.time * colorSpeed, 1.0f);
        Color gamingColor = Color.HSVToRGB(hue, 1.0f, 1.0f);
        Color emissionColor = gamingColor * emissionIntensity;

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material mat = runtimeMaterials[i];
            if (mat == null) continue;

            if (mat.HasProperty("_Color"))
            {
                mat.color = gamingColor;
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", emissionColor);
            }
        }
    }

    private void RestoreColors()
    {
        if (runtimeMaterials == null) return;

        for (int i = 0; i < runtimeMaterials.Length; i++)
        {
            Material mat = runtimeMaterials[i];
            if (mat == null) continue;

            if (mat.HasProperty("_Color"))
            {
                mat.color = originalColors[i];
            }

            if (mat.HasProperty("_EmissionColor"))
            {
                mat.SetColor("_EmissionColor", originalEmissionColors[i]);
            }
        }
    }
}