#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ポーズ画面プレハブをワンクリック生成するエディタ拡張。
/// メニュー: Tools > Osumituki > Create Pause Menu Prefab
///
/// 生成物: Assets/Prefabs/UI/PauseMenu.prefab
///   PauseMenu (Canvas + CanvasScaler + PauseMenu.cs)
///   └─ PausePanel
///      ├─ Background (pause_map)
///      ├─ Resume_tudukeru (tudukeru)   ← options[0]
///      ├─ Title_titlehe   (titlehe)    ← options[1]
///      └─ Cursor          (cursor)
///
/// 生成後: 各ステージシーンにこのプレハブを置く。位置は好みで微調整。
/// クリック操作はしない（選択式）ので EventSystem は不要。
/// </summary>
public static class PauseMenuBuilder
{
    private const string DIR = "Assets/Textures/PauseScene/";
    private const string P_MAP    = DIR + "pause_map.png";
    private const string P_RESUME = DIR + "tudukeru.png";
    private const string P_TITLE  = DIR + "titlehe.png";
    private const string P_CURSOR = DIR + "cursor.png";
    private const string OUT_DIR  = "Assets/Prefabs/UI";
    private const string OUT_PATH = OUT_DIR + "/PauseMenu.prefab";

    [MenuItem("Tools/Osumituki/Create Pause Menu Prefab")]
    public static void CreatePrefab()
    {
        Sprite bg     = LoadSprite(P_MAP);
        Sprite resume = LoadSprite(P_RESUME);
        Sprite title  = LoadSprite(P_TITLE);
        Sprite cursor = LoadSprite(P_CURSOR);

        if (bg == null || resume == null || title == null || cursor == null)
        {
            Debug.LogWarning("[PauseMenuBuilder] 一部の画像が見つかりません。" +
                             "Textures/PauseScene/ に pause_map / tudukeru / titlehe / cursor があるか確認。" +
                             "（無い分は空Imageで生成します）");
        }

        // ルート: Canvas
        var root = new GameObject("PauseMenu", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;   // HUDより前。LoadingCanvas等があれば適宜
        var scaler = root.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // パネル（全画面）
        var panel = new GameObject("PausePanel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        StretchFull(panel.GetComponent<RectTransform>());

        // 背景（全画面・マップ）
        NewImage("Background", panel.transform, bg, stretch: true, nativeSize: false);

        // 選択肢（参考画像に合わせ右下寄り。位置は後で微調整）
        var resumeRT = NewImage("Resume_tudukeru", panel.transform, resume, stretch: false, nativeSize: true);
        SetCenter(resumeRT, new Vector2(540f, -180f));

        var titleRT = NewImage("Title_titlehe", panel.transform, title, stretch: false, nativeSize: true);
        SetCenter(titleRT, new Vector2(560f, -330f));

        // カーソル（options と同じ親。実行時に選択肢へ移動する）
        var cursorRT = NewImage("Cursor", panel.transform, cursor, stretch: false, nativeSize: true);
        SetCenter(cursorRT, new Vector2(420f, -180f));

        // PauseMenu を付けて配線
        var pm = root.AddComponent<PauseMenu>();
        var so = new SerializedObject(pm);
        so.FindProperty("pausePanel").objectReferenceValue = panel;
        so.FindProperty("cursor").objectReferenceValue = cursorRT;
        var opts = so.FindProperty("options");
        opts.arraySize = 2;
        opts.GetArrayElementAtIndex(0).objectReferenceValue = resumeRT;   // 続ける
        opts.GetArrayElementAtIndex(1).objectReferenceValue = titleRT;    // タイトルへ
        so.ApplyModifiedProperties();

        // 保存（固定パスに上書き更新。増殖させない。既存があれば確認）
        EnsureFolder(OUT_DIR);
        if (AssetDatabase.LoadAssetAtPath<GameObject>(OUT_PATH) != null)
        {
            bool ok = EditorUtility.DisplayDialog(
                "ポーズ画面プレハブの上書き",
                OUT_PATH + " を上書きします。\n（プレハブを手で調整した位置などは失われます）",
                "上書き", "キャンセル");
            if (!ok)
            {
                Object.DestroyImmediate(root);
                return;
            }
        }

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, OUT_PATH);
        Object.DestroyImmediate(root);   // シーン上の一時インスタンスは消す

        Debug.Log($"[PauseMenuBuilder] 生成/更新: {OUT_PATH}\n" +
                  "各ステージシーンにこのプレハブをドラッグし、Title Scene Name を設定してください。");
        Selection.activeObject = prefab;
        EditorGUIUtility.PingObject(prefab);
    }

    // ── helpers ──

    private static Sprite LoadSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[PauseMenuBuilder] テクスチャが見つかりません: {path}");
            return null;
        }
        if (importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;   // Sprite型に直して読めるように
            importer.SaveAndReimport();
            Debug.Log($"[PauseMenuBuilder] {path} を Sprite 型に変更しました（.meta が更新されます）。");
        }

        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
        {
            // 型変更直後などで直接取得できない場合のフォールバック（サブアセットからSpriteを探す）
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is Sprite s) { sprite = s; break; }
        }
        if (sprite == null)
            Debug.LogWarning($"[PauseMenuBuilder] Sprite を取得できませんでした: {path}（Texture Type が Sprite か確認）");
        return sprite;
    }

    private static RectTransform NewImage(string name, Transform parent, Sprite sprite,
                                          bool stretch, bool nativeSize)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;   // クリックしないので不要

        var rt = go.GetComponent<RectTransform>();
        if (stretch)
        {
            StretchFull(rt);
        }
        else
        {
            SetCenter(rt, Vector2.zero);
            if (nativeSize && sprite != null) img.SetNativeSize();
        }
        return rt;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetCenter(RectTransform rt, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
    }

    private static void EnsureFolder(string folder)
    {
        if (AssetDatabase.IsValidFolder(folder)) return;
        var parts = folder.Split('/');
        string cur = parts[0];   // "Assets"
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif
