#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Art = RoomiesCasinoArt;
using Object = UnityEngine.Object;

/// <summary>
/// コンビニ（CORNER STORE）の建物（Roomies/Store/1 Build Building。2026/11/24 ゆせさんの依頼「コンビニを深堀り。ローポリをやめて」）。何度実行しても同じ結果。
///
/// 前は 2 階建てのレンガの箱（中に入れない。窓は棚の絵）だった。平屋の、中に入れる店に作り替えた。
/// 外形は前と同じ（x 18.6〜33.6、z -18.7〜-8.7）。前へ出すのは入口のひさし（1.3m）と、店先のごみ箱・足ふきだけ。両隣と道へは出ない。
///
/// 町のプレハブ（RoomiesNeighborhood）の「Town store」に置く：外壁・ガラスの面・看板の帯・ひさし・屋上・中の床と天井と明かり。
/// 売り場の物（棚・冷蔵ケース・レジ・店の人）と自動ドアは、ギミックのプレハブ（Roomies/Store/2 Build Gimmick Prefabs）。
/// 前の店の部品は、町の作り直しの表から外した（StoreBuiltSeparately）。表の物を消すには Town/1・2・3 を実行し直す。
///
/// 高さ：床の上面 0.12、天井 3.42、看板の帯 3.42〜4.62、屋上の面 4.55、パラペットの上 5.0。
/// 位置や寸法を直すときは、ここの数字を直して実行し直す（シーンやプレハブで直した位置は、作り直すと消える）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    /// <summary>
    /// true の間、町の作り直しの表（1 Windows And Walls・2 Houses And Roofs・3 Street And Night Lights）は CORNER STORE を作らない
    /// （コンビニは Roomies/Store/1 Build Building が建てる）。
    /// </summary>
    internal const bool StoreBuiltSeparately = true;

    internal const string StoreGroup = "Town store";
    // 外形・壁の厚さ・床・天井・入口
    internal const float SX0 = 18.6f, SX1 = 33.6f, SZ0 = -18.7f, SZ1 = -8.7f, ST = .25f, SFloor = .12f, SCeil = 3.42f;
    internal const float SDoorX0 = 25.3f, SDoorX1 = 26.9f, SDoorH = 2.5f, SFasciaTop = 4.62f, SRoofDeck = 4.55f, STop = 5.0f;
    internal const float SGlassY0 = .5f, SGlassY1 = 3.1f;
    const float SIX0 = SX0 + ST, SIX1 = SX1 - ST, SIZ0 = SZ0 + ST, SIZ1 = SZ1 - ST;
    /// <summary>店の真ん中の床（売り場のプレハブの原点）。</summary>
    internal static readonly Vector3 StoreFloorCenter = new Vector3((SX0 + SX1) / 2f, SFloor, (SZ0 + SZ1) / 2f);

    [MenuItem("Roomies/Store/1 Build Building")]
    internal static void BuildStore()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        Art.Begin();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            StripOldStore(root.transform.Find(Workplaces));
            var g = Fresh(root.transform, StoreGroup);
            StoreShell(g);
            StoreFront(g);
            StoreSigns(g);
            StoreRoof(g);
            StoreInterior(g);
            ApplySwatches(g);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Art.End();

        HideOldStoreBox();
        // 新しい明かりを朝夜の切り替えに登録し直す（シーンも保存される）
        RoomiesMaterialFinishBuilder.BuildTimeOfDay();
        CombineGroups(StoreGroup);
        Debug.Log("[Store] building built");
    }

    /// <summary>前の店の看板・ひさし（古い町の道具が置いた物）を外す。</summary>
    static void StripOldStore(Transform work)
    {
        if (work == null) return;
        foreach (Transform c in work.Cast<Transform>().ToArray())
        {
            bool inStore = c.position.x > SX0 - .5f && c.position.x < SX1 + .5f && c.position.z > SZ0 - .5f && c.position.z < SZ1 + 2.0f;
            if (!inStore) continue;
            if (c.name == "CORNER STORE fascia" || c.name == "Sign - CORNER STORE" || c.name == "Shop awning" || c.name == "Awning stripe" || c.name == "CORNER STORE roof trim")
                Object.DestroyImmediate(c.gameObject);
        }
    }

    /// <summary>シーンの Demo/pCube1（前の店の形の、クリーム色の箱）の表示を切る。当たり判定は持っていない。</summary>
    static void HideOldStoreBox()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene s = SceneManager.GetSceneAt(i);
            if (!s.isLoaded || s.name != "GameRoom") continue;
            var demo = s.GetRootGameObjects().FirstOrDefault(r => r.name == "Demo");
            var box = demo != null ? demo.transform.Find("pCube1") : null;
            if (box != null && box.TryGetComponent<MeshRenderer>(out var r) && r.enabled)
            {
                r.enabled = false;
                EditorSceneManager.MarkSceneDirty(s);
                EditorSceneManager.SaveScene(s);
            }
        }
    }

    // ================================================================ 外壁（当たり判定つき）
    static void StoreShell(Transform g)
    {
        var wall = Mat("store_wall");
        // 奥の壁。西と東の壁は、窓の所で分けて置く（StoreSideWall）。前の 0.4m は角の柱が覆う
        Box(g, "Store back wall", SIX0, SIX1, 0, SRoofDeck, SZ0, SIZ0, wall);
        Solid(g, "Store wall collider", new Vector3(SX0, 0, SZ0), new Vector3(SIX0, SRoofDeck, SZ1));
        Solid(g, "Store wall collider", new Vector3(SIX1, 0, SZ0), new Vector3(SX1, SRoofDeck, SZ1));
        Solid(g, "Store wall collider", new Vector3(SIX0, 0, SZ0), new Vector3(SIX1, SRoofDeck, SIZ0));

        // 横の壁と窓（西：パンの棚の上、東：コーヒーの台の上）
        StoreSideWall(g, true, -11.7f, -9.9f, 1.85f, 2.95f);
        StoreSideWall(g, false, -11.4f, -9.6f, 1.25f, 2.95f);

        // 足元の帯（灰）と、窓の上の高さのさんご色の線（横と奥）
        var plinth = Swatch("steel_dark"); var line = Swatch("coral");
        Box(g, "Store plinth", SX0 - .03f, SX0, 0, .42f, SZ0, SZ1 - .4f, plinth);
        Box(g, "Store plinth", SX1, SX1 + .03f, 0, .42f, SZ0, SZ1 - .4f, plinth);
        Box(g, "Store plinth", SX0, SX1, 0, .42f, SZ0 - .03f, SZ0, plinth);
        Box(g, "Store line", SX0 - .025f, SX0, 3.05f, 3.15f, SZ0, SZ1 - 2.3f, line);
        Box(g, "Store line", SX1, SX1 + .025f, 3.05f, 3.15f, SZ0, SZ1 - 2.3f, line);
        Box(g, "Store line", SX0, SX1, 3.05f, 3.15f, SZ0 - .025f, SZ0, line);

        // 奥：お店の人の出入り口と、屋根の雨どい
        Prop(g, "Store back door", Art.StoreStaffDoor(), 31.8f, 0, SZ0 - .04f, 180);
        Box(g, "Store back step", 31.1f, 32.5f, 0, .1f, SZ0 - .7f, SZ0, Swatch("stone"));
        foreach (float x in new[] { SX0 + .2f, SX1 - .2f })
            Prop(g, "Store downpipe", Art.Build("store_downpipe", lp => lp.Lathe(new[] { (.05f, 0f), (.05f, SRoofDeck) }, 12, "steel_dark", 0, true)), x, 0, SZ0 - .07f);
    }

    /// <summary>横の壁（west が true なら西）。z0〜z1・y0〜y1 の所を窓にして、ガラスと枠を入れる。</summary>
    static void StoreSideWall(Transform g, bool west, float z0, float z1, float y0, float y1)
    {
        var wall = Mat("store_wall"); var glass = Art.GlassMaterial(); var frame = Swatch("steel");
        float xo = west ? SX0 : SIX1, xi = west ? SIX0 : SX1, front = SZ1 - .4f;
        Box(g, "Store side wall", xo, xi, 0, SRoofDeck, SZ0, z0, wall);
        Box(g, "Store side wall", xo, xi, 0, SRoofDeck, z1, front, wall);
        Box(g, "Store side wall", xo, xi, 0, y0, z0, z1, wall);
        Box(g, "Store side wall", xo, xi, y1, SRoofDeck, z0, z1, wall);
        float gx = (xo + xi) / 2f;
        Box(g, "Store side glass", gx - .01f, gx + .01f, y0, y1, z0, z1, glass);
        float fx = west ? SX0 - .03f : SX1 + .03f;
        foreach (float z in new[] { z0, z1 }) Box(g, "Store side frame", fx - .04f, fx + .04f, y0 - .05f, y1 + .05f, z - .04f, z + .04f, frame);
        foreach (float y in new[] { y0, y1 }) Box(g, "Store side frame", fx - .04f, fx + .04f, y - .04f, y + .04f, z0, z1, frame);
    }

    // ================================================================ 正面（ガラスの面・角の柱・入口）
    static void StoreFront(Transform g)
    {
        var glass = Art.GlassMaterial(); var kick = Swatch("metal_dark"); var steel = Swatch("steel");
        float fz = SZ1;     // 正面の外の面
        // 角の柱（角の丸い四角）。前の箱の外壁の厚みの分だけ、前へ少し出す
        foreach (float x in new[] { SX0 + .33f, SX1 - .33f })
            PropMat(g, "Store corner pillar", Art.StorePillar(.66f, .5f, SFasciaTop - .02f), x, 0, fz - .17f, Mat("store_wall"));
        // 正面の当たり判定（入口だけ空ける）
        Solid(g, "Store front collider", new Vector3(SX0, 0, fz - ST), new Vector3(SDoorX0, SCeil, fz));
        Solid(g, "Store front collider", new Vector3(SDoorX1, 0, fz - ST), new Vector3(SX1, SCeil, fz));
        Solid(g, "Store front collider", new Vector3(SDoorX0, SDoorH, fz - ST), new Vector3(SDoorX1, SCeil, fz));
        // 腰の黒い板・ガラス・上の帯
        float gx0 = SX0 + .66f, gx1 = SX1 - .66f;
        foreach (var (x0, x1) in new[] { (gx0, SDoorX0), (SDoorX1, gx1) })
        {
            Box(g, "Store kick plate", x0, x1, 0, SGlassY0, fz - .2f, fz - .02f, kick);
            Box(g, "Store front glass", x0, x1, SGlassY0, SGlassY1, fz - .13f, fz - .11f, glass);
        }
        Box(g, "Store transom glass", SDoorX0, SDoorX1, SDoorH + .06f, SGlassY1, fz - .13f, fz - .11f, glass);
        Box(g, "Store door header", SDoorX0 - .04f, SDoorX1 + .04f, SDoorH, SDoorH + .07f, fz - .2f, fz - .04f, steel);
        Box(g, "Store transom", gx0, gx1, SGlassY1, SCeil, fz - .22f, fz - .03f, Swatch("store_cream"));
        Box(g, "Store glass sill", gx0, gx1, SGlassY0 - .03f, SGlassY0 + .02f, fz - .2f, fz, steel);
        // 縦の桟（角の丸い細い柱）
        var mullion = Art.Build("store_mullion", lp => lp.Prism(Art.Chamfer(.07f, .1f, .03f), 0, SGlassY1 - SGlassY0, "steel"));
        foreach (float x in new[] { gx0 + .03f, 21.0f, 22.75f, 24.45f, SDoorX0 + .02f, SDoorX1 - .02f, 27.75f, 29.45f, 31.2f, gx1 - .03f })
            Prop(g, "Store mullion", mullion, x, SGlassY0, fz - .12f);
        // 入口の足ふき（段差 0.12 をなくす台。当たり判定つき）
        PropMat(g, "Store door step", Art.Build("store_step", lp => lp.Prism(Art.FrontRounded(2.3f, .5f, .2f), 0, SFloor, "stone")), 26.1f, 0, fz, Mat("store_floor"));
        Solid(g, "Store step collider", new Vector3(SDoorX0 - .35f, 0, fz - .02f), new Vector3(SDoorX1 + .35f, SFloor, fz + .5f));
        Prop(g, "Store door mat", Art.Build("store_mat", lp => lp.Prism(Art.Chamfer(1.5f, .9f, .1f), 0, .012f, "ink", "coral_dark")), 26.1f, SFloor, fz - .62f);
        // 入口のひさしと、その下の明かり（夜だけ。名前の頭が Shop light の光は朝に消える）
        Prop(g, "Store canopy", Art.StoreCanopy(3.6f, 1.3f, .16f), 26.1f, 2.78f, fz);
        PointLight(g, "Shop light - CORNER STORE canopy", new Vector3(26.1f, 2.6f, fz + .7f), 2.4f, 6f, new Color(1f, .9f, .78f));
        // ガラスの内側に貼ったポスター（外を向く。裏は紙）と、店先ののぼり（Codex の絵）
        Prop(g, "Store window poster", Art.StorePoster(.5f, .667f, 0), 23.75f, 1.2f, fz - .16f);
        Prop(g, "Store window poster", Art.StorePoster(.5f, .667f, 1), 28.6f, 1.2f, fz - .16f);
        Prop(g, "Store nobori", Art.StoreNobori(0), 24.0f, 0, fz + .45f, 12);
        Prop(g, "Store nobori", Art.StoreNobori(1), 29.3f, 0, fz + .45f, -12);
        // 店先のごみ箱（入口の右。歩道の通り道にはかからない）
        Prop(g, "Store bins", Art.StoreBins(), 32.0f, 0, fz + .03f + .21f);
    }

    // ================================================================ 看板（帯・光る板・お店の印・文字）
    static void StoreSigns(Transform g)
    {
        float fz = SZ1;
        // 帯：上から見て角の丸い板。前と両脇へ 2m ほど回りこむ。中は天井より上なので見えない
        PropMat(g, "Store fascia", Art.StoreFascia(SX1 - SX0 + .3f, 2.6f, SFasciaTop - SCeil), (SX0 + SX1) / 2f, SCeil, fz - 1.0f, Mat("store_coral"));
        // 帯の下の、店先を照らす細い光の線と、クリームの細い線
        Box(g, "Store light strip", SX0 + .7f, SX1 - .7f, SCeil - .03f, SCeil, fz + .2f, fz + .28f, Swatch("panel_glow", true));
        Box(g, "Store cream line", SX0 - .15f, SX1 + .15f, SCeil + .12f, SCeil + .17f, fz + .3f, fz + .31f, Swatch("store_cream"));
        // 光る板・お店の印・文字（オリジナル。実在の店の名前・色はまねしない）
        float sy = SCeil + .2f;
        Prop(g, "Store sign panel", Art.StoreSignPanel(7.4f, .82f), 26.1f, sy, fz + .34f);
        // 通りから見ると x の大きいほうが左。お店の印を左に、文字をその右に
        Prop(g, "Store emblem", Art.StoreEmblem(.68f), 29.15f, sy + .07f, fz + .42f);
        Art.NeonText(g, "Sign - CORNER STORE", "CORNER STORE", new Vector3(25.45f, sy + .52f, fz + .45f), 0, .44f, "8F3626", 1f);
        Art.NeonText(g, "Sign - CORNER STORE sub", "ごはん・のみもの・まいにちの物", new Vector3(25.45f, sy + .16f, fz + .45f), 0, .16f, "5A5E66", 1f);
        // 横の帯にも、お店の印
        Prop(g, "Store emblem west", Art.StoreEmblem(.6f), SX0 - .16f, sy + .1f, fz - .95f, -90);
        Prop(g, "Store emblem east", Art.StoreEmblem(.6f), SX1 + .16f, sy + .1f, fz - .95f, 90);
        // 夜に正面を照らす光（朝に消える）
        SpotLight(g, "Shop light - CORNER STORE", new Vector3(26.1f, 6.4f, fz + 3.3f), new Vector3(26.1f, 3.6f, fz), 6.5f, 11f, 112f, new Color(1f, .9f, .78f));
    }

    // ================================================================ 屋上（パラペット・屋上の面・室外機）
    static void StoreRoof(Transform g)
    {
        var wall = Mat("store_wall"); var cap = Swatch("store_cream");
        Box(g, "Store roof deck", SIX0, SIX1, SRoofDeck - .15f, SRoofDeck, SIZ0, SZ1 - 1.0f, Swatch("stone"));
        // 横と奥のパラペット（前は看板の帯がパラペットの代わり）
        Box(g, "Store parapet", SX0, SIX0, SRoofDeck, STop - .1f, SZ0, SZ1 - 2.2f, wall);
        Box(g, "Store parapet", SIX1, SX1, SRoofDeck, STop - .1f, SZ0, SZ1 - 2.2f, wall);
        Box(g, "Store parapet", SIX0, SIX1, SRoofDeck, STop - .1f, SZ0, SIZ0, wall);
        Box(g, "Store parapet cap", SX0 - .05f, SIX0 + .05f, STop - .1f, STop, SZ0 - .05f, SZ1 - 2.2f, cap);
        Box(g, "Store parapet cap", SIX1 - .05f, SX1 + .05f, STop - .1f, STop, SZ0 - .05f, SZ1 - 2.2f, cap);
        Box(g, "Store parapet cap", SX0 - .05f, SX1 + .05f, STop - .1f, STop, SZ0 - .05f, SIZ0 + .05f, cap);
        // 帯のうしろの立ち上がり（帯の上と屋上のあいだをふさぐ）
        Box(g, "Store fascia back", SX0 + .25f, SX1 - .25f, SRoofDeck, SFasciaTop, SZ1 - 2.3f, SZ1 - 2.1f, wall);
        foreach (var (x, z) in new[] { (21.5f, -15.8f), (23.4f, -15.8f), (30.6f, -14.6f) })
            Prop(g, "Store roof unit", Art.StoreRoofUnit(), x, SRoofDeck, z);
    }

    // ================================================================ 中（床・天井・明かり・壁の線）
    static void StoreInterior(Transform g)
    {
        Box(g, "Store floor", SIX0, SIX1, 0, SFloor, SIZ0, SIZ1, Mat("store_floor"));
        Solid(g, "Store floor collider", new Vector3(SIX0, 0, SIZ0), new Vector3(SIX1, SFloor, SIZ1));
        // 天井：少しだけ光る白（アトラスの色見本）。天井には明かりの光がほとんど届かず、暗い灰色に見えていた
        Box(g, "Store ceiling", SIX0, SIX1, SCeil, SCeil + .08f, SIZ0, SIZ1, Swatch("ceiling_glow", true));
        // 天井ぎわの、さんご色の線（中の 3 面）
        var line = Swatch("coral");
        Box(g, "Store inner line", SIX0, SIX0 + .015f, SCeil - .32f, SCeil - .2f, SIZ0, SIZ1, line);
        Box(g, "Store inner line", SIX1 - .015f, SIX1, SCeil - .32f, SCeil - .2f, SIZ0, SIZ1, line);
        Box(g, "Store inner line", SIX0, SIX1, SCeil - .32f, SCeil - .2f, SIZ0, SIZ0 + .015f, line);
        // 天井の明かり（2 列 × 4）と、光（中の明かりは朝も点いたまま。名前の頭を Shop light にしない）
        var panel = Art.StoreCeilingPanel(1.3f, .62f);
        for (int i = 0; i < 4; i++)
            foreach (float z in new[] { -15.6f, -11.6f })
                Prop(g, "Store ceiling light", panel, 20.9f + i * 3.4f, SCeil, z);
        foreach (float x in new[] { 21.2f, 26.1f, 31.0f })
            PointLight(g, "Store interior light", new Vector3(x, SCeil - .3f, -14.4f), 4.6f, 9.5f, new Color(1f, .97f, .92f));
        // レジの前（お客さんが立つ所）を照らす光
        PointLight(g, "Store interior light", new Vector3(30.6f, SCeil - .35f, -11.0f), 3.0f, 6.5f, new Color(1f, .96f, .9f));
    }
}
#endif
