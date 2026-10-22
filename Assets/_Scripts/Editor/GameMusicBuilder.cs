#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// BGM再生口のプレハブを作り、シーンへ置くツール。
///
/// 規約（AGENTS.md「実行時に作らない」）：
///  ・AudioSourceも曲の割り当ても、ここでプレハブに作り込む。実行時には何も生成しない。
///  ・曲を差し替えるときはプレハブの `Menu Track` / `Day Track` / `Night Track` を入れ替える。
///
/// メニュー：
///  Roomies/Audio/Rebuild Music Prefab        … プレハブを作り直す（曲も割り当てる）
///  Roomies/Audio/Place Music In Open Scene   … 開いているシーンへ1つ置く
/// </summary>
internal static class GameMusicBuilder
{
    private const string PrefabFolder = "Assets/_Prefabs/Audio";
    private const string PrefabPath = PrefabFolder + "/GameMusic.prefab";
    private const string MusicFolder = "Assets/_Audio/Music";

    [MenuItem("Roomies/Audio/Rebuild Music Prefab")]
    private static void RebuildPrefab()
    {
        System.IO.Directory.CreateDirectory(PrefabFolder);

        var root = new GameObject("GameMusic");
        try
        {
            GameMusic music = root.AddComponent<GameMusic>();

            AudioSource a = CreateSource(root.transform, "Source A");
            AudioSource b = CreateSource(root.transform, "Source B");

            var serialized = new SerializedObject(music);
            Assign(serialized, "sourceA", a);
            Assign(serialized, "sourceB", b);
            Assign(serialized, "menuTrack", LoadClip("menu"));
            Assign(serialized, "dayTrack", LoadClip("day"));
            Assign(serialized, "nightTrack", LoadClip("night"));
            serialized.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Audio] BGMプレハブを作りました → {PrefabPath}");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    [MenuItem("Roomies/Audio/Place Music In Open Scene")]
    private static void PlaceInScene()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            Debug.LogError("[Audio] Play Modeを抜けてから実行してください。");
            return;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null)
        {
            Debug.LogError("[Audio] 先に Roomies/Audio/Rebuild Music Prefab を実行してください。");
            return;
        }

        if (Object.FindAnyObjectByType<GameMusic>() != null)
        {
            Debug.Log("[Audio] このシーンには既にGameMusicがあります。");
            return;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, "Place music player");
        instance.transform.position = Vector3.zero;

        EditorSceneManager.MarkSceneDirty(instance.scene);
        Selection.activeGameObject = instance;
        Debug.Log($"[Audio] {instance.scene.name} にBGM再生口を置きました（シーンを保存してください）");
    }

    private static AudioSource CreateSource(Transform parent, string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        AudioSource source = go.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;   // 2D（どこにいても同じ音量）
        source.volume = 0f;
        source.priority = 0;        // BGMは効果音に押し出されないようにする
        return source;
    }

    private static AudioClip LoadClip(string id)
    {
        foreach (string extension in new[] { "mp3", "wav", "ogg" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>($"{MusicFolder}/{id}.{extension}");
            if (clip != null) return clip;
        }

        Debug.LogWarning($"[Audio] 曲が見つかりません: {MusicFolder}/{id}.*");
        return null;
    }

    private static void Assign(SerializedObject serialized, string field, Object value)
    {
        SerializedProperty property = serialized.FindProperty(field);
        if (property == null)
        {
            Debug.LogWarning($"[Audio] {field} が見つかりません。");
            return;
        }
        property.objectReferenceValue = value;
    }
}
#endif
