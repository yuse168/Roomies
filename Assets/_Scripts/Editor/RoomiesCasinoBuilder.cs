#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// カジノ（3 階建て）の配置ツール。実行時には動かない。何度実行しても同じ結果。
///
///  1 Build Building        … 建物を作り直す（Roomies/Art/Rebuild/3 Casino と同じ。町のプレハブの Town casino / 2F / 3F）
///  3 Place In Open Scene   … 開いている GameRoom に「Roomies_Casino」を置き、階ごとの表示の切り替えをつなぐ
///
/// 置いた物の位置や数値は、シーンビューとインスペクターで直してよい（もう一度実行すると、ここの値に戻る）。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    internal const string SceneRootName = "Roomies_Casino";
    const string EnvironmentRoot = "Roomies_EnvironmentArt";

    [MenuItem("Roomies/Casino/1 Build Building")]
    static void BuildBuilding() => RoomiesTownRebuild.BuildCasino();

    [MenuItem("Roomies/Casino/3 Place In Open Scene")]
    internal static void PlaceInOpenScene()
    {
        Scene scene = GameRoomScene();
        if (!scene.IsValid()) { Debug.LogWarning("[Casino] GameRoom を開いて実行する"); return; }

        var env = scene.GetRootGameObjects().FirstOrDefault(g => g.name == EnvironmentRoot);
        if (env == null) { Debug.LogWarning("[Casino] " + EnvironmentRoot + " がない"); return; }

        Transform root = FindOrCreateRoot(scene, SceneRootName);
        PlaceFloorVisibility(root, env.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Casino] placed in " + scene.name);
    }

    internal static Scene GameRoomScene()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (s.isLoaded && s.name == "GameRoom") return s;
        }
        return default;
    }

    static Transform FindOrCreateRoot(Scene scene, string name)
    {
        var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (go == null)
        {
            go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
        }
        go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        return go.transform;
    }

    static Transform FindOrCreateChild(Transform parent, string name)
    {
        Transform t = parent.Find(name);
        if (t == null)
        {
            t = new GameObject(name).transform;
            t.SetParent(parent, false);
        }
        return t;
    }

    /// <summary>階ごとの表示の切り替え：2 階・3 階の中身（町のプレハブのグループ）を、その階にいるときだけ出す。</summary>
    static void PlaceFloorVisibility(Transform root, Transform env)
    {
        var vis = FindOrCreateChild(root, "FloorVisibility").gameObject;
        var comp = vis.GetComponent<CasinoFloorVisibility>();
        if (comp == null) comp = vis.AddComponent<CasinoFloorVisibility>();

        var so = new SerializedObject(comp);
        so.FindProperty("minXZ").vector2Value = new Vector2(-1.2f, -19.6f);
        so.FindProperty("maxXZ").vector2Value = new Vector2(16.8f, -7.8f);
        var floors = so.FindProperty("floors");
        floors.arraySize = 2;
        SetFloor(floors.GetArrayElementAtIndex(0), "2F", RoomiesTownRebuild.KF2 - .3f, RoomiesTownRebuild.KF3 - .3f,
            env.Find(RoomiesTownRebuild.CasinoGroup2F), root.Find("Floor_2F"));
        SetFloor(floors.GetArrayElementAtIndex(1), "3F", RoomiesTownRebuild.KF3 - .3f, RoomiesTownRebuild.KRoof + 3f,
            env.Find(RoomiesTownRebuild.CasinoGroup3F), root.Find("Floor_3F"));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(comp);
    }

    static void SetFloor(SerializedProperty p, string label, float minY, float maxY, params Transform[] here)
    {
        p.FindPropertyRelative("label").stringValue = label;
        p.FindPropertyRelative("minY").floatValue = minY;
        p.FindPropertyRelative("maxY").floatValue = maxY;
        var list = p.FindPropertyRelative("onlyWhenHere");
        var objects = here.Where(t => t != null).Select(t => t.gameObject).ToArray();
        list.arraySize = objects.Length;
        for (int i = 0; i < objects.Length; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = objects[i];
    }
}
#endif
