#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>
/// 部屋の小物（缶・瓶・観葉植物・電飾・ピザ）の作り直しを確かめる道具（エディタ専用・実行時には動かない）。
/// ・同じ角度の写真（こたつ・棚・観葉植物・電飾）を撮る
/// ・「Roomies_RoomRestyle」の数（描画物・三角形・マテリアル・プリミティブ）を数える
/// ・作り直しの前後で、小物以外の部品が変わっていないかを比べるための一覧を書き出す
/// バッチ実行：-executeMethod RoomiesPropsCheck.BatchShots -propsOut &lt;フォルダ&gt; -propsTag before|after
/// </summary>
internal static class RoomiesPropsCheck
{
    const string GameRoom = "Assets/_Scenes/GameRoom.unity";
    const string MenuScene = "Assets/_Scenes/MainMenuSteam.unity";
    const string Root = "Roomies_RoomRestyle";
    const float F = 3.2725f;

    // 名前・カメラの位置・見る点（ワールド座標）
    static readonly (string name, Vector3 eye, Vector3 target, float fov)[] Shots =
    {
        ("kotatsu", new Vector3(17.75f, F + 1.15f, 18.55f), new Vector3(16.45f, F + .45f, 19.95f), 50f),
        ("shelf_plant", new Vector3(19.7f, F + 1.45f, 17.6f), new Vector3(22.3f, F + .85f, 18.75f), 55f),
        ("fairy_lights", new Vector3(15.0f, F + 1.75f, 16.9f), new Vector3(13.25f, F + 2.2f, 18.7f), 50f),
        ("overview", new Vector3(20.6f, F + 1.73f, 15.6f), new Vector3(16.9f, F + .55f, 20.0f), 60f),
    };

    // 作り直しの対象（親の名前）
    internal static readonly string[] PropGroups = { "Can", "Bottle", "Pizza box", "House plant", "Fairy lights", "Pendant lamp", "Kitchen lamp" };

    static string Arg(string key, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        int i = Array.IndexOf(args, key);
        return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
    }

    /// <summary>バッチ用：写真・数・一覧を書き出す（シーンは保存しない）。</summary>
    internal static void BatchShots()
    {
        string dir = Arg("-propsOut", "Temp/PropsCheck"), tag = Arg("-propsTag", "check");
        Directory.CreateDirectory(dir);
        var scene = EditorSceneManager.OpenScene(GameRoom, OpenSceneMode.Single);
        foreach (var s in Shots) Capture(s.eye, s.target, s.fov, Path.Combine(dir, $"{tag}_{s.name}.jpg"));
        File.WriteAllText(Path.Combine(dir, $"{tag}_stats.txt"), Stats(scene));
        File.WriteAllText(Path.Combine(dir, $"{tag}_others.txt"), OthersListing(scene));
        var menu = EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        File.WriteAllText(Path.Combine(dir, $"{tag}_stats_title.txt"), Stats(menu));
        File.WriteAllText(Path.Combine(dir, $"{tag}_others_title.txt"), OthersListing(menu));
        Debug.Log("[Props Check] wrote " + dir);
    }

    /// <summary>バッチ用：GameRoom とタイトルの部屋を作り直す（Build Room / Build Title Room と同じ）。</summary>
    internal static void BatchRebuild()
    {
        // 観葉植物の葉を板＋アルファで比べるとき：-plantCards true
        RoomiesRoomRestyleBuilder.PlantCardLeaves = Arg("-plantCards", "false") == "true";
        EditorSceneManager.OpenScene(GameRoom, OpenSceneMode.Single);
        RoomiesRoomRestyleBuilder.Build();
        EditorSceneManager.OpenScene(MenuScene, OpenSceneMode.Single);
        RoomiesRoomRestyleBuilder.BuildTitle();
        AssetDatabase.SaveAssets();
        Debug.Log("[Props Check] rebuilt room and title room (plant cards: " + RoomiesRoomRestyleBuilder.PlantCardLeaves + ")");
        RoomiesRoomRestyleBuilder.PlantCardLeaves = false;
    }

    static void Capture(Vector3 eye, Vector3 target, float fov, string path)
    {
        const int W = 1600, H = 900;
        var go = new GameObject("PropsCheckCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = go.AddComponent<Camera>();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true; data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        go.transform.position = eye; go.transform.rotation = Quaternion.LookRotation(target - eye);
        cam.fieldOfView = fov; cam.nearClipPlane = .05f; cam.farClipPlane = 400f;
        var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render(); cam.Render();
        var prev = RenderTexture.active; RenderTexture.active = rt;
        var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        File.WriteAllBytes(path, tex.EncodeToJPG(88));
        cam.targetTexture = null;
        Object.DestroyImmediate(tex); Object.DestroyImmediate(rt); Object.DestroyImmediate(go);
    }

    static bool InProp(Transform t, Transform root)
    {
        for (var p = t; p != null && p != root; p = p.parent) if (PropGroups.Contains(p.name)) return true;
        return false;
    }

    static int Tris(Mesh m) { int n = 0; if (m == null) return 0; for (int i = 0; i < m.subMeshCount; i++) n += (int)m.GetIndexCount(i) / 3; return n; }

    static bool IsPrimitiveLike(Mesh m)
    {
        if (m == null) return false;
        string n = m.name;
        // 組み込みの箱・円柱・球と、それを丸めたもの（Bevel）、角丸の箱・回転体・管・三角柱（部品を積んで作る形）
        return n == "Cube" || n == "Cylinder" || n == "Sphere" || n.StartsWith("bev") || n.StartsWith("rbox_") || n.StartsWith("lathe_") || n.StartsWith("tube_") || n.StartsWith("prism_");
    }

    static string Stats(UnityEngine.SceneManagement.Scene scene)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Root);
        if (root == null) return "no " + Root;
        var sb = new StringBuilder();
        void Line(string label, IEnumerable<MeshRenderer> rs)
        {
            var list = rs.ToList();
            int tris = list.Sum(r => Tris(r.GetComponent<MeshFilter>()?.sharedMesh));
            int prim = list.Count(r => IsPrimitiveLike(r.GetComponent<MeshFilter>()?.sharedMesh));
            int draws = list.Sum(r => r.sharedMaterials.Length);
            int mats = list.SelectMany(r => r.sharedMaterials).Where(m => m != null).Distinct().Count();
            int meshes = list.Select(r => r.GetComponent<MeshFilter>()?.sharedMesh).Where(m => m != null).Distinct().Count();
            sb.AppendLine($"{label}\trenderers={list.Count}\tsubmeshDraws={draws}\ttriangles={tris}\tprimitiveParts={prim}\tmaterials={mats}\tmeshes={meshes}");
        }
        var all = root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToList();
        Line("ALL", all);
        Line("PROPS", all.Where(r => InProp(r.transform, root.transform)));
        foreach (var g in PropGroups)
            Line("  " + g, all.Where(r => { for (var p = r.transform; p != null && p != root.transform; p = p.parent) if (p.name == g) return true; return false; }));
        sb.AppendLine($"lights={root.GetComponentsInChildren<Light>(true).Length}");
        return sb.ToString();
    }

    /// <summary>小物以外の部品の一覧（並べ替えて比べる）。位置は mm、角度は 0.1 度に丸める。</summary>
    static string OthersListing(UnityEngine.SceneManagement.Scene scene)
    {
        var root = scene.GetRootGameObjects().FirstOrDefault(g => g.name == Root);
        if (root == null) return "no " + Root;
        string PathOf(Transform t) { var s = t.name; for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s; return s; }
        string R(Vector3 v, float k) => $"{Mathf.Round(v.x * k) / k},{Mathf.Round(v.y * k) / k},{Mathf.Round(v.z * k) / k}";
        var lines = new List<string>();
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (InProp(t, root.transform)) continue;
            var mf = t.GetComponent<MeshFilter>(); var mr = t.GetComponent<MeshRenderer>(); var light = t.GetComponent<Light>(); var col = t.GetComponent<Collider>();
            string extra = "";
            if (mf != null) extra += " mesh=" + (mf.sharedMesh ? mf.sharedMesh.name : "null");
            if (mr != null) extra += " mats=" + string.Join("+", mr.sharedMaterials.Select(m => m ? m.name : "null")) + " shadow=" + mr.shadowCastingMode;
            if (light != null) extra += $" light={light.intensity:F2}/{light.range:F2}/{light.color}";
            if (col != null) extra += " collider=" + col.GetType().Name + (col is BoxCollider b ? R(b.center, 1000) + "/" + R(b.size, 1000) : "");
            lines.Add($"{PathOf(t)} p={R(t.position, 1000)} r={R(t.eulerAngles, 10)} s={R(t.lossyScale, 1000)} on={t.gameObject.activeSelf}{extra}");
        }
        // ルートの外（シーンの他の物）は数だけ
        int outside = scene.GetRootGameObjects().Where(g => g != root).Sum(g => g.GetComponentsInChildren<Transform>(true).Length);
        lines.Sort(StringComparer.Ordinal);
        lines.Add("#outside transforms=" + outside);
        return string.Join("\n", lines);
    }
}
#endif
