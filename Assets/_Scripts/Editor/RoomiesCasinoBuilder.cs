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
///  2 Build Gimmick Prefabs … エレベーターと店の人のプレハブ（RoomiesCasinoBuilder.Gimmicks.cs）と、
///                            上の階の台の Variant（数値だけ違う。RoomiesCasinoBuilder.Rates.cs）を作る
///  3 Place In Open Scene   … 開いている GameRoom に、鍵の持ち主（CasinoAccess）・エレベーター・店の人 2 人・上の階の台を置き、
///                            「Roomies_Casino」の階ごとの表示の切り替えをつなぐ
///  Check (Play Mode)       … 鍵を買う・挿す・上がる・降りるを通しで確かめる（RoomiesCasinoBuilder.Check.cs）
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
        PlaceGimmicks(scene);
        var looks = PlaceTables(scene);
        PlaceFloorVisibility(root, env.transform, looks);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Casino] placed in " + scene.name);
    }

    /// <summary>数を数える（確認用。何も変えない）：建物のグループとギミックのプレハブの、描く物・三角形・素材の数を Temp/RoomiesCasino/stats.txt に書く。</summary>
    [MenuItem("Roomies/Casino/Stats")]
    static void Stats()
    {
        var sb = new System.Text.StringBuilder();
        var allMats = new System.Collections.Generic.HashSet<Material>();
        int totalTris = 0, totalRenderers = 0;
        void Count(string label, GameObject root)
        {
            int tris = 0, renderers = 0; var mats = new System.Collections.Generic.HashSet<Material>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || mf.sharedMesh == null) continue;
                if (mf.GetComponent<TMPro.TMP_Text>() != null) continue;   // 文字は別
                tris += (int)(mf.sharedMesh.triangles.Length / 3); renderers++;
                foreach (var m in r.sharedMaterials) if (m != null) { mats.Add(m); allMats.Add(m); }
            }
            int texts = root.GetComponentsInChildren<TMPro.TMP_Text>(true).Length, lights = root.GetComponentsInChildren<Light>(true).Length;
            sb.AppendLine($"{label,-28} renderers {renderers,4}  tris {tris,7}  materials {mats.Count,2}  texts {texts,2}  lights {lights,2}");
            totalTris += tris; totalRenderers += renderers;
        }
        var town = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab");
        foreach (string g in new[] { RoomiesTownRebuild.CasinoGroup, RoomiesTownRebuild.CasinoGroup2F, RoomiesTownRebuild.CasinoGroup3F, RoomiesTownRebuild.CasinoWindows2F, RoomiesTownRebuild.CasinoWindows3F })
        {
            var t = town.transform.Find(g);
            if (t != null) Count(g, t.gameObject);
        }
        sb.AppendLine($"{"building total",-28} renderers {totalRenderers,4}  tris {totalTris,7}  materials {allMats.Count,2}");
        sb.AppendLine("materials: " + string.Join(", ", allMats.Select(m => m.name).OrderBy(n => n)));
        sb.AppendLine();
        // 描く範囲（bounds）の確認：メッシュに書いてある範囲が、頂点から計算し直した範囲と同じか。
        // ずれていると、カメラが近いときに「画面の外」と判定されて、描かれなくなる。距離で消す仕組み（LODGroup）が付いていないかも数える
        int boundsBad = 0, boundsChecked = 0, lodGroups = 0, shadowTris = 0;
        foreach (string g in new[] { RoomiesTownRebuild.CasinoGroup, RoomiesTownRebuild.CasinoGroup2F, RoomiesTownRebuild.CasinoGroup3F, RoomiesTownRebuild.CasinoWindows2F, RoomiesTownRebuild.CasinoWindows3F })
        {
            var t = town.transform.Find(g);
            if (t == null) continue;
            lodGroups += t.GetComponentsInChildren<LODGroup>(true).Length;
            foreach (var mf in t.GetComponentsInChildren<MeshFilter>(true))
            {
                var r = mf.GetComponent<MeshRenderer>();
                if (r == null || mf.sharedMesh == null || mf.GetComponent<TMPro.TMP_Text>() != null) continue;
                var verts = mf.sharedMesh.vertices;
                if (verts.Length == 0) continue;
                var real = new Bounds(verts[0], Vector3.zero);
                foreach (var v in verts) real.Encapsulate(v);
                var stored = mf.sharedMesh.bounds;
                boundsChecked++;
                bool bad = (stored.min - real.min).magnitude > .001f || (stored.max - real.max).magnitude > .001f;
                if (bad) { boundsBad++; sb.AppendLine($"BOUNDS NG  {g}/{mf.name}: stored {stored.min}..{stored.max}, real {real.min}..{real.max}"); }
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off) shadowTris += mf.sharedMesh.triangles.Length / 3;
                if (mf.transform.parent != null && mf.transform.parent.name == "Combined")
                {
                    var wb = r.bounds;
                    sb.AppendLine($"  {g}/{mf.name,-44} tris {mf.sharedMesh.triangles.Length / 3,6}  shadow {(r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off ? "on " : "off")}  x {wb.min.x:F1}..{wb.max.x:F1}  y {wb.min.y:F1}..{wb.max.y:F1}  z {wb.min.z:F1}..{wb.max.z:F1}");
                }
            }
        }
        sb.AppendLine($"bounds: checked {boundsChecked}, wrong {boundsBad}.  LODGroups in the building: {lodGroups}.  shadow-casting tris: {shadowTris}");
        sb.AppendLine();
        // コンビニ（建物のグループと、売り場・自動ドア・店の人のプレハブ）
        var storeGroup = town.transform.Find(RoomiesTownRebuild.StoreGroup);
        if (storeGroup != null) Count(RoomiesTownRebuild.StoreGroup, storeGroup.gameObject);
        foreach (string path in new[] { SlotBasePath, BlackjackBasePath, RoulettePath, ElevatorPath, ClerkPath, StoreShopPath, StoreDoorPath, StoreClerkPath })
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (p != null) Count(System.IO.Path.GetFileNameWithoutExtension(path) + ".prefab", p);
        }
        System.IO.Directory.CreateDirectory("Temp/RoomiesCasino");
        System.IO.File.WriteAllText("Temp/RoomiesCasino/stats.txt", sb.ToString());
        Debug.Log("[Casino] stats written: Temp/RoomiesCasino/stats.txt");
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

    // 店の人の立つ所（1 階は両替所の中、2 階はその真上のカウンターの中）。正面は通りの側（+Z）
    static readonly Vector3 ClerkXZ = new Vector3(14.6f, 0f, -17.95f);

    /// <summary>通信する物（鍵の持ち主・エレベーター・店の人）をシーンの直下に置く。</summary>
    static void PlaceGimmicks(Scene scene)
    {
        if (AssetDatabase.LoadAssetAtPath<GameObject>(ElevatorPath) == null ||
            AssetDatabase.LoadAssetAtPath<GameObject>(ClerkPath) == null) BuildGimmickPrefabs();

        // 鍵の持ち主（お金の係と同じく、シーンに 1 つ置く物）
        var access = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "CasinoAccess");
        if (access == null)
        {
            access = new GameObject("CasinoAccess");
            SceneManager.MoveGameObjectToScene(access, scene);
        }
        if (access.GetComponent<Unity.Netcode.NetworkObject>() == null) access.AddComponent<Unity.Netcode.NetworkObject>();
        if (access.GetComponent<CasinoAccess>() == null) access.AddComponent<CasinoAccess>();
        EditorUtility.SetDirty(access);

        PlacePrefab(scene, ElevatorPath, "CasinoElevator", ElevatorPosition, 0f);

        // 1 階は 2 階の鍵、2 階は 3 階の鍵を売る。3 階の店の人は、鍵を売らずに話すだけ（sells = 0）
        foreach (var (name, floor, sells) in new[] { ("CasinoClerk_1F", 1, 2), ("CasinoClerk_2F", 2, 3), ("CasinoClerk_3F", 3, 0) })
        {
            var p = ClerkXZ; p.y = RoomiesTownRebuild.KFloorY(floor) + RoomiesTownRebuild.KClerkStep;   // カウンターの中の踏み台の上
            var go = PlacePrefab(scene, ClerkPath, name, p, 0f);
            var so = new SerializedObject(go.GetComponent<CasinoClerk>());
            so.FindProperty("sellsFloor").intValue = sells;
            if (floor == 3)
            {
                so.FindProperty("clerkName").stringValue = "VIP ルームの店員";
                SetStrings(so.FindProperty("smallTalk"), new[]
                {
                    "VIP ルームへようこそ。ここまで来る方は、そう多くありません。",
                    "この階の台は、お客さまに少しだけ有利ですよ。",
                    "チンチロは、ピンゾロが出ると 10 倍です。",
                    "ルーレットで 0 が出ても、赤と黒の賭け金はお返しします。",
                    "夜景がきれいでしょう。負けたときは、窓の外をどうぞ。",
                    "ブラックジャックで 21 がそろうと、3 倍でお返しします。",
                    "勝ちすぎて、下の階の店員ににらまれませんように。",
                    "お飲み物は、気持ちだけお出ししています。",
                });
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    static void SetStrings(SerializedProperty array, string[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).stringValue = values[i];
    }

    /// <summary>プレハブをシーンの直下に置く（もうあれば位置と向きだけ戻す）。</summary>
    internal static GameObject PlacePrefab(Scene scene, string prefabPath, string name, Vector3 position, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        var go = scene.GetRootGameObjects().FirstOrDefault(g => g.name == name);
        if (go != null && PrefabUtility.GetCorrespondingObjectFromSource(go) != prefab)
        {
            Object.DestroyImmediate(go);
            go = null;
        }
        if (go == null)
        {
            go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            go.name = name;
        }
        go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        go.transform.localScale = Vector3.one;
        // プレハブを作り直すと中の部品の ID が変わるので、もう無い部品への上書きをシーンから消す
        PrefabUtility.RemoveUnusedOverrides(new[] { go }, InteractionMode.AutomatedAction);
        EditorUtility.SetDirty(go);
        return go;
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
    static void PlaceFloorVisibility(Transform root, Transform env, System.Collections.Generic.Dictionary<int, System.Collections.Generic.List<GameObject>> looks)
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
            new[] { env.Find(RoomiesTownRebuild.CasinoGroup2F) }.Concat(looks[2].Select(g => g.transform)).ToArray(), env.Find(RoomiesTownRebuild.CasinoWindows2F));
        SetFloor(floors.GetArrayElementAtIndex(1), "3F", RoomiesTownRebuild.KF3 - .3f, RoomiesTownRebuild.KRoof + 3f,
            new[] { env.Find(RoomiesTownRebuild.CasinoGroup3F) }.Concat(looks[3].Select(g => g.transform)).ToArray(), env.Find(RoomiesTownRebuild.CasinoWindows3F));
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(comp);
    }

    static void SetFloor(SerializedProperty p, string label, float minY, float maxY, Transform[] here, Transform away)
    {
        // その階にいないときだけ出す物（外から見た窓の明かり）
        var awayList = p.FindPropertyRelative("onlyWhenAway");
        awayList.arraySize = away != null ? 1 : 0;
        if (away != null) awayList.GetArrayElementAtIndex(0).objectReferenceValue = away.gameObject;

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
