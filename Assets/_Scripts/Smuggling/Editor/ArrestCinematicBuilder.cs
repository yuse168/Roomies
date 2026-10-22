#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 逮捕演出のプレハブを作り、シーンへ置くツール。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・赤青フラッシュ・白フラッシュ・暗転・スタンプ・追跡中の表示は、
///    すべてここでプレハブの子として作る。実行時は色と透明度を動かすだけ。
///  ・文言・色・大きさはインスペクターとシーンビューで直せる。
///
/// メニュー：
///  Roomies/Smuggling/Rebuild Arrest Cinematic Prefab
///  Roomies/Smuggling/Place Arrest Cinematic In Open Scene
/// </summary>
internal static class ArrestCinematicBuilder
{
    private const string PrefabFolder = "Assets/_Prefabs/UI";
    private const string PrefabPath = PrefabFolder + "/ArrestCinematic.prefab";

    [MenuItem("Roomies/Smuggling/Rebuild Arrest Cinematic Prefab")]
    private static void RebuildPrefab()
    {
        System.IO.Directory.CreateDirectory(PrefabFolder);

        var root = new GameObject("ArrestCinematic");
        try
        {
            ArrestCinematic cinematic = root.AddComponent<ArrestCinematic>();

            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9500;          // 夜イベントバナー(9000)より前、暗転(10000)より後ろ

            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            // 追跡中：画面ふちの赤い脈動と警告文
            Image vignette = FullScreen(root.transform, "Chase vignette", new Color(0.85f, 0.1f, 0.15f, 0f));
            TextMeshProUGUI chaseLabel = Label(root.transform, "Chase label", "警察に追われている！",
                64f, new Color(1f, 0.35f, 0.35f), new Vector2(0f, -120f), 1f);
            TextMeshProUGUI chaseHint = Label(root.transform, "Chase hint", "紙袋を捨てるか、逃げ切れ！",
                34f, new Color(1f, 0.85f, 0.85f), new Vector2(0f, -200f), 1f);
            chaseLabel.gameObject.SetActive(false);
            chaseHint.gameObject.SetActive(false);

            // 逮捕：赤青→白フラッシュ→暗転→スタンプ
            Image red = FullScreen(root.transform, "Siren red", new Color(0.9f, 0.08f, 0.12f, 0f));
            Image blue = FullScreen(root.transform, "Siren blue", new Color(0.1f, 0.3f, 0.95f, 0f));
            Image white = FullScreen(root.transform, "White flash", new Color(1f, 1f, 1f, 0f));
            Image black = FullScreen(root.transform, "Black fade", new Color(0f, 0f, 0f, 0f));
            TextMeshProUGUI stamp = Label(root.transform, "Arrested stamp", "ARRESTED",
                150f, new Color(1f, 0.25f, 0.25f), Vector2.zero, 0.5f);

            var serialized = new SerializedObject(cinematic);
            Assign(serialized, "chaseVignette", vignette);
            Assign(serialized, "chaseLabel", chaseLabel);
            Assign(serialized, "chaseHint", chaseHint);
            Assign(serialized, "sirenRed", red);
            Assign(serialized, "sirenBlue", blue);
            Assign(serialized, "whiteFlash", white);
            Assign(serialized, "blackFade", black);
            Assign(serialized, "stamp", stamp);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Smuggling] 逮捕演出のプレハブを作りました → {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Roomies/Smuggling/Place Arrest Cinematic In Open Scene")]
    private static void PlaceInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Smuggling] Play Modeを抜けてから実行してください。");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[Smuggling] 先に Rebuild Arrest Cinematic Prefab を実行してください。");
            return;
        }

        if (Object.FindAnyObjectByType<ArrestCinematic>() != null)
        {
            Debug.Log("[Smuggling] このシーンには既にArrestCinematicがあります。");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place arrest cinematic");

        EditorSceneManager.MarkSceneDirty(instance.scene);
        Selection.activeGameObject = instance;
        Debug.Log($"[Smuggling] {instance.scene.name} に逮捕演出を置きました（シーンを保存してください）");
    }

    private static Image FullScreen(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);

        var rect = (RectTransform)go.transform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static TextMeshProUGUI Label(Transform parent, string name, string text,
                                         float size, Color color, Vector2 anchoredPosition,
                                         float alpha)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.color = new Color(color.r, color.g, color.b, alpha);
        label.alignment = TextAlignmentOptions.Center;
        label.fontStyle = FontStyles.Bold;
        label.raycastTarget = false;
        label.enableWordWrapping = false;

        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(1400f, size * 1.6f);
        rect.anchoredPosition = anchoredPosition;

        // 追跡中の文字は画面上部に出す
        if (anchoredPosition.y < 0f)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }

        return label;
    }

    private static void Assign(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[Smuggling] {field} が見つかりません。");
            return;
        }
        property.objectReferenceValue = value;
    }
}
#endif
