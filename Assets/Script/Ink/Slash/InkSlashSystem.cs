using UnityEngine;
using System.Collections;

public class InkSlashSystem : MonoBehaviour
{
    [Header("斬撃プレハブ（空ならQuadを自動生成）")]
    [SerializeField] private GameObject slashPrefab;

    [Header("Trail用テクスチャ（SlashTrail.png）")]
    [SerializeField] private Texture2D trailTexture;

    [Header("斬撃テクスチャ（横一文字, 縦斬り, 斜め, 円弧 の順）")]
    [SerializeField] private Texture2D[] slashTextures;

    [Header("斬撃パターン（空ならデフォルト4種を自動生成）")]
    [SerializeField] private SlashPattern[] patterns;

    private int currentPatternIndex;
    private LayerMask hitMask;

    public SlashPattern CurrentPattern =>
        patterns != null && patterns.Length > 0 ? patterns[currentPatternIndex] : null;

    public int CurrentPatternIndex => currentPatternIndex;
    public int PatternCount => patterns != null ? patterns.Length : 0;

    public SlashPattern GetPattern(int index)
    {
        if (patterns == null || index < 0 || index >= patterns.Length) return null;
        return patterns[index];
    }

    private void Start()
    {
        int playerLayer = LayerMask.NameToLayer("Player");
        hitMask = playerLayer >= 0 ? ~(1 << playerLayer) : ~0;

        if (patterns == null || patterns.Length == 0)
        {
            patterns = CreateDefaultPatterns();
            Debug.Log("[InkSlashSystem] デフォルトパターン4種を生成しました。");
        }

        currentPatternIndex = 0;
    }

    public void SelectPattern(int index)
    {
        if (patterns == null || patterns.Length == 0) return;
        currentPatternIndex = Mathf.Clamp(index, 0, patterns.Length - 1);
    }

    public void NextPattern()
    {
        if (patterns == null || patterns.Length == 0) return;
        currentPatternIndex = (currentPatternIndex + 1) % patterns.Length;
    }

    public void PrevPattern()
    {
        if (patterns == null || patterns.Length == 0) return;
        currentPatternIndex = (currentPatternIndex - 1 + patterns.Length) % patterns.Length;
    }

    public void CreateSlash(Vector3 position, Vector3 direction)
    {
        SlashPattern pat = CurrentPattern;

        if (pat == null)
        {
            Debug.LogError("[InkSlashSystem] CurrentPattern が null です");
            return;
        }

        if (pat.spawnDelay > 0f)
        {
            StartCoroutine(CreateSlashDelayed(position, direction, pat));
            return;
        }

        CreateSlashAfterDelay(position, direction, pat);
    }

    public void CreateSlash(Vector3 position, Vector3 direction, SlashPattern pattern)
    {
        if (pattern == null)
        {
            Debug.LogError("[InkSlashSystem] 指定された SlashPattern が null です");
            return;
        }

        if (pattern.spawnDelay > 0f)
        {
            StartCoroutine(CreateSlashDelayed(position, direction, pattern));
            return;
        }

        CreateSlashAfterDelay(position, direction, pattern);
    }

    private GameObject CreateDefaultSlashObject(Vector3 position, Vector3 direction, SlashPattern pat)
    {
        GameObject obj = new GameObject("Slash_" + pat.patternName);
        obj.transform.position = position;
        obj.transform.rotation = Quaternion.LookRotation(direction);

        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = "Visual";
        quad.transform.SetParent(obj.transform, false);

        Collider col = quad.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Vector2 size = pat.visualSize;
        if (size.sqrMagnitude < 0.01f)
        {
            size = new Vector2(3f, 1.5f);
        }

        quad.transform.localScale = new Vector3(size.x, size.y, 1f);
        quad.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);

        Renderer renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader slashShader = FindSlashShader();

            if (slashShader == null)
            {
                Debug.LogError("[InkSlashSystem] 全Shader取得失敗。Visual用Material生成をスキップします。");
            }
            else
            {
                Material mat = new Material(slashShader);
                mat.color = new Color(0.02f, 0.02f, 0.05f, 1f);

                if (pat.slashTexture != null)
                {
                    SetTextureSafe(mat, pat.slashTexture);
                }

                mat.renderQueue = 3000;

                renderer.material = mat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        TrailRenderer trail = obj.AddComponent<TrailRenderer>();
        trail.time = pat.trailTime > 0 ? pat.trailTime : 0.3f;

        float tw = pat.trailWidth > 0 ? pat.trailWidth : 0.5f;
        trail.startWidth = tw;
        trail.endWidth = tw * 0.1f;
        trail.minVertexDistance = 0.1f;
        trail.autodestruct = false;
        trail.numCornerVertices = 4;
        trail.numCapVertices = 4;

        Shader trailShader = FindSlashShader();

        if (trailShader == null)
        {
            Debug.LogError("[InkSlashSystem] Trail Shader取得失敗。Trail用Material生成をスキップします。");
        }
        else
        {
            Material trailMat = new Material(trailShader);
            trailMat.color = new Color(0.02f, 0.02f, 0.05f, 0.8f);
            trailMat.renderQueue = 3000;

            if (trailTexture != null)
            {
                SetTextureSafe(trailMat, trailTexture);
            }

            trail.material = trailMat;
        }

        Gradient colorGrad = new Gradient();
        colorGrad.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(0.02f, 0.02f, 0.05f), 0f),
                new GradientColorKey(new Color(0.1f, 0.1f, 0.13f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        trail.colorGradient = colorGrad;

        return obj;
    }

    private Shader FindSlashShader()
    {
        Shader shader = Shader.Find("Ink/SlashVisual");

        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        return shader;
    }

    private void SetTextureSafe(Material mat, Texture2D tex)
    {
        if (mat == null || tex == null) return;

        if (mat.HasProperty("_MainTex"))
        {
            mat.SetTexture("_MainTex", tex);
        }
        else if (mat.HasProperty("_BaseMap"))
        {
            mat.SetTexture("_BaseMap", tex);
        }
    }

    private SlashPattern[] CreateDefaultPatterns()
    {
        var horizontal = ScriptableObject.CreateInstance<SlashPattern>();
        horizontal.patternName = "横一文字";
        horizontal.direction = SlashDirection.Horizontal;
        horizontal.length = 8f;
        horizontal.width = 12f;
        horizontal.arcAngle = 120f;
        horizontal.speed = 25f;
        horizontal.gravity = 2f;
        horizontal.lifetime = 1.5f;
        horizontal.baseDamage = 30f;
        horizontal.inkDensity = 220;
        horizontal.trailInterval = 0.1f;
        horizontal.trailRadius = 0.8f;
        horizontal.trailDensity = 150;
        horizontal.impactRadius = 2.0f;
        horizontal.inkCost = 0.1f;
        horizontal.visualSize = new Vector2(4f, 1.5f);
        horizontal.visualRotation = 90f;
        horizontal.trailWidth = 0.6f;
        horizontal.trailTime = 0.25f;
        horizontal.fadeSpeed = 1.2f;

        var vertical = ScriptableObject.CreateInstance<SlashPattern>();
        vertical.patternName = "縦斬り";
        vertical.direction = SlashDirection.Vertical;
        vertical.length = 15f;
        vertical.width = 2f;
        vertical.arcAngle = 0f;
        vertical.speed = 35f;
        vertical.gravity = 1f;
        vertical.lifetime = 2f;
        vertical.baseDamage = 80f;
        vertical.inkDensity = 230;
        vertical.trailInterval = 0.3f;
        vertical.trailRadius = 0.2f;
        vertical.trailDensity = 140;
        vertical.impactRadius = 1.0f;
        vertical.inkCost = 0.2f;
        vertical.visualSize = new Vector2(1f, 3.5f);
        vertical.visualRotation = 0f;
        vertical.trailWidth = 0.3f;
        vertical.trailTime = 0.35f;
        vertical.fadeSpeed = 0.8f;

        var diagonal = ScriptableObject.CreateInstance<SlashPattern>();
        diagonal.patternName = "斜め";
        diagonal.direction = SlashDirection.DiagonalR;
        diagonal.length = 10f;
        diagonal.width = 6f;
        diagonal.arcAngle = 60f;
        diagonal.speed = 28f;
        diagonal.gravity = 2f;
        diagonal.lifetime = 1.8f;
        diagonal.baseDamage = 50f;
        diagonal.inkDensity = 170;
        diagonal.trailInterval = 0.4f;
        diagonal.trailRadius = 0.35f;
        diagonal.trailDensity = 100;
        diagonal.impactRadius = 1.5f;
        diagonal.inkCost = 0.15f;
        diagonal.visualSize = new Vector2(3f, 3f);
        diagonal.visualRotation = 45f;
        diagonal.trailWidth = 0.4f;
        diagonal.trailTime = 0.3f;
        diagonal.fadeSpeed = 1.0f;

        var circle = ScriptableObject.CreateInstance<SlashPattern>();
        circle.patternName = "円弧";
        circle.direction = SlashDirection.Circle;
        circle.length = 6f;
        circle.width = 6f;
        circle.arcAngle = 360f;
        circle.speed = 15f;
        circle.gravity = 3f;
        circle.lifetime = 1.2f;
        circle.baseDamage = 20f;
        circle.inkDensity = 150;
        circle.trailInterval = 0.3f;
        circle.trailRadius = 0.4f;
        circle.trailDensity = 90;
        circle.impactRadius = 2.5f;
        circle.inkCost = 0.25f;
        circle.visualSize = new Vector2(3.5f, 3.5f);
        circle.visualRotation = 0f;
        circle.trailWidth = 0.5f;
        circle.trailTime = 0.2f;
        circle.fadeSpeed = 1.5f;

        SlashPattern[] result = new[] { horizontal, vertical, diagonal, circle };

        if (slashTextures != null)
        {
            for (int i = 0; i < result.Length && i < slashTextures.Length; i++)
            {
                if (slashTextures[i] != null)
                {
                    result[i].slashTexture = slashTextures[i];
                }
            }
        }

        return result;
    }

    private void HideSlashVisual(GameObject obj)
    {
        if (obj == null) return;

        Transform visual = obj.transform.Find("Visual");

        if (visual != null)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = false;
            }

            return;
        }

        Renderer[] allRenderers = obj.GetComponentsInChildren<Renderer>(true);

        for (int i = 0; i < allRenderers.Length; i++)
        {
            if (allRenderers[i] is TrailRenderer) continue;
            if (allRenderers[i] is ParticleSystemRenderer) continue;

            allRenderers[i].enabled = false;
        }
    }

    private IEnumerator CreateSlashDelayed(Vector3 position, Vector3 direction, SlashPattern pat)
    {
        yield return new WaitForSeconds(pat.spawnDelay);

        CreateSlashImmediate(position, direction, pat);
    }

    private void CreateSlashImmediate(Vector3 position, Vector3 direction, SlashPattern pat)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            Debug.LogWarning("[InkSlashSystem] direction が0なので補正");
            direction = transform.forward;
        }

        Debug.Log(
            "[InkSlashSystem] CreateSlash " +
            " Pos:" + position +
            " Dir:" + direction +
            " Speed:" + pat.speed
        );

        GameObject obj;

        if (slashPrefab != null)
        {
            obj = Instantiate(
                slashPrefab,
                position,
                Quaternion.LookRotation(direction)
            );
        }
        else
        {
            obj = CreateDefaultSlashObject(position, direction, pat);
        }

        if (obj == null)
        {
            Debug.LogError("[InkSlashSystem] 斬撃オブジェクト生成失敗");
            return;
        }

        FlyingSlash slash = obj.GetComponent<FlyingSlash>();

        if (slash == null)
        {
            slash = obj.AddComponent<FlyingSlash>();
        }

        slash.velocity = direction.normalized * pat.speed;
        slash.pattern = pat;
        slash.hitMask = hitMask;
    }

    private void CreateArcSlash(Vector3 position, Vector3 direction, SlashPattern pat)
    {
        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = transform.forward;
        }

        direction.Normalize();

        int count = Mathf.Max(1, pat.arcProjectileCount);
        float totalAngle = Mathf.Max(1.0f, pat.arcAngle);

        float startAngle = -totalAngle * 0.5f;
        float step = count > 1 ? totalAngle / (count - 1) : 0.0f;

        int centerIndex = count / 2;

        for (int i = 0; i < count; ++i)
        {
            float angle = startAngle + step * i;

            Vector3 dir =
                Quaternion.AngleAxis(angle, Vector3.up) * direction;

            GameObject obj;

            if (slashPrefab != null)
            {
                obj = Instantiate(
                    slashPrefab,
                    position,
                    Quaternion.LookRotation(dir)
                );
            }
            else
            {
                obj = CreateDefaultSlashObject(position, dir, pat);
            }

            if (obj == null) continue;

            bool isCenter = i == centerIndex;

            FlyingSlash slash = obj.GetComponent<FlyingSlash>();

            if (slash == null)
            {
                slash = obj.AddComponent<FlyingSlash>();
            }

            slash.velocity = dir.normalized * pat.speed;
            slash.pattern = pat;
            slash.hitMask = hitMask;

            slash.spawnEffect = isCenter;

            if (!isCenter && pat.hideSubProjectiles)
            {
                HideSlashVisual(obj);
            }
        }
    }

    private void CreateSlashAfterDelay(Vector3 position, Vector3 direction, SlashPattern pat)
    {
        if (pat.GetInkShape() == InkShape.Arc &&
            pat.arcProjectileCount > 1)
        {
            CreateArcSlash(position, direction, pat);
            return;
        }

        CreateSlashImmediate(position, direction, pat);
    }
}