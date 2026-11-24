#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 町の作り直し 2：家の形と屋根。何度実行しても同じ結果。
/// ・裏の家並み（店の裏、境界の外 z -27〜-33 の11棟）を、切妻屋根・軒・煙突・扉・埋め込んだ窓・前庭のある家に作り直す。
///   置き場所は町のプレハブ（RoomiesNeighborhood）の「Town houses」。境界の外なので当たり判定は付けない。
/// ・遠景（GameRoom の Roomies_Backdrop/Outer town）の家29棟を、同じ形の簡単な版（窓は枠とガラスの重ね）に作り直す。
///   位置・向き・大きさ・色は元の家のまま。遠景の配置ツール（RoomiesBackdropBuilder）も同じ形で作る。
/// ・店3軒の屋上に、パラペット（手すり壁）・笠木・軒の出・室外機・換気筒・水槽・点検口と手すりを置く（「Town rooftops」）。
/// 寸法と色は下の表。家の向きは +Z が正面、原点は正面の壁の足元の真ん中。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string HouseGroup = "Town houses";
    const string RooftopGroup = "Town rooftops";
    const string Gardens = "Pocket gardens and distant silhouettes";
    const string ShapeDir = "Assets/_Art/Restyle/Town";
    const string GablePath = ShapeDir + "/town_gable.asset";

    internal enum RoofKind { FrontGable, SideGable }

    internal class HouseSpec
    {
        public float width = 6, depth = 6, floorHeight = 2.8f, pitch = 38;
        public int floors = 2;
        public RoofKind roof = RoofKind.SideGable;
        public string wall = "wall_h1", roofMat = "roof_slate", frame = "frame_h_white";
        public bool doorLeft = true, planters, far;
        public float Eave => .3f + floors * floorHeight;
    }

    [MenuItem("Roomies/Art/Town/2 Houses And Roofs")]
    internal static void BuildHousesAndRoofs()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        int houses = 0, roofs = 0;
        try
        {
            RemoveOldBackRow(root.transform);
            var work = root.transform.Find(Workplaces);
            foreach (var trimName in new[] { "CORNER STORE roof trim", "NEIGHBORHOOD WORKS roof trim" })
            { var tr = work.Find(trimName); if (tr != null) Object.DestroyImmediate(tr.gameObject); }   // 屋上のパラペットに置き換える
            var group = Fresh(root.transform, HouseGroup);
            foreach (var (x, spec) in BackRow())
            {
                var h = new GameObject($"House {houses + 1:00}") { isStatic = true }.transform;
                h.SetParent(group, false);
                h.localPosition = new Vector3(x, 0, -27f);
                BuildHouseModel(h, spec);
                FrontYard(h, spec);
                houses++;
            }
            var tops = Fresh(root.transform, RooftopGroup);
            foreach (var r in Rooftops()) { BuildRooftop(tops, r); roofs++; }
            RoomiesBevelBuilder.ApplyTownUnder(group);
            RoomiesBevelBuilder.ApplyTownUnder(tops);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }

        int far = RebuildBackdropHouses();
        AssetDatabase.SaveAssets();
        CombineForSpeed();
        Debug.Log($"[Town] houses: back row {houses}, rooftops {roofs}, backdrop houses {far}");
    }

    static Transform Fresh(Transform root, string name)
    {
        var old = root.Find(name);
        if (old != null) Object.DestroyImmediate(old.gameObject);
        var g = new GameObject(name) { isStatic = true }.transform;
        g.SetParent(root, false);
        return g;
    }

    // ================================================================ 表：裏の家並み（元の11棟と同じ x、正面は z=-27）
    static IEnumerable<(float x, HouseSpec spec)> BackRow()
    {
        HouseSpec H(float w, int floors, RoofKind roof, string wall, string roofMat, string frame, bool doorLeft, bool planters = false, float pitch = 38)
            => new HouseSpec { width = w, floors = floors, roof = roof, wall = wall, roofMat = roofMat, frame = frame, doorLeft = doorLeft, planters = planters, pitch = pitch };
        yield return (-36, H(6.2f, 2, RoofKind.SideGable, "wall_h1", "roof_slate", "frame_h_navy", true, true));
        yield return (-29, H(5.8f, 2, RoofKind.FrontGable, "wall_b1", "roof_terra", "frame_h_white", false));
        yield return (-22, H(6.0f, 3, RoofKind.SideGable, "wall_h3", "roof_slate", "frame_h_white", true, false, 34));
        yield return (-15, H(5.6f, 2, RoofKind.FrontGable, "wall_h4", "roof_teal", "frame_h_white", true, true, 42));
        yield return (-8, H(6.2f, 2, RoofKind.SideGable, "wall_b2", "roof_terra", "frame_h_green", false));
        yield return (-1, H(5.8f, 2, RoofKind.FrontGable, "wall_h2", "roof_slate", "frame_h_white", false, true));
        yield return (6, H(6.0f, 3, RoofKind.FrontGable, "wall_h1", "roof_terra", "frame_h_navy", true, false, 40));
        yield return (13, H(5.8f, 2, RoofKind.SideGable, "wall_h5", "roof_slate", "frame_h_white", false, true));
        yield return (20, H(6.2f, 2, RoofKind.FrontGable, "wall_b1", "roof_moss", "frame_h_white", true));
        yield return (27, H(5.8f, 2, RoofKind.SideGable, "wall_h3", "roof_terra", "frame_h_navy", true, true, 34));
        yield return (34, H(6.0f, 2, RoofKind.FrontGable, "wall_h4", "roof_slate", "frame_h_white", false));
    }

    static void RemoveOldBackRow(Transform root)
    {
        var g = root.Find(Gardens);
        if (g == null) return;
        foreach (Transform c in g.Cast<Transform>().ToArray())
            if (c.name == "Distant home" || c.name == "Distant roof" || c.name == "Distant window") Object.DestroyImmediate(c.gameObject);
    }

    // ================================================================ 家の組み立て
    /// <summary>家を1棟組み立てる（parent の下に部品を置く。原点は正面の壁の足元の真ん中、+Z が正面）。</summary>
    internal static void BuildHouseModel(Transform h, HouseSpec s)
    {
        float W = s.width, D = s.depth, E = s.Eave, T = .22f;
        var wall = Mat(s.wall); var trim = Mat(s.frame); var sill = Mat("sill"); var dark = Mat("metal_dark");
        float doorC = s.doorLeft ? -W / 2 + 1.25f : W / 2 - 1.25f;
        float winC = s.doorLeft ? W / 2 - 1.7f : -W / 2 + 1.7f;
        float[] upper = { -W * .24f, W * .24f };

        if (!s.far)
        {
            // 正面：厚みのある外壁に窓と扉をくり抜く（町の店と同じ作り）
            var f = new Facade { name = "House front", wall = s.wall, frame = s.frame, origin = new Vector3(-W / 2, 0, -T), yaw = 0, width = W, height = E, thick = T, collider = false };
            float o = W / 2;   // 外壁の表は左端が原点
            f.openings.Add(new Opening(o + doorC - .5f, o + doorC + .5f, 0, 2.25f, Kind.Door, "glass_home_lit"));
            f.openings.Add(new Opening(o + winC - .7f, o + winC + .7f, .95f, 2.35f, Kind.Window, "glass_home_lit"));
            for (int fl = 1; fl < s.floors; fl++)
                for (int i = 0; i < upper.Length; i++)
                {
                    float y0 = .3f + fl * s.floorHeight + .75f;
                    f.openings.Add(new Opening(o + upper[i] - .6f, o + upper[i] + .6f, y0, y0 + 1.35f, Kind.Window, (fl + i) % 3 == 0 ? "glass_home_lit" : "glass_home_dark"));
                }
            Build(h, f).transform.SetSiblingIndex(0);
            Box(h, "House wall", -W / 2, W / 2, 0, E, -D, -T, wall);
            // 階の境の帯
            for (int fl = 1; fl < s.floors; fl++) { float y = .3f + fl * s.floorHeight; Box(h, "Floor band", -W / 2 - .04f, W / 2 + .04f, y - .07f, y + .07f, -.02f, .07f, sill); }
            // 扉の上の小さな庇（持ち送り2本）
            Box(h, "Door canopy", doorC - .8f, doorC + .8f, 2.5f, 2.6f, 0, .7f, trim);
            Box(h, "Door canopy bracket", doorC - .72f, doorC - .64f, 2.2f, 2.5f, 0, .5f, trim);
            Box(h, "Door canopy bracket", doorC + .64f, doorC + .72f, 2.2f, 2.5f, 0, .5f, trim);
            // 側面の窓（家と家のすき間から見える分）
            SideAndBackWindows(h, s, false);
            // 植木箱
            if (s.planters && s.floors > 1)
                foreach (float u in upper)
                {
                    float y = .3f + s.floorHeight + .75f;
                    Box(h, "Planter box", u - .62f, u + .62f, y - .32f, y - .1f, .02f, .32f, Mat("planter_wood"));
                    Box(h, "Planter leaves", u - .56f, u + .56f, y - .1f, y + .12f, .05f, .3f, Mat("planter_leaf"));
                }
            // 雨どいの縦管
            float pipeX = s.doorLeft ? W / 2 - .12f : -W / 2 + .12f;
            Box(h, "Downpipe", pipeX - .05f, pipeX + .05f, 0, E, .02f, .12f, dark);
        }
        else
        {
            // 遠景：本体は箱1つ、窓は「枠＋少し奥のガラス」の重ねで埋め込んだように見せる
            Box(h, "House wall", -W / 2, W / 2, 0, E, -D, 0, wall);
            Box(h, "Plinth", -W / 2 - .03f, W / 2 + .03f, 0, .3f, -D - .03f, .05f, Mat("plinth"));
            FarWindow(h, doorC, 0, 1.1f, 2.25f, trim, "Door", true);
            FarWindow(h, winC, .95f, 1.5f, 1.4f, trim, "glass_home_lit");
            for (int fl = 1; fl < s.floors; fl++)
                for (int i = 0; i < upper.Length; i++)
                    FarWindow(h, upper[i], .3f + fl * s.floorHeight + .75f, 1.2f, 1.35f, trim, (fl + i) % 3 == 0 ? "glass_home_lit" : "glass_home_dark");
            Box(h, "Door canopy", doorC - .8f, doorC + .8f, 2.5f, 2.6f, 0, .6f, trim);
            SideAndBackWindows(h, s, true);
        }

        BuildRoof(h, s);
    }

    /// <summary>壁の面（z=0、+Z が外）に付ける窓：4本の枠が外へ出て、ガラスは壁の面に沿って奥に見える。扉は下の枠なし。</summary>
    static void FarWindow(Transform h, float cx, float y0, float w, float hgt, Material trim, string glass, bool door = false)
    {
        float x0 = cx - w / 2, x1 = cx + w / 2, y1 = y0 + hgt, f = .09f, d = .1f;
        Box(h, "Window frame", x0 - f, x0, y0, y1 + f, 0, d, trim);
        Box(h, "Window frame", x1, x1 + f, y0, y1 + f, 0, d, trim);
        Box(h, "Window frame", x0, x1, y1, y1 + f, 0, d, trim);
        if (door) { Box(h, "Door", x0, x1, y0, y1, 0, .03f, Mat("door_wood")); return; }
        Box(h, "Window frame", x0, x1, y0 - f, y0, 0, d * .6f, trim);
        Box(h, "Window glazing", x0, x1, y0, y1, 0, .02f, Mat(glass));
        Box(h, "Window mullion", cx - .025f, cx + .025f, y0, y1, 0, .05f, trim);
        Box(h, "Window sill", x0 - .14f, x1 + .14f, y0 - f - .08f, y0 - f, 0, .16f, Mat("sill"));
    }

    /// <summary>横と裏の面の窓。面ごとに向きを変えた空の親を置き、その上に FarWindow を並べる。</summary>
    static void SideAndBackWindows(Transform h, HouseSpec s, bool back)
    {
        float W = s.width, D = s.depth;
        var trim = Mat(s.frame);
        var faces = new List<(string name, Vector3 pos, float yaw, float[] xs)>
        {
            ("Left face", new Vector3(-W / 2, 0, -D / 2), -90, back ? new[] { -D * .22f, D * .22f } : new[] { D * .05f }),
            ("Right face", new Vector3(W / 2, 0, -D / 2), 90, back ? new[] { -D * .22f, D * .22f } : new[] { -D * .05f }),
        };
        if (back) faces.Add(("Back face", new Vector3(0, 0, -D), 180, new[] { -W * .24f, W * .24f }));
        int k = 0;
        foreach (var (name, pos, yaw, xs) in faces)
        {
            var face = new GameObject(name) { isStatic = true }.transform;
            face.SetParent(h, false);
            face.localPosition = pos; face.localRotation = Quaternion.Euler(0, yaw, 0);
            for (int fl = 0; fl < s.floors; fl++)
                foreach (float x in xs)
                    FarWindow(face, x, .3f + fl * s.floorHeight + .9f, back ? 1.1f : .9f, 1.25f, trim, (k++ % 3 == 0) ? "glass_home_lit" : "glass_home_dark");
        }
    }

    /// <summary>切妻屋根：壁の三角（妻）・屋根板2枚・棟・煙突。FrontGable は妻が正面、SideGable は妻が横。</summary>
    static void BuildRoof(Transform h, HouseSpec s)
    {
        float W = s.width, D = s.depth, E = s.Eave, p = s.pitch * Mathf.Deg2Rad, ov = .4f, t = .16f;
        bool front = s.roof == RoofKind.FrontGable;
        float span = front ? W : D, length = front ? D : W;     // 妻の幅と、棟の長さ
        float rise = span / 2 * Mathf.Tan(p);
        var gable = GableMesh();
        var wall = Mat(s.wall); var roof = Mat(s.roofMat); var trim = Mat(s.frame);

        // 妻の三角（壁と同じ素材）
        var g = new GameObject("Gable") { isStatic = true };
        g.transform.SetParent(h, false);
        g.transform.localPosition = new Vector3(0, E, -D / 2);
        g.transform.localRotation = Quaternion.Euler(0, front ? 0 : 90, 0);
        g.transform.localScale = new Vector3(span, rise, length);
        g.AddComponent<MeshFilter>().sharedMesh = gable;
        g.AddComponent<MeshRenderer>().sharedMaterial = wall;

        // 屋根板2枚（軒と妻側に ov だけ出す）。棟の線で合わせる
        float run = span / 2 + ov, slab = run / Mathf.Cos(p);
        foreach (float side in new[] { -1f, 1f })
        {
            float a = side * run / 2;                                         // 水平の中心
            float yb = E + rise - Mathf.Tan(p) * run / 2;                   // 屋根板の下面の中心の高さ
            var n = new Vector2(side * Mathf.Sin(p), Mathf.Cos(p)) * (t / 2);
            if (front)
                RBox(h, "Roof panel", new Vector3(a + n.x, yb + n.y, -D / 2), new Vector3(slab, t, length + 2 * ov), new Vector3(0, 0, -side * s.pitch), roof);
            else
                RBox(h, "Roof panel", new Vector3(0, yb + n.y, -D / 2 + a + n.x), new Vector3(length + 2 * ov, t, slab), new Vector3(side * s.pitch, 0, 0), roof);
            // 軒先の鼻隠し（屋根板の下の縁）
            float ex = side * (span / 2 + ov - .06f), ey = E - Mathf.Tan(p) * ov - .02f;
            if (front) RBox(h, "Eave fascia", new Vector3(ex, ey, -D / 2), new Vector3(.08f, .2f, length + 2 * ov), Vector3.zero, trim);
            else RBox(h, "Eave fascia", new Vector3(0, ey, -D / 2 + ex), new Vector3(length + 2 * ov, .2f, .08f), Vector3.zero, trim);
        }
        float ridgeY = E + rise + t / Mathf.Cos(p) - .02f;
        if (front) RBox(h, "Roof ridge", new Vector3(0, ridgeY, -D / 2), new Vector3(.24f, .12f, length + 2 * ov + .02f), Vector3.zero, roof);
        else RBox(h, "Roof ridge", new Vector3(0, ridgeY, -D / 2), new Vector3(length + 2 * ov + .02f, .12f, .24f), Vector3.zero, roof);

        // 正面の妻の小窓（屋根裏）
        if (front && !s.far)
        {
            float y = E + rise * .38f;
            RBox(h, "Window frame", new Vector3(0, y, .03f), new Vector3(.8f, .8f, .08f), Vector3.zero, trim);
            RBox(h, "Window glazing", new Vector3(0, y, .05f), new Vector3(.6f, .6f, .06f), Vector3.zero, Mat("glass_home_dark"));
        }
        if (!front && !s.far)
            RBox(h, "Gutter", new Vector3(0, E - Mathf.Tan(p) * ov - .14f, ov - .02f), new Vector3(W + 2 * ov, .12f, .14f), Vector3.zero, Mat("metal_dark"));

        // 煙突：棟より 0.7m 上まで
        float cx = front ? W * .26f * (s.doorLeft ? 1 : -1) : W * .3f * (s.doorLeft ? 1 : -1);
        float cz = front ? -D * .7f : -D * .3f;
        float top = E + rise + .7f;
        Box(h, "Chimney", cx - .3f, cx + .3f, E, top, cz - .3f, cz + .3f, Mat("wall_b1"));
        Box(h, "Chimney cap", cx - .38f, cx + .38f, top, top + .1f, cz - .38f, cz + .38f, Mat("sill"));
        if (!s.far) Box(h, "Chimney pot", cx - .1f, cx + .1f, top + .1f, top + .35f, cz - .1f, cz + .1f, Mat("roof_terra"));
    }

    /// <summary>家の前の小道・生け垣・郵便受け（境界の柵より外側だけ）。</summary>
    static void FrontYard(Transform h, HouseSpec s)
    {
        float W = s.width;
        float doorC = s.doorLeft ? -W / 2 + 1.25f : W / 2 - 1.25f;
        Box(h, "Front path", doorC - .55f, doorC + .55f, 0, .04f, .45f, 2.7f, Mat("paving"));
        float hedgeA = s.doorLeft ? doorC + .85f : -W / 2 + .1f, hedgeB = s.doorLeft ? W / 2 - .1f : doorC - .85f;
        Box(h, "Hedge", hedgeA, hedgeB, 0, .75f, 2.0f, 2.6f, Mat("planter_leaf"));
        float mx = s.doorLeft ? doorC - .85f : doorC + .85f;
        Box(h, "Mailbox post", mx - .04f, mx + .04f, 0, 1.0f, 2.3f, 2.38f, Mat("metal_dark"));
        Box(h, "Mailbox", mx - .16f, mx + .16f, 1.0f, 1.24f, 2.2f, 2.5f, Mat(s.frame));
    }

    // ================================================================ 遠景の家（GameRoom の Roomies_Backdrop）
    static int RebuildBackdropHouses()
    {
        var scene = EditorSceneManager.GetActiveScene();
        var back = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_Backdrop");
        if (back == null) return 0;
        int n = 0;
        foreach (var house in back.GetComponentsInChildren<Transform>(true).Where(t => t.name == "House").ToArray())
        {
            var spec = BackdropSpec(house);
            if (spec == null) continue;   // もう作り直してある（元の Body が無い）なら、記録した寸法で作り直す
            foreach (Transform c in house.Cast<Transform>().ToArray()) Object.DestroyImmediate(c.gameObject);
            BuildFarHouse(house, spec);
            n++;
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        return n;
    }

    /// <summary>遠景の家を1棟：house の下に Model（原点は正面の壁）と、作り直すときの寸法の印を置く。</summary>
    internal static void BuildFarHouse(Transform house, HouseSpec spec)
    {
        var model = new GameObject("Model") { isStatic = true }.transform;
        model.SetParent(house, false);
        model.localPosition = new Vector3(0, 0, spec.depth / 2);
        BuildHouseModel(model, spec);
        new GameObject(SpecMark + EncodeSpec(spec)) { isStatic = true }.transform.SetParent(model, false);
    }

    /// <summary>元の遠景の家（Body の大きさ・色、屋根の色）から寸法を読む。作り直したあとは Model の印から読む。</summary>
    static HouseSpec BackdropSpec(Transform house)
    {
        var body = house.Find("Body");
        var model = house.Find("Model");
        if (body == null && model != null) return null;   // もう作り直してある（まとめたあとも部品が二重にならないように触らない）
        if (body == null) return null;
        Vector3 size = body.localScale;
        string wallName = body.GetComponent<Renderer>().sharedMaterial.name;
        var panel = house.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Panel");
        string roofName = panel != null ? panel.GetComponent<Renderer>().sharedMaterial.name : "ink";
        var spec = FarSpec(size.x, size.z, size.y, wallName, roofName, house.GetSiblingIndex());
        return spec;
    }

    /// <summary>遠景の家の寸法と色（遠景の配置ツールからも呼ぶ）。</summary>
    internal static HouseSpec FarSpec(float width, float depth, float height, string wall, string roof, int seed)
    {
        int floors = Mathf.Max(1, Mathf.RoundToInt((height - .3f) / 2.8f));
        string W(string n) => n.Contains("pink") ? "wall_h4" : n.Contains("mustard") ? "wall_h5" : n.Contains("paper") ? "wall_h3" : seed % 4 == 0 ? "wall_b2" : "wall_h1";
        string R(string n) => n.Contains("coral") ? "roof_terra" : n.Contains("teal") ? "roof_teal" : "roof_slate";
        return new HouseSpec
        {
            width = width * .8f, depth = depth * .8f, floors = floors, floorHeight = (height - .3f) / floors,
            roof = seed % 2 == 0 ? RoofKind.SideGable : RoofKind.FrontGable, pitch = 34 + seed % 3 * 4,
            wall = W(wall), roofMat = R(roof), frame = seed % 3 == 0 ? "frame_h_navy" : "frame_h_white", doorLeft = seed % 2 == 1, far = true,
        };
    }

    const string SpecMark = "Spec ";

    static string EncodeSpec(HouseSpec s)
    {
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return string.Join("|", s.width.ToString("F2", inv), s.depth.ToString("F2", inv), s.floors, s.floorHeight.ToString("F3", inv), (int)s.roof,
            s.pitch.ToString("F0", inv), s.wall, s.roofMat, s.frame, s.doorLeft ? "1" : "0");
    }

    static HouseSpec ParseSpec(string s)
    {
        var p = s.Split('|');
        var inv = System.Globalization.CultureInfo.InvariantCulture;
        return new HouseSpec
        {
            width = float.Parse(p[0], inv), depth = float.Parse(p[1], inv), floors = int.Parse(p[2]), floorHeight = float.Parse(p[3], inv),
            roof = (RoofKind)int.Parse(p[4]), pitch = float.Parse(p[5], inv), wall = p[6], roofMat = p[7], frame = p[8], doorLeft = p[9] == "1", far = true,
        };
    }

    // ================================================================ 店の屋上
    class Rooftop
    {
        public string name, wall; public float x0, x1, z0, z1, top, parapet = .7f, frontOut;
        public bool tank, hatch;
        public (float u, float v, float w, float d)[] units = new (float, float, float, float)[0];
    }

    static IEnumerable<Rooftop> Rooftops()
    {
        // 外形は Demo の箱（pCube1/3/4）。正面（+z）は外壁が厚くなった分だけ前に出ている
        // CORNER STORE の屋上は、コンビニの建物の側（Roomies/Store/1 Build Building）
        if (!StoreBuiltSeparately)
            yield return new Rooftop { name = "CORNER STORE", wall = "wall_corner", x0 = 18.6f, x1 = 33.6f, z0 = -18.7f, z1 = -8.46f, top = 6f, frontOut = .55f, tank = true, hatch = true,
                units = new[] { (.25f, .35f, 1.3f, .9f), (.42f, .35f, 1.3f, .9f), (.75f, .6f, 1.6f, 1.1f) } };
        yield return new Rooftop { name = "NEIGHBORHOOD WORKS", wall = "wall_works", x0 = -14.1f, x1 = -2.1f, z0 = -18.2f, z1 = -8.96f, top = 5f, parapet = .6f, frontOut = .5f, hatch = true,
            units = new[] { (.3f, .4f, 1.2f, .9f), (.66f, .3f, 1.2f, .9f) } };
        yield return new Rooftop { name = "ROOMIES EXPRESS", wall = "wall_express", x0 = -32.4f, x1 = -18.5f, z0 = -20.2f, z1 = -9.2f, top = 10.2f, parapet = .8f, frontOut = .35f, tank = true,
            units = new[] { (.2f, .5f, 1.6f, 1.1f), (.36f, .5f, 1.6f, 1.1f), (.7f, .3f, 2.4f, 1.4f) } };
    }

    static void BuildRooftop(Transform parent, Rooftop r)
    {
        var t = new GameObject(r.name + " rooftop") { isStatic = true }.transform;
        t.SetParent(parent, false);
        var wall = Mat(r.wall); var cap = Mat("sill"); var deck = Mat("roof_flat"); var metal = Mat("metal_dark"); var unit = Mat("ac_unit");
        float y = r.top, h = r.parapet, th = .25f;
        // 屋上の防水面（元の箱の天面を隠す）
        Box(t, "Roof deck", r.x0 + th, r.x1 - th, y, y + .04f, r.z0 + th, r.z1 - th, deck);
        // パラペット（手すり壁）と笠木
        Box(t, "Parapet", r.x0, r.x1, y, y + h, r.z1 - th, r.z1, wall);
        Box(t, "Parapet", r.x0, r.x1, y, y + h, r.z0, r.z0 + th, wall);
        Box(t, "Parapet", r.x0, r.x0 + th, y, y + h, r.z0 + th, r.z1 - th, wall);
        Box(t, "Parapet", r.x1 - th, r.x1, y, y + h, r.z0 + th, r.z1 - th, wall);
        Box(t, "Parapet coping", r.x0 - .06f, r.x1 + .06f, y + h, y + h + .08f, r.z1 - th - .06f, r.z1 + .06f, cap);
        Box(t, "Parapet coping", r.x0 - .06f, r.x1 + .06f, y + h, y + h + .08f, r.z0 - .06f, r.z0 + th + .06f, cap);
        Box(t, "Parapet coping", r.x0 - .06f, r.x0 + th + .06f, y + h, y + h + .08f, r.z0 + th, r.z1 - th, cap);
        Box(t, "Parapet coping", r.x1 - th - .06f, r.x1 + .06f, y + h, y + h + .08f, r.z0 + th, r.z1 - th, cap);
        // 軒の出（正面の上に張り出すコーニス）と持ち送り
        Box(t, "Eave cornice", r.x0 - .15f, r.x1 + .15f, y - .14f, y + .02f, r.z1 - .1f, r.z1 + r.frontOut, cap);
        Box(t, "Eave cornice", r.x0 - .1f, r.x1 + .1f, y - .24f, y - .14f, r.z1 - .1f, r.z1 + r.frontOut * .6f, cap);

        float W = r.x1 - r.x0, D = r.z1 - r.z0;
        Vector3 P(float u, float v) => new Vector3(r.x0 + u * W, y, r.z0 + v * D);
        // 室外機：箱・上のファン・脚
        foreach (var (u, v, w, d) in r.units)
        {
            var c = P(u, v);
            Box(t, "AC unit base", c.x - w / 2, c.x + w / 2, y + .04f, y + .16f, c.z - d / 2, c.z + d / 2, metal);
            Box(t, "AC unit", c.x - w / 2 + .03f, c.x + w / 2 - .03f, y + .16f, y + .95f, c.z - d / 2 + .03f, c.z + d / 2 - .03f, unit);
            Cyl(t, "AC fan", new Vector3(c.x, y + .97f, c.z), new Vector3(Mathf.Min(w, d) * .75f, .02f, Mathf.Min(w, d) * .75f), metal);
        }
        // 室外機をつなぐダクト
        if (r.units.Length > 1)
        {
            var a = P(r.units[0].u, r.units[0].v); var b = P(r.units[r.units.Length - 1].u, r.units[r.units.Length - 1].v);
            Box(t, "Duct", Mathf.Min(a.x, b.x), Mathf.Max(a.x, b.x), y + .04f, y + .34f, a.z + r.units[0].d / 2 + .1f, a.z + r.units[0].d / 2 + .4f, unit);
        }
        // 換気筒
        foreach (var (u, v) in new[] { (.12f, .8f), (.55f, .78f), (.88f, .2f) })
        {
            var c = P(u, v);
            Cyl(t, "Vent pipe", new Vector3(c.x, y + .45f, c.z), new Vector3(.22f, .45f, .22f), metal);
            Cyl(t, "Vent cap", new Vector3(c.x, y + .93f, c.z), new Vector3(.38f, .04f, .38f), metal);
        }
        // 水槽（脚の上）
        if (r.tank)
        {
            var c = P(.86f, .7f);
            foreach (var (dx, dz) in new[] { (-.55f, -.55f), (.55f, -.55f), (-.55f, .55f), (.55f, .55f) })
                Box(t, "Tank leg", c.x + dx - .06f, c.x + dx + .06f, y, y + 1.2f, c.z + dz - .06f, c.z + dz + .06f, metal);
            Box(t, "Tank platform", c.x - .7f, c.x + .7f, y + 1.2f, y + 1.3f, c.z - .7f, c.z + .7f, metal);
            Cyl(t, "Water tank", new Vector3(c.x, y + 2.15f, c.z), new Vector3(1.3f, .85f, 1.3f), unit);
            Cyl(t, "Water tank lid", new Vector3(c.x, y + 3.02f, c.z), new Vector3(1.0f, .03f, 1.0f), metal);
        }
        // 点検口と、そのまわりの手すり
        if (r.hatch)
        {
            var c = P(.5f, .25f);
            Box(t, "Roof hatch", c.x - .5f, c.x + .5f, y, y + .45f, c.z - .5f, c.z + .5f, metal);
            Box(t, "Roof hatch lid", c.x - .58f, c.x + .58f, y + .45f, y + .52f, c.z - .58f, c.z + .58f, unit);
            float x0 = c.x - 1.1f, x1 = c.x + 1.1f, z0 = c.z - 1.1f, z1 = c.z + 1.1f;
            foreach (var (px, pz) in new[] { (x0, z0), (x1, z0), (x0, z1), (x1, z1) })
                Box(t, "Guard rail post", px - .03f, px + .03f, y, y + 1.05f, pz - .03f, pz + .03f, metal);
            foreach (float ry in new[] { .55f, 1.05f })
            {
                Box(t, "Guard rail", x0, x1, y + ry - .03f, y + ry + .03f, z0 - .03f, z0 + .03f, metal);
                Box(t, "Guard rail", x0 - .03f, x0 + .03f, y + ry - .03f, y + ry + .03f, z0, z1, metal);
                Box(t, "Guard rail", x1 - .03f, x1 + .03f, y + ry - .03f, y + ry + .03f, z0, z1, metal);
            }
        }
        // 正面のパラペットの上の手すり（屋上に出る人のため。細い鉄の柵）
        for (float x = r.x0 + .4f; x <= r.x1 - .3f; x += 1.5f)
            Box(t, "Guard rail post", x - .025f, x + .025f, y + h + .08f, y + h + .7f, r.z1 - .16f, r.z1 - .1f, metal);
        Box(t, "Guard rail", r.x0 + .35f, r.x1 - .35f, y + h + .66f, y + h + .72f, r.z1 - .17f, r.z1 - .09f, metal);
    }

    // ================================================================ 部品
    static GameObject RBox(Transform parent, string name, Vector3 center, Vector3 size, Vector3 euler, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    /// <summary>円柱。size は 直径x・高さの半分y・直径z（Unity の円柱は高さ2）。</summary>
    static GameObject Cyl(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    /// <summary>妻の三角の柱（幅1・高さ1・奥行き1、底の真ん中が原点）。面ごとに頂点を分けて角を立てる。</summary>
    static Mesh GableMesh()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(GablePath);
        if (mesh != null) return mesh;
        Directory.CreateDirectory(ShapeDir);
        Vector3 a = new Vector3(-.5f, 0, 0), b = new Vector3(.5f, 0, 0), c = new Vector3(0, 1, 0);
        var v = new List<Vector3>(); var tri = new List<int>();
        void Face(params Vector3[] q) { int i = v.Count; v.AddRange(q); for (int k = 1; k < q.Length - 1; k++) { tri.Add(i); tri.Add(i + k + 1); tri.Add(i + k); } }
        Vector3 F(Vector3 p) => p + new Vector3(0, 0, .5f); Vector3 B(Vector3 p) => p + new Vector3(0, 0, -.5f);
        Face(F(a), F(c), F(b));                 // 正面
        Face(B(a), B(b), B(c));                 // 背面
        Face(B(a), B(c), F(c), F(a));           // 左の斜面
        Face(B(c), B(b), F(b), F(c));           // 右の斜面
        Face(B(a), F(a), F(b), B(b));           // 底
        mesh = new Mesh { name = "town_gable", vertices = v.ToArray(), triangles = tri.ToArray() };
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); mesh.RecalculateTangents();
        AssetDatabase.CreateAsset(mesh, GablePath);
        return mesh;
    }
}
#endif
