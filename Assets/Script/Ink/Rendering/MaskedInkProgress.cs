
//using UnityEngine;

///// <summary>
///// マスク画像を使った段階的な塗り表現（累積版）
///// 
///// ■ 仕組み:
///// - ヒットされるたびに次のマスクを RenderTexture に焼き込んで累積
///// - 各マスクの濃さは個別に設定可能（色は全ステップ共通）
///// - シェーダーは累積結果1枚を読むだけ
///// 
///// ■ 使い方:
///// 1. PaintableSurfaceが付いているオブジェクトに併用してアタッチ
///// 2. MaskedInkマテリアルを別途用意してメッシュレンダラーに適用
///// 3. masks[] にマスク画像を順番に登録
///// 4. maskStrengths[] に各マスクの濃さ（0〜1）を設定
///// 
///// ■ 動作:
/////   ヒット1回: masks[0] × strengths[0] が累積される
/////   ヒット2回: + masks[1] × strengths[1] が上に重なる
/////   ヒット3回: + masks[2] × strengths[2] が上に重なる
/////   全部255で飽和（重なりすぎても真っ黒以上にはならない）
///// </summary>
//[RequireComponent(typeof(Renderer))]
//public class MaskedInkProgress : MonoBehaviour
//{
//    [System.Serializable]
//    public class MaskStep
//    {
//        public Texture2D mask;
//        [Range(0f, 1f)] public float strength = 1f;
//    }

//    [Header("マスク設定")]
//    [Tooltip("段階ごとのマスク画像と濃さ。配列サイズ = ステップ数")]
//    [SerializeField] private MaskStep[] steps;

//    [Header("累積テクスチャの解像度")]
//    [SerializeField] private int textureResolution = 512;

//    [Header("クールダウン")]
//    [Tooltip("Advance後、次のAdvanceを受け付けるまでの待機時間（秒）")]
//    [SerializeField] private float advanceCooldown = 0.3f;

//    [Header("ヒット時の挙動")]
//    [Tooltip("PaintableSurface.OnPaintedで1ヒット来たときに進める量。1.0=1ステップ、0.5=半ステップ")]
//    [SerializeField] private float progressPerHit = 1f;

//    [Header("自動取得")]
//    [SerializeField] private PaintableSurface paintableSurface;
//    [SerializeField] private Renderer targetRenderer;

//    // ── 内部状態 ──
//    private int currentStep = -1;
//    private float subProgress = 0f;      // 次ステップまでの進捗 0〜1
//    private RenderTexture accumRT;
//    private RenderTexture tempRT;
//    private MaterialPropertyBlock propBlock;
//    private Material blendMaterial;
//    private float lastAdvanceTime = -999f;

//    // ── プロパティ ──
//    public int CurrentStep => currentStep;
//    public bool IsFinished => steps != null && currentStep >= steps.Length - 1;
//    /// <summary>次のステップまでの進捗（0〜1）</summary>
//    public float SubProgress => subProgress;

//    // ====================================================================
//    //  初期化
//    // ====================================================================

//    private void Awake()
//    {
//        if (paintableSurface == null)
//            paintableSurface = GetComponent<PaintableSurface>();
//        if (targetRenderer == null)
//            targetRenderer = GetComponent<Renderer>();

//        propBlock = new MaterialPropertyBlock();

//        // 累積RenderTextureを作成（R8で十分、サイズは設定値）
//        accumRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.R8);
//        accumRT.filterMode = FilterMode.Bilinear;
//        accumRT.wrapMode = TextureWrapMode.Clamp;
//        accumRT.Create();

//        tempRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.R8);
//        tempRT.filterMode = FilterMode.Bilinear;
//        tempRT.wrapMode = TextureWrapMode.Clamp;
//        tempRT.Create();

//        // 初期状態は黒（塗られていない）
//        ClearRT(accumRT);

//        // 累積用ブレンドマテリアル
//        Shader blendShader = Shader.Find("Hidden/MaskAdditiveBlend");
//        if (blendShader != null)
//        {
//            blendMaterial = new Material(blendShader);
//        }
//        else
//        {
//            Debug.LogError("[MaskedInkProgress] シェーダー Hidden/MaskAdditiveBlend が見つかりません");
//        }
//    }

//    private void OnEnable()
//    {
//        if (paintableSurface != null)
//            paintableSurface.OnPainted += HandlePainted;
//        UpdateShader();
//    }

//    private void OnDisable()
//    {
//        if (paintableSurface != null)
//            paintableSurface.OnPainted -= HandlePainted;
//    }

//    private void OnDestroy()
//    {
//        if (accumRT != null) accumRT.Release();
//        if (tempRT != null) tempRT.Release();
//        if (blendMaterial != null) Destroy(blendMaterial);
//    }

//    // ====================================================================
//    //  イベント処理
//    // ====================================================================

//    private void HandlePainted(int cells, byte density)
//    {
//        // クールダウン中は無視（多段ヒット対策）
//        if (Time.time - lastAdvanceTime < advanceCooldown) return;

//        AddProgress(progressPerHit);
//        lastAdvanceTime = Time.time;
//    }

//    // ====================================================================
//    //  進捗操作
//    // ====================================================================

//    /// <summary>1ステップ進める（次のマスクを累積RTに焼き込む）</summary>
//    public void Advance()
//    {
//        if (steps == null || steps.Length == 0) return;
//        if (currentStep >= steps.Length - 1) return;

//        currentStep++;
//        var step = steps[currentStep];
//        if (step.mask != null && blendMaterial != null)
//        {
//            BlendMaskInto(step.mask, step.strength);
//        }
//        UpdateShader();
//    }

//    /// <summary>
//    /// 指定したステップ数だけ進める
//    /// 例: AdvanceBy(2) で2段階一気に進む
//    /// </summary>
//    public void AdvanceBy(int stepCount)
//    {
//        if (stepCount <= 0) return;
//        for (int i = 0; i < stepCount; i++)
//        {
//            if (currentStep >= (steps?.Length ?? 0) - 1) break;
//            Advance();
//        }
//    }

//    /// <summary>
//    /// 進捗量を加算（0〜1の範囲、複数ステップにまたがってもOK）
//    /// 内部の subProgress が 1.0 を超えるたびに次ステップへ進む
//    /// 例: AddProgress(0.3) を4回呼ぶと 0.3→0.6→0.9→1.2 で1ステップ進む（subProgress=0.2残る）
//    /// 例: AddProgress(2.5) で2ステップ進む（subProgress=0.5残る）
//    /// </summary>
//    public void AddProgress(float amount)
//    {
//        if (amount <= 0f) return;
//        if (steps == null || steps.Length == 0) return;

//        subProgress += amount;

//        // subProgress が 1.0 を超えるたびに次ステップへ
//        while (subProgress >= 1f && currentStep < steps.Length - 1)
//        {
//            subProgress -= 1f;
//            Advance();
//        }

//        // 最終ステップに到達したらsubProgressを0に固定
//        if (currentStep >= steps.Length - 1)
//        {
//            subProgress = 0f;
//        }
//    }

//    /// <summary>進捗をリセット</summary>
//    public void ResetProgress()
//    {
//        currentStep = -1;
//        subProgress = 0f;
//        lastAdvanceTime = -999f;
//        ClearRT(accumRT);
//        UpdateShader();
//    }

//    /// <summary>特定のステップまで一気に進める（再構築）</summary>
//    public void SetStep(int step)
//    {
//        if (steps == null || steps.Length == 0) return;

//        currentStep = -1;
//        subProgress = 0f;
//        ClearRT(accumRT);

//        int target = Mathf.Clamp(step, -1, steps.Length - 1);
//        while (currentStep < target)
//        {
//            currentStep++;
//            var s = steps[currentStep];
//            if (s.mask != null && blendMaterial != null)
//                BlendMaskInto(s.mask, s.strength);
//        }
//        UpdateShader();
//    }

//    // ====================================================================
//    //  マスク合成（CPU/GPU）
//    // ====================================================================

//    /// <summary>
//    /// 既存の累積RTに「新しいマスク × strength」を加算ブレンドする
//    /// </summary>
//    private void BlendMaskInto(Texture2D newMask, float strength)
//    {
//        // 1. accumRT → tempRT にコピー（読み書き同時禁止のため一時退避）
//        Graphics.Blit(accumRT, tempRT);

//        // 2. tempRT（過去の累積）と newMask × strength を加算して accumRT に書き出す
//        blendMaterial.SetTexture("_PrevTex", tempRT);
//        blendMaterial.SetTexture("_NewMask", newMask);
//        blendMaterial.SetFloat("_NewStrength", strength);
//        Graphics.Blit(null, accumRT, blendMaterial);
//    }

//    private void ClearRT(RenderTexture rt)
//    {
//        var prev = RenderTexture.active;
//        RenderTexture.active = rt;
//        GL.Clear(true, true, Color.black);
//        RenderTexture.active = prev;
//    }

//    // ====================================================================
//    //  シェーダー送信
//    // ====================================================================

//    private void UpdateShader()
//    {
//        if (targetRenderer == null || propBlock == null) return;

//        targetRenderer.GetPropertyBlock(propBlock);
//        propBlock.SetTexture("_MaskTex", accumRT);
//        targetRenderer.SetPropertyBlock(propBlock);
//    }

//#if UNITY_EDITOR
//    private void OnDrawGizmosSelected()
//    {
//        if (!Application.isPlaying) return;
//        string status = currentStep < 0 ? "未着手" : $"Step {currentStep + 1}/{steps?.Length ?? 0}";
//        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, status);
//    }
//#endif
//}

using UnityEngine;

/// <summary>
/// マスク画像を使った段階的な塗り表現（数値駆動版）
/// 
/// ■ 仕組み:
/// - 外部から数値を渡して進捗を進める
/// - 進捗が閾値を超えると次のマスクへ進む
/// - PaintableSurfaceとは独立（連動なし）
/// 
/// ■ 使い方:
/// 1. このコンポーネントをアタッチ
/// 2. MaskedInkマテリアルをメッシュレンダラーに適用
/// 3. masks[] にマスク画像を順番に登録
/// 4. 外部スクリプトから Advance() / AdvanceBy(n) / AddProgress(amount) を呼ぶ
/// 
/// ■ 動作:
///   masks.Length = 3 のとき
///     初期           → 何も表示なし
///     1回Advance     → masks[0] が累積される
///     2回Advance     → + masks[1] が上に重なる
///     3回Advance     → + masks[2] が上に重なる（最終）
///     それ以上       → 変化なし
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

    [Header("自動取得")]
    [SerializeField] private Renderer targetRenderer;

    // ── 内部状態 ──
    private int currentStep = -1;
    private float subProgress = 0f;      // 次ステップまでの進捗 0〜1
    private RenderTexture accumRT;
    private RenderTexture tempRT;
    private MaterialPropertyBlock propBlock;
    private Material blendMaterial;

    // ── プロパティ ──
    /// <summary>現在のステップ（-1なら未着手、0以上はマスクのインデックス）</summary>
    public int CurrentStep => currentStep;
    /// <summary>次のステップまでの進捗（0〜1）</summary>
    public float SubProgress => subProgress;
    /// <summary>最後のステップまで到達したか</summary>
    public bool IsFinished => steps != null && currentStep >= steps.Length - 1;

    // ====================================================================
    //  初期化
    // ====================================================================

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<Renderer>();

        propBlock = new MaterialPropertyBlock();

        // 累積RenderTextureを作成
        accumRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.R8);
        accumRT.filterMode = FilterMode.Bilinear;
        accumRT.wrapMode = TextureWrapMode.Clamp;
        accumRT.Create();

        tempRT = new RenderTexture(textureResolution, textureResolution, 0, RenderTextureFormat.R8);
        tempRT.filterMode = FilterMode.Bilinear;
        tempRT.wrapMode = TextureWrapMode.Clamp;
        tempRT.Create();

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
        UpdateShader();
    }

    private void OnDestroy()
    {
        if (accumRT != null) accumRT.Release();
        if (tempRT != null) tempRT.Release();
        if (blendMaterial != null) Destroy(blendMaterial);
    }

    // ====================================================================
    //  進捗操作（外部から呼ぶ用）
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
            BlendMaskInto(step.mask, step.strength);
        }
        UpdateShader();
    }

    /// <summary>
    /// 指定したステップ数だけ進める
    /// 例: AdvanceBy(2) で2段階一気に進む
    /// </summary>
    public void AdvanceBy(int stepCount)
    {
        if (stepCount <= 0) return;
        for (int i = 0; i < stepCount; i++)
        {
            if (currentStep >= (steps?.Length ?? 0) - 1) break;
            Advance();
        }
    }

    /// <summary>
    /// 進捗量を加算（0〜1の範囲、複数ステップにまたがってもOK）
    /// 内部の subProgress が 1.0 を超えるたびに次ステップへ進む
    /// 例: AddProgress(0.3) を4回呼ぶと 0.3→0.6→0.9→1.2 で1ステップ進む（subProgress=0.2残る）
    /// 例: AddProgress(2.5) で2ステップ進む（subProgress=0.5残る）
    /// </summary>
    public void AddProgress(float amount)
    {
        if (amount <= 0f) return;
        if (steps == null || steps.Length == 0) return;

        subProgress += amount;

        // subProgress が 1.0 を超えるたびに次ステップへ
        while (subProgress >= 1f && currentStep < steps.Length - 1)
        {
            subProgress -= 1f;
            Advance();
        }

        // 最終ステップに到達したらsubProgressを0に固定
        if (currentStep >= steps.Length - 1)
        {
            subProgress = 0f;
        }
    }

    /// <summary>進捗をリセット</summary>
    public void ResetProgress()
    {
        currentStep = -1;
        subProgress = 0f;
        ClearRT(accumRT);
        UpdateShader();
    }

    /// <summary>特定のステップまで一気に進める（再構築）</summary>
    public void SetStep(int step)
    {
        if (steps == null || steps.Length == 0) return;

        currentStep = -1;
        subProgress = 0f;
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

    private void BlendMaskInto(Texture2D newMask, float strength)
    {
        // 1. accumRT → tempRT にコピー
        Graphics.Blit(accumRT, tempRT);

        // 2. tempRT + newMask × strength を accumRT に書き出す
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
        string status = currentStep < 0
            ? "未着手"
            : $"Step {currentStep + 1}/{steps?.Length ?? 0} (sub: {subProgress:F2})";
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, status);
    }
#endif
}