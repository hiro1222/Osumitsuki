using UnityEngine;

/// <summary>
/// マスク画像を使った段階的な塗り表現（累積版）
/// 
/// ■ 仕組み:
/// - ヒットされるたびに次のマスクを RenderTexture に焼き込んで累積
/// - 各マスクの濃さは個別に設定可能（色は全ステップ共通）
/// - シェーダーは累積結果1枚を読むだけ
/// 
/// ■ 使い方:
/// 1. PaintableSurfaceが付いているオブジェクトに併用してアタッチ
/// 2. MaskedInkマテリアルを別途用意してメッシュレンダラーに適用
/// 3. masks[] にマスク画像を順番に登録
/// 4. maskStrengths[] に各マスクの濃さ（0〜1）を設定
/// 
/// ■ 動作:
///   ヒット1回: masks[0] × strengths[0] が累積される
///   ヒット2回: + masks[1] × strengths[1] が上に重なる
///   ヒット3回: + masks[2] × strengths[2] が上に重なる
///   全部255で飽和（重なりすぎても真っ黒以上にはならない）
/// </summary>
[RequireComponent(typeof(Renderer))]
public class MaskedInkProgress : MonoBehaviour
{
    [System.Serializable]
    public class MaskStep
    {
        public Texture2D mask;
        [Range(0f, 1f)] public float strength = 1f;
    }

    [Header("マスク設定")]
    [Tooltip("段階ごとのマスク画像と濃さ。配列サイズ = ステップ数")]
    [SerializeField] private MaskStep[] steps;

    [Header("累積テクスチャの解像度")]
    [SerializeField] private int textureResolution = 512;

    [Header("クールダウン")]
    [Tooltip("Advance後、次のAdvanceを受け付けるまでの待機時間（秒）")]
    [SerializeField] private float advanceCooldown = 0.3f;

    [Header("自動取得")]
    [SerializeField] private PaintableSurface paintableSurface;
    [SerializeField] private Renderer targetRenderer;

    // ── 内部状態 ──
    private int currentStep = -1;
    private RenderTexture accumRT;       // 累積マスクの結果
    private RenderTexture tempRT;        // Blit用の一時バッファ
    private MaterialPropertyBlock propBlock;
    private Material blendMaterial;      // マスク加算用のシェーダー
    private float lastAdvanceTime = -999f;  // 最後にAdvanceした時刻

    // ── プロパティ ──
    public int CurrentStep => currentStep;
    public bool IsFinished => steps != null && currentStep >= steps.Length - 1;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Awake()
    {
        if (paintableSurface == null)
            paintableSurface = GetComponent<PaintableSurface>();
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        propBlock = new MaterialPropertyBlock();

        // 累積RenderTextureを作成（R8で十分、サイズは設定値）
        accumRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.R8);
        accumRT.filterMode = FilterMode.Bilinear;
        accumRT.wrapMode = TextureWrapMode.Clamp;
        accumRT.Create();

        tempRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.R8);
        tempRT.filterMode = FilterMode.Bilinear;
        tempRT.wrapMode = TextureWrapMode.Clamp;
        tempRT.Create();

        // 初期状態は黒（塗られていない）
        ClearRT(accumRT);

        // 累積用ブレンドマテリアル
        Shader blendShader = Shader.Find("Hidden/MaskAdditiveBlend");
        if (blendShader != null)
        {
            blendMaterial = new Material(blendShader);
        }
        else
        {
            Debug.LogError("[MaskedInkProgress] シェーダー Hidden/MaskAdditiveBlend が見つかりません");
        }
    }

    private void OnEnable()
    {
        if (paintableSurface != null)
            paintableSurface.OnPainted += HandlePainted;
        UpdateShader();
    }

    private void OnDisable()
    {
        if (paintableSurface != null)
            paintableSurface.OnPainted -= HandlePainted;
    }

    private void OnDestroy()
    {
        if (accumRT != null) accumRT.Release();
        if (tempRT != null) tempRT.Release();
        if (blendMaterial != null) Destroy(blendMaterial);
    }

    // ====================================================================
    //  イベント処理
    // ====================================================================

    private void HandlePainted(int cells, byte density)
    {
        // クールダウン中は無視（多段ヒット対策）
        if (Time.time - lastAdvanceTime < advanceCooldown) return;

        Advance();
        lastAdvanceTime = Time.time;
    }

    // ====================================================================
    //  進捗操作
    // ====================================================================

    /// <summary>1ステップ進める（次のマスクを累積RTに焼き込む）</summary>
    public void Advance()
    {
        if (steps == null || steps.Length == 0) return;
        if (currentStep >= steps.Length - 1) return;

        currentStep++;
        var step = steps[currentStep];
        if (step.mask != null && blendMaterial != null)
        {
            // 累積RTに マスク × strength を加算ブレンド
            BlendMaskInto(step.mask, step.strength);
        }
        UpdateShader();
    }

    /// <summary>進捗をリセット</summary>
    public void ResetProgress()
    {
        currentStep = -1;
        lastAdvanceTime = -999f;
        ClearRT(accumRT);
        UpdateShader();
    }

    /// <summary>特定のステップまで一気に進める（再構築）</summary>
    public void SetStep(int step)
    {
        if (steps == null || steps.Length == 0) return;

        currentStep = -1;
        ClearRT(accumRT);

        int target = Mathf.Clamp(step, -1, steps.Length - 1);
        while (currentStep < target)
        {
            currentStep++;
            var s = steps[currentStep];
            if (s.mask != null && blendMaterial != null)
                BlendMaskInto(s.mask, s.strength);
        }
        UpdateShader();
    }

    // ====================================================================
    //  マスク合成（CPU/GPU）
    // ====================================================================

    /// <summary>
    /// 既存の累積RTに「新しいマスク × strength」を加算ブレンドする
    /// </summary>
    private void BlendMaskInto(Texture2D newMask, float strength)
    {
        // 1. accumRT → tempRT にコピー（読み書き同時禁止のため一時退避）
        Graphics.Blit(accumRT, tempRT);

        // 2. tempRT（過去の累積）と newMask × strength を加算して accumRT に書き出す
        blendMaterial.SetTexture("_PrevTex", tempRT);
        blendMaterial.SetTexture("_NewMask", newMask);
        blendMaterial.SetFloat("_NewStrength", strength);
        Graphics.Blit(null, accumRT, blendMaterial);
    }

    private void ClearRT(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, Color.black);
        RenderTexture.active = prev;
    }

    // ====================================================================
    //  シェーダー送信
    // ====================================================================

    private void UpdateShader()
    {
        if (targetRenderer == null || propBlock == null) return;

        targetRenderer.GetPropertyBlock(propBlock);
        propBlock.SetTexture("_MaskTex", accumRT);
        targetRenderer.SetPropertyBlock(propBlock);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying) return;
        string status = currentStep < 0 ? "未着手" : $"Step {currentStep + 1}/{steps?.Length ?? 0}";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, status);
    }
#endif
}