#if UNITY_EDITOR
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 夜の設備（冷蔵庫・ブレーカー）のプレハブを作り、シーンへ置くツール。
///
/// 規約（AGENTS.md）：
///  ・見た目は `Visual/Placeholder` の下。本アセットが来たら Placeholder を消して差し替える。
///  ・動く部品は `Pivot_Door` / `Pivot_Lever` を軸にし、その下の `Visual` に見た目を入れる。
///  ・ルートの原点は足元、正面は +Z。触る判定のコライダーはルートに1つ。
///  ・実行時には何も生成しない。
///
/// メニュー：
///  Roomies/Night/Rebuild Appliance Prefabs
///  Roomies/Night/Place Appliances In Open Scene
/// </summary>
internal static class NightApplianceBuilder
{
    private const string PrefabFolder = "Assets/_Prefabs/Night";
    private const string FridgePath = PrefabFolder + "/NightFridge.prefab";
    private const string BreakerPath = PrefabFolder + "/HouseBreaker.prefab";
    private const string MaterialFolder = "Assets/Resources/RoomiesArt/Materials";

    // 置き場所（家の中。シーンビューで動かしてよい）
    // 共用スペース（床 y=3.3、壁は x 13.1〜22.8 / z 14.0〜23.5）の北側の壁ぎわ
    private static readonly Vector3 FridgePosition = new Vector3(21.8f, 3.3f, 22.9f);
    private static readonly Vector3 BreakerPosition = new Vector3(19.3f, 4.7f, 23.2f);

    [MenuItem("Roomies/Night/Rebuild Appliance Prefabs")]
    private static void RebuildPrefabs()
    {
        System.IO.Directory.CreateDirectory(PrefabFolder);
        BuildFridge();
        BuildBreaker();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Roomies/Night/Place Appliances In Open Scene")]
    private static void PlaceInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Night] Play Modeを抜けてから実行してください。");
            return;
        }

        Place<NightFridge>(FridgePath, FridgePosition, 180f);
        Place<HouseBreaker>(BreakerPath, BreakerPosition, 180f);
    }

    private static void Place<T>(string prefabPath, Vector3 position, float yaw) where T : Component
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"[Night] 先に Rebuild Appliance Prefabs を実行してください（{prefabPath}）。");
            return;
        }

        if (Object.FindAnyObjectByType<T>() != null)
        {
            Debug.Log($"[Night] このシーンには既に {typeof(T).Name} があります。");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place night appliance");
        instance.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

        EditorSceneManager.MarkSceneDirty(instance.scene);
        Debug.Log($"[Night] {typeof(T).Name} を {position} に置きました（位置はシーンビューで調整してください）");
    }

    // ---- 冷蔵庫 ----

    private static void BuildFridge()
    {
        var root = new GameObject("NightFridge");
        try
        {
            root.AddComponent<NetworkObject>();
            NightFridge fridge = root.AddComponent<NightFridge>();

            // 触る判定（ルートに1つだけ）
            var trigger = root.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 0.9f, 0f);
            trigger.size = new Vector3(0.8f, 1.8f, 0.75f);

            Transform visual = Group(root.transform, "Visual");
            Transform placeholder = Group(visual, "Placeholder");

            // 本体（扉のぶんだけ手前を空ける）
            Box(placeholder, "Body", new Vector3(0f, 0.9f, 0.06f), new Vector3(0.78f, 1.8f, 0.62f), "cream");
            Box(placeholder, "Top", new Vector3(0f, 1.82f, 0.06f), new Vector3(0.82f, 0.06f, 0.66f), "silver");

            // 扉は軸の下に置く（本アセットでも軸を合わせるだけで開く）
            Transform pivot = Group(root.transform, "Pivot_Door");
            pivot.localPosition = new Vector3(-0.38f, 0.9f, -0.3f);

            Transform doorVisual = Group(pivot, "Visual");
            Box(doorVisual, "Door", new Vector3(0.38f, 0f, 0f), new Vector3(0.78f, 1.76f, 0.1f), "paper");
            Box(doorVisual, "Handle", new Vector3(0.70f, 0f, -0.08f), new Vector3(0.05f, 0.9f, 0.05f), "silver");

            var serialized = new SerializedObject(fridge);
            serialized.FindProperty("doorPivot").objectReferenceValue = pivot;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, FridgePath);
            Debug.Log($"[Night] 冷蔵庫のプレハブを作りました → {FridgePath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---- ブレーカー ----

    private static void BuildBreaker()
    {
        var root = new GameObject("HouseBreaker");
        try
        {
            root.AddComponent<NetworkObject>();
            HouseBreaker breaker = root.AddComponent<HouseBreaker>();

            var trigger = root.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 0f, 0.06f);
            trigger.size = new Vector3(0.44f, 0.56f, 0.2f);

            Transform visual = Group(root.transform, "Visual");
            Transform placeholder = Group(visual, "Placeholder");

            Box(placeholder, "Box", new Vector3(0f, 0f, 0.06f), new Vector3(0.4f, 0.52f, 0.12f), "ink");
            Box(placeholder, "Panel", new Vector3(0f, 0.04f, -0.01f), new Vector3(0.32f, 0.3f, 0.02f), "silver");

            Transform pivot = Group(root.transform, "Pivot_Lever");
            pivot.localPosition = new Vector3(0f, 0.04f, -0.02f);

            Transform leverVisual = Group(pivot, "Visual");
            Box(leverVisual, "Lever", new Vector3(0f, 0.06f, -0.03f), new Vector3(0.08f, 0.16f, 0.04f), "coral");

            var serialized = new SerializedObject(breaker);
            serialized.FindProperty("leverPivot").objectReferenceValue = pivot;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BreakerPath);
            Debug.Log($"[Night] ブレーカーのプレハブを作りました → {BreakerPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    // ---- 部品 ----

    private static Transform Group(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static void Box(Transform parent, string name, Vector3 localPosition, Vector3 size, string material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;

        // 見た目だけ。触る判定はルートのコライダーに任せる
        Object.DestroyImmediate(box.GetComponent<Collider>());

        var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialFolder}/{material}.mat");
        if (mat != null) box.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }
}
#endif
