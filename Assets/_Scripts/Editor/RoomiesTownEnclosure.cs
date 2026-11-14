#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 町の囲い（2026/11/14）。何度実行しても同じ結果。
/// 遊べる範囲（x ±39.3 / z ±24。見えない壁 Roomies_Boundary）の外側 1〜10m に、町の中から見てすき間のない囲いを置く。
/// ・北：家の裏の並び（窓・室外機・とい・物干しとベランダ・勝手口）と、手前のブロック塀・生け垣。高架の下は倉庫。
/// ・西：倉庫の裏（波板・高窓・扉・換気扇・配管）。大通りの出口は工事の仮囲いとバリケードでふさぐ。
/// ・東：線路の盛土（コンクリートの擁壁・手すり・架線の柱・防音の壁）と、手前の金網のフェンス。
///   大通りは盛土の下をくぐるトンネルにして、口をバリケード、中を仮囲いでふさぐ。
/// ・南：裏の家並み（Town houses）の家と家のすき間と、両端の角を板塀でふさぐ。
/// 置く物はすべて町のプレハブ（RoomiesNeighborhood）の「Town enclosure」の下（シーンの Roomies_EnvironmentArt）。
/// 境界の外なので当たり判定は付けない。遊べる範囲と Roomies_Boundary は変えない。
/// 寸法と色は下の表。最後に「9 Combine For Speed」と同じ方法で、素材ごとに1つのメッシュへまとめる。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string EnclosureGroup = "Town enclosure";

    // ================================================================ 表（寸法・色）
    const float EncGround = -.1f;           // 境界の外の地面は y -0.08（RoomiesNature の Outer terrain）。少し埋める
    const float NorthWallZ = 25.35f, NorthWallH = 1.6f;
    const float NorthBackZ = 26.6f, NorthDepth = 7f;
    const float WestBackX = -41f, WestClosureX = -44f;
    const float EastFenceX = 40.75f, EastWallX = 42f, EastWallH = 5f, EastBodyX = 52f, EastLength = 70f;
    const float PortalHalf = 6.6f, PortalH = 3.9f, EastClosureX = 45f;
    const float SouthGapZ = -27.6f, SouthGapH = 2.2f;
    const float RoadHalf = 3.98f, ClosureHalf = 7.6f;
    // まとめる区画の大きさ。囲いは町をぐるっと回るので区画に分けず、素材（と影の有無）ごとに1つにする（描く回数を減らす）
    const float EncCombineCell = 0f;

    // 北の家（x0〜x1、階数、壁・屋根・枠、屋根の向き、手前の塀：block / hedge、物干し）
    static readonly (float x0, float x1, int floors, string wall, string roof, string frame, RoofKind kind, bool hedge, bool laundry)[] NorthHouses =
    {
        (-40.8f, -33.6f, 2, "wall_h2", "roof_slate", "frame_h_white", RoofKind.SideGable, false, true),
        (-33.1f, -26.9f, 3, "wall_b2", "roof_terra", "frame_h_navy", RoofKind.FrontGable, false, false),
        (-26.4f, -18.2f, 2, "wall_h4", "roof_teal", "frame_h_white", RoofKind.SideGable, true, true),
        (-8.2f, -1.6f, 2, "wall_h1", "roof_slate", "frame_h_green", RoofKind.SideGable, false, true),
        (-1.1f, 5.3f, 3, "wall_h3", "roof_terra", "frame_h_white", RoofKind.FrontGable, false, false),
        (5.8f, 12.6f, 2, "wall_b1", "roof_moss", "frame_h_white", RoofKind.SideGable, false, true),
        (13.1f, 19.3f, 2, "wall_h5", "roof_slate", "frame_h_navy", RoofKind.FrontGable, true, false),
        (19.8f, 26.8f, 3, "wall_h2", "roof_terra", "frame_h_white", RoofKind.SideGable, true, true),
        (27.3f, 33.7f, 2, "wall_h4", "roof_slate", "frame_h_white", RoofKind.FrontGable, false, true),
        (34.2f, 41.8f, 2, "wall_b2", "roof_teal", "frame_h_green", RoofKind.SideGable, false, false),
    };
    // 高架の下の倉庫（橋脚 z 36.3〜39.7 と、桁の下面 y 9.6 にかからない）
    const float ViaductStoreX0 = -18.1f, ViaductStoreX1 = -8.3f, ViaductStoreH = 6.8f, ViaductStoreDepth = 7f;

    // 西の倉庫（z0〜z1、奥行き、高さ、屋根の色）。町に向いた面は x = WestBackX
    static readonly (float z0, float z1, float depth, float height, string roof)[] WestSheds =
    {
        (-34f, -7.6f, 16f, 7.5f, "roof_teal"),
        (7.6f, 20f, 14f, 8.5f, "roof_slate"),
        (20.5f, 34f, 15f, 7f, "roof_flat"),
    };

    // 洗濯物の色
    static readonly string[] LaundryColors = { "flag_white", "awning_a", "flag_blue", "flower_yellow", "awning_b" };

    [MenuItem("Roomies/Art/Town/4 Enclosure")]
    internal static void BuildEnclosure()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        int parts, made;
        try
        {
            var g = Fresh(root.transform, EnclosureGroup);
            EncNorth(EncGroup(g, "North - house backs"));
            EncWest(EncGroup(g, "West - warehouses"));
            EncEast(EncGroup(g, "East - railway embankment"));
            EncSouth(EncGroup(g, "South - gap fences"));
            (parts, made) = CombineUnder(g, Slug(EnclosureGroup), EncCombineCell, true);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Town] enclosure: {parts} parts -> {made} meshes");
    }

    static Transform EncGroup(Transform parent, string name)
    {
        var t = new GameObject(name) { isStatic = true }.transform;
        t.SetParent(parent, false);
        return t;
    }

    /// <summary>向きのある空の親（+Z が外向き＝町の方）。</summary>
    static Transform EncFrame(Transform parent, string name, Vector3 pos, float yaw)
    {
        var t = EncGroup(parent, name);
        t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0);
        return t;
    }

    // ================================================================ 北：家の裏
    static void EncNorth(Transform g)
    {
        foreach (var h in NorthHouses)
        {
            float w = h.x1 - h.x0, cx = (h.x0 + h.x1) / 2;
            var spec = new HouseSpec { width = w, depth = NorthDepth, floors = h.floors, floorHeight = 2.8f, roof = h.kind, wall = h.wall, roofMat = h.roof, frame = h.frame, doorLeft = cx > 0, far = true, pitch = h.kind == RoofKind.FrontGable ? 40 : 34 };
            // 正面は北（外）向き。裏の面（z = NorthBackZ）が町を向く
            var house = EncFrame(g, $"House back {cx:F0}", new Vector3(cx, EncGround, NorthBackZ + NorthDepth), 0);
            BuildHouseModel(house, spec);
            var back = EncFrame(house, "Back details", new Vector3(0, 0, -NorthDepth), 180);
            EncHouseBack(back, spec, h.laundry);
            // 手前の塀（裏庭 1m の先）
            if (h.hedge) EncHedge(g, h.x0 - .25f, h.x1 + .25f, NorthWallZ - .2f, NorthWallZ + .5f);
            else EncBlockWall(g, h.x0 - .25f, h.x1 + .25f, NorthWallZ, NorthWallH);
        }
        // 家と家のすき間の板塀（裏の面にそろえる）
        for (int i = 0; i < NorthHouses.Length - 1; i++)
        {
            float a = NorthHouses[i].x1, b = NorthHouses[i + 1].x0;
            if (b - a > 2f) continue;   // 高架の下（倉庫）は別
            EncBoardFence(g, a, b, NorthBackZ + .3f, 2.6f, true);
        }
        EncViaductStore(g);
        EncBlockWall(g, ViaductStoreX0 - .25f, ViaductStoreX1 + .25f, NorthWallZ, 1.2f);
    }

    /// <summary>家の裏の面（+Z が町の方、x は家の幅の中）：勝手口・室外機・とい・物干しとベランダ・メーター。</summary>
    static void EncHouseBack(Transform t, HouseSpec s, bool laundry)
    {
        float W = s.width, E = s.Eave;
        var trim = Mat(s.frame); var dark = Mat("metal_dark"); var unit = Mat("ac_unit"); var sill = Mat("sill");
        // 勝手口（真ん中）と小さな庇・段
        FarWindow(t, 0, .3f, .9f, 2.05f, trim, "Door", true);
        Box(t, "Back door canopy", -.75f, .75f, 2.55f, 2.63f, 0, .6f, dark);
        Box(t, "Back door step", -.7f, .7f, 0, .3f, 0, .45f, Mat("stair_concrete"));
        // 室外機（1階の窓の下）と冷媒管
        float acX = -W * .24f;
        Box(t, "AC unit", acX - .42f, acX + .42f, .12f, .74f, .05f, .38f, unit);
        Box(t, "AC grille", acX - .3f, acX + .14f, .2f, .66f, .38f, .4f, dark);
        Box(t, "AC stand", acX - .45f, acX + .45f, 0, .12f, .02f, .4f, dark);
        Box(t, "AC pipe", acX + .3f, acX + .36f, .5f, 1.15f, .02f, .08f, unit);
        // 2階以上の窓の横の室外機（壁の金具）
        if (s.floors >= 2)
        {
            float y = .3f + s.floorHeight + .25f, x = W * .24f + 1.0f;
            Box(t, "AC unit", x - .36f, x + .36f, y, y + .55f, .06f, .36f, unit);
            Box(t, "AC grille", x - .26f, x + .1f, y + .07f, y + .48f, .36f, .38f, dark);
            Box(t, "AC bracket", x - .34f, x + .34f, y - .06f, y, .02f, .4f, dark);
        }
        // といの縦管（端）と、横の雨どい（軒が裏に来る屋根だけ）
        float pipeX = s.doorLeft ? -W / 2 + .14f : W / 2 - .14f;
        Box(t, "Downpipe", pipeX - .05f, pipeX + .05f, 0, E, .03f, .13f, dark);
        if (s.roof == RoofKind.SideGable)
        {
            float p = s.pitch * Mathf.Deg2Rad;
            float y = E - Mathf.Tan(p) * .4f - .14f;
            Box(t, "Gutter", -W / 2 - .4f, W / 2 + .4f, y - .06f, y + .06f, .38f, .52f, dark);
        }
        // 電気のメーターと配線（縦管の反対の端）
        float mx = s.doorLeft ? W / 2 - .55f : -W / 2 + .55f;
        Box(t, "Meter box", mx - .14f, mx + .14f, 1.4f, 1.8f, .02f, .12f, Mat("ac_unit"));
        Box(t, "Meter conduit", mx - .02f, mx + .02f, 1.8f, 2.7f, .02f, .06f, dark);
        // ベランダと物干し（2階の真ん中）
        if (laundry && s.floors >= 2)
        {
            float y = .3f + s.floorHeight, bw = Mathf.Min(3.2f, W - 1.2f);
            Box(t, "Balcony slab", -bw / 2, bw / 2, y - .12f, y + .04f, 0, .95f, sill);
            Box(t, "Balcony panel", -bw / 2, bw / 2, y + .04f, y + 1.0f, .88f, .95f, trim);
            Box(t, "Balcony panel", -bw / 2, -bw / 2 + .07f, y + .04f, y + 1.0f, 0, .95f, trim);
            Box(t, "Balcony panel", bw / 2 - .07f, bw / 2, y + .04f, y + 1.0f, 0, .95f, trim);
            Box(t, "Balcony rail", -bw / 2 - .02f, bw / 2 + .02f, y + 1.0f, y + 1.06f, .84f, .97f, dark);
            // 物干しの金具2本と竿
            float py = y + 2.05f;
            foreach (float x in new[] { -bw / 2 + .15f, bw / 2 - .15f })
                Box(t, "Laundry bracket", x - .03f, x + .03f, py - .25f, py + .03f, 0, .7f, dark);
            Box(t, "Laundry pole", -bw / 2 + .05f, bw / 2 - .05f, py - .03f, py + .03f, .55f, .61f, Mat("handle"));
            // 洗濯物（表の位置で色と大きさを変える）
            int n = Mathf.FloorToInt((bw - .5f) / .55f);
            for (int i = 0; i < n; i++)
            {
                float x = -bw / 2 + .35f + i * .55f;
                float len = (i % 3 == 0) ? .8f : (i % 3 == 1 ? .55f : .65f);
                string c = LaundryColors[(i + Mathf.RoundToInt(W * 10)) % LaundryColors.Length];
                Box(t, "Laundry", x, x + .42f, py - .03f - len, py - .03f, .56f, .6f, Mat(c));
            }
        }
    }

    static void EncBlockWall(Transform g, float x0, float x1, float z, float h)
    {
        if (x1 - x0 < .05f) return;
        var block = Mat("jail_block"); var cap = Mat("sill");
        Box(g, "Block wall", x0, x1, EncGround, h, z, z + .2f, block);
        Box(g, "Block wall cap", x0 - .02f, x1 + .02f, h, h + .07f, z - .03f, z + .23f, cap);
        // 控え（4m ごと。町の外側に出す）
        for (float x = x0 + 1.8f; x < x1 - .5f; x += 4f)
            Box(g, "Block wall pier", x - .2f, x + .2f, EncGround, h - .2f, z + .2f, z + .6f, block);
    }

    static void EncHedge(Transform g, float x0, float x1, float z0, float z1)
    {
        var leaf = Mat("planter_leaf");
        Box(g, "Hedge", x0, x1, EncGround, 1.55f, z0, z1, leaf);
        Box(g, "Hedge top", x0 + .08f, x1 - .08f, 1.55f, 1.75f, z0 + .08f, z1 - .08f, leaf);
        Box(g, "Hedge curb", x0, x1, EncGround, .2f, z0 - .08f, z0 + .02f, Mat("plinth"));
    }

    /// <summary>板塀（x0〜x1、z の線に沿う）。alongX が false のときは z 方向に伸ばす（x0〜x1 を z として、z を x として読む）。</summary>
    static void EncBoardFence(Transform g, float a0, float a1, float c, float h, bool alongX)
    {
        var wood = Mat("door_wood"); var post = Mat("metal_dark");
        void B(string n, float u0, float u1, float y0, float y1, float v0, float v1, Material m)
        {
            if (alongX) Box(g, n, u0, u1, y0, y1, v0, v1, m); else Box(g, n, v0, v1, y0, y1, u0, u1, m);
        }
        B("Board fence", a0, a1, EncGround, h, c - .04f, c + .04f, wood);
        // 横の桟（町の側）と柱
        foreach (float y in new[] { .4f, h - .3f })
            B("Board fence rail", a0, a1, y - .05f, y + .05f, c - .1f, c - .04f, wood);
        for (float u = a0; u <= a1 + .001f; u += Mathf.Max(.5f, (a1 - a0) / Mathf.Max(1, Mathf.Ceil((a1 - a0) / 2.4f))))
            B("Board fence post", u - .05f, u + .05f, EncGround, h + .05f, c - .12f, c + .02f, post);
    }

    /// <summary>高架の下の倉庫（シャッター2枚・扉・明かり取り）。正面は町（-Z）向き。</summary>
    static void EncViaductStore(Transform g)
    {
        float x0 = ViaductStoreX0, x1 = ViaductStoreX1, z0 = NorthBackZ - .4f, H = ViaductStoreH;
        var t = EncFrame(g, "Under viaduct store", new Vector3((x0 + x1) / 2, EncGround, z0), 180);
        float W = x1 - x0;
        var wall = Mat("wall_express"); var dark = Mat("metal_dark"); var slat = Mat("shutter");
        Box(t, "Store wall", -W / 2, W / 2, 0, H, -ViaductStoreDepth, 0, wall);
        Box(t, "Store roof", -W / 2 - .1f, W / 2 + .1f, H, H + .25f, -ViaductStoreDepth, .35f, Mat("roof_flat"));
        Box(t, "Store fascia", -W / 2, W / 2, H - .7f, H, 0, .12f, Mat("frame_express"));
        // シャッター2枚（箱・ガイド・継ぎ目）
        foreach (float cx in new[] { -W * .25f, W * .18f })
        {
            float sw = W * .3f;
            Box(t, "Shutter", cx - sw / 2, cx + sw / 2, 0, 3.0f, 0, .06f, slat);
            for (float y = .3f; y < 3f; y += .3f)
                Box(t, "Shutter slat", cx - sw / 2, cx + sw / 2, y - .015f, y + .015f, .06f, .08f, dark);
            Box(t, "Shutter housing", cx - sw / 2 - .1f, cx + sw / 2 + .1f, 3.0f, 3.4f, 0, .35f, slat);
            Box(t, "Shutter guide", cx - sw / 2 - .1f, cx - sw / 2, 0, 3.0f, 0, .12f, dark);
            Box(t, "Shutter guide", cx + sw / 2, cx + sw / 2 + .1f, 0, 3.0f, 0, .12f, dark);
        }
        // 扉と小窓
        float dx = W / 2 - .9f;
        Box(t, "Store door", dx - .45f, dx + .45f, 0, 2.1f, 0, .05f, Mat("frame_h_green"));
        Box(t, "Store door frame", dx - .52f, dx + .52f, 2.1f, 2.18f, 0, .08f, dark);
        Box(t, "Store window", dx - .4f, dx + .4f, 2.6f, 3.2f, 0, .04f, Mat("jail_frosted"));
        Box(t, "Store lamp", -.15f, .15f, 3.55f, 3.75f, 0, .2f, Mat("bulb"));
        // 2階の事務所の窓（看板の下）
        for (float x = -W / 2 + 1.2f; x < W / 2 - .8f; x += 2.2f)
        {
            Box(t, "Window frame", x - .7f, x + .7f, 4.25f, 5.35f, 0, .08f, dark);
            Box(t, "Window glazing", x - .62f, x + .62f, 4.33f, 5.27f, .08f, .1f, Mat("glass_home_dark"));
        }
        // 前のパレット
        Box(t, "Pallet", -W * .45f, -W * .45f + 1.2f, 0, .15f, .1f, .5f, Mat("planter_wood"));
        Box(t, "Pallet", -W * .45f, -W * .45f + 1.2f, .15f, .3f, .1f, .5f, Mat("planter_wood"));
    }

    // ================================================================ 西：倉庫の裏と、大通りの仮囲い
    static void EncWest(Transform g)
    {
        for (int i = 0; i < WestSheds.Length; i++)
        {
            var s = WestSheds[i];
            // 町に向いた面は +X。親の +Z を +X に向ける（親の +X は -Z）
            var t = EncFrame(g, $"Warehouse back {i + 1}", new Vector3(WestBackX, EncGround, (s.z0 + s.z1) / 2), 90);
            EncShedBack(t, s.z1 - s.z0, s.depth, s.height, s.roof, i);
            if (i < WestSheds.Length - 1)
            {
                float a = s.z1, b = WestSheds[i + 1].z0;
                if (b - a > .01f && b - a < 2f)
                    Box(g, "Shed gap screen", WestBackX - .3f, WestBackX - .1f, EncGround, Mathf.Min(s.height, WestSheds[i + 1].height) - .3f, a - .1f, b + .1f, Mat("shutter"));
            }
        }
        // 大通りの出口：奥に仮囲い、手前にバリケード
        var closure = EncFrame(g, "West road closure", new Vector3(WestClosureX, EncGround, 0), 90);
        EncHoarding(closure, ClosureHalf * 2, 3f);
        var site = EncFrame(g, "West construction site", new Vector3(WestClosureX - 2f, EncGround, 0), 90);
        EncScaffold(site, ClosureHalf * 2 - .4f, 10f, 8f);
        EncClosureFloor(g, WestClosureX, -40f);
        var bar = EncFrame(g, "West barricade", new Vector3(-40.8f, 0, 0), 90);
        EncBarricades(bar, 5.4f);
    }

    /// <summary>倉庫の裏の面（+Z が町の方、x は -L/2〜L/2）：波板・腰の壁・高窓・扉・換気扇・配管・とい。</summary>
    static void EncShedBack(Transform t, float L, float D, float H, string roof, int seed)
    {
        var metal = Mat("shutter"); var dark = Mat("metal_dark"); var conc = Mat("wall_express");
        Box(t, "Shed body", -L / 2, L / 2, 0, H, -D, 0, metal);
        Box(t, "Shed wainscot", -L / 2, L / 2, 0, 1.2f, 0, .08f, conc);
        // 波板の筋（町の側だけ）
        for (float x = -L / 2 + .25f; x < L / 2 - .1f; x += .5f)
            Box(t, "Shed rib", x - .04f, x + .04f, 1.2f, H - .3f, 0, .05f, metal);
        // 横の面（倉庫と倉庫のあいだ・道の出口から見える）にも筋と腰の壁
        foreach (float s in new[] { -1f, 1f })
        {
            float x0 = s * L / 2, x1 = s * (L / 2 + .05f);
            for (float z = -D + .25f; z < -.1f; z += .5f)
                Box(t, "Shed rib", Mathf.Min(x0, x1), Mathf.Max(x0, x1), 1.2f, H - .3f, z - .04f, z + .04f, metal);
            Box(t, "Shed wainscot", Mathf.Min(x0, s * (L / 2 + .08f)), Mathf.Max(x0, s * (L / 2 + .08f)), 0, 1.2f, -D, 0, conc);
        }
        // 屋根・軒先・とい・縦管
        Box(t, "Shed roof", -L / 2 - .3f, L / 2 + .3f, H, H + .3f, -D - .3f, .5f, Mat(roof));
        Box(t, "Shed gutter", -L / 2 - .3f, L / 2 + .3f, H - .18f, H + .02f, .5f, .7f, dark);
        foreach (float x in new[] { -L / 2 + .4f, L / 2 - .4f })
            Box(t, "Shed downpipe", x - .06f, x + .06f, 0, H - .1f, .52f, .64f, dark);
        // 高窓の列（すりガラス。夜はうっすら明るい）
        int n = Mathf.Max(2, Mathf.FloorToInt(L / 4.2f));
        for (int i = 0; i < n; i++)
        {
            float cx = -L / 2 + L * (i + .5f) / n;
            float y0 = H - 2.4f, y1 = H - 1.4f;
            Box(t, "Window frame", cx - 1.15f, cx + 1.15f, y0 - .08f, y1 + .08f, .04f, .1f, dark);
            Box(t, "Window glazing", cx - 1.05f, cx + 1.05f, y0, y1, .1f, .12f, Mat("jail_frosted"));
            Box(t, "Window mullion", cx - .03f, cx + .03f, y0, y1, .1f, .14f, dark);
        }
        // 扉（庇・段）と、扉の横の明かり
        float dx = (seed % 2 == 0 ? .22f : -.28f) * L;
        Box(t, "Shed door", dx - .5f, dx + .5f, 0, 2.15f, .08f, .13f, Mat("frame_h_navy"));
        Box(t, "Shed door frame", dx - .58f, dx + .58f, 2.15f, 2.25f, .08f, .16f, dark);
        Box(t, "Shed door canopy", dx - .9f, dx + .9f, 2.55f, 2.63f, .05f, .9f, dark);
        Box(t, "Shed door step", dx - .7f, dx + .7f, 0, .18f, .05f, .55f, Mat("stair_concrete"));
        Box(t, "Shed lamp", dx + .75f, dx + 1.0f, 2.3f, 2.5f, .05f, .22f, Mat("bulb"));
        // 換気扇と配管
        float fx = -dx * .9f;
        Box(t, "Exhaust fan", fx - .45f, fx + .45f, H - 3.9f, H - 3.0f, .05f, .3f, Mat("ac_unit"));
        Box(t, "Exhaust grille", fx - .35f, fx + .35f, H - 3.8f, H - 3.1f, .3f, .32f, dark);
        Box(t, "Pipe run", Mathf.Min(fx, dx) - 1f, Mathf.Max(fx, dx) + 1f, 3.3f, 3.42f, .14f, .26f, dark);
        Box(t, "Pipe drop", fx + .6f, fx + .72f, 1.2f, 3.3f, .14f, .26f, dark);
        // 電気のメーター
        Box(t, "Meter box", dx + 1.3f, dx + 1.7f, 1.4f, 1.9f, .08f, .2f, Mat("ac_unit"));
        // 手前のドラム缶とパレット（境界の外の細い所だけ）
        float bx = -dx * .4f;
        foreach (float o in new[] { 0f, .7f })
            Cyl(t, "Drum", new Vector3(bx + o, .45f, .45f), new Vector3(.58f, .45f, .58f), Mat(seed % 2 == 0 ? "frame_h_navy" : "hydrant"));
        Box(t, "Pallet", bx + 1.5f, bx + 2.7f, 0, .15f, .1f, .5f, Mat("planter_wood"));
    }

    /// <summary>工事の仮囲い（+Z が町の方、x は -W/2〜W/2）。白い鋼板・上の青い帯・柱・注意の板。</summary>
    static void EncHoarding(Transform t, float W, float H)
    {
        var panel = Mat("frame_works"); var dark = Mat("metal_dark");
        int k = 0;
        for (float x = -W / 2; x < W / 2 - .01f; x += .5f, k++)
            Box(t, "Hoarding panel", x, Mathf.Min(x + .5f, W / 2), 0, H, 0, k % 2 == 0 ? .05f : .08f, panel);
        Box(t, "Hoarding band", -W / 2, W / 2, H - .45f, H - .2f, .08f, .1f, Mat("flag_blue"));
        Box(t, "Hoarding rail", -W / 2, W / 2, H - .06f, H + .04f, -.05f, .1f, dark);
        for (float x = -W / 2 + .1f; x <= W / 2; x += 2.5f)
            Box(t, "Hoarding post", x - .05f, x + .05f, 0, H, -.15f, -.05f, dark);
        // 注意の板（黄と黒の帯）
        Box(t, "Notice board", -1.2f, 1.2f, 1.2f, 2.2f, .08f, .11f, Mat("frame_express"));
        for (int i = 0; i < 6; i++)
        {
            float x = -1.1f + i * .4f;
            Box(t, "Notice stripe", x, x + .2f, 1.25f, 1.45f, .11f, .12f, Mat("casino_black"));
            Box(t, "Notice stripe", x + .2f, x + .4f, 1.95f, 2.15f, .11f, .12f, Mat("casino_black"));
        }
        // 出入りの扉（片側）
        Box(t, "Hoarding gate", W / 2 - 3.2f, W / 2 - 1.4f, 0, H - .5f, .08f, .12f, panel);
        Box(t, "Hoarding gate frame", W / 2 - 3.3f, W / 2 - 1.3f, H - .5f, H - .42f, .08f, .14f, dark);
    }

    /// <summary>仮囲いの奥の工事中の建物（+Z が町の方）：養生シートで覆った足場・パイプの格子・上に出た柱。</summary>
    static void EncScaffold(Transform t, float W, float H, float D)
    {
        var dark = Mat("metal_dark");
        Box(t, "Site frame", -W / 2 + .3f, W / 2 - .3f, 0, H - 1f, -D, -.6f, Mat("wall_express"));
        Box(t, "Scaffold sheet", -W / 2, W / 2, 0, H, -.04f, 0, Mat("casino_felt"));
        Box(t, "Scaffold sheet", -W / 2 - .04f, -W / 2, 0, H, -D * .6f, 0, Mat("casino_felt"));
        Box(t, "Scaffold sheet", W / 2, W / 2 + .04f, 0, H, -D * .6f, 0, Mat("casino_felt"));
        for (float x = -W / 2; x <= W / 2 + .01f; x += W / Mathf.Ceil(W / 1.8f))
            Box(t, "Scaffold pipe", x - .03f, x + .03f, 0, H + .4f, 0, .06f, dark);
        for (float y = 1.8f; y < H; y += 1.8f)
            Box(t, "Scaffold pipe", -W / 2 - .05f, W / 2 + .05f, y - .03f, y + .03f, .06f, .12f, dark);
        // シートの上に出た鉄骨の柱と梁
        foreach (float x in new[] { -W / 2 + .6f, 0f, W / 2 - .6f })
            Box(t, "Site column", x - .15f, x + .15f, H - 1f, H + 1.6f, -D * .5f - .15f, -D * .5f + .15f, Mat("hydrant"));
        Box(t, "Site beam", -W / 2 + .45f, W / 2 - .45f, H + 1.3f, H + 1.6f, -D * .5f - .12f, -D * .5f + .12f, Mat("hydrant"));
        // 会社の看板（白い板）
        Box(t, "Site sign", -2.4f, 2.4f, H - 2.6f, H - 1.4f, 0, .1f, Mat("frame_works"));
        Box(t, "Site sign band", -2.4f, 2.4f, H - 2.6f, H - 2.4f, .1f, .11f, Mat("flag_blue"));
    }

    /// <summary>バリケード（+Z が町の方、x は -half〜half）：A形の台に赤白の板、黄色の灯、両端にコーン。</summary>
    static void EncBarricades(Transform t, float half)
    {
        int units = Mathf.Max(1, Mathf.RoundToInt(half * 2 / 3.6f));
        float uw = half * 2 / units;
        var red = Mat("hydrant"); var white = Mat("road_paint_white"); var dark = Mat("metal_dark");
        for (int u = 0; u < units; u++)
        {
            float x0 = -half + u * uw + .1f, x1 = x0 + uw - .2f;
            foreach (float x in new[] { x0 + .1f, x1 - .1f })
            {
                RBox(t, "Barricade leg", new Vector3(x, .5f, .22f), new Vector3(.06f, 1.05f, .06f), new Vector3(-14, 0, 0), dark);
                RBox(t, "Barricade leg", new Vector3(x, .5f, -.22f), new Vector3(.06f, 1.05f, .06f), new Vector3(14, 0, 0), dark);
            }
            foreach (float y in new[] { .55f, .9f })
            {
                int k = 0;
                for (float x = x0; x < x1 - .01f; x += .4f, k++)
                    Box(t, "Barricade board", x, Mathf.Min(x + .4f, x1), y - .1f, y + .1f, .02f, .06f, k % 2 == 0 ? red : white);
            }
            // 点滅灯（見た目だけ。光は出さない）
            float lx = (x0 + x1) / 2;
            Box(t, "Barricade lamp base", lx - .08f, lx + .08f, 1.0f, 1.06f, -.03f, .09f, Mat("frame_express"));
            Box(t, "Barricade lamp", lx - .07f, lx + .07f, 1.06f, 1.2f, -.02f, .08f, Mat("bulb"));
        }
        foreach (float x in new[] { -half - .5f, half + .5f })
        {
            Box(t, "Cone base", x - .2f, x + .2f, 0, .04f, -.2f, .2f, red);
            Cyl(t, "Cone", new Vector3(x, .2f, 0), new Vector3(.28f, .16f, .28f), red);
            Cyl(t, "Cone", new Vector3(x, .42f, 0), new Vector3(.2f, .07f, .2f), white);
            Cyl(t, "Cone", new Vector3(x, .6f, 0), new Vector3(.13f, .11f, .13f), red);
        }
    }

    /// <summary>道の出口の床（町の道路の高さにそろえる：車道 0.1・歩道 0.15）。</summary>
    static void EncClosureFloor(Transform g, float xa, float xb)
    {
        float x0 = Mathf.Min(xa, xb), x1 = Mathf.Max(xa, xb);
        Box(g, "Closure road", x0, x1, -.05f, RoadTop, -RoadHalf, RoadHalf, Mat("plinth"));
        foreach (float s in new[] { -1f, 1f })
        {
            float z0 = s * RoadHalf, z1 = s * ClosureHalf;
            Box(g, "Closure sidewalk", x0, x1, -.05f, WalkTop, Mathf.Min(z0, z1), Mathf.Max(z0, z1), Mat("paving"));
            Box(g, "Closure curb", x0, x1, -.05f, WalkTop + .01f, Mathf.Min(z0, z0 + s * .15f), Mathf.Max(z0, z0 + s * .15f), Mat("curb_granite"));
        }
    }

    // ================================================================ 東：線路の盛土
    static void EncEast(Transform g)
    {
        var conc = Mat("wall_express"); var dark = Mat("metal_dark"); var cap = Mat("sill"); var joint = Mat("plinth");
        float L = EastLength, H = EastWallH, ph = PortalHalf, pt = PortalH;
        // 盛土の本体（中は見えない）。道の所はトンネルの穴を空ける
        var face = Mat("police_stone");   // 擁壁は西を向いて日陰になりやすいので、暖かい色のコンクリート
        Box(g, "Embankment body", EastWallX, EastBodyX, EncGround, H, -L, -ph, face);
        Box(g, "Embankment body", EastWallX, EastBodyX, EncGround, H, ph, L, face);
        Box(g, "Embankment body", EastWallX, EastBodyX, pt, H, -ph, ph, face);
        // 擁壁の笠木・目地・水抜き穴
        Box(g, "Retaining wall coping", EastWallX - .12f, EastWallX + .5f, H, H + .15f, -L, L, cap);
        for (float z = -L + 3f; z < L; z += 6f)
        {
            if (Mathf.Abs(z) < ph + .3f) continue;
            Box(g, "Retaining wall joint", EastWallX - .02f, EastWallX, EncGround, H, z - .03f, z + .03f, joint);
        }
        for (float z = -L + 1.5f; z < L; z += 3f)
        {
            if (Mathf.Abs(z) < ph + .5f) continue;
            Box(g, "Weep hole", EastWallX - .015f, EastWallX, .9f, 1.05f, z - .07f, z + .07f, Mat("casino_black"));
        }
        // 盛土の裏の斜面（遠景から見える分）
        float run = 8f, ang = Mathf.Atan2(H, run) * Mathf.Rad2Deg, slab = Mathf.Sqrt(run * run + H * H);
        RBox(g, "Embankment slope", new Vector3(EastBodyX + run / 2, H / 2 - .4f, 0), new Vector3(slab, .8f, L * 2), new Vector3(0, 0, -ang), Mat("planter_leaf"));
        // 線路：砂利・枕木の帯・レール
        Box(g, "Track ballast", EastWallX + .6f, EastBodyX - 1.6f, H, H + .35f, -L, L, joint);
        Box(g, "Track sleepers", 45.3f, 47.7f, H + .35f, H + .45f, -L, L, Mat("planter_wood"));
        foreach (float x in new[] { 45.8f, 47.2f })
            Box(g, "Rail", x - .05f, x + .05f, H + .45f, H + .6f, -L, L, Mat("handle"));
        // 手前の手すり（町の側）
        for (float z = -L; z <= L; z += 2f)
            Box(g, "Embankment rail post", EastWallX + .2f, EastWallX + .26f, H + .15f, H + 1.2f, z - .03f, z + .03f, dark);
        foreach (float y in new[] { .65f, 1.15f })
            Box(g, "Embankment rail", EastWallX + .19f, EastWallX + .27f, H + y - .04f, H + y + .04f, -L, L, dark);
        // 架線の柱・腕・線
        for (float z = -L + 10f; z < L; z += 15f)
        {
            Box(g, "Catenary mast", EastWallX + .9f, EastWallX + 1.2f, H + .35f, H + 7.2f, z - .15f, z + .15f, dark);
            Box(g, "Catenary arm", EastWallX + .9f, 47.8f, H + 6.4f, H + 6.55f, z - .06f, z + .06f, dark);
            Box(g, "Catenary hanger", 46.45f, 46.55f, H + 5.6f, H + 6.4f, z - .03f, z + .03f, dark);
        }
        Box(g, "Contact wire", 46.47f, 46.53f, H + 5.57f, H + 5.63f, -L, L, dark);
        Box(g, "Messenger wire", 46.47f, 46.53f, H + 6.37f, H + 6.43f, -L, L, dark);
        // 向こう側の防音の壁
        Box(g, "Noise wall base", EastBodyX - 1.6f, EastBodyX - 1.3f, H, H + .9f, -L, L, conc);
        Box(g, "Noise wall panel", EastBodyX - 1.55f, EastBodyX - 1.45f, H + .9f, H + 2.3f, -L, L, Mat("shutter"));
        for (float z = -L; z <= L; z += 4f)
            Box(g, "Noise wall post", EastBodyX - 1.65f, EastBodyX - 1.35f, H + .9f, H + 2.4f, z - .1f, z + .1f, dark);

        EncPortal(g);
        EncChainLink(g, SouthGapZ, -ph - .3f);
        EncChainLink(g, ph + .3f, NorthWallZ);
    }

    /// <summary>大通りが盛土の下をくぐる口：枠・中の壁と天井・奥の仮囲い・口のバリケード。</summary>
    static void EncPortal(Transform g)
    {
        float ph = PortalHalf, pt = PortalH, x0 = EastWallX;
        var conc = Mat("wall_express"); var dark = Mat("plinth");
        // 口の枠（少し出す）
        Box(g, "Portal frame", x0 - .3f, x0 + .2f, pt, pt + .7f, -ph - .5f, ph + .5f, conc);
        foreach (float s in new[] { -1f, 1f })
            Box(g, "Portal frame", x0 - .3f, x0 + .2f, EncGround, pt, s > 0 ? ph : -ph - .5f, s > 0 ? ph + .5f : -ph, conc);
        // 中の天井と壁（暗めのコンクリート）
        Box(g, "Underpass ceiling", x0 + .2f, EastClosureX + .2f, pt - .1f, pt, -ph, ph, dark);
        foreach (float s in new[] { -1f, 1f })
            Box(g, "Underpass wall", x0 + .2f, EastClosureX + .2f, EncGround, pt, s > 0 ? ph - .1f : -ph, s > 0 ? ph : -ph + .1f, dark);
        Box(g, "Underpass lamp", x0 + 1.5f, x0 + 2.3f, pt - .18f, pt - .1f, -.1f, .1f, Mat("bulb"));
        // 奥の仮囲い（町の方を向く）
        var closure = EncFrame(g, "East underpass closure", new Vector3(EastClosureX, EncGround, 0), -90);
        EncHoarding(closure, ph * 2, pt - .1f);
        EncClosureFloor(g, 40f, EastClosureX);
        var bar = EncFrame(g, "East barricade", new Vector3(40.8f, 0, 0), -90);
        EncBarricades(bar, 5.4f);
    }

    /// <summary>金網のフェンス（x = EastFenceX、z0〜z1）。柱・上下の横棒・網（細い線の格子）。</summary>
    static void EncChainLink(Transform g, float z0, float z1)
    {
        if (z1 - z0 < .5f) return;
        var wire = Mat("jail_bars"); var post = Mat("metal_dark");
        float x = EastFenceX, H = 1.8f;
        for (float z = z0; z <= z1 + .01f; z += (z1 - z0) / Mathf.Max(1, Mathf.Ceil((z1 - z0) / 2.5f)))
            Box(g, "Fence post", x - .04f, x + .04f, EncGround, H + .05f, z - .04f, z + .04f, post);
        foreach (float y in new[] { .08f, H })
            Box(g, "Fence rail", x - .025f, x + .025f, y - .025f, y + .025f, z0, z1, post);
        for (float y = .3f; y < H - .1f; y += .3f)
            Box(g, "Fence wire", x - .008f, x + .008f, y - .008f, y + .008f, z0, z1, wire);
        for (float z = z0 + .15f; z < z1; z += .3f)
            Box(g, "Fence wire", x - .008f, x + .008f, .08f, H, z - .008f, z + .008f, wire);
        // 足元の砂利と草
        Box(g, "Fence gravel", x - .3f, EastWallX, EncGround, .03f, z0, z1, Mat("road_gutter"));
        for (float z = z0 + 1.3f; z < z1 - .5f; z += 3.7f)
            Box(g, "Weeds", x + .25f, x + .75f, EncGround, .35f + .15f * Mathf.Repeat(z * .37f, 1f), z - .3f, z + .3f, Mat("planter_leaf"));
    }

    // ================================================================ 南：裏の家並みのすき間
    static void EncSouth(Transform g)
    {
        var row = BackRow().Select(r => (x0: r.x - r.spec.width / 2, x1: r.x + r.spec.width / 2)).OrderBy(r => r.x0).ToArray();
        for (int i = 0; i < row.Length - 1; i++)
            EncBoardFence(g, row[i].x1, row[i + 1].x0, SouthGapZ, SouthGapH, true);
        // 両端の角（西は倉庫の裏、東は盛土の擁壁まで）
        EncBoardFence(g, WestBackX, row[0].x0, SouthGapZ, SouthGapH, true);
        EncBoardFence(g, row[row.Length - 1].x1, EastWallX, SouthGapZ, SouthGapH, true);
    }

    // ================================================================ 調査
    /// <summary>境界のまわり（|x| 34〜62・|z| 20〜48）にある見た目と当たり判定を Temp/RoomiesTown/edge_survey.txt に書く（調査用。シーンは変えない）。</summary>
    [MenuItem("Roomies/Art/Town/Enclosure - Survey Edges")]
    internal static void SurveyEdges()
    {
        var sb = new StringBuilder();
        bool Near(Bounds b)
        {
            bool inX = b.max.x > -62 && b.min.x < 62, inZ = b.max.z > -48 && b.min.z < 48;
            bool edge = b.max.x > 34 || b.min.x < -34 || b.max.z > 20 || b.min.z < -20;
            return inX && inZ && edge;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
            foreach (var root in SceneManager.GetSceneAt(i).GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var b = r.bounds;
                    if (!Near(b) || b.size.magnitude < .6f) continue;
                    sb.AppendLine($"REN {Path(r.transform)} min {b.min.x:F1},{b.min.y:F1},{b.min.z:F1} max {b.max.x:F1},{b.max.y:F1},{b.max.z:F1}");
                }
                foreach (var c in root.GetComponentsInChildren<Collider>(true))
                {
                    var b = c.bounds;
                    if (!Near(b)) continue;
                    sb.AppendLine($"{(c.isTrigger ? "TRG" : "COL")} {Path(c.transform)} min {b.min.x:F1},{b.min.y:F1},{b.min.z:F1} max {b.max.x:F1},{b.max.y:F1},{b.max.z:F1}");
                }
            }
        Directory.CreateDirectory("Temp/RoomiesTown");
        File.WriteAllText("Temp/RoomiesTown/edge_survey.txt", sb.ToString());
    }
}
#endif
