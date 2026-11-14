#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 大通りをまたぐ歩道橋（2026/11/14）。何度実行しても同じ結果。
/// ・店の側（南。EXPRESS の荷さばき場）と、家・トラックの側（北）をつなぐ。橋は x = FbX、大通りと両側の歩道の上を z 方向にまたぐ。
/// ・南は階段（EXPRESS の前の空き地を西へ下りる。下り口は箱の出る所の近く）。
///   北は階段（まっすぐ北の芝生へ下りる）と、スロープ（東のトラックの方へ、歩道に沿って下りる）。
/// ・橋の上と階段・スロープには手すり。縦の柵の間（FbBarStep ごと）は小さな箱が抜けて道へ落ちる幅で、プレイヤーは通れない。
///   柵の当たり判定は手すりの高さ（FbRailH）まで。その上は、跳んで越えないよう、手すりの線に沿ったすき間のない見えない板を
///   FbGuardH まで立てている（柵の間に体が挟まると、そこから跳べてしまうため。Footbridge Check で確認）。
/// ・地面の当たり判定（Demo/pPlane1）は変えない。橋・階段（段に沿った坂の板）・スロープ・柱・柵に自分の当たり判定を付ける。
/// 置く物は町のプレハブ（RoomiesNeighborhood）の「Town footbridge」の下（シーンの Roomies_EnvironmentArt）。
/// 寸法は下の表。最後に「9 Combine For Speed」と同じ方法で、見た目だけの部品を素材ごとに1つのメッシュへまとめる。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string FootbridgeGroup = "Town footbridge";

    // ================================================================ 表（寸法）
    const float FbX = -19.6f;               // 橋の中心の x（高架の西、EXPRESS の東の端の手前）
    const float FbTop = 4.6f;               // 橋の上の床の高さ（歩く面）
    const float FbUnder = 4.05f;            // 橋げたの下面（車道 y 0.1 からおよそ 3.95m あく）
    const float FbHalf = 1.0f, FbGirder = .22f;   // 床の半分の幅と、両側の桁の厚さ
    const float FbLandIn = 6.6f, FbLandOut = 8.4f; // 両端の踊り場（|z|。歩道は |z| 6.4 まで）
    const int FbRisers = 20;                // 階段の段の数（1段 FbTop / 20 = 0.23m）
    const float FbTread = .30f;             // 踏み面の奥行き
    const float FbStairW = 1.8f;            // 南の階段の幅（踊り場の奥行きと同じ）。北の階段は床と同じ幅
    const float FbRampW = 1.8f, FbRampDeg = 20f;  // スロープの幅と傾き
    const float FbRailH = 1.1f;             // 手すりの高さ
    const float FbBarStep = .9f;            // 縦の柵の間隔（いちばん広いとき。中は約 0.82m あく）
    const float FbGuardH = 3.2f;            // 手すりの上の見えない板の上端（柵に挟まって跳んでも越えない高さ）
    const float FbColumn = .3f;             // 柱の太さ

    static float FbRise => FbTop / FbRisers;
    static float FbStairRun => (FbRisers - 1) * FbTread;
    static float FbRampRun => FbTop / Mathf.Tan(FbRampDeg * Mathf.Deg2Rad);

    [MenuItem("Roomies/Art/Town/5 Footbridge")]
    internal static void BuildFootbridge()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        int parts, made;
        try
        {
            var g = Fresh(root.transform, FootbridgeGroup);
            FbDeck(EncFrame(g, "Deck", new Vector3(FbX, 0, -FbLandOut), 0));
            // 南の階段：踊り場の西から、EXPRESS の前を西へ下りる（下から上へ +z）
            float sx = FbX - FbHalf - FbGirder - FbStairRun;
            FbStairs(EncFrame(g, "South stairs", new Vector3(sx, 0, -(FbLandIn + FbLandOut) / 2), 90), FbStairW);
            // 北の階段：床の北の端からまっすぐ北へ下りる
            FbStairs(EncFrame(g, "North stairs", new Vector3(FbX, 0, FbLandOut + FbStairRun), 180), FbHalf * 2);
            // スロープ：北の踊り場の東から、歩道に沿って東（トラックの方）へ下りる
            float rx = FbX + FbHalf + FbGirder + FbRampRun;
            FbRamp(EncFrame(g, "Ramp", new Vector3(rx, 0, (FbLandIn + FbLandOut) / 2), -90));
            (parts, made) = CombineUnder(g, Slug(FootbridgeGroup), 0f, true);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Town] footbridge: {parts} parts -> {made} meshes, stairs rise {FbRise:F3} tread {FbTread} ({Mathf.Atan2(FbRise, FbTread) * Mathf.Rad2Deg:F1}°), ramp {FbRampDeg}° run {FbRampRun:F2}");
    }

    // ================================================================ 橋（枠の +z が北。z 0 が南の端）
    static void FbDeck(Transform t)
    {
        float L = FbLandOut * 2, land = FbLandOut - FbLandIn, gw = FbHalf + FbGirder;
        var steel = Mat("frame_house"); var dark = Mat("metal_dark"); var floor = Mat("paving");
        // 床・両側の桁・両端の受け
        Box(t, "Deck slab", -FbHalf, FbHalf, FbTop - .2f, FbTop, 0, L, floor);
        foreach (float s in new[] { -1f, 1f })
        {
            Box(t, "Deck girder", s * FbHalf, s * gw, FbUnder, FbTop, -FbGirder, L, steel);
            Box(t, "Girder stiffener line", s * (gw - .01f), s * (gw + .015f), FbUnder + .06f, FbUnder + .12f, -FbGirder, L, dark);
        }
        Box(t, "Deck end beam", -gw, gw, FbUnder, FbTop, -FbGirder, 0, steel);
        for (float z = 1.2f; z < L - .5f; z += 1.4f)
            Box(t, "Deck cross beam", -FbHalf, FbHalf, FbTop - .36f, FbTop - .2f, z - .07f, z + .07f, dark);
        // 柱（両端の踊り場の下に4本ずつ。歩道にはかからない）と、柱の上の受け
        foreach (float z in new[] { .15f, land - .15f, L - land + .15f, L - .15f })
        {
            foreach (float s in new[] { -1f, 1f })
                FbColumnAt(t, new Vector3(s * (FbHalf - .15f), 0, z), FbUnder);
            Box(t, "Column cap", -gw, gw, FbUnder - .2f, FbUnder, z - .18f, z + .18f, steel);
        }
        // 当たり判定：床（桁の外まで）
        Col(t, "Deck collider", new Vector3(0, (FbUnder + FbTop) / 2, (L - FbGirder) / 2), Quaternion.identity, new Vector3(gw * 2, FbTop - FbUnder, L + FbGirder));
        // 手すり：西は南の踊り場（階段の口）をあける、東は北の踊り場（スロープの口）をあける、南の端はふさぐ。北の端は階段へ続く
        float rx = FbHalf + FbGirder / 2;
        FbRail(t, new Vector3(-rx, FbTop, land), new Vector3(-rx, FbTop, L));
        FbRail(t, new Vector3(rx, FbTop, 0), new Vector3(rx, FbTop, L - land));
        FbRail(t, new Vector3(-rx, FbTop, -FbGirder / 2), new Vector3(rx, FbTop, -FbGirder / 2));
        // 階段とスロープの口に点字ブロック
        Box(t, "Tactile strip", -FbHalf + .05f, -FbHalf + .35f, FbTop, FbTop + .015f, .1f, land - .1f, Mat("tactile_yellow"));
        Box(t, "Tactile strip", FbHalf - .35f, FbHalf - .05f, FbTop, FbTop + .015f, L - land + .1f, L - .1f, Mat("tactile_yellow"));
        Box(t, "Tactile strip", -FbHalf + .1f, FbHalf - .1f, FbTop, FbTop + .015f, L - .35f, L - .05f, Mat("tactile_yellow"));
    }

    // ================================================================ 階段（枠の +z が上り。z 0 が1段目、z = FbStairRun が橋の床の端）
    static void FbStairs(Transform t, float w)
    {
        float r = FbRise, tr = FbTread, run = FbStairRun, half = w / 2;
        var steel = Mat("frame_house"); var dark = Mat("metal_dark"); var tread = Mat("stair_tread");
        for (int k = 1; k < FbRisers; k++)
        {
            float z0 = (k - 1) * tr, y = k * r;
            Box(t, "Stair tread", -half, half, y - .05f, y, z0 - .03f, z0 + tr, tread);
            Box(t, "Stair riser", -half, half, y - r, y - .05f, z0 - .03f, z0, dark);
            Box(t, "Stair nosing", -half, half, y - .012f, y + .004f, z0 - .045f, z0 + .02f, Mat("tactile_yellow"));
        }
        // 段の先をつないだ線（踏み面の先の角を通る）。当たり判定の坂も、側げたもこの線に合わせる
        float Nose(float z) => r * (z / tr + 1);
        var bottom = new Vector3(0, 0, -tr); var top = new Vector3(0, FbTop, run);
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s * (half + .1f);
            SlopeBox(t, "Stair stringer", new Vector3(x, .12f, -tr), new Vector3(x, Nose(run) + .12f, run), .2f, .5f, steel);
            // 中ほどの柱
            float zc = run * .5f;
            FbColumnAt(t, new Vector3(x, 0, zc), Nose(zc) + .12f - .5f);
            FbRail(t, new Vector3(x, Nose(0) + .12f, 0), new Vector3(x, Nose(run) + .12f, run));
        }
        // 当たり判定：段の先をつないだ坂の板（37.5°。CharacterController の坂の上限 45° より緩い）。下は地面の下まで伸ばす
        SlopeCol(t, "Stair slope collider", bottom + new Vector3(0, -.5f * FbTop / (run + tr), -.5f), top, w + .4f, .3f);
        // 上り口・下り口の点字ブロック
        Box(t, "Tactile strip", -half + .1f, half - .1f, 0, .02f, -tr - .65f, -tr - .35f, Mat("tactile_yellow"));
    }

    // ================================================================ スロープ（枠の +z が上り。z 0 が下の端）
    static void FbRamp(Transform t)
    {
        float run = FbRampRun, half = FbRampW / 2, k = FbTop / run;
        var steel = Mat("frame_house"); var floor = Mat("paving");
        SlopeBox(t, "Ramp slab", new Vector3(0, 0, 0), new Vector3(0, FbTop, run), FbRampW, .2f, floor);
        // 下の端（地面に埋まる所）の見切り
        Box(t, "Ramp toe", -half, half, 0, .03f, -.25f, .02f, Mat("stair_tread"));
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s * (half + .1f);
            SlopeBox(t, "Ramp stringer", new Vector3(x, .1f, 0), new Vector3(x, FbTop + .1f, run), .2f, .5f, steel);
            for (float z = run - 4f; z > 2f; z -= 4f)
                FbColumnAt(t, new Vector3(x, 0, z), k * z + .1f - .5f);
            FbRail(t, new Vector3(x, .1f, 0), new Vector3(x, FbTop + .1f, run));
        }
        // すべり止めの線（見た目だけ）と、下の口の点字ブロック
        for (float z = 1f; z < run - .5f; z += 1f)
            Beam(t, "Ramp groove", new Vector3(-half + .05f, k * z + .012f, z), new Vector3(half - .05f, k * z + .012f, z), .06f, .02f, Mat("metal_dark"));
        Box(t, "Tactile strip", -half + .1f, half - .1f, 0, .02f, -.65f, -.35f, Mat("tactile_yellow"));
        // 当たり判定：板1枚（20°）。下は地面の下まで伸ばす
        SlopeCol(t, "Ramp collider", new Vector3(0, -.5f * k, -.5f), new Vector3(0, FbTop, run), FbRampW + .4f, .3f);
    }

    // ================================================================ 部品
    /// <summary>柱（足元の台つき）と当たり判定。top は柱の上端の高さ。</summary>
    static void FbColumnAt(Transform t, Vector3 p, float top)
    {
        if (top < .4f) return;
        float c = FbColumn / 2;
        Box(t, "Column", p.x - c, p.x + c, 0, top, p.z - c, p.z + c, Mat("frame_house"));
        Box(t, "Column footing", p.x - c - .12f, p.x + c + .12f, 0, .15f, p.z - c - .12f, p.z + c + .12f, Mat("plinth"));
        Col(t, "Column collider", new Vector3(p.x, top / 2, p.z), Quaternion.identity, new Vector3(FbColumn, top, FbColumn));
    }

    /// <summary>手すり：a から b（柵の足元の線）へ、縦の柵を FbBarStep 以下の間隔で立て、上に手すり。当たり判定は柵ごと（手すりの高さまで）と、その上の見えない板。</summary>
    static void FbRail(Transform t, Vector3 a, Vector3 b)
    {
        var white = Mat("frame_h_white");
        float flat = new Vector2(b.x - a.x, b.z - a.z).magnitude;
        int n = Mathf.Max(1, Mathf.CeilToInt(flat / FbBarStep - .001f));
        for (int i = 0; i <= n; i++)
        {
            var p = Vector3.Lerp(a, b, i / (float)n);
            float c = (i == 0 || i == n) ? .045f : .025f;
            Box(t, i == 0 || i == n ? "Rail post" : "Rail bar", p.x - c, p.x + c, p.y, p.y + FbRailH, p.z - c, p.z + c, white);
            Col(t, "Rail bar collider", new Vector3(p.x, p.y + (FbRailH + .05f) / 2 - .05f, p.z), Quaternion.identity, new Vector3(.08f, FbRailH + .15f, .08f));
        }
        // 手すりから上の見えない板（坂でも上下の幅が同じになるよう、傾きの分だけ厚さを縮める）
        var rot = Quaternion.LookRotation(b - a, Vector3.up);
        float cos = flat / Mathf.Max(.001f, (b - a).magnitude), gh = FbGuardH - FbRailH;
        Col(t, "Rail guard collider", (a + b) / 2 + Vector3.up * (FbRailH + gh / 2), rot, new Vector3(.08f, gh * cos, (b - a).magnitude + .08f));
        var up = Vector3.up * FbRailH;
        Beam(t, "Handrail", a + up, b + up, .07f, .06f, white);
        Beam(t, "Handrail base", a + Vector3.up * .02f, b + Vector3.up * .02f, .1f, .04f, Mat("frame_house"));
        // 手すりの棒そのものには当たり判定を付けない（上の見えない板で足りる）
    }

    /// <summary>a から b へ伸びる角材（a・b は芯の線。w は横、h は縦の太さ）。見た目だけ。</summary>
    static GameObject Beam(Transform parent, string name, Vector3 a, Vector3 b, float w, float h, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = (a + b) / 2;
        go.transform.localRotation = Quaternion.LookRotation(b - a, Vector3.up);
        go.transform.localScale = new Vector3(w, h, (b - a).magnitude);
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    /// <summary>上の面の芯が a から b を通る坂の板（厚さ th は下へ）。見た目だけ。</summary>
    static GameObject SlopeBox(Transform parent, string name, Vector3 a, Vector3 b, float w, float th, Material mat)
    {
        var rot = Quaternion.LookRotation(b - a, Vector3.up);
        var down = rot * Vector3.down * (th / 2);
        return Beam(parent, name, a + down, b + down, w, th, mat);
    }

    /// <summary>上の面の芯が a から b を通る坂の当たり判定（厚さ th は下へ）。</summary>
    static void SlopeCol(Transform parent, string name, Vector3 a, Vector3 b, float w, float th)
    {
        var rot = Quaternion.LookRotation(b - a, Vector3.up);
        Col(parent, name, (a + b) / 2 + rot * Vector3.down * (th / 2), rot, new Vector3(w, th, (b - a).magnitude));
    }

    /// <summary>見た目のない当たり判定（BoxCollider）。</summary>
    static void Col(Transform parent, string name, Vector3 center, Quaternion rot, Vector3 size)
    {
        var go = new GameObject(name) { isStatic = true };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = center;
        go.transform.localRotation = rot;
        go.AddComponent<BoxCollider>().size = size;
    }
}
#endif
