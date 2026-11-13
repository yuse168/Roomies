#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 作り直し2-2：メインの家（ROOMIES。プレイヤーが朝を迎える家）の外まわりを0から作る（2026/11/13）。何度実行しても同じ結果。
/// ・中のゲームの仕組みはそのまま：2階の部屋（ベッド・冷蔵庫・クローゼット・ブレーカー・スポーン地点・家具）、2階の壁と柱
///   （部屋の内壁も兼ねる。当たり判定もそのまま）、床（Demo/pCube11）、階段の当たり判定（Demo/pCube12 と Entry stairs の段）、
///   屋根の当たり判定、出入口（2階の正面の開口 x 19.4〜21.6）は変えない。
/// ・作るもの（「Town main house」）：1階のレンガの外壁と窓（当たり判定つき）、2階の外側の板張りと窓枠・窓台・植木箱、
///   正面に妻を向けた切妻屋根、外階段（段・側板・手すり）、玄関の庇と灯り、看板と番地の板。
/// ・外した見た目：段の見た目、Demo/pCube12（階段の古い見た目。シーン）、屋根の板の見た目、古い看板の板、町の作り直し 1 の家の1階の外壁。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string MainHouseGroup = "Town main house";
    // 1階の箱（Demo/pCube11）と2階の壁の面
    const float HX0 = 12.9f, HX1 = 22.9f, HZ0 = 13.7f, HZ1 = 23.7f, HFloor2 = 3.3f;
    const float WinY0 = 4.255f, WinY1 = 5.455f, UpperTop = 5.86f, RoofBase = 6.12f;

    [MenuItem("Roomies/Art/Rebuild/2 Main House")]
    internal static void BuildMainHouse()
    {
        BuildWindowsAndWalls();   // 町の作り直し 1 から家の1階の外壁を外した状態に作り直す
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            var house = root.transform.Find(House);
            var stairs = root.transform.Find("Entry stairs");
            StripOldHouse(house, stairs);
            var g = Fresh(root.transform, MainHouseGroup);
            HouseGroundFloor(g);
            HouseUpperCladding(g);
            HouseRoof(g);
            HouseStairs(g, stairs);
            HouseEntrance(g, house);
            RoomiesBevelBuilder.ApplyTownUnder(g);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        HideOldStairMesh();
        AssetDatabase.SaveAssets();
        RoomiesMaterialFinishBuilder.BuildTimeOfDay();
        CombineForSpeed();
        Debug.Log("[Rebuild] main house built");
    }

    static void StripOldHouse(Transform house, Transform stairs)
    {
        if (house != null)
        {
            foreach (var n in new[] { "Entry trim", "Roof cap", "Home sign", "Address plaque" })
                foreach (Transform c in house.Cast<Transform>().Where(c => c.name == n).ToArray()) Object.DestroyImmediate(c.gameObject);
            var roof = house.Find("House roof");
            if (roof != null) { Object.DestroyImmediate(roof.GetComponent<MeshRenderer>()); Object.DestroyImmediate(roof.GetComponent<MeshFilter>()); }
            // 看板の文字は新しい外壁の面より前へ
            foreach (var n in new[] { "Sign - ROOMIES", "Sign - 05" })
            {
                var t = house.Find(n); if (t == null) continue;
                var p = t.position; p.z = 13.5f; t.position = p;
            }
            // エアコンは新しい東の外壁の外へ
            foreach (Transform c in house) if (c.name.StartsWith("air_conditioner")) { var p = c.position; p.x = HX1 + .25f + .2f; c.position = p; }
        }
        if (stairs != null)
            foreach (Transform c in stairs.Cast<Transform>().ToArray())
            {
                if (c.GetComponent<Collider>() == null) { Object.DestroyImmediate(c.gameObject); continue; }
                Object.DestroyImmediate(c.GetComponent<MeshRenderer>()); Object.DestroyImmediate(c.GetComponent<MeshFilter>());
            }
    }

    /// <summary>シーンの Demo/pCube12（古い階段の見た目）を隠す。当たり判定（MeshCollider）は残す。</summary>
    static void HideOldStairMesh()
    {
        var scene = SceneManager.GetActiveScene();
        var demo = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Demo");
        var r = demo != null ? demo.transform.Find("pCube12")?.GetComponent<MeshRenderer>() : null;
        if (r != null && r.enabled) { r.enabled = false; EditorUtility.SetDirty(r); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
    }

    // ================================================================ 1階：レンガの外壁と窓（当たり判定つき）
    static void HouseGroundFloor(Transform g)
    {
        const float T = .25f;
        var walls = new List<Facade>
        {
            // 正面は階段（x 19.6〜21.5。上の2段は z 13.3〜14.0）をふさがないよう、左右に分ける
            new Facade { name = "Main house ground front left", wall = "house_brick", frame = "frame_house", origin = new Vector3(19.55f, 0, HZ0), yaw = 180, width = 19.55f - (HX0 - T), height = HFloor2, thick = T, band = true },
            new Facade { name = "Main house ground back", wall = "house_brick", frame = "frame_house", origin = new Vector3(HX0 - T, 0, HZ1), yaw = 0, width = HX1 - HX0 + 2 * T, height = HFloor2, thick = T, band = true },
            new Facade { name = "Main house ground east", wall = "house_brick", frame = "frame_house", origin = new Vector3(HX1, 0, HZ1), yaw = 90, width = HZ1 - HZ0, height = HFloor2, thick = T, band = true },
            new Facade { name = "Main house ground west", wall = "house_brick", frame = "frame_house", origin = new Vector3(HX0, 0, HZ0), yaw = -90, width = HZ1 - HZ0, height = HFloor2, thick = T, band = true },
        };
        // 窓（外壁の左端からの位置）。正面の右（階段の後ろ）とエアコンの所は空ける
        void W(Facade f, float x0, float x1, string glass) => f.openings.Add(new Opening(x0, x1, .95f, 2.45f, Kind.Window, glass));
        W(walls[0], 1.4f, 3.0f, "glass_home_lit"); W(walls[0], 3.95f, 5.55f, "glass_home_dark");
        walls.Add(new Facade { name = "Main house ground front right", wall = "house_brick", frame = "frame_house", origin = new Vector3(HX1 + T, 0, HZ0), yaw = 180, width = HX1 + T - 21.55f, height = HFloor2, thick = T, band = true });
        W(walls[1], 1.6f, 3.2f, "glass_home_dark"); W(walls[1], 4.5f, 6.1f, "glass_home_lit"); W(walls[1], 7.4f, 9.0f, "glass_home_dark");
        W(walls[2], 7.0f, 8.4f, "glass_home_lit"); W(walls[2], .9f, 2.3f, "glass_home_dark");
        W(walls[3], 1.4f, 2.8f, "glass_home_dark"); W(walls[3], 4.3f, 5.7f, "glass_home_lit"); W(walls[3], 7.2f, 8.6f, "glass_home_dark");
        foreach (var f in walls) Build(g, f);
    }

    // ================================================================ 2階：板張り・窓枠・窓台・植木箱
    // 面：名前, 外の面の位置, 向き（外向きの法線）, 面に沿った範囲, 開口（面に沿った範囲・下・上）
    static void HouseUpperCladding(Transform g)
    {
        var faces = new (string name, Vector3 n, float plane, float a0, float a1, (float a0, float a1, float y0, float y1)[] holes)[]
        {
            ("north", Vector3.forward, 23.61f, HX0 - .1f, HX1 + .1f, new[] { (13.35f, 17.77f, WinY0, WinY1), (18.09f, 22.51f, WinY0, WinY1) }),
            ("west", Vector3.left, 13.02f, HZ0 - .1f, HZ1 + .1f, new[] { (14.1f, 18.52f, WinY0, WinY1), (18.84f, 23.26f, WinY0, WinY1) }),
            ("east", Vector3.right, 22.86f, HZ0 - .1f, HZ1 + .1f, new[] { (14.1f, 18.52f, WinY0, WinY1), (18.84f, 23.26f, WinY0, WinY1) }),
            ("front", Vector3.back, 13.75f, HX0 - .1f, HX1 + .1f, new[] { (19.4f, 21.6f, HFloor2 - .05f, 5.55f) }),
        };
        var siding = Mat("house_siding"); var trim = Mat("frame_house"); var sill = Mat("sill");
        const float th = .13f, board = .2f;
        foreach (var f in faces)
        {
            // 板張り：下から板を重ねる（板ごとに少し厚さを変えて影の筋を出す）
            int row = 0;
            for (float y = HFloor2 - .05f; y < UpperTop; y += board, row++)
            {
                float y1 = Mathf.Min(y + board, UpperTop);
                float t0 = row % 2 == 0 ? th : th - .02f;
                var xs = new List<float> { f.a0, f.a1 };
                foreach (var h in f.holes) if (h.y0 < y1 && h.y1 > y) { xs.Add(h.a0); xs.Add(h.a1); }
                xs.Sort();
                for (int i = 0; i < xs.Count - 1; i++)
                {
                    float a = xs[i], b = xs[i + 1], mid = (a + b) / 2;
                    if (f.holes.Any(h => h.a0 <= mid && h.a1 >= mid && h.y0 < y1 - .01f && h.y1 > y + .01f)) continue;
                    FaceBox(g, "Siding board", f.n, f.plane, a, b, y, y1, 0, t0, siding);
                }
            }
            // 角の板と、床の高さの帯
            FaceBox(g, "Corner board", f.n, f.plane, f.a0 - .02f, f.a0 + .14f, HFloor2 - .05f, UpperTop, 0, th + .03f, trim);
            FaceBox(g, "Corner board", f.n, f.plane, f.a1 - .14f, f.a1 + .02f, HFloor2 - .05f, UpperTop, 0, th + .03f, trim);
            if (f.name == "front")
            {   // 出入口（x 19.4〜21.6）の足元は空ける
                FaceBox(g, "Belly band", f.n, f.plane, f.a0, 19.4f - .12f, HFloor2 - .12f, HFloor2 + .06f, 0, th + .05f, trim);
                FaceBox(g, "Belly band", f.n, f.plane, 21.6f + .12f, f.a1, HFloor2 - .12f, HFloor2 + .06f, 0, th + .05f, trim);
            }
            else FaceBox(g, "Belly band", f.n, f.plane, f.a0, f.a1, HFloor2 - .12f, HFloor2 + .06f, 0, th + .05f, trim);
            FaceBox(g, "Frieze", f.n, f.plane, f.a0 - .02f, f.a1 + .02f, UpperTop - .02f, RoofBase, 0, th + .04f, trim);
            // 窓枠・桟・窓台・植木箱（正面の開口は出入口なので枠だけ）
            foreach (var h in f.holes)
            {
                bool door = f.name == "front";
                FaceBox(g, "Window casing", f.n, f.plane, h.a0 - .12f, h.a0, h.y0, h.y1 + .12f, 0, th + .04f, trim);
                FaceBox(g, "Window casing", f.n, f.plane, h.a1, h.a1 + .12f, h.y0, h.y1 + .12f, 0, th + .04f, trim);
                FaceBox(g, "Window casing", f.n, f.plane, h.a0 - .12f, h.a1 + .12f, h.y1, h.y1 + .12f, 0, th + .04f, trim);
                if (door) continue;
                FaceBox(g, "Window sill", f.n, f.plane, h.a0 - .18f, h.a1 + .18f, h.y0 - .08f, h.y0, 0, th + .12f, sill);
                for (int k = 1; k < 3; k++) { float a = Mathf.Lerp(h.a0, h.a1, k / 3f); FaceBox(g, "Window mullion", f.n, f.plane, a - .04f, a + .04f, h.y0, h.y1, -.04f, th, trim); }
                FaceBox(g, "Window transom", f.n, f.plane, h.a0, h.a1, h.y1 - .32f, h.y1 - .26f, -.04f, th, trim);
                FaceBox(g, "Planter box", f.n, f.plane, h.a0 + .2f, h.a1 - .2f, h.y0 - .4f, h.y0 - .1f, th, th + .32f, Mat("planter_wood"));
                FaceBox(g, "Planter leaves", f.n, f.plane, h.a0 + .25f, h.a1 - .25f, h.y0 - .1f, h.y0 + .12f, th + .03f, th + .3f, Mat("planter_leaf"));
                for (float a = h.a0 + .45f; a < h.a1 - .4f; a += .55f)
                    FaceBox(g, "Planter flower", f.n, f.plane, a - .06f, a + .06f, h.y0 + .1f, h.y0 + .2f, th + .12f, th + .22f, Mat(((int)(a * 3)) % 2 == 0 ? "flower_pink" : "flower_yellow"));
            }
        }
    }

    /// <summary>外の面（法線 n、面の位置 plane）に沿った箱。a0〜a1 は面に沿った位置、d0〜d1 は面から外への厚さ。</summary>
    static void FaceBox(Transform g, string name, Vector3 n, float plane, float a0, float a1, float y0, float y1, float d0, float d1, Material m)
    {
        if (Mathf.Abs(n.z) > .5f)
        {
            float z0 = plane + n.z * d0, z1 = plane + n.z * d1;
            Box(g, name, a0, a1, y0, y1, Mathf.Min(z0, z1), Mathf.Max(z0, z1), m);
        }
        else
        {
            float x0 = plane + n.x * d0, x1 = plane + n.x * d1;
            Box(g, name, Mathf.Min(x0, x1), Mathf.Max(x0, x1), y0, y1, a0, a1, m);
        }
    }

    // ================================================================ 屋根：正面に妻を向けた切妻（屋根裏の窓・煙突・軒）
    static void HouseRoof(Transform g)
    {
        var h = new GameObject("Main house roof") { isStatic = true }.transform;
        h.SetParent(g, false);
        h.localPosition = new Vector3((HX0 + HX1) / 2, 0, HZ0 - .25f);   // 正面の外壁の外の面
        h.localRotation = Quaternion.Euler(0, 180, 0);                  // 家の前（-z）を向く
        var spec = new HouseSpec
        {
            width = HX1 - HX0 + .5f, depth = HZ1 - HZ0 + .5f, floors = 2, floorHeight = (RoofBase - .3f) / 2, pitch = 30,
            roof = RoofKind.FrontGable, wall = "house_siding", roofMat = "roof_teal", frame = "frame_house", doorLeft = false,
        };
        BuildRoof(h, spec);
        // 妻の板張り風の横筋と、屋根の下の換気口
        float rise = spec.width / 2 * Mathf.Tan(spec.pitch * Mathf.Deg2Rad);
        for (float y = RoofBase + .3f; y < RoofBase + rise - .4f; y += .3f)
        {
            float half = (1 - (y - RoofBase) / rise) * spec.width / 2 - .15f;
            if (Mathf.Abs(y - (RoofBase + rise * .38f)) < .5f) continue;   // 屋根裏の窓の所は空ける
            Box(h, "Gable board line", -half, half, y, y + .02f, .0f, .015f, Mat("frame_house"));
        }
    }

    // ================================================================ 外階段：段（元の当たり判定の上）・側板・手すり
    static void HouseStairs(Transform g, Transform stairs)
    {
        var treadCols = stairs != null ? stairs.GetComponentsInChildren<BoxCollider>().OrderBy(c => c.bounds.center.z).ToArray() : new BoxCollider[0];
        var concrete = Mat("stair_concrete"); var nosing = Mat("metal_dark"); var rail = Mat("frame_house");
        float x0 = 19.6f, x1 = 21.5f;
        foreach (var c in treadCols)
        {
            var b = c.bounds;
            Box(g, "Stair block", x0, x1, 0, b.max.y - .04f, b.min.z, b.max.z, concrete);
            Box(g, "Stair tread", x0 - .02f, x1 + .02f, b.max.y - .04f, b.max.y, b.min.z - .03f, b.max.z, Mat("stair_tread"));
            Box(g, "Stair nosing", x0, x1, b.max.y - .045f, b.max.y + .002f, b.min.z - .04f, b.min.z, nosing);
        }
        // 踊り場（扉の前）
        Box(g, "Stair landing", x0 - .02f, x1 + .02f, HFloor2 - .04f, HFloor2, 13.7f, 13.95f, Mat("stair_tread"));
        // 側板（斜め）と手すり：下 (z 10, y .3) から上 (z 14, y 3.3)
        Vector3 bot = new Vector3(0, .3f, 10f), top = new Vector3(0, HFloor2, 14f);
        float len = Vector3.Distance(bot, top), ang = Mathf.Atan2(top.y - bot.y, top.z - bot.z) * Mathf.Rad2Deg;
        foreach (float sx in new[] { x0 - .08f, x1 + .08f })
        {
            var mid = new Vector3(sx, (bot.y + top.y) / 2 - .15f, (bot.z + top.z) / 2);
            RBox(g, "Stair stringer", mid, new Vector3(.16f, .5f, len + .2f), new Vector3(-ang, 0, 0), concrete);
            // 手すり：支柱と上の手すり
            for (int k = 0; k <= 6; k++)
            {
                float t = k / 6f; var p = Vector3.Lerp(bot, top, t);
                Box(g, "Rail post", sx - .025f, sx + .025f, p.y, p.y + .95f, p.z - .025f, p.z + .025f, rail);
            }
            var rmid = new Vector3(sx, (bot.y + top.y) / 2 + .95f, (bot.z + top.z) / 2);
            RBox(g, "Handrail", rmid, new Vector3(.06f, .06f, len + .1f), new Vector3(-ang, 0, 0), rail);
            var r2 = new Vector3(sx, (bot.y + top.y) / 2 + .5f, (bot.z + top.z) / 2);
            RBox(g, "Mid rail", r2, new Vector3(.03f, .03f, len), new Vector3(-ang, 0, 0), rail);
        }
        // 下の植木鉢2つ
        foreach (float px in new[] { x0 - .55f, x1 + .55f })
        {
            Cyl(g, "Stair pot", new Vector3(px, .25f, 9.9f), new Vector3(.45f, .25f, .45f), Mat("roof_terra"));
            Cyl(g, "Stair pot plant", new Vector3(px, .62f, 9.9f), new Vector3(.5f, .2f, .5f), Mat("planter_leaf"));
        }
    }

    // ================================================================ 玄関：庇・灯り・看板・番地
    static void HouseEntrance(Transform g, Transform house)
    {
        float face = 13.62f;   // 板張りの外の面（正面）
        var trim = Mat("frame_house");
        Box(g, "Porch canopy", 19.15f, 21.85f, 5.62f, 5.74f, face - .85f, face, trim);
        Box(g, "Porch canopy edge", 19.15f, 21.85f, 5.55f, 5.62f, face - .87f, face - .8f, Mat("sill"));
        foreach (float x in new[] { 19.25f, 21.75f }) RBox(g, "Porch bracket", new Vector3(x, 5.4f, face - .3f), new Vector3(.06f, .5f, .06f), new Vector3(-50, 0, 0), trim);
        // 扉の横の灯り（夜だけ光る。名前の頭 Porch light で朝に消える）
        Box(g, "Door lamp", 21.9f, 22.12f, 4.55f, 4.85f, face - .14f, face, Mat("metal_dark"));
        Box(g, "Door lamp lens", 21.93f, 22.09f, 4.6f, 4.8f, face - .15f, face - .14f, Mat("bulb"));
        PointLight(g, "Porch light - main house", new Vector3(20.5f, 5.2f, face - .6f), 2.2f, 7f, new Color(1f, .82f, .6f));
        // 看板の板（文字 Sign - ROOMIES の後ろ）と番地の板（Sign - 05 の後ろ）
        Box(g, "Home sign board", 14.3f, 18.1f, 4.72f, 5.45f, 13.52f, face, Mat("sign_wood"));
        Box(g, "Home sign frame", 14.25f, 18.15f, 4.68f, 4.72f, 13.5f, face, trim);
        Box(g, "Home sign frame", 14.25f, 18.15f, 5.45f, 5.49f, 13.5f, face, trim);
        Box(g, "Address plaque", 18.8f, 19.25f, 4.52f, 5.02f, 13.52f, face, Mat("frame_express"));
        // 雨どい（四隅）
        foreach (var (x, z) in new[] { (HX0 - .3f, HZ0 - .3f), (HX1 + .3f, HZ0 - .3f), (HX0 - .3f, HZ1 + .3f), (HX1 + .3f, HZ1 + .3f) })
            Box(g, "Downpipe", x - .05f, x + .05f, 0, RoofBase, z - .05f, z + .05f, Mat("metal_dark"));
    }
}
#endif
