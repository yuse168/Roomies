#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// カジノの建物を作り直す（Roomies/Art/Rebuild/3 Casino ＝ Roomies/Casino/1 Build Building）。何度実行しても同じ結果。
/// 2026/11/23 に 3 階建て・木とレンガと暖色の明かりの見た目に作り替えた（参考画像の「溜まり場のカジノ」）。
///
/// 町のプレハブ（RoomiesNeighborhood）の中のグループ：
///  ・Town casino            … 外壁・床の板・屋根・窓（RoomiesTownRebuild.CasinoTower.cs）と、外観の飾り・1 階の中（CasinoBuilding.cs）
///  ・Town casino 2F / 3F    … 上の階の中（その階にいる人の画面でだけ出す）
///  ・Town casino 2F windows / 3F windows … 外から見た窓の明かり（その階にいないときだけ出す）
/// 仕組みの物（スロット・台・エレベーター・店の人）はプレハブ（Roomies/Casino/2 Build Gimmick Prefabs）で、シーンに置いてある。ここでは触らない。
/// 通用口の扉（シーンの DoorPivot）と、店の並びの自販機もそのまま。
///
/// 壁・床・梁は、町の素材（素材の型の子。三方向から貼るのでどの大きさの箱にも模様が合う）の箱で組む。
/// 小物は RoomiesCasinoArt の「1 つの物 = 1 つのメッシュ」を置く。最後にグループごと・素材ごとにまとめる（CombineGroups）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    internal const string CasinoGroup = "Town casino";
    internal const string CasinoGroup2F = "Town casino 2F", CasinoGroup3F = "Town casino 3F";
    internal const string CasinoWindows2F = "Town casino 2F windows", CasinoWindows3F = "Town casino 3F windows";

    [MenuItem("Roomies/Art/Rebuild/3 Casino")]
    internal static void BuildCasino()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        RoomiesCasinoArt.Begin();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            var work = root.transform.Find(Workplaces);
            StripOldCasino(work);

            var g = Fresh(root.transform, CasinoGroup);
            CasinoTowerShell(g);
            CasinoFacade(g, work);
            CasinoFloor1(g, work);

            CasinoFloor2(Fresh(root.transform, CasinoGroup2F));
            CasinoFloor3(Fresh(root.transform, CasinoGroup3F));

            // 外から見た窓の明かりは、はじめは隠しておく（エディタでは上の階の中が見える。実行中は CasinoFloorVisibility が出し入れする）
            foreach (int floor in new[] { 2, 3 })
            {
                var glow = Fresh(root.transform, floor == 2 ? CasinoWindows2F : CasinoWindows3F);
                CasinoWindowGlow(glow, floor);
                glow.gameObject.SetActive(false);
            }

            foreach (string name in new[] { CasinoGroup, CasinoGroup2F, CasinoGroup3F, CasinoWindows2F, CasinoWindows3F })
                ApplySwatches(root.transform.Find(name));
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        RoomiesCasinoArt.End();

        // まとめ直すのはカジノのグループだけ（町ぜんぶをまとめ直すと、ほかの建物のメッシュまで書き換わる）。
        // 窓の明かりのグループは数枚の板なので、まとめない
        RoomiesMaterialFinishBuilder.BuildTimeOfDay();
        CombineGroups(CasinoGroup, CasinoGroup2F, CasinoGroup3F);
        // グループを作り直すと、シーンから指している先が切れるので、つなぎ直す
        RoomiesCasinoBuilder.PlaceInOpenScene();
        Debug.Log("[Rebuild] casino building built");
    }

    // ================================================================ アトラスの色の箱
    // 模様のいらない箱（足元の石・腰の板・壁の布・光る板）は、町の素材ではなくアトラスの色見本で塗る（マテリアルを増やさないため）。
    // 組むときは目印のマテリアルを付けておき、最後にまとめて「色見本の UV の箱」と casino_atlas（光る物は casino_atlas_glow）に替える。
    static readonly System.Collections.Generic.Dictionary<string, Material> swatchMarkers = new System.Collections.Generic.Dictionary<string, Material>();

    static Material Swatch(string color, bool glow = false)
    {
        string key = "@" + color + (glow ? "@glow" : "");
        if (!swatchMarkers.TryGetValue(key, out var m) || m == null)
            swatchMarkers[key] = m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = key, hideFlags = HideFlags.HideAndDontSave };
        return m;
    }

    static void ApplySwatches(Transform group)
    {
        if (group == null) return;
        foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true))
        {
            var m = r.sharedMaterial;
            if (m == null || !m.name.StartsWith("@")) continue;
            string[] parts = m.name.Split('@');   // "@色" か "@色@glow"
            string color = parts[1]; bool glow = parts.Length > 2;
            var mesh = RoomiesCasinoArt.Build("box_" + color + (glow ? "_glow" : ""),
                lp => { lp.Sharp = true; lp.Box(new Vector3(-.5f, -.5f, -.5f), new Vector3(.5f, .5f, .5f), color, glow); });   // 伸ばして使うので、角は丸めない
            r.GetComponent<MeshFilter>().sharedMesh = mesh;
            r.sharedMaterials = RoomiesCasinoArt.MaterialsFor(mesh);
        }
    }

    /// <summary>元の屋根付きの「Arcade」：当たり判定（屋根・柱・奥の壁）とベンチを外す（建物の壁・屋根に置き換える）。</summary>
    static void StripOldCasino(Transform work)
    {
        if (work == null) return;
        foreach (Transform c in work.Cast<Transform>().ToArray())
            if (c.name.StartsWith("Arcade") || (c.name == "bench" && c.position.x > -2f && c.position.x < 17f && c.position.z < -10f))
                Object.DestroyImmediate(c.gameObject);
    }
}
#endif
