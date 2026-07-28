#if UNITY_EDITOR
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 家具の同期に必要なPrefab登録とDayManager参照を自動修復する。
/// 手動でも Tools/Roomies/家具ネットワーク設定を修復 から再実行できる。
/// </summary>
[InitializeOnLoad]
internal static class FurnitureNetworkInstaller
{
    private const string PrefabPath =
        "Assets/_Prefabs/NetworkFurniture.prefab";
    private const string NetworkPrefabsPath =
        "Assets/DefaultNetworkPrefabs.asset";
    private const string GameRoomPath =
        "Assets/_Scenes/GameRoom.unity";

    static FurnitureNetworkInstaller()
    {
        EditorApplication.delayCall += EnsureInstalledAfterReload;
    }

    private static void EnsureInstalledAfterReload()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        EnsureInstalled(false);
    }

    [MenuItem("Tools/Roomies/家具ネットワーク設定を修復")]
    private static void RepairFromMenu()
    {
        EnsureInstalled(true);
    }

    private static void EnsureInstalled(bool showDialog)
    {
        GameObject prefab = EnsurePrefab();
        if (prefab == null) return;

        bool listChanged = EnsureNetworkPrefabRegistration(prefab);
        bool sceneChanged = EnsureDayManagerReference(prefab);
        AssetDatabase.SaveAssets();

        string result =
            $"[Furniture] NetworkFurniture設定確認完了 " +
            $"PrefabList更新={listChanged}, GameRoom更新={sceneChanged}";
        Debug.Log(result);

        if (showDialog)
            EditorUtility.DisplayDialog("家具ネットワーク設定", result, "OK");
    }

    private static GameObject EnsurePrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab != null) return prefab;

        GameObject root = new GameObject("NetworkFurniture");
        root.AddComponent<NetworkObject>();
        root.AddComponent<NetworkTransform>();
        root.AddComponent<NetworkFurniture>();

        prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.ImportAsset(PrefabPath, ImportAssetOptions.ForceUpdate);
        Debug.Log("[Furniture] NetworkFurniture.prefabを自動生成しました。");
        return prefab;
    }

    private static bool EnsureNetworkPrefabRegistration(GameObject prefab)
    {
        Object listAsset = AssetDatabase.LoadAssetAtPath<Object>(
            NetworkPrefabsPath);
        if (listAsset == null)
        {
            Debug.LogError(
                "[Furniture] DefaultNetworkPrefabs.assetが見つかりません。");
            return false;
        }

        SerializedObject serializedList = new SerializedObject(listAsset);
        SerializedProperty list = serializedList.FindProperty("List");
        if (list == null)
        {
            Debug.LogError("[Furniture] NetworkPrefabsList.Listが見つかりません。");
            return false;
        }

        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty entry = list.GetArrayElementAtIndex(i);
            SerializedProperty prefabProperty =
                entry.FindPropertyRelative("Prefab");
            if (prefabProperty != null &&
                prefabProperty.objectReferenceValue == prefab)
                return false;
        }

        int newIndex = list.arraySize;
        list.InsertArrayElementAtIndex(newIndex);
        SerializedProperty newEntry = list.GetArrayElementAtIndex(newIndex);
        SetObjectReference(newEntry, "Prefab", prefab);
        SetObjectReference(newEntry, "SourcePrefabToOverride", null);
        SetObjectReference(newEntry, "OverridingTargetPrefab", null);

        SerializedProperty overrideProperty =
            newEntry.FindPropertyRelative("Override");
        if (overrideProperty != null) overrideProperty.intValue = 0;

        SerializedProperty sourceHash =
            newEntry.FindPropertyRelative("SourceHashToOverride");
        if (sourceHash != null) sourceHash.longValue = 0;

        serializedList.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(listAsset);
        return true;
    }

    private static bool EnsureDayManagerReference(GameObject prefab)
    {
        Scene scene = SceneManager.GetSceneByPath(GameRoomPath);
        bool openedHere = !scene.IsValid() || !scene.isLoaded;

        if (openedHere)
            scene = EditorSceneManager.OpenScene(
                GameRoomPath,
                OpenSceneMode.Additive);

        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogError("[Furniture] GameRoomシーンを開けませんでした。");
            return false;
        }

        if (!openedHere && scene.isDirty)
        {
            Debug.LogWarning(
                "[Furniture] GameRoomに未保存変更があるため、" +
                "DayManager参照の自動修復を保留しました。");
            return false;
        }

        DayManager dayManager = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            dayManager = root.GetComponentInChildren<DayManager>(true);
            if (dayManager != null) break;
        }

        if (dayManager == null)
        {
            Debug.LogError("[Furniture] GameRoomのDayManagerが見つかりません。");
            if (openedHere) EditorSceneManager.CloseScene(scene, true);
            return false;
        }

        SerializedObject serializedDayManager =
            new SerializedObject(dayManager);
        SerializedProperty prefabProperty =
            serializedDayManager.FindProperty("networkFurniturePrefab");

        bool changed = prefabProperty != null &&
                       prefabProperty.objectReferenceValue != prefab;
        if (changed)
        {
            prefabProperty.objectReferenceValue = prefab;
            serializedDayManager.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(dayManager);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        if (openedHere) EditorSceneManager.CloseScene(scene, true);
        return changed;
    }

    private static void SetObjectReference(
        SerializedProperty parent,
        string relativeName,
        Object value)
    {
        SerializedProperty property =
            parent.FindPropertyRelative(relativeName);
        if (property != null) property.objectReferenceValue = value;
    }
}
#endif
