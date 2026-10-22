#if UNITY_EDITOR
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 寝る仕組み（ベッド・睡眠の管理・今日のおねがい）をシーンへ置くツール。
///
/// 規約（AGENTS.md）：
///  ・見た目は `Visual/Placeholder` の下。本アセットが来たら Placeholder を消して差し替える。
///  ・寝る位置は `Visual` の外の `Anchor_Sleep`。見た目を替えたら位置を合わせ直すだけでよい。
///  ・ルートの原点は足元、正面は +Z（ベッドは頭側が +Z）。触る判定はルートに1つ。
///  ・実行時には何も生成しない。
///
/// メニュー：
///  Roomies/Night/Rebuild Bed Prefab
///  Roomies/Night/Place Beds In Open Scene
/// </summary>
internal static class RoomiesSleepBuilder
{
    private const string PrefabFolder = "Assets/_Prefabs/Night";
    private const string BedPath = PrefabFolder + "/Bed.prefab";
    private const string MaterialFolder = "Assets/Resources/RoomiesArt/Materials";

    private const string RootName = "Beds";

    // 置き場所（家の共用スペース。床 y=3.3、壁は x 13.1〜22.8 / z 14.0〜23.5）
    // 西側の壁ぎわに、部屋の内側（+X）へ頭を向けて5台ならべる。
    private const float BedFloorY = 3.3f;
    private const float BedWallX = 14.3f;
    private const float BedFirstZ = 15.2f;
    private const float BedSpacingZ = 1.55f;
    private const int BedCount = 5;          // 最大5人ぶん
    private const float BedYaw = 90f;        // 頭（+Z）が +X を向く

    [MenuItem("Roomies/Night/Rebuild Bed Prefab")]
    private static void RebuildPrefab()
    {
        System.IO.Directory.CreateDirectory(PrefabFolder);
        BuildBed();
        AssetDatabase.SaveAssets();
    }

    [MenuItem("Roomies/Night/Place Beds In Open Scene")]
    private static void PlaceInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Night] Play Modeを抜けてから実行してください。");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BedPath);
        if (prefab == null)
        {
            Debug.LogError($"[Night] 先に Rebuild Bed Prefab を実行してください（{BedPath}）。");
            return;
        }

        GameObject root = GameObject.Find(RootName);
        if (root == null)
        {
            root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Place beds");
        }

        int placed = 0;
        for (int i = 0; i < BedCount; i++)
        {
            string name = $"Bed {i + 1}";
            if (root.transform.Find(name) != null) continue;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            Undo.RegisterCreatedObjectUndo(instance, "Place bed");
            instance.name = name;
            instance.transform.SetParent(root.transform, true);
            instance.transform.SetPositionAndRotation(
                new Vector3(BedWallX, BedFloorY, BedFirstZ + BedSpacingZ * i),
                Quaternion.Euler(0f, BedYaw, 0f));
            placed++;
        }

        EnsureManager<SleepManager>("SleepManager");
        EnsureManager<DailyRequestManager>("DailyRequestManager");

        EditorSceneManager.MarkSceneDirty(root.scene);
        Selection.activeGameObject = root;
        Debug.Log($"[Night] ベッドを{placed}台置き、睡眠と今日のおねがいの係も置きました" +
                  "（位置はシーンビューで調整してください。シーンの保存も必要です）");
    }

    /// <summary>サーバーが持つ係をシーンに1つ置く（すでにあれば触らない）。</summary>
    private static void EnsureManager<T>(string name) where T : NetworkBehaviour
    {
        if (Object.FindAnyObjectByType<T>() != null) return;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Place manager");
        go.AddComponent<NetworkObject>();
        Undo.AddComponent<T>(go);
    }

    // ---- ベッドのプレハブ ----

    private static void BuildBed()
    {
        var root = new GameObject("Bed");
        try
        {
            root.AddComponent<NetworkObject>();
            Bed bed = root.AddComponent<Bed>();

            // 触る判定（ルートに1つだけ）
            var trigger = root.AddComponent<BoxCollider>();
            trigger.center = new Vector3(0f, 0.35f, 0f);
            trigger.size = new Vector3(1.15f, 0.7f, 2.05f);

            Transform visual = Group(root.transform, "Visual");
            Transform placeholder = Group(visual, "Placeholder");

            // 枠・マットレス・枕・かけ布団（頭側が +Z）
            Box(placeholder, "Frame", new Vector3(0f, 0.16f, 0f), new Vector3(1.10f, 0.32f, 2.00f), "wood");
            Box(placeholder, "Mattress", new Vector3(0f, 0.40f, 0f), new Vector3(1.02f, 0.18f, 1.92f), "cream");
            Box(placeholder, "Blanket", new Vector3(0f, 0.50f, -0.30f), new Vector3(1.04f, 0.06f, 1.26f), "paper");
            Box(placeholder, "Pillow", new Vector3(0f, 0.53f, 0.74f), new Vector3(0.76f, 0.12f, 0.34f), "paper");
            Box(placeholder, "Headboard", new Vector3(0f, 0.55f, 1.02f), new Vector3(1.10f, 0.78f, 0.08f), "wood");

            // 寝る位置（見た目の外。差し替えたらここだけ合わせ直す）
            Transform anchor = Group(root.transform, "Anchor_Sleep");
            anchor.localPosition = new Vector3(0f, 0.55f, -0.10f);
            anchor.localRotation = Quaternion.identity;

            var serialized = new SerializedObject(bed);
            serialized.FindProperty("sleepAnchor").objectReferenceValue = anchor;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, BedPath);
            Debug.Log($"[Night] ベッドのプレハブを作りました → {BedPath}");
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
