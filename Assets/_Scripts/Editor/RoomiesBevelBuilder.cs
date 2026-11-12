#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 角の丸み（ベベル）を部品ごとに強・中・弱で付ける（2026/11/13）。何度実行しても同じ結果。
/// ・部品の強さは下の表（親の名前/部品の名前 の正規表現）で決める。同じ家具の中でも、天板は中・脚は弱、のように分ける。
/// ・丸みの幅は「部品の実寸の短い辺 × 比率」。強いほど比率が大きい。上限もある（丸めすぎない）。
/// ・ただの箱（Cube）と角丸の箱（rbox）は角丸の箱に、円柱（Cylinder）はふちの丸い円柱にする。
///   Transform（位置・大きさ・向き）はそのまま、メッシュだけ差し替える。当たり判定（Collider）は触らない。
/// ・メッシュは Assets/_Art/Restyle/BevelMeshes.asset にまとめて保存する。
/// </summary>
internal static class RoomiesBevelBuilder
{
    internal enum Tier { None, Weak, Medium, Strong }

    const string MeshAsset = "Assets/_Art/Restyle/BevelMeshes.asset";
    // 丸みの分割数（強いほど細かい。弱は面取り1段）。三角形を増やしすぎないため
    static readonly Dictionary<Tier, int> Segments = new Dictionary<Tier, int> { [Tier.Strong] = 3, [Tier.Medium] = 2, [Tier.Weak] = 1 };
    // 比率（短い辺に対して）と上限（m）
    static readonly Dictionary<Tier, (float ratio, float max)> TierSize = new Dictionary<Tier, (float, float)>
    {
        [Tier.Strong] = (.32f, .09f), [Tier.Medium] = (.09f, .035f), [Tier.Weak] = (.025f, .008f),
    };

    // 親の名前/部品の名前 に対する表（上から順に最初に当たったもの）。当たらない物は：箱・円柱は「弱」、角丸の箱は元のまま
    static readonly (Regex path, Tier tier)[] Rules =
    {
        // ---- 柔らかい物（強）
        (R(@"Sofa/(Seat cushion|Back cushion|Pillow|Throw|Throw fall)$"), Tier.Strong),
        (R(@"(Bed|Bed look[^/]*)/(.*/)?(Duvet|Duvet side|Duvet fold|Pillow|Mattress)$"), Tier.Strong),
        (R(@"Office chair/(Seat|Back|Arm rest|Hoodie|Hoodie fold)$"), Tier.Strong),
        (R(@"Curtain/Fold$"), Tier.Strong),
        (R(@"Laundry heap/"), Tier.Strong),
        (R(@"(Floor clutter|Laundry heap)/Sock$"), Tier.Strong),
        (R(@"Entry shoes/Sneaker$"), Tier.Strong),
        (R(@"Game controller/(Body|Grip)$"), Tier.Strong),
        (R(@"Chip bag/Bag$"), Tier.Strong),
        (R(@"PC desk/Mouse$"), Tier.Strong),
        (R(@"Slot toy/Button$"), Tier.Strong),
        (R(@"Pivot_Door/Visual/Restyle/Magnet$"), Tier.Strong),
        // ---- 家具・家電の本体（中）
        (R(@"Sofa/(Arm|Back|Base)$"), Tier.Medium),
        (R(@"(Bed|Bed look[^/]*)/(.*/)?(Headboard|Footboard|Frame)$"), Tier.Medium),
        (R(@"Low table/Top$"), Tier.Medium),
        (R(@"PC desk/(Top|Drawer unit|Drawer front|PC tower|Monitor|Keyboard)$"), Tier.Medium),
        (R(@"Shelf/(Board|Box|Storage bin|Shoe box|Toolbox|Game console box|Box on top)$"), Tier.Medium),
        (R(@"Kitchen counter/(Worktop|Cabinet|Door)$"), Tier.Medium),
        (R(@"Kitchen corner/Drink crate$"), Tier.Medium),
        (R(@"Microwave/Body$"), Tier.Medium),
        (R(@"TV corner/(Stand|TV|Console)$"), Tier.Medium),
        (R(@"Entry shoes/Sneaker sole$"), Tier.Medium),
        (R(@"Snack box/Box$"), Tier.Medium),
        (R(@"Cardboard stack/(Big box|Small box)$"), Tier.Medium),
        (R(@"Pizza box/Box$"), Tier.Medium),
        (R(@"Floor fan/(Motor|Base)$"), Tier.Medium),
        (R(@"Aquarium/Base$"), Tier.Medium),
        (R(@"Basket hoop/Backboard$"), Tier.Medium),
        (R(@"Slot toy/(Body|Top sign)$"), Tier.Medium),
        (R(@"(Fridge look|Visual/Restyle|Pivot_Door/Visual/Restyle)/(Body|Fridge door|Freezer door|Box on top|Tin on top)$"), Tier.Medium),
        (R(@"CharacterCloset/Visual/(Body|LeftDoor|RightDoor)$"), Tier.Medium),
        (R(@"BlackjackTable/Visual/(TableTop|BackRail|FrontRail|LeftRail|RightRail)$"), Tier.Medium),
        (R(@"SlotMachine[^/]*/Visual$"), Tier.Medium),
        // ---- 薄い物・印刷・細かい飾りは丸めない（なし）
        (R(@"/(Label|Lid print|Print|Pages|Cover photo|Text lines|Left header|Right header|Left stamp|Right panel|Bin label|Paper scrap|Sticky note|Target|Flight|Shaft|Bull|Bullseye|Tape|Arrow mark|Gravel|Water|Lid light|Console light|PC light|Pepperoni|Cheese|Crust|Hanging wire|Power cable|Cord|Stalk|Soil|Ceiling liner|Flyer|Flyer line|Flyer print|Note|Photo|Photo print|Coupon|Door seam|BLACKJACK_Plaque|Mouse pad|Stand shelf gap|Seal|Band)$"), Tier.None),
        // ---- 細い金属・柱・脚・薄い板（弱）は既定の「弱」
    };

    static Regex R(string pattern) => new Regex(pattern, RegexOptions.Compiled);

    // ================================================================ メニュー
    [MenuItem("Roomies/Art/Restyle/Bevel/Apply To Room And Title")]
    internal static void ApplyToRoom()
    {
        foreach (var scene in OpenRoomScenes())
        {
            var root = scene.GetRootGameObjects().First(g => g.name == "Roomies_RoomRestyle");
            int n = ApplyUnder(root.transform, root.name, skipPrefabInstances: true);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Bevel] {scene.name}: {n} parts");
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>ギミック付きプレハブの Visual の中だけ（ルート・当たり判定・スクリプトは触らない）。</summary>
    internal static readonly string[] GimmickPrefabs =
    {
        "Assets/_Prefabs/Night/Bed.prefab", "Assets/_Prefabs/Night/NightFridge.prefab",
        "Assets/_Prefabs/Facilities/CharacterCloset.prefab", "Assets/_Prefabs/BlackjackTable.prefab", "Assets/_Prefabs/Slot.prefab",
    };

    [MenuItem("Roomies/Art/Restyle/Bevel/Apply To Gimmick Prefabs")]
    internal static void ApplyToPrefabs()
    {
        foreach (var path in GimmickPrefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int n = 0;
                foreach (var visual in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Visual"))
                    n += ApplyUnder(visual, root.name + "/" + RelPath(visual, root.transform), false);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Bevel] {path}: {n} parts");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>古い形式（分割数の入っていない名前）のメッシュを消す。部屋とタイトルに付け直したあとに使う。</summary>
    [MenuItem("Roomies/Art/Restyle/Bevel/Prune Old Meshes")]
    internal static void PruneOld()
    {
        int n = 0;
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(MeshAsset).OfType<Mesh>().ToArray())
            if (!m.name.Contains("_s") && !AssetDatabase.IsMainAsset(m)) { Object.DestroyImmediate(m, true); n++; }
        cache = null;
        AssetDatabase.SaveAssets();
        Debug.Log($"[Bevel] pruned {n} meshes");
    }

    // ================================================================ 町（2026/11/13）
    // 町のプレハブ（RoomiesNeighborhood）の部品。遠景（Roomies_Backdrop）は遠くて見えないので丸めない。鉱山は触らない
    const string NeighborhoodPrefab = "Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab";
    static Regex Ri(string p) => new Regex(p, RegexOptions.IgnoreCase | RegexOptions.Compiled);
    static readonly (Regex name, Tier tier)[] TownRules =
    {
        // 大きな平面・ガラス・線・文字・薄い物は丸めない
        (Ri(@"joint|seam|stripe|dash|crosswalk|route|lane|road|ground|grass|lawn|sidewalk|plane|highlight|text|label|plate|decal|line|mark|snow|band|floor|ceiling|liner|plot|soil|glazing|^window$|lower window|water|paper|poster"), Tier.None),
        // 柔らかい物（強）
        (Ri(@"cushion|pillow|bag|sack|mattress"), Tier.Strong),
        // 木の家具・看板・箱・扉（中）
        (Ri(@"bench|sign|board|crate|vending|counter|table|planter|\bbin\b|box|door|kiosk|pedestal|seat|machine|mailbox|fascia|locker|cabinet|shutter|button"), Tier.Medium),
        // 柱・梁・窓枠・手すり・階段・屋根・壁などは既定の「弱」
    };

    static Tier TownTierFor(string name) { foreach (var r in TownRules) if (r.name.IsMatch(name)) return r.tier; return Tier.Weak; }

    [MenuItem("Roomies/Art/Restyle/Bevel/Apply To Town")]
    internal static void ApplyToTown()
    {
        var meshes = LoadMeshAsset();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        int n = 0;
        try
        {
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || PrefabUtility.IsPartOfPrefabInstance(mf) || RoomiesMaterialFinishBuilder.InMining(mf.transform)) continue;
                var shape = ShapeOf(mf.sharedMesh); if (shape == Shape.Other) continue;
                if (mf.sharedMesh.name.StartsWith("rbox_")) continue;   // 角丸の箱（部屋の物）はそのまま
                var tier = TownTierFor(mf.name);
                if (tier == Tier.None) continue;
                Vector3 b = mf.sharedMesh.bounds.size, s = mf.transform.lossyScale;
                s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
                Vector3 world = Vector3.Scale(b, s);
                if (world.x < 1e-4f || world.y < 1e-4f || world.z < 1e-4f) continue;
                var (ratio, max) = TierSize[tier];
                float minSide = shape == Shape.Box ? Mathf.Min(world.x, Mathf.Min(world.y, world.z)) : Mathf.Min(world.x, world.y);
                float r = Mathf.Min(Mathf.Clamp(minSide * ratio, .002f, max), minSide * .49f);
                var mesh = shape == Shape.Box ? BoxMesh(meshes, b, s, r, Segments[tier]) : CylinderMesh(meshes, b, s, r, Segments[tier], minSide);
                if (mf.sharedMesh != mesh) { mf.sharedMesh = mesh; n++; }
            }
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Bevel] town prefab: {n} parts");
    }

    [MenuItem("Roomies/Art/Restyle/Bevel/Preview Town Tiers")]
    internal static void PreviewTownTiers()
    {
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded))
        {
            var town = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_EnvironmentArt");
            if (town == null) continue;
            foreach (var r in town.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null || ShapeOf(mf.sharedMesh) == Shape.Other) continue;
                var tier = mf.sharedMesh.name.StartsWith("bev") ? TownTierFor(r.name) : Tier.None;
                var c = tier == Tier.Strong ? new Color(1f, .25f, .3f) : tier == Tier.Medium ? new Color(1f, .8f, .2f) : tier == Tier.Weak ? new Color(.3f, .55f, 1f) : new Color(.55f, .55f, .55f);
                var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", c); r.SetPropertyBlock(block);
            }
        }
    }

    [MenuItem("Roomies/Art/Restyle/Bevel/Clear Town Preview")]
    internal static void ClearTownPreview()
    {
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s => s.isLoaded))
        {
            var town = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_EnvironmentArt");
            if (town != null) foreach (var r in town.GetComponentsInChildren<MeshRenderer>(true)) r.SetPropertyBlock(null);
        }
    }

    // 強さを色で見る（保存されない。撮影用）
    [MenuItem("Roomies/Art/Restyle/Bevel/Preview Tiers")]
    internal static void PreviewTiers()
    {
        foreach (var scene in OpenRoomScenes())
        {
            var root = scene.GetRootGameObjects().First(g => g.name == "Roomies_RoomRestyle");
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = r.GetComponent<MeshFilter>(); if (mf == null || mf.sharedMesh == null) continue;
                var shape = ShapeOf(mf.sharedMesh); if (shape == Shape.Other) continue;
                var tier = TierFor(PathOf(r.transform, root.transform, root.name), shape, mf.sharedMesh);
                var c = tier == Tier.Strong ? new Color(1f, .25f, .3f) : tier == Tier.Medium ? new Color(1f, .8f, .2f) : tier == Tier.Weak ? new Color(.3f, .55f, 1f) : new Color(.55f, .55f, .55f);
                var block = new MaterialPropertyBlock(); block.SetColor("_BaseColor", c); r.SetPropertyBlock(block);
            }
        }
    }

    [MenuItem("Roomies/Art/Restyle/Bevel/Clear Preview")]
    internal static void ClearPreview()
    {
        foreach (var scene in OpenRoomScenes())
            foreach (var r in scene.GetRootGameObjects().First(g => g.name == "Roomies_RoomRestyle").GetComponentsInChildren<MeshRenderer>(true))
                r.SetPropertyBlock(null);
    }

    static IEnumerable<Scene> OpenRoomScenes() =>
        Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt)
            .Where(s => s.isLoaded && s.GetRootGameObjects().Any(g => g.name == "Roomies_RoomRestyle")).ToArray();

    // ================================================================ 本体
    enum Shape { Other, Box, Cylinder }

    static Shape ShapeOf(Mesh mesh)
    {
        string n = mesh.name;
        if (n == "Cube" || n.StartsWith("rbox_") || n.StartsWith("bevB_")) return Shape.Box;
        if (n == "Cylinder" || n.StartsWith("bevY_")) return Shape.Cylinder;
        return Shape.Other;
    }

    static Tier TierFor(string path, Shape shape, Mesh mesh)
    {
        foreach (var rule in Rules) if (rule.path.IsMatch(path)) return rule.tier;
        if (mesh.name.StartsWith("rbox_")) return Tier.None;   // 角丸の箱は表になければ元のまま
        return Tier.Weak;
    }

    static int ApplyUnder(Transform root, string rootPath, bool skipPrefabInstances)
    {
        var meshes = LoadMeshAsset();
        int n = 0;
        foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
        {
            // メッシュが外れた部品（片付けで消えた場合）は、元の箱に戻してから付け直す
            if (mf.sharedMesh == null && mf.GetComponent<MeshRenderer>() != null && !PrefabUtility.IsPartOfPrefabInstance(mf))
                mf.sharedMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            if (mf.sharedMesh == null) continue;
            if (skipPrefabInstances && PrefabUtility.IsPartOfPrefabInstance(mf)) continue;
            var shape = ShapeOf(mf.sharedMesh); if (shape == Shape.Other) continue;
            string path = PathOf(mf.transform, root, rootPath);
            var tier = TierFor(path, shape, mf.sharedMesh);
            if (tier == Tier.None) continue;
            Vector3 b = mf.sharedMesh.bounds.size, s = mf.transform.lossyScale;
            s = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            Vector3 world = Vector3.Scale(b, s);
            if (world.x < 1e-4f || world.y < 1e-4f || world.z < 1e-4f) continue;
            var (ratio, max) = TierSize[tier];
            float minSide = shape == Shape.Box ? Mathf.Min(world.x, Mathf.Min(world.y, world.z)) : Mathf.Min(world.x, world.y);
            float r = Mathf.Clamp(minSide * ratio, .002f, max);
            r = Mathf.Min(r, minSide * .49f);
            int seg = Segments[tier];
            var mesh = shape == Shape.Box ? BoxMesh(meshes, b, s, r, seg) : CylinderMesh(meshes, b, s, r, seg, minSide);
            if (mf.sharedMesh != mesh) { Undo.RecordObject(mf, "Bevel"); mf.sharedMesh = mesh; EditorUtility.SetDirty(mf); }
            n++;
        }
        return n;
    }

    static string PathOf(Transform t, Transform root, string rootPath) => t == root ? rootPath : rootPath + "/" + RelPath(t, root);

    static string RelPath(Transform t, Transform root)
    {
        var parts = new List<string>();
        for (var x = t; x != null && x != root; x = x.parent) parts.Add(x.name);
        parts.Reverse();
        return string.Join("/", parts);
    }

    // ================================================================ メッシュ
    static Dictionary<string, Mesh> cache;

    static Dictionary<string, Mesh> LoadMeshAsset()
    {
        if (cache != null && cache.Values.All(m => m != null)) return cache;
        cache = new Dictionary<string, Mesh>();
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(MeshAsset).OfType<Mesh>()) cache[m.name] = m;
        return cache;
    }

    static Mesh Store(Dictionary<string, Mesh> meshes, string key, System.Action<Mesh> fill)
    {
        if (meshes.TryGetValue(key, out var mesh) && mesh != null) return mesh;
        mesh = new Mesh { name = key };
        fill(mesh);
        mesh.RecalculateBounds();
        if (AssetDatabase.LoadMainAssetAtPath(MeshAsset) == null) AssetDatabase.CreateAsset(mesh, MeshAsset);
        else AssetDatabase.AddObjectToAsset(mesh, MeshAsset);
        meshes[key] = mesh;
        return mesh;
    }

    static string K(float f) => Mathf.RoundToInt(f * 10000).ToString();

    /// <summary>角丸の箱。b＝元のメッシュの大きさ、s＝拡大率、r＝丸みの幅（ワールドの m）。拡大後に角が真円になるよう、拡大率で割って作る。</summary>
    static Mesh BoxMesh(Dictionary<string, Mesh> meshes, Vector3 b, Vector3 s, float r, int seg)
    {
        string key = $"bevB_{K(b.x)}_{K(b.y)}_{K(b.z)}_{K(s.x)}_{K(s.y)}_{K(s.z)}_{K(r)}_s{seg}";
        return Store(meshes, key, m =>
        {
            Vector3 size = Vector3.Scale(b, s), h = size * .5f;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int face = 0; face < 6; face++)
            {
                int a = face / 2, u = (a + 1) % 3, w = (a + 2) % 3; float sign = face % 2 == 0 ? 1 : -1;
                float[] tu = Steps(h[u], r, seg), tw = Steps(h[w], r, seg);
                int nu = tu.Length, start = verts.Count;
                for (int j = 0; j < tw.Length; j++) for (int i = 0; i < nu; i++)
                {
                    var p = Vector3.zero; p[a] = sign * h[a]; p[u] = tu[i]; p[w] = tw[j];
                    var inner = new Vector3(Mathf.Clamp(p.x, -(h.x - r), h.x - r), Mathf.Clamp(p.y, -(h.y - r), h.y - r), Mathf.Clamp(p.z, -(h.z - r), h.z - r));
                    var dir = p - inner; var nrm = dir.sqrMagnitude < 1e-12f ? Axis(a, sign) : dir.normalized;
                    var world = inner + nrm * r;
                    verts.Add(Div(world, s)); norms.Add(Vector3.Scale(nrm, s).normalized);
                    uvs.Add(new Vector2(p[u] / size[u] + .5f, p[w] / size[w] + .5f));
                }
                for (int j = 0; j < tw.Length - 1; j++) for (int i = 0; i < nu - 1; i++)
                {
                    int v0 = start + j * nu + i, v1 = v0 + 1, v2 = v0 + nu, v3 = v2 + 1;
                    if (sign > 0) { tris.Add(v0); tris.Add(v1); tris.Add(v3); tris.Add(v0); tris.Add(v3); tris.Add(v2); }
                    else { tris.Add(v0); tris.Add(v3); tris.Add(v1); tris.Add(v0); tris.Add(v2); tris.Add(v3); }
                }
            }
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateTangents();
        });
    }

    static Vector3 Axis(int a, float sign) { var v = Vector3.zero; v[a] = sign; return v; }
    static Vector3 Div(Vector3 v, Vector3 s) => new Vector3(v.x / s.x, v.y / s.y, v.z / s.z);

    // 面の上の分割位置：角の丸みの中は細かく、平らな所は1区間
    static float[] Steps(float h, float r, int seg)
    {
        var list = new List<float>();
        for (int i = 0; i <= seg; i++) list.Add(-h + r - r * Mathf.Cos(i * Mathf.PI / 2 / seg));
        for (int i = seg; i >= 0; i--) list.Add(h - r + r * Mathf.Cos(i * Mathf.PI / 2 / seg));
        return list.Distinct().ToArray();
    }

    /// <summary>ふちの丸い円柱（Unity の円柱と同じ：直径 1、高さ 2 を拡大）。</summary>
    static Mesh CylinderMesh(Dictionary<string, Mesh> meshes, Vector3 b, Vector3 s, float r, int seg, float minSide)
    {
        // 細い円柱（缶のふた・脚など）は周りの分割も減らす
        int around = minSide < .06f ? 12 : minSide < .2f ? 16 : 24;
        string key = $"bevY_{K(b.x)}_{K(b.y)}_{K(b.z)}_{K(s.x)}_{K(s.y)}_{K(s.z)}_{K(r)}_s{seg}_a{around}";
        return Store(meshes, key, m =>
        {
            float sx = s.x * b.x, sz = s.z * b.z, hy = s.y * b.y * .5f;   // 世界での直径（x,z）と半分の高さ
            float rad = Mathf.Min(sx, sz) * .5f;
            r = Mathf.Min(r, rad * .9f, hy * .9f);
            int arc = seg;
            // 断面（半径, 高さ, 法線の向き）を下から上へ
            var prof = new List<(float x, float y, Vector2 n)>();
            prof.Add((0, -hy, new Vector2(0, -1)));
            prof.Add((rad - r, -hy, new Vector2(0, -1)));
            for (int i = 1; i <= arc; i++) { float t = i * Mathf.PI / 2 / arc; var n = new Vector2(Mathf.Sin(t), -Mathf.Cos(t)); prof.Add((rad - r + n.x * r, -hy + r + n.y * r, n)); }
            prof.Add((rad, hy - r, new Vector2(1, 0)));
            for (int i = 1; i <= arc; i++) { float t = i * Mathf.PI / 2 / arc; var n = new Vector2(Mathf.Cos(t), Mathf.Sin(t)); prof.Add((rad - r + n.x * r, hy - r + n.y * r, n)); }
            prof.Add((0, hy, new Vector2(0, 1)));
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            var scale = new Vector3(s.x * b.x, s.y * b.y * .5f, s.z * b.z);   // 元のメッシュ（直径1・高さ2）からの拡大
            var meshScale = new Vector3(s.x, s.y, s.z);
            for (int k = 0; k < prof.Count; k++)
                for (int i = 0; i <= around; i++)
                {
                    float ang = i * Mathf.PI * 2 / around, c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                    // 楕円の断面に合わせる（x と z の直径が違うとき）
                    var world = new Vector3(c * prof[k].x * (sx / (2 * rad)), prof[k].y, sn * prof[k].x * (sz / (2 * rad)));
                    var nrm = new Vector3(c * prof[k].n.x, prof[k].n.y, sn * prof[k].n.x).normalized;
                    verts.Add(Div(world, meshScale)); norms.Add(Vector3.Scale(nrm, meshScale).normalized);
                    uvs.Add(new Vector2((float)i / around, (float)k / (prof.Count - 1)));
                }
            int row = around + 1;
            for (int k = 0; k < prof.Count - 1; k++)
                for (int i = 0; i < around; i++)
                {
                    int v0 = k * row + i, v1 = v0 + 1, v2 = v0 + row, v3 = v2 + 1;
                    tris.Add(v0); tris.Add(v2); tris.Add(v1); tris.Add(v1); tris.Add(v2); tris.Add(v3);
                }
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
            m.RecalculateTangents();
        });
    }
}
#endif
