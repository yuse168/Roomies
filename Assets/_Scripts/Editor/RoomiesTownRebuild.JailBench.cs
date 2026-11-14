#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 牢屋の作業台（JailLabor_Placeholder.prefab）の見た目（2026/11/14）。何度実行しても同じ結果。
/// ・触るのはプレハブの Visual の中だけ。仮の見た目 Visual/Placeholder を消し、Visual/Workbench に組む。
///   ルートの当たり判定（1.5 × 1.1 × 0.75）と SmugglingJailLabor はそのまま。見た目に当たり判定は付けない。
/// ・作業台は当たり判定の箱（x ±0.75・z ±0.375）の中に収める（すり抜ける見た目を外に出さない）。前（-Z）が留置室の入口側。
///   天板・脚・下の棚と部品の箱・引き出し・前の左の万力・工具・部品の皿と小箱・後ろの有孔ボードと掛けた工具・クランプの電気スタンド。
/// ・シーンに直接置いてあった前の見た目「Workstation Art」（RoomiesPlayabilityPolish が置いた物）は消す（見た目が二重にならないように）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string JailLaborPrefab = "Assets/_Prefabs/Smuggling/JailLabor_Placeholder.prefab";
    const float BenchHalfX = .75f, BenchHalfZ = .375f, BenchTop = .98f, BenchSlab = .08f;

    [MenuItem("Roomies/Art/Rebuild/5 Jail Workbench")]
    internal static void BuildJailWorkbench()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(JailLaborPrefab);
        try
        {
            var visual = root.transform.Find("Visual");
            if (visual == null)
            {
                visual = new GameObject("Visual").transform;
                visual.SetParent(root.transform, false);
            }
            foreach (var name in new[] { "Placeholder", "Workbench" })
            {
                var old = visual.Find(name);
                if (old != null) Object.DestroyImmediate(old.gameObject);
            }
            var bench = new GameObject("Workbench").transform;
            bench.SetParent(visual, false);
            WorkbenchFrame(bench);
            WorkbenchVise(bench);
            WorkbenchTools(bench);
            WorkbenchPegboard(bench);
            WorkbenchLamp(bench);
            RoomiesBevelBuilder.ApplyTownUnder(bench);
            PrefabUtility.SaveAsPrefabAsset(root, JailLaborPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();

        // シーンの作業台：前の見た目（シーンにだけある子）を消し、もう無い部品への上書きを外す
        var scene = EditorSceneManager.GetActiveScene();
        foreach (var labor in Object.FindObjectsByType<SmugglingJailLabor>(FindObjectsInactive.Include).Where(l => l.gameObject.scene == scene))
        {
            var art = labor.transform.Find("Workstation Art");
            if (art != null) Object.DestroyImmediate(art.gameObject);
            if (PrefabUtility.IsPartOfPrefabInstance(labor))
                PrefabUtility.RemoveUnusedOverrides(new[] { PrefabUtility.GetOutermostPrefabInstanceRoot(labor) }, InteractionMode.AutomatedAction);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Rebuild] jail workbench built");
    }

    // ================================================================ 天板・脚・棚・引き出し
    static void WorkbenchFrame(Transform g)
    {
        var top = Mat("planter_wood"); var steel = Mat("metal_dark"); var navy = Mat("frame_h_navy"); var crate = Mat("deck_wood"); var handle = Mat("handle");
        float x0 = -BenchHalfX, x1 = BenchHalfX, z0 = -BenchHalfZ, z1 = BenchHalfZ;
        // 天板（厚い板を貼り合わせた形。継ぎ目の線）
        Box(g, "Workbench top", x0, x1, BenchTop - BenchSlab, BenchTop, z0, z1, top);
        foreach (float z in new[] { -.225f, -.075f, .075f, .225f })
            Box(g, "Top seam", x0 + .01f, x1 - .01f, BenchTop, BenchTop + .002f, z - .004f, z + .004f, steel);
        Box(g, "Top front band", x0, x1, BenchTop - BenchSlab - .06f, BenchTop - BenchSlab, z0, z0 + .03f, steel);
        // 脚（角の鉄）と、下の横つなぎ・棚
        foreach (float x in new[] { x0 + .03f, x1 - .08f })
            foreach (float z in new[] { z0 + .03f, z1 - .08f })
                Box(g, "Steel leg", x, x + .05f, 0f, BenchTop - BenchSlab, z, z + .05f, steel);
        foreach (float x in new[] { x0 + .03f, x1 - .08f })
            Box(g, "Steel stretcher", x, x + .05f, .16f, .2f, z0 + .03f, z1 - .03f, steel);
        Box(g, "Lower shelf", x0 + .04f, x1 - .04f, .2f, .23f, z0 + .05f, z1 - .05f, top);
        foreach (float x in new[] { x0 + .03f, x1 - .08f })
            Box(g, "Leg foot", x - .01f, x + .06f, 0f, .02f, z0 + .02f, z0 + .09f, steel);
        // 下の棚の部品の箱（木箱2つ・ふたの開いた方にねじ）
        Box(g, "Parts crate", -.6f, -.18f, .23f, .5f, -.25f, .2f, crate);
        Box(g, "Parts crate slat", -.605f, -.175f, .34f, .36f, -.255f, .205f, Mat("planter_wood"));
        Box(g, "Parts crate", -.12f, .22f, .23f, .45f, -.22f, .18f, crate);
        Box(g, "Crate bolts", -.1f, .2f, .45f, .47f, -.2f, .16f, Mat("jail_bars"));
        // 右の引き出し（2段。取っ手）
        Box(g, "Drawer cabinet", .28f, .7f, .5f, BenchTop - BenchSlab, z0 + .02f, z1 - .06f, navy);
        foreach (float y in new[] { .58f, .76f })
        {
            Box(g, "Drawer front", .3f, .68f, y - .07f, y + .07f, z0 + .005f, z0 + .02f, navy);
            Box(g, "Drawer handle", .43f, .55f, y - .012f, y + .012f, z0 - .015f, z0 + .005f, handle);
        }
    }

    // ================================================================ 万力（前の左の角）
    static void WorkbenchVise(Transform g)
    {
        var body = Mat("frame_h_navy"); var steel = Mat("jail_bars"); var dark = Mat("metal_dark");
        float x = -.48f, z0 = -BenchHalfZ;
        Box(g, "Vise base", x - .1f, x + .1f, BenchTop, BenchTop + .03f, z0 + .02f, z0 + .24f, dark);
        Box(g, "Vise body", x - .07f, x + .07f, BenchTop + .03f, BenchTop + .13f, z0 + .06f, z0 + .22f, body);
        Box(g, "Vise jaw fixed", x - .09f, x + .09f, BenchTop + .03f, BenchTop + .15f, z0 + .02f, z0 + .06f, body);
        Box(g, "Vise jaw plate", x - .085f, x + .085f, BenchTop + .1f, BenchTop + .145f, z0 + .015f, z0 + .02f, steel);
        Box(g, "Vise jaw moving", x - .09f, x + .09f, BenchTop + .03f, BenchTop + .15f, z0 - .02f, z0 + .01f, body);
        Box(g, "Vise slide bar", x - .03f, x + .03f, BenchTop + .05f, BenchTop + .09f, z0 - .02f, z0 + .06f, steel);
        // ねじと、横に通した棒の取っ手（前に出すぎないように天板の縁から 6cm まで）
        Cyl(g, "Vise screw", new Vector3(x, BenchTop + .07f, z0 - .035f), new Vector3(.025f, .02f, .025f), steel).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Cyl(g, "Vise handle bar", new Vector3(x, BenchTop + .07f, z0 - .05f), new Vector3(.014f, .1f, .014f), steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        foreach (float s in new[] { -1f, 1f })
            Box(g, "Vise handle knob", x + s * .1f - .012f, x + s * .1f + .012f, BenchTop + .058f, BenchTop + .082f, z0 - .062f, z0 - .038f, dark);
        // 万力にはさんだ板（作業中の部品）
        Box(g, "Clamped part", x - .06f, x + .06f, BenchTop + .1f, BenchTop + .2f, z0 + .005f, z0 + .012f, Mat("planter_wood"));
    }

    // ================================================================ 天板の上の工具と部品
    static void WorkbenchTools(Transform g)
    {
        var wood = Mat("planter_wood"); var steel = Mat("jail_bars"); var red = Mat("hydrant"); var dark = Mat("metal_dark");
        float y = BenchTop;
        // 作業マット
        Box(g, "Work mat", -.25f, .45f, y, y + .006f, -.3f, .12f, Mat("frame_h_green"));
        // かなづち（柄と頭）
        var handle = RBox(g, "Hammer handle", new Vector3(.08f, y + .025f, -.12f), new Vector3(.03f, .03f, .28f), new Vector3(0f, 25f, 0f), wood);
        RBox(g, "Hammer head", handle.transform.localPosition + Quaternion.Euler(0f, 25f, 0f) * new Vector3(0f, .01f, .14f), new Vector3(.12f, .04f, .04f), new Vector3(0f, 25f, 0f), steel);
        // スパナ（平たい柄と口）
        RBox(g, "Wrench", new Vector3(.3f, y + .012f, -.05f), new Vector3(.025f, .012f, .22f), new Vector3(0f, -40f, 0f), steel);
        RBox(g, "Wrench jaw", new Vector3(.3f, y + .012f, -.05f) + Quaternion.Euler(0f, -40f, 0f) * new Vector3(0f, 0f, .12f), new Vector3(.06f, .012f, .04f), new Vector3(0f, -40f, 0f), steel);
        // ドライバー（赤い柄と軸）
        Cyl(g, "Screwdriver grip", new Vector3(-.1f, y + .016f, -.24f), new Vector3(.03f, .045f, .03f), red).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Cyl(g, "Screwdriver shaft", new Vector3(-.02f, y + .016f, -.24f), new Vector3(.008f, .06f, .008f), steel).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        // 部品の皿（ねじ・ナット）と、仕分けの小箱3つ（後ろ）
        Box(g, "Parts tray", .2f, .42f, y, y + .03f, .0f, .1f, dark);
        for (int i = 0; i < 6; i++)
            Cyl(g, "Loose nut", new Vector3(.23f + (i % 3) * .07f, y + .035f, .025f + (i / 3) * .05f), new Vector3(.025f, .008f, .025f), steel);
        string[] bins = { "chip_red", "frame_express", "chip_blue" };
        for (int i = 0; i < bins.Length; i++)
        {
            float x0 = -.62f + i * .2f;
            Box(g, "Sorting bin", x0, x0 + .17f, y, y + .1f, .14f, .3f, Mat(bins[i]));
            Box(g, "Sorting bin inside", x0 + .015f, x0 + .155f, y + .1f, y + .101f, .155f, .285f, dark);
        }
        // 組み立て途中の部品（小さな箱と、上の歯車）
        Box(g, "Part block", -.2f, -.02f, y + .006f, y + .09f, -.08f, .06f, Mat("ac_unit"));
        Cyl(g, "Part gear", new Vector3(-.11f, y + .1f, -.01f), new Vector3(.1f, .01f, .1f), Mat("casino_gold"));
    }

    // ================================================================ 後ろの有孔ボードと掛けた工具
    static void WorkbenchPegboard(Transform g)
    {
        var steel = Mat("metal_dark"); var board = Mat("ac_unit"); var tool = Mat("jail_bars"); var red = Mat("hydrant");
        float z = BenchHalfZ - .03f, y0 = BenchTop + .12f, y1 = BenchTop + .72f;
        foreach (float x in new[] { -BenchHalfX + .03f, BenchHalfX - .07f })
            Box(g, "Pegboard post", x, x + .04f, BenchTop, y1 + .03f, z - .02f, z + .02f, steel);
        Box(g, "Pegboard", -BenchHalfX + .05f, BenchHalfX - .05f, y0, y1, z, z + .015f, board);
        Box(g, "Pegboard rail", -BenchHalfX + .03f, BenchHalfX - .03f, y1, y1 + .03f, z - .02f, z + .02f, steel);
        // 掛けた工具（板の前に少し浮かせて）
        float f = z - .012f;
        Box(g, "Hanging saw blade", -.6f, -.3f, y0 + .3f, y0 + .4f, f - .004f, f, tool);
        Box(g, "Hanging saw grip", -.3f, -.22f, y0 + .28f, y0 + .42f, f - .02f, f, red);
        Box(g, "Hanging wrench", -.12f, -.1f, y0 + .12f, y0 + .42f, f - .006f, f, tool);
        Box(g, "Hanging wrench", -.04f, -.02f, y0 + .16f, y0 + .42f, f - .006f, f, tool);
        Box(g, "Hanging wrench", .04f, .06f, y0 + .2f, y0 + .42f, f - .006f, f, tool);
        Cyl(g, "Hanging tape roll", new Vector3(.25f, y0 + .32f, f - .02f), new Vector3(.12f, .02f, .12f), Mat("frame_express")).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        foreach (float x in new[] { -.55f, -.11f, -.03f, .05f, .25f })
            Box(g, "Peg hook", x - .006f, x + .006f, y0 + .43f, y0 + .45f, f - .03f, f, steel);
        // 作業の手順の紙
        Box(g, "Instruction paper", .38f, .62f, y0 + .1f, y0 + .42f, f - .002f, f, Mat("flag_white"));
        Box(g, "Instruction paper line", .41f, .59f, y0 + .32f, y0 + .34f, f - .003f, f - .002f, steel);
    }

    // ================================================================ クランプの電気スタンド（後ろの右）
    static void WorkbenchLamp(Transform g)
    {
        var arm = Mat("metal_dark"); var shade = Mat("frame_express");
        float x = .55f, z = BenchHalfZ - .1f;
        Box(g, "Lamp clamp", x - .04f, x + .04f, BenchTop, BenchTop + .05f, z - .04f, z + .04f, arm);
        Cyl(g, "Lamp arm", new Vector3(x, BenchTop + .3f, z), new Vector3(.02f, .25f, .02f), arm);
        // 前へ倒した腕と、下を向いた笠と電球
        var upper = Cyl(g, "Lamp arm", new Vector3(x, BenchTop + .62f, z - .1f), new Vector3(.018f, .12f, .018f), arm);
        upper.transform.localRotation = Quaternion.Euler(-55f, 0f, 0f);
        Cyl(g, "Lamp joint", new Vector3(x, BenchTop + .55f, z), new Vector3(.035f, .02f, .035f), arm).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        Cyl(g, "Lamp shade", new Vector3(x, BenchTop + .66f, z - .22f), new Vector3(.14f, .06f, .14f), shade);
        Cyl(g, "Lamp shade top", new Vector3(x, BenchTop + .72f, z - .22f), new Vector3(.08f, .02f, .08f), shade);
        Cyl(g, "Lamp bulb", new Vector3(x, BenchTop + .6f, z - .22f), new Vector3(.07f, .015f, .07f), AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/Templates/Variants/Emissive/glow_bulb.mat"));
    }
}
#endif
