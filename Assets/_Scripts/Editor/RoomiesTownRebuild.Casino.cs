#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 作り直し2-3：カジノ（店の並びの屋根付きの「Arcade」）を0から作る（2026/11/13）。何度実行しても同じ結果。
/// ・仕組みはそのまま：スロット3台（SlotMachine・画面の Canvas・Visual の当たり判定）、ブラックジャック台（BlackjackTable・部品の当たり判定）、
///   前の扉（DoorPivot）、自販機、ベンチ、奥の壁・屋根・柱の当たり判定。
/// ・建物（町のプレハブ「Town casino」）：絨毯の床、奥の壁（板の腰壁と布張り・ネオンの看板の台）、左右の壁（右は通り抜けの開口）、
///   天井の照明、台の上の吊りランプ、電球の並んだ正面の看板、金の柱。元の看板の文字は残す。
/// ・スロットの筐体：Slot.prefab の Visual/Cabinet に0から組む（BuildSlotCabinets）。当たり判定と画面はそのまま。
/// ・ブラックジャック台：BlackjackTable.prefab の Visual の中で、部品の見た目だけ外し（当たり判定は残す）、「Visual/Art」に新しい台を組む。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string CasinoGroup = "Town casino";
    const string BlackjackPrefab = "Assets/_Prefabs/BlackjackTable.prefab";
    const float CX0 = -1.2f, CX1 = 16.8f, CZBack = -19.47f, CZFront = -7.8f, CFloor = .118f, CCeil = 3.46f;

    [MenuItem("Roomies/Art/Rebuild/3 Casino")]
    internal static void BuildCasino()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            var work = root.transform.Find(Workplaces);
            StripOldCasino(work);
            var g = Fresh(root.transform, CasinoGroup);
            BuildCasinoBuilding(g, work);
            RoomiesBevelBuilder.ApplyTownUnder(g);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        BuildBlackjackArt();
        BuildSlotCabinets();
        PlaceSlots();
        AssetDatabase.SaveAssets();
        RoomiesMaterialFinishBuilder.BuildTimeOfDay();
        CombineForSpeed();
        Debug.Log("[Rebuild] casino building built");
    }

    /// <summary>元の屋根付きの「Arcade」：見た目は前の作り直しで外してある。当たり判定（屋根・柱・奥の壁）とベンチも外す（建物の壁・屋根に置き換える）。</summary>
    static void StripOldCasino(Transform work)
    {
        if (work == null) return;
        foreach (Transform c in work.Cast<Transform>().ToArray())
            if (c.name.StartsWith("Arcade") || (c.name == "bench" && c.position.x > -2f && c.position.x < 17f && c.position.z < -10f))
                Object.DestroyImmediate(c.gameObject);
    }

    // ================================================================ ブラックジャック台（プレハブの Visual/Art）
    static void BuildBlackjackArt()
    {
        var root = PrefabUtility.LoadPrefabContents(BlackjackPrefab);
        try
        {
            var visual = root.transform.Find("Visual");
            foreach (Transform c in visual.Cast<Transform>().ToArray())
            {
                if (c.name == "Art") { Object.DestroyImmediate(c.gameObject); continue; }
                var r = c.GetComponent<MeshRenderer>(); if (r != null) r.enabled = false;   // 当たり判定は残し、見た目だけ切る
            }
            var g = new GameObject("Art").transform; g.SetParent(visual, false);
            var felt = Mat("casino_felt"); var rail = Mat("casino_leather"); var wood = Mat("casino_wood"); var gold = Mat("casino_gold");
            // 台：楕円の天板（フェルト）と、まわりの革のふち、木の幕板、柱と台座
            Cyl(g, "Table rail", new Vector3(0, .9f, 0), new Vector3(3.42f, .07f, 2.0f), rail);
            Cyl(g, "Table felt", new Vector3(0, .96f, 0), new Vector3(3.08f, .012f, 1.7f), felt);
            Cyl(g, "Table apron", new Vector3(0, .76f, 0), new Vector3(3.2f, .08f, 1.82f), wood);
            foreach (float x in new[] { -.9f, .9f }) Box(g, "Table pedestal", x - .3f, x + .3f, 0, .7f, -.35f, .35f, wood);
            Box(g, "Table base", -1.4f, 1.4f, 0, .06f, -.5f, .5f, Mat("casino_black"));
            Box(g, "Foot rail", -1.3f, 1.3f, .18f, .22f, -.62f, -.58f, gold);
            // フェルトの賭けの円（プレイヤーの側＝入口の +z の側に5つ）と線
            for (int i = 0; i < 5; i++)
            {
                float a = Mathf.Lerp(-55, 55, i / 4f) * Mathf.Deg2Rad;
                var p = new Vector3(Mathf.Sin(a) * 1.05f, .968f, Mathf.Cos(a) * .52f);
                Cyl(g, "Bet ring", p, new Vector3(.24f, .002f, .24f), Mat("casino_felt_line"));
                Cyl(g, "Bet ring inner", p + Vector3.up * .002f, new Vector3(.2f, .002f, .2f), felt);
            }
            Box(g, "Felt line", -.9f, .9f, .968f, .971f, -.02f, .0f, Mat("casino_felt_line"));
            // 親の側（奥の -z）：チップ入れ・カードの箱・捨て札・チップの山
            Box(g, "Chip tray", -.5f, .5f, .965f, 1.02f, -.75f, -.55f, Mat("casino_black"));
            for (int i = 0; i < 8; i++)
            {
                Material[] chips = { Mat("chip_red"), Mat("chip_blue"), Mat("chip_green"), Mat("chip_black") };
                Cyl(g, "Chip roll", new Vector3(-.43f + i * .123f, 1.03f, -.65f), new Vector3(.05f, .03f, .05f), chips[i % 4]).transform.localRotation = Quaternion.Euler(90, 0, 0);
            }
            Box(g, "Card shoe", .75f, 1.0f, .965f, 1.08f, -.62f, -.35f, Mat("casino_black"));
            Box(g, "Card shoe face", .75f, 1.0f, .965f, 1.05f, -.35f, -.33f, Mat("casino_card"));
            Box(g, "Discard holder", -1.0f, -.78f, .965f, 1.06f, -.58f, -.38f, Mat("chip_red"));
            foreach (var (x, z, n, m) in new[] { (-.6f, .35f, 4, "chip_red"), (.25f, .5f, 6, "chip_blue"), (.75f, .25f, 3, "chip_green") })
                for (int k = 0; k < n; k++) Cyl(g, "Chip", new Vector3(x, .975f + k * .012f, z), new Vector3(.05f, .006f, .05f), Mat(m));
            // 札（親の奥の縁。プレイヤーの方を向く）
            Box(g, "Plaque", -.7f, .7f, .99f, 1.16f, -.86f, -.82f, Mat("casino_black"));
            Box(g, "Plaque trim", -.72f, .72f, 1.16f, 1.18f, -.87f, -.81f, gold);
            Box(g, "Plaque trim", -.72f, .72f, .97f, .99f, -.87f, -.81f, gold);
            Box(g, "Plaque inlay", -.55f, .55f, 1.05f, 1.1f, -.815f, -.81f, gold);
            PrefabUtility.SaveAsPrefabAsset(root, BlackjackPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    // ================================================================ スロットの筐体（Slot.prefab の Visual/Cabinet）
    const string SlotPrefab = "Assets/_Prefabs/Slot.prefab";

    /// <summary>
    /// Slot.prefab の中で筐体を0から組む（2026/11/13 ゆせさんの許可 18:46）。
    /// ・視線で台を選ぶ当たり判定（Visual の BoxCollider。SlotMachine.interactionCollider が参照）と画面の Canvas はそのまま。
    /// ・Visual の下の古い見た目（RoomiesSlotArt）は消し、「Visual/Cabinet」に組む。Visual は伸ばして回した箱なので、
    ///   Cabinet で向きと大きさを打ち消し、中の部品は「画面の正面が +z」の普通の座標で置く。
    /// ・前にシーンの3台に足した筐体（Visual/Cabinet）と、表示を切った上書きは消して元に戻す。
    /// </summary>
    [MenuItem("Roomies/Art/Rebuild/3b Slot Cabinet (Slot.prefab)")]
    internal static void BuildSlotCabinets()
    {
        // シーン側：前に足した筐体を消し、上書きを戻す
        var scene = SceneManager.GetActiveScene();
        foreach (var sm in Object.FindObjectsByType<SlotMachine>())
        {
            if (sm.gameObject.scene != scene) continue;
            var visual = sm.transform.Find("Visual"); if (visual == null) continue;
            foreach (Transform c in visual.Cast<Transform>().ToArray())
                if (c.name == "Cabinet" && PrefabUtility.IsAddedGameObjectOverride(c.gameObject)) Object.DestroyImmediate(c.gameObject);
            var vr = visual.GetComponent<MeshRenderer>(); if (vr != null && PrefabUtility.IsPartOfPrefabInstance(vr)) PrefabUtility.RevertObjectOverride(vr, InteractionMode.AutomatedAction);
            var art = visual.Find("RoomiesSlotArt"); if (art != null && PrefabUtility.IsPartOfPrefabInstance(art)) PrefabUtility.RevertObjectOverride(art.gameObject, InteractionMode.AutomatedAction);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);

        // プレハブ側
        var root = PrefabUtility.LoadPrefabContents(SlotPrefab);
        try
        {
            var sm = root.GetComponentInChildren<SlotMachine>(true);
            var visual = sm.transform.Find("Visual");
            var canvas = sm.GetComponentInChildren<Canvas>(true);
            foreach (Transform c in visual.Cast<Transform>().ToArray()) Object.DestroyImmediate(c.gameObject);   // RoomiesSlotArt と古い Cabinet
            var vr = visual.GetComponent<MeshRenderer>(); if (vr != null) vr.enabled = false;

            // 画面の正面を +z にした枠（原点は SlotMachine）
            var frame = new GameObject("frame").transform;
            frame.SetParent(root.transform, false);
            frame.position = sm.transform.position;
            frame.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(-canvas.transform.forward, Vector3.up).normalized, Vector3.up);
            var cw = new Vector3[4]; ((RectTransform)canvas.transform).GetWorldCorners(cw);
            var c4 = cw.Select(p => frame.InverseTransformPoint(p)).ToArray();
            // 画面の左右が入れ替わらないよう、x の小さい方を左下・大きい方を右上にそろえる
            var lo = new Vector3(c4.Min(p => p.x), c4.Min(p => p.y), c4.Average(p => p.z));
            var hi = new Vector3(c4.Max(p => p.x), c4.Max(p => p.y), c4.Average(p => p.z));
            var box = visual.GetComponent<BoxCollider>();
            var bb = new Bounds(frame.InverseTransformPoint(visual.TransformPoint(box.center)), Vector3.zero);
            foreach (var sx in new[] { -.5f, .5f }) foreach (var sy in new[] { -.5f, .5f }) foreach (var sz in new[] { -.5f, .5f })
                bb.Encapsulate(frame.InverseTransformPoint(visual.TransformPoint(box.center + Vector3.Scale(box.size, new Vector3(sx, sy, sz)))));
            // 足元の高さ（プレハブの中の床）。シーンでは床が 0.118 なので、プレハブの中の y に合わせて組む
            var g = SlotCabinet(new[] { lo, lo, hi, hi }, bb, 0);
            g.SetParent(visual, false);
            g.position = frame.position; g.rotation = frame.rotation;
            // Visual の伸ばしを打ち消して、Cabinet の中を 1m = 1 にする
            var ls = Vector3.one;
            for (int i = 0; i < 3; i++)
            {
                var axis = g.localRotation * (i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward);
                ls[i] = 1f / visual.TransformVector(axis).magnitude;
            }
            g.localScale = ls;
            Object.DestroyImmediate(frame.gameObject);
            PrefabUtility.SaveAsPrefabAsset(root, SlotPrefab);
            Debug.Log($"[Rebuild] slot cabinet built in prefab: screen {lo}-{hi}, body {bb.min}-{bb.max}, cabinet lossy {g.lossyScale}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>画面（Canvas の四隅）を囲む筐体を、ワールド座標で組む。正面は +z、人から見た左は +x。</summary>
    static Transform SlotCabinet(Vector3[] c, Bounds body, int index)
    {
        var g = new GameObject("Cabinet").transform;
        float sx0 = Mathf.Min(c[0].x, c[2].x), sx1 = Mathf.Max(c[0].x, c[2].x), sy0 = c[0].y, sy1 = c[2].y, face = c[0].z;
        float cx = (sx0 + sx1) / 2, bx0 = body.min.x, bx1 = body.max.x, bz0 = body.min.z, bz1 = body.max.z;
        Material[] colors = { Mat("slot_red"), Mat("slot_purple"), Mat("slot_teal") };
        var shell = colors[index % 3]; var chrome = Mat("slot_chrome"); var gold = Mat("casino_gold"); var black = Mat("casino_black");
        float front = face - .004f;   // 画面のすぐ後ろ
        // 台座と本体（左右の側板は色、前の縁は金属）
        Box(g, "Slot base", bx0 - .04f, bx1 + .04f, body.min.y, .48f, bz0, bz1 + .14f, black);
        Box(g, "Slot kick plate", bx0, bx1, .08f, .4f, bz1 + .14f, bz1 + .16f, chrome);
        Box(g, "Slot body", bx0, bx1, .48f, sy1 + .12f, bz0, front, shell);
        Box(g, "Slot side trim", bx0 - .02f, bx0 + .03f, .48f, sy1 + .12f, front - .05f, front + .02f, chrome);
        Box(g, "Slot side trim", bx1 - .03f, bx1 + .02f, .48f, sy1 + .12f, front - .05f, front + .02f, chrome);
        // 画面の後ろの黒いガラス（ゲーム中は Canvas の画面がこの手前に出る）
        Box(g, "Screen glass", sx0, sx1, sy0, sy1, front - .002f, front + .001f, Mat("slot_screen"));
        // 画面のまわりの縁（画面には重ならない）
        Box(g, "Screen bezel", sx0 - .06f, sx1 + .06f, sy1, sy1 + .06f, front, front + .035f, gold);
        Box(g, "Screen bezel", sx0 - .06f, sx1 + .06f, sy0 - .06f, sy0, front, front + .035f, gold);
        Box(g, "Screen bezel", sx0 - .06f, sx0, sy0, sy1, front, front + .035f, gold);
        Box(g, "Screen bezel", sx1, sx1 + .06f, sy0, sy1, front, front + .035f, gold);
        // ボタンの台（画面の下・前へ斜め）とボタン、コインの受け皿
        RBox(g, "Button deck", new Vector3(cx, sy0 - .1f, front + .12f), new Vector3(bx1 - bx0 - .04f, .05f, .26f), new Vector3(-15, 0, 0), black);
        Material[] btn = { Mat("chip_red"), Mat("chip_blue"), Mat("bulb") };
        for (int i = 0; i < 3; i++)
        {
            var b = Cyl(g, "Slot button", new Vector3(sx1 - .14f - i * .18f, sy0 - .07f, front + .14f), new Vector3(.08f, .015f, .08f), btn[i]);
            b.transform.localRotation = Quaternion.Euler(-15, 0, 0);
        }
        Box(g, "Coin tray", cx - .3f, cx + .3f, .5f, .58f, front, bz1 + .2f, chrome);
        Box(g, "Coin tray lip", cx - .3f, cx + .3f, .58f, .62f, bz1 + .17f, bz1 + .2f, chrome);
        // 上の看板（光る）と回転灯
        Box(g, "Topper", bx0 - .03f, bx1 + .03f, sy1 + .12f, sy1 + .42f, bz0, front + .04f, black);
        var neon = Mat(index % 2 == 0 ? "neon_pink" : "neon_cyan");
        Box(g, "Topper frame", bx0 + .03f, bx1 - .03f, sy1 + .16f, sy1 + .18f, front + .04f, front + .06f, neon);
        Box(g, "Topper frame", bx0 + .03f, bx1 - .03f, sy1 + .36f, sy1 + .38f, front + .04f, front + .06f, neon);
        Box(g, "Topper frame", bx0 + .03f, bx0 + .05f, sy1 + .16f, sy1 + .38f, front + .04f, front + .06f, neon);
        Box(g, "Topper frame", bx1 - .05f, bx1 - .03f, sy1 + .16f, sy1 + .38f, front + .04f, front + .06f, neon);
        Material[] dots = { Mat("chip_red"), Mat("bulb"), Mat("chip_red") };
        for (int k = 0; k < 3; k++)
            Cyl(g, "Topper light", new Vector3(cx + (k - 1) * .22f, sy1 + .27f, front + .05f), new Vector3(.12f, .01f, .12f), dots[k]).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Cyl(g, "Candle light", new Vector3(cx, sy1 + .52f, (bz0 + front) / 2), new Vector3(.12f, .1f, .12f), Mat(index % 2 == 0 ? "chip_red" : "bulb"));
        // レバー（人から見て右＝ -x の側）
        float lx = bx0 - .06f, lz = (bz0 + front) / 2 + .05f;
        Box(g, "Lever housing", lx - .05f, lx + .04f, 1.0f, 1.25f, lz - .1f, lz + .1f, chrome);
        Cyl(g, "Lever arm", new Vector3(lx - .05f, 1.48f, lz), new Vector3(.035f, .25f, .035f), chrome);
        var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere); ball.name = "Lever ball";
        Object.DestroyImmediate(ball.GetComponent<Collider>());
        ball.transform.SetParent(g, false); ball.transform.localPosition = new Vector3(lx - .05f, 1.76f, lz); ball.transform.localScale = Vector3.one * .1f;
        ball.GetComponent<MeshRenderer>().sharedMaterial = Mat("chip_red"); ball.isStatic = true;
        // 横の光る線
        Box(g, "Side light strip", bx0 - .005f, bx0 + .005f, .55f, sy1, (bz0 + front) / 2 - .02f, (bz0 + front) / 2 + .02f, Mat(index % 2 == 0 ? "neon_pink" : "neon_cyan"));
        Box(g, "Side light strip", bx1 - .005f, bx1 + .005f, .55f, sy1, (bz0 + front) / 2 - .02f, (bz0 + front) / 2 + .02f, Mat(index % 2 == 0 ? "neon_pink" : "neon_cyan"));
        foreach (var t in g.GetComponentsInChildren<Transform>()) t.gameObject.isStatic = false;   // スロットはネットワークの物の子
        return g;
    }

    /// <summary>絨毯：深い赤に金と紺の菱形の模様（2m で繰り返す）。URP Lit。</summary>
    static Material CasinoCarpet(float w, float d)
    {
        var tex = RoomiesNature.Tex("casino_carpet", 512, 512, (float u, float v, out Color c, out float h) =>
        {
            float gu = u * 2, gv = v * 2; float fu = gu - Mathf.Floor(gu), fv = gv - Mathf.Floor(gv);
            float dd = Mathf.Abs(fu - .5f) + Mathf.Abs(fv - .5f);
            c = new Color(.42f, .08f, .1f);
            if (Mathf.Abs(dd - .42f) < .025f) c = new Color(.85f, .66f, .28f);
            else if (dd < .2f) c = new Color(.1f, .12f, .3f);
            else if (Mathf.Abs(dd - .2f) < .02f) c = new Color(.85f, .66f, .28f);
            float n = RoomiesNature.Fbm(u, v, 64, 2, 7);
            c *= .9f + .2f * n; c.a = 1; h = n;
        }, 1.5f, out var nrm);
        var m = RoomiesNature.Lit("M_Casino_Carpet", tex, nrm, Color.white, .05f, false, .4f);
        m.SetTextureScale("_BaseMap", new Vector2(w / 4f, d / 4f));
        return m;
    }
}
#endif
