#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>メインの家の部品（RoomiesTownRebuild.MainHouse.cs の配置ツールから呼ぶ）。座標は町のまま（m）。</summary>
internal static partial class RoomiesTownRebuild
{
    // ================================================================ 1階：しっくいの外壁・よろい戸の窓・花の箱
    static void HouseGround(Transform g)
    {
        float gx0 = HX0 - HT, gx1 = HX1 + HT, gz0 = HZ0 - HT, gz1 = HZ1 + HT;
        var walls = new List<Facade>
        {
            new Facade { name = "House ground front", wall = "house_stucco", frame = "frame_h_white", origin = new Vector3(gx1, 0, HZ0), yaw = 180, width = gx1 - gx0, height = HFloor2, thick = HT, band = true },
            new Facade { name = "House ground back", wall = "house_stucco", frame = "frame_h_white", origin = new Vector3(gx0, 0, HZ1), yaw = 0, width = gx1 - gx0, height = HFloor2, thick = HT, band = true },
            new Facade { name = "House ground east", wall = "house_stucco", frame = "frame_h_white", origin = new Vector3(HX1, 0, HZ1), yaw = 90, width = HZ1 - HZ0, height = HFloor2, thick = HT, band = true },
            new Facade { name = "House ground west", wall = "house_stucco", frame = "frame_h_white", origin = new Vector3(HX0, 0, HZ0), yaw = -90, width = HZ1 - HZ0, height = HFloor2, thick = HT, band = true },
        };
        // 窓の位置（外壁の左端から）。正面は踊り場の下の玄関（飾りの扉）と、階段の手前の窓
        float F(float worldX) => gx1 - worldX;
        walls[0].openings.Add(new Opening(F(21.15f), F(19.95f), 0, 2.3f, Kind.Door, "glass_home_lit"));
        walls[0].openings.Add(new Opening(F(14.25f), F(12.95f), .95f, 2.35f, Kind.Window, "glass_home_lit"));
        foreach (var (a, b) in new[] { (1.4f, 2.8f), (4.6f, 6.0f), (7.8f, 9.2f) }) walls[1].openings.Add(new Opening(a, b, .95f, 2.35f, Kind.Window, "glass_home_dark"));
        foreach (var (a, b) in new[] { (1.2f, 2.6f), (6.8f, 8.2f) }) walls[2].openings.Add(new Opening(a, b, .95f, 2.35f, Kind.Window, "glass_home_lit"));
        foreach (var (a, b) in new[] { (1.4f, 2.8f), (4.3f, 5.7f), (7.2f, 8.6f) }) walls[3].openings.Add(new Opening(a, b, .95f, 2.35f, Kind.Window, "glass_home_dark"));
        foreach (var f in walls) Build(g, f);
        // よろい戸と花の箱（窓ごと。正面の窓と玄関には付けない）
        void Shutters(Vector3 n, float plane, float a0, float a1)
        {
            foreach (var (s0, s1) in new[] { (a0 - .62f, a0 - .08f), (a1 + .08f, a1 + .62f) })
            {
                FaceBox(g, "Shutter", n, plane, s0, s1, .9f, 2.4f, 0, .05f, Mat("house_shutter"));
                for (float y = 1.02f; y < 2.3f; y += .11f) FaceBox(g, "Shutter louver", n, plane, s0 + .05f, s1 - .05f, y, y + .05f, .05f, .07f, Mat("house_shutter"));
            }
            FaceBox(g, "Flower box", n, plane, a0 - .05f, a1 + .05f, .55f, .82f, .02f, .3f, Mat("planter_wood"));
            FaceBox(g, "Flower box leaves", n, plane, a0, a1, .82f, .98f, .05f, .27f, Mat("planter_leaf"));
            for (float a = a0 + .15f; a < a1 - .1f; a += .22f)
                FaceBox(g, "Flower", n, plane, a - .05f, a + .05f, .95f, 1.04f, .1f, .2f, Mat(((int)(a * 7)) % 3 == 0 ? "flower_yellow" : "flower_pink"));
        }
        foreach (var (a, b) in new[] { (1.4f, 2.8f), (4.6f, 6.0f), (7.8f, 9.2f) }) Shutters(Vector3.forward, gz1, gx0 + a, gx0 + b);
        foreach (var (a, b) in new[] { (1.2f, 2.6f), (6.8f, 8.2f) }) Shutters(Vector3.right, gx1, HZ1 - b, HZ1 - a);
        foreach (var (a, b) in new[] { (1.4f, 2.8f), (4.3f, 5.7f), (7.2f, 8.6f) }) Shutters(Vector3.left, gx0, HZ0 + a, HZ0 + b);
        // 足元の石
        foreach (var (n, plane, a0, a1) in new[] { (Vector3.forward, gz1, gx0, gx1), (Vector3.right, gx1, gz0, gz1), (Vector3.left, gx0, gz0, gz1) })
            FaceBox(g, "House stone base", n, plane, a0 - .04f, a1 + .04f, 0, .45f, 0, .05f, Mat("house_stone"));
        FaceBox(g, "House stone base", Vector3.back, gz0, gx0 - .04f, StairX0, 0, .45f, 0, .05f, Mat("house_stone"));
    }

    // ================================================================ 2階：縦の板張り・窓まわり・しま模様の日よけ
    static void HouseUpper(Transform g)
    {
        var faces = new (string name, Vector3 n, float plane, float a0, float a1, (float a0, float a1, float y0, float y1)[] holes)[]
        {
            ("north", Vector3.forward, 23.61f, HX0 - .1f, HX1 + .1f, new[] { (13.35f, 17.77f, WinY0, WinY1), (18.09f, 22.51f, WinY0, WinY1) }),
            ("west", Vector3.left, 13.02f, HZ0 - .1f, HZ1 + .1f, new[] { (14.1f, 18.52f, WinY0, WinY1), (18.84f, 23.26f, WinY0, WinY1) }),
            ("east", Vector3.right, 22.86f, HZ0 - .1f, HZ1 + .1f, new[] { (14.1f, 18.52f, WinY0, WinY1), (18.84f, 23.26f, WinY0, WinY1) }),
            ("front", Vector3.back, 13.75f, HX0 - .1f, HX1 + .1f, new[] { (19.4f, 21.6f, HFloor2 - .05f, 5.55f) }),
        };
        var board = Mat("house_batten"); var trim = Mat("frame_h_white");
        const float th = .12f;
        foreach (var f in faces)
        {
            bool Hole(float a, float y0, float y1) => f.holes.Any(h => h.a0 < a && h.a1 > a && h.y0 < y1 && h.y1 > y0);
            // 下地の板：縦の帯ごとに、開口のない高さだけ
            var xs = new List<float> { f.a0, f.a1 };
            foreach (var h in f.holes) { xs.Add(h.a0); xs.Add(h.a1); }
            xs = xs.Distinct().OrderBy(x => x).ToList();
            for (int i = 0; i < xs.Count - 1; i++)
            {
                float a = xs[i], b = xs[i + 1], mid = (a + b) / 2, y = HFloor2 - .05f;
                foreach (var h in f.holes.Where(h => h.a0 < mid && h.a1 > mid).OrderBy(h => h.y0))
                {
                    if (h.y0 > y + .01f) FaceBox(g, "Board", f.n, f.plane, a, b, y, h.y0, 0, th, board);
                    y = Mathf.Max(y, h.y1);
                }
                if (UpperTop > y + .01f) FaceBox(g, "Board", f.n, f.plane, a, b, y, UpperTop, 0, th, board);
            }
            // 押し縁（40cm ごと）
            for (float a = f.a0 + .2f; a < f.a1 - .1f; a += .4f)
            {
                float y = HFloor2 - .05f;
                foreach (var h in f.holes.Where(h => h.a0 < a && h.a1 > a).OrderBy(h => h.y0))
                {
                    if (h.y0 > y + .01f) FaceBox(g, "Batten", f.n, f.plane, a - .03f, a + .03f, y, h.y0, th, th + .03f, board);
                    y = Mathf.Max(y, h.y1);
                }
                if (UpperTop > y + .01f) FaceBox(g, "Batten", f.n, f.plane, a - .03f, a + .03f, y, UpperTop, th, th + .03f, board);
            }
            FaceBox(g, "Corner board", f.n, f.plane, f.a0 - .02f, f.a0 + .14f, HFloor2 - .05f, UpperTop, 0, th + .05f, trim);
            FaceBox(g, "Corner board", f.n, f.plane, f.a1 - .14f, f.a1 + .02f, HFloor2 - .05f, UpperTop, 0, th + .05f, trim);
            FaceBox(g, "Frieze", f.n, f.plane, f.a0 - .02f, f.a1 + .02f, UpperTop - .02f, RoofBase, 0, th + .06f, trim);
            if (f.name != "front") FaceBox(g, "Belly band", f.n, f.plane, f.a0, f.a1, HFloor2 - .12f, HFloor2 + .08f, 0, th + .07f, trim);
            else
            {
                FaceBox(g, "Belly band", f.n, f.plane, f.a0, 19.4f - .12f, HFloor2 - .12f, HFloor2 + .08f, 0, th + .07f, trim);
                FaceBox(g, "Belly band", f.n, f.plane, 21.6f + .12f, f.a1, HFloor2 - .12f, HFloor2 + .08f, 0, th + .07f, trim);
            }
            foreach (var h in f.holes)
            {
                bool door = f.name == "front";
                FaceBox(g, "Window casing", f.n, f.plane, h.a0 - .12f, h.a0, h.y0, h.y1 + .12f, 0, th + .05f, trim);
                FaceBox(g, "Window casing", f.n, f.plane, h.a1, h.a1 + .12f, h.y0, h.y1 + .12f, 0, th + .05f, trim);
                FaceBox(g, "Window casing", f.n, f.plane, h.a0 - .12f, h.a1 + .12f, h.y1, h.y1 + .12f, 0, th + .05f, trim);
                if (door) continue;
                FaceBox(g, "Window sill", f.n, f.plane, h.a0 - .18f, h.a1 + .18f, h.y0 - .08f, h.y0, 0, th + .12f, Mat("sill"));
                for (int k = 1; k < 4; k++) { float a = Mathf.Lerp(h.a0, h.a1, k / 4f); FaceBox(g, "Window mullion", f.n, f.plane, a - .035f, a + .035f, h.y0, h.y1, -.04f, th, trim); }
                Awning(g, f.n, f.plane + 0, h.a0 - .1f, h.a1 + .1f, h.y1 + .14f, th);
            }
        }
        // 正面：看板の板（ROOMIES）・番地の板・扉の横の灯
        float face = 13.75f - th;
        Box(g, "Home sign board", 14.3f, 18.1f, 4.72f, 5.42f, 13.52f, face, Mat("sign_wood"));
        Box(g, "Home sign frame", 14.25f, 18.15f, 4.68f, 4.72f, 13.5f, face, trim);
        Box(g, "Home sign frame", 14.25f, 18.15f, 5.42f, 5.46f, 13.5f, face, trim);
        Box(g, "Address plaque", 18.75f, 19.15f, 4.35f, 4.85f, 13.52f, face, Mat("frame_express"));
        Box(g, "Door lamp", 21.85f, 22.07f, 4.55f, 4.85f, face - .14f, face, Mat("metal_dark"));
        Box(g, "Door lamp lens", 21.88f, 22.04f, 4.6f, 4.8f, face - .15f, face - .14f, Mat("bulb"));
    }

    /// <summary>しま模様の日よけ：窓の上から外へ傾いた布（2色を交互）と、両わきの金具。</summary>
    static void Awning(Transform g, Vector3 n, float plane, float a0, float a1, float y, float th)
    {
        const float depth = .7f, tilt = 24f;
        float out0 = th + .02f;
        bool alongX = Mathf.Abs(n.z) > .5f;
        int k = 0;
        for (float a = a0; a < a1 - .01f; a += .3f, k++)
        {
            float b = Mathf.Min(a + .3f, a1), mid = (a + b) / 2;
            var center = alongX
                ? new Vector3(mid, y - Mathf.Sin(tilt * Mathf.Deg2Rad) * depth / 2, plane + n.z * (out0 + Mathf.Cos(tilt * Mathf.Deg2Rad) * depth / 2))
                : new Vector3(plane + n.x * (out0 + Mathf.Cos(tilt * Mathf.Deg2Rad) * depth / 2), y - Mathf.Sin(tilt * Mathf.Deg2Rad) * depth / 2, mid);
            var size = alongX ? new Vector3(b - a, .03f, depth) : new Vector3(depth, .03f, b - a);
            var euler = alongX ? new Vector3(n.z > 0 ? tilt : -tilt, 0, 0) : new Vector3(0, 0, n.x > 0 ? -tilt : tilt);
            RBox(g, "Awning stripe", center, size, euler, Mat(k % 2 == 0 ? "awning_a" : "awning_b"));
        }
        // 前の垂れ
        float edge = out0 + Mathf.Cos(tilt * Mathf.Deg2Rad) * depth, ey = y - Mathf.Sin(tilt * Mathf.Deg2Rad) * depth;
        FaceBox(g, "Awning valance", n, plane, a0, a1, ey - .16f, ey, edge - .02f, edge, Mat("awning_a"));
    }

    // ================================================================ 寄棟屋根
    static void HouseHipRoof(Transform g)
    {
        float ov = .55f, x0 = HX0 - .1f - ov, x1 = HX1 + .1f + ov, z0 = HZ0 - .1f - ov, z1 = HZ1 + .1f + ov, y0 = RoofBase;
        float pitch = 27f * Mathf.Deg2Rad;
        float rise = Mathf.Min(x1 - x0, z1 - z0) / 2 * Mathf.Tan(pitch);
        var apexA = new Vector3((x0 + x1) / 2, y0 + rise, (z0 + z1) / 2);   // 正方形に近いので、頂は1点（方形屋根）
        var mb = new RoomiesNature.MB(1);
        void Tri(Vector3 a, Vector3 b, Vector3 c)
        {
            var nrm = Vector3.Cross(b - a, c - a).normalized;
            int i = mb.v.Count; mb.v.Add(a); mb.v.Add(b); mb.v.Add(c);
            mb.n.Add(nrm); mb.n.Add(nrm); mb.n.Add(nrm); mb.uv.Add(Vector2.zero); mb.uv.Add(Vector2.right); mb.uv.Add(Vector2.up);
            mb.t[0].Add(i); mb.t[0].Add(i + 1); mb.t[0].Add(i + 2);
        }
        var c00 = new Vector3(x0, y0, z0); var c10 = new Vector3(x1, y0, z0); var c11 = new Vector3(x1, y0, z1); var c01 = new Vector3(x0, y0, z1);
        Tri(c00, apexA, c10); Tri(c10, apexA, c11); Tri(c11, apexA, c01); Tri(c01, apexA, c00);
        Tri(c00, c10, c11); Tri(c00, c11, c01);   // 軒の裏
        var mesh = RoomiesNature.StoreMesh("Assets/_Art/Restyle/Town/house_roof.asset", mb.ToMesh("house_hip_roof"));
        var roof = new GameObject("House hip roof") { isStatic = true };
        roof.transform.SetParent(g, false);
        roof.AddComponent<MeshFilter>().sharedMesh = mesh;
        roof.AddComponent<MeshRenderer>().sharedMaterial = Mat("roof_teal");
        // 隅棟の笠木（四隅から頂へ）
        foreach (var c in new[] { c00, c10, c11, c01 })
        {
            var d = apexA - c; var mid = (c + apexA) / 2 + Vector3.up * .05f;
            RBox(g, "Hip ridge", mid, new Vector3(.16f, .1f, d.magnitude + .05f), Quaternion.LookRotation(d).eulerAngles, Mat("roof_slate"));
        }
        Cyl(g, "Roof finial", apexA + Vector3.up * .15f, new Vector3(.18f, .15f, .18f), Mat("casino_gold"));
        // 軒の鼻隠しと雨どい・縦樋
        var trim = Mat("frame_h_white"); var dark = Mat("metal_dark");
        Box(g, "Eave fascia", x0, x1, y0 - .18f, y0, z0 - .03f, z0 + .04f, trim);
        Box(g, "Eave fascia", x0, x1, y0 - .18f, y0, z1 - .04f, z1 + .03f, trim);
        Box(g, "Eave fascia", x0 - .03f, x0 + .04f, y0 - .18f, y0, z0, z1, trim);
        Box(g, "Eave fascia", x1 - .04f, x1 + .03f, y0 - .18f, y0, z0, z1, trim);
        Box(g, "Gutter", x0, x1, y0 - .26f, y0 - .14f, z0 - .14f, z0 - .03f, dark);
        Box(g, "Gutter", x0, x1, y0 - .26f, y0 - .14f, z1 + .03f, z1 + .14f, dark);
        // 縦樋：裏の両角と東の前の角。雨どいから壁の角へ斜めに下ろし、壁に沿って地面まで
        float wx0 = HX0 - HT, wx1 = HX1 + HT, wz0 = HZ0 - HT, wz1 = HZ1 + HT;
        foreach (var (gx, gz, wx, wz) in new[] { (x0 + .15f, z1 + .09f, wx0 - .06f, wz1 + .06f), (x1 - .15f, z1 + .09f, wx1 + .06f, wz1 + .06f), (x1 - .15f, z0 - .09f, wx1 + .06f, wz0 - .06f) })
        {
            var a = new Vector3(gx, y0 - .26f, gz); var b = new Vector3(wx, y0 - 1.0f, wz);
            RBox(g, "Downpipe elbow", (a + b) / 2, new Vector3(.09f, .09f, (b - a).magnitude), Quaternion.LookRotation(b - a).eulerAngles, dark);
            Box(g, "Downpipe", wx - .05f, wx + .05f, 0, y0 - 1.0f, wz - .05f, wz + .05f, dark);
        }
        // 軒の裏（2階の壁から外への板）
        Box(g, "Soffit", x0, x1, y0 - .02f, y0, z0, z1, Mat("house_batten"));
        // 煙突（東の奥）
        float cx = 20.8f, cz = 21.2f, top = y0 + rise * .75f;
        Box(g, "Chimney", cx - .38f, cx + .38f, y0 - .2f, top, cz - .38f, cz + .38f, Mat("house_brick"));
        Box(g, "Chimney cap", cx - .46f, cx + .46f, top, top + .1f, cz - .46f, cz + .46f, Mat("sill"));
        Cyl(g, "Chimney pot", new Vector3(cx, top + .3f, cz), new Vector3(.22f, .2f, .22f), Mat("roof_terra"));
    }

    // ================================================================ 外階段：正面の壁に沿って西から東へ上がる
    static void HouseStair(Transform g)
    {
        float rise = HFloor2 / StairSteps;
        var stone = Mat("house_stone"); var tread = Mat("deck_wood"); var rail = Mat("frame_h_white");
        for (int i = 0; i < StairSteps; i++)
        {
            float x0 = StairX0 + i * StairRun, x1 = x0 + StairRun, top = (i + 1) * rise;
            Box(g, "Stair block", x0, x1, 0, top - .04f, StairZ0, StairZ1, stone);
            Box(g, "Stair tread", x0 - .03f, x1, top - .04f, top, StairZ0 - .03f, StairZ1, tread);
            Solid(g, "Stair step collider", new Vector3(x0, 0, StairZ0), new Vector3(x1, top, StairZ1));
            // 外側の手すりの当たり判定（段ごとに1つ。落ちないように）
            Solid(g, "Stair rail collider", new Vector3(x0, top, StairZ0 - .05f), new Vector3(x1, top + 1.0f, StairZ0 + .02f));
        }
        // 外側の側壁（しっくい）と、手すり（白い支柱・手すり・中桟）
        var bot = new Vector3(StairX0, 0, 0); var topP = new Vector3(LandX0, HFloor2, 0);
        float len = Mathf.Sqrt((topP.x - bot.x) * (topP.x - bot.x) + HFloor2 * HFloor2), ang = Mathf.Atan2(HFloor2, topP.x - bot.x) * Mathf.Rad2Deg;
        for (int k = 0; k <= StairSteps; k += 2)
        {
            float x = StairX0 + k * StairRun + .05f, y = Mathf.Min(k + 1, StairSteps) * rise;
            Box(g, "Rail post", x - .03f, x + .03f, y, y + .95f, StairZ0 - .03f, StairZ0 + .03f, rail);
        }
        var mid = new Vector3((StairX0 + LandX0) / 2, HFloor2 / 2 + .95f + rise / 2, StairZ0);
        RBox(g, "Handrail", mid, new Vector3(len + .1f, .07f, .07f), new Vector3(0, 0, ang), rail);
        RBox(g, "Mid rail", mid + Vector3.down * .5f, new Vector3(len, .035f, .035f), new Vector3(0, 0, ang), rail);
    }

    // ================================================================ 踊り場と、その下の玄関
    static void HousePorch(Transform g)
    {
        var deck = Mat("deck_wood"); var rail = Mat("frame_h_white"); var post = Mat("frame_h_white");
        float y1 = HFloor2, y0 = HFloor2 - .2f;
        // 木の床（板の目）と梁・柱
        Box(g, "Landing joist", LandX0, LandX1, y0, y1 - .04f, LandZ0, StairZ1, Mat("planter_wood"));
        for (float x = LandX0; x < LandX1 - .01f; x += .14f) Box(g, "Landing board", x + .005f, Mathf.Min(x + .135f, LandX1), y1 - .04f, y1, LandZ0, StairZ1, deck);
        Solid(g, "Landing collider", new Vector3(LandX0, y0, LandZ0), new Vector3(LandX1, y1, StairZ1));
        foreach (float x in new[] { LandX0 + .1f, LandX1 - .1f })
        {
            Box(g, "Landing post", x - .1f, x + .1f, 0, y0, LandZ0 + .02f, LandZ0 + .22f, post);
            Box(g, "Landing post base", x - .15f, x + .15f, 0, .15f, LandZ0 - .03f, LandZ0 + .27f, Mat("house_stone"));
            Solid(g, "Landing post collider", new Vector3(x - .1f, 0, LandZ0 + .02f), new Vector3(x + .1f, y0, LandZ0 + .22f));
        }
        // 手すり（外側と東の端）
        void RailRun(float x0, float x1, float z0, float z1)
        {
            bool alongX = x1 - x0 > z1 - z0;
            Box(g, "Landing top rail", x0, x1, y1 + .92f, y1 + 1.0f, z0, z1, rail);
            Box(g, "Landing bottom rail", x0, x1, y1 + .08f, y1 + .13f, z0, z1, rail);
            float len = alongX ? x1 - x0 : z1 - z0;
            for (float a = .1f; a < len; a += .14f)
            {
                if (alongX) Box(g, "Baluster", x0 + a - .02f, x0 + a + .02f, y1 + .13f, y1 + .92f, z0 + .01f, z1 - .01f, rail);
                else Box(g, "Baluster", x0 + .01f, x1 - .01f, y1 + .13f, y1 + .92f, z0 + a - .02f, z0 + a + .02f, rail);
            }
            Solid(g, "Landing rail collider", new Vector3(x0, y1, z0), new Vector3(x1, y1 + 1.0f, z1));
        }
        RailRun(LandX0, LandX1, LandZ0, LandZ0 + .06f);
        RailRun(LandX1 - .06f, LandX1, LandZ0, StairZ1);
        RailRun(LandX0 - .02f, LandX0 + .04f, LandZ0, StairZ0);
        // 踊り場の上のひさし（2階の扉の上）と灯（夜だけ）
        float face = 13.75f - .12f;
        Box(g, "Porch canopy", 19.1f, 21.9f, 5.62f, 5.74f, face - 1.0f, face, rail);
        Box(g, "Porch canopy roof", 19.05f, 21.95f, 5.74f, 5.8f, face - 1.05f, face, Mat("roof_teal"));
        foreach (float x in new[] { 19.2f, 21.8f }) RBox(g, "Porch bracket", new Vector3(x, 5.4f, face - .35f), new Vector3(.06f, .55f, .06f), new Vector3(-50, 0, 0), rail);
        PointLight(g, "Porch light - main house", new Vector3(20.5f, 5.2f, face - .7f), 2.2f, 7f, new Color(1f, .82f, .6f));
        // 踊り場の下の玄関（1階の飾りの扉）：玄関マット・ベンチ・植木鉢・郵便受け・灯
        float gface = HZ0 - HT;
        Box(g, "Doormat", 19.95f, 21.15f, 0, .03f, gface - .8f, gface, Mat("casino_red"));
        Box(g, "Porch bench seat", 21.6f, 22.8f, .42f, .48f, gface - .45f, gface - .05f, Mat("planter_wood"));
        foreach (float x in new[] { 21.7f, 22.7f }) Box(g, "Porch bench leg", x - .04f, x + .04f, 0, .42f, gface - .42f, gface - .08f, Mat("metal_dark"));
        Cyl(g, "Porch pot", new Vector3(19.4f, .3f, gface - .4f), new Vector3(.5f, .3f, .5f), Mat("roof_terra"));
        Cyl(g, "Porch pot plant", new Vector3(19.4f, .8f, gface - .4f), new Vector3(.6f, .3f, .6f), Mat("planter_leaf"));
        Box(g, "Ground door lamp", 21.3f, 21.5f, 1.9f, 2.2f, gface - .14f, gface, Mat("metal_dark"));
        Box(g, "Ground door lamp lens", 21.33f, 21.47f, 1.93f, 2.17f, gface - .15f, gface - .14f, Mat("bulb"));
        Box(g, "Mailbox", 19.55f, 19.85f, 1.2f, 1.55f, gface - .18f, gface, Mat("frame_h_navy"));
        // 階段の上り口の両わきの植木鉢（上り口は西の端。ふさがない）
        foreach (var (x, z) in new[] { (StairX0 - .35f, StairZ0 - .45f), (StairX0 + 1.2f, StairZ0 - .45f) })
        {
            Cyl(g, "Stair pot", new Vector3(x, .25f, z), new Vector3(.45f, .25f, .45f), Mat("roof_terra"));
            Cyl(g, "Stair pot plant", new Vector3(x, .65f, z), new Vector3(.5f, .22f, .5f), Mat("planter_leaf"));
        }
    }
}
#endif
