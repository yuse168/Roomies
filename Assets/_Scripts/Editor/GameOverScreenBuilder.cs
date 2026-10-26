#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ゲームオーバー画面のプレハブを作り、シーンへ置くツール。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・パネル・文字・ボタンはここで実体として作る。実行時は表示の切り替えだけ。
///  ・色・文言・配置はインスペクターとシーンビューで直せる。
///
/// メニュー：
///  Roomies/UI/Rebuild Game Over Screen
///  Roomies/UI/Place Game Over Screen In Open Scene
/// </summary>
internal static class GameOverScreenBuilder
{
    private const string PrefabFolder = "Assets/_Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/GameOverScreen.prefab";

    private static readonly Color Ink = new Color(0.10f, 0.11f, 0.16f, 0.96f);
    private static readonly Color Dim = new Color(0f, 0f, 0f, 0.72f);
    private static readonly Color Red = new Color(0.95f, 0.31f, 0.33f);
    private static readonly Color Lime = new Color(0.40f, 0.85f, 0.34f);
    private static readonly Color Paper = new Color(0.97f, 0.96f, 0.93f);

    [MenuItem("Roomies/UI/Rebuild Game Over Screen")]
    private static void RebuildPrefab()
    {
        System.IO.Directory.CreateDirectory(PrefabFolder);

        var root = new GameObject("GameOverScreen");
        try
        {
            GameOverScreen screen = root.AddComponent<GameOverScreen>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9800;             // 逮捕演出(9500)より前

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            root.AddComponent<GraphicRaycaster>();

            // 表示本体。実行時はこの Root だけを出し入れする。
            var content = new GameObject("Root", typeof(RectTransform));
            content.transform.SetParent(root.transform, false);
            Stretch((RectTransform)content.transform);

            Image dim = Panel(content.transform, "Dim", Dim);
            Stretch(dim.rectTransform);

            Image card = Panel(content.transform, "Card", Ink);
            RectTransform cardRect = card.rectTransform;
            cardRect.anchorMin = new Vector2(0.5f, 0.5f);
            cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(880f, GameOverScreen.CardHeight);

            // 並びは GameOverScreen の定数（理由の文が3行でもボタンに重ならない）
            TextMeshProUGUI title = Label(cardRect, "Title", "GAME OVER", 86f, Red,
                new Vector2(0f, GameOverScreen.TitleTop), new Vector2(820f, GameOverScreen.TitleHeight));
            TextMeshProUGUI reason = Label(cardRect, "Reason",
                "家賃 ¥0 を払えなかった。\n大家さんに追い出されました……",
                34f, Paper, new Vector2(0f, GameOverScreen.ReasonTop),
                new Vector2(760f, GameOverScreen.ReasonHeight));
            reason.fontStyle = FontStyles.Normal;

            Button retry = CreateButton(cardRect, "RetryButton", "もう一度", Lime,
                new Vector2(-180f, GameOverScreen.ButtonBottom));
            Button menu = CreateButton(cardRect, "MenuButton", "メインメニューへ", Paper,
                new Vector2(180f, GameOverScreen.ButtonBottom));

            var serialized = new SerializedObject(screen);
            Assign(serialized, "root", content);
            Assign(serialized, "titleLabel", title);
            Assign(serialized, "reasonLabel", reason);
            Assign(serialized, "retryButton", retry);
            Assign(serialized, "menuButton", menu);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            content.SetActive(false);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[UI] ゲームオーバー画面のプレハブを作りました → {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Roomies/UI/Place Game Over Screen In Open Scene")]
    private static void PlaceInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[UI] Play Modeを抜けてから実行してください。");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[UI] 先に Rebuild Game Over Screen を実行してください。");
            return;
        }

        if (Object.FindAnyObjectByType<GameOverScreen>() != null)
        {
            Debug.Log("[UI] このシーンには既にGameOverScreenがあります。");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place game over screen");

        EditorSceneManager.MarkSceneDirty(instance.scene);
        Selection.activeGameObject = instance;
        Debug.Log($"[UI] {instance.scene.name} にゲームオーバー画面を置きました（シーンを保存してください）");
    }

    // ---- 部品 ----

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static Image Panel(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        return image;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text,
                                         float size, Color color,
                                         Vector2 anchoredPosition, Vector2 sizeDelta)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = color;
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;

        RectTransform rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = sizeDelta;
        rect.anchoredPosition = anchoredPosition;
        return label;
    }

    private static Button CreateButton(Transform parent, string name, string text,
                                       Color color, Vector2 anchoredPosition)
    {
        Image background = Panel(parent, name, color);
        RectTransform rect = background.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(320f, 84f);
        rect.anchoredPosition = anchoredPosition;

        var button = background.gameObject.AddComponent<Button>();
        button.targetGraphic = background;

        var labelGo = new GameObject("Label", typeof(RectTransform));
        labelGo.transform.SetParent(background.transform, false);
        var label = labelGo.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = 30f;
        label.color = new Color(0.10f, 0.11f, 0.16f);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        Stretch(label.rectTransform);

        return button;
    }

    private static void Assign(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[UI] {field} が見つかりません。");
            return;
        }
        property.objectReferenceValue = value;
    }
}
#endif
