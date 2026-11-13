#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 作り直し3-1：カジノを1つの建物として0から設計する（2026/11/13 ゆせさんの依頼 20:34）。Rebuild/3 Casino から呼ぶ。
/// 外形：x -1.2〜16.8、z -19.6〜-7.8、高さ 5.4m（屋上の看板は上へ）。床は Demo/pCube2 の上面（y 0.118）。
/// 入口：正面の真ん中の大きな入口（ガラスの扉を開け放した形。いつでも通れる）と、左の通用口（シーンの DoorPivot。通信で開け閉めする扉）。
/// 中：絨毯・木の腰壁と布張りの壁・金の柱・格天井と間接照明・シャンデリア3つ・バー（左の壁）・両替所（右奥）・
///     スロットの列（3台はシーンのまま）・ブラックジャック台のまわり（敷物・ロープ・看板）。
/// 当たり判定：外壁（入口と通用口を空ける）・屋根・バーのカウンター・両替所。仕組みの物（スロット・台・自販機・扉）は動かさない。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const float KX0 = -1.2f, KX1 = 16.8f, KZ0 = -19.6f, KZ1 = -7.8f, KT = .25f, KFloor = .118f, KCeil = 4.4f, KTop = 5.4f;
    const float KDoorX0 = .76f, KDoorX1 = 1.9f, KEntX0 = 6.3f, KEntX1 = 9.3f, KEntH = 3.0f;

    static void BuildCasinoBuilding(Transform g, Transform work)
    {
        CasinoShell(g);
        CasinoFacade(g, work);
        CasinoInterior(g, work);
    }

    // ================================================================ 外壁・屋根（当たり判定つき）
    static void CasinoShell(Transform g)
    {
        var wall = Mat("casino_facade"); var cream = Mat("casino_cream"); var gold = Mat("casino_gold"); var black = Mat("casino_black");
        float ix0 = KX0 + KT, ix1 = KX1 - KT, iz0 = KZ0 + KT, iz1 = KZ1 - KT;
        // 正面（入口と通用口を空ける）
        foreach (var (x0, x1) in new[] { (KX0, KDoorX0), (KDoorX1, KEntX0), (KEntX1, KX1) })
        {
            Box(g, "Casino front wall", x0, x1, 0, KTop, iz1, KZ1, wall);
            Solid(g, "Casino wall collider", new Vector3(x0, 0, iz1), new Vector3(x1, KTop, KZ1));
        }
        Box(g, "Casino front wall", KDoorX0, KDoorX1, 2.18f, KTop, iz1, KZ1, wall);
        Box(g, "Casino front wall", KEntX0, KEntX1, KEntH, KTop, iz1, KZ1, wall);
        Solid(g, "Casino wall collider", new Vector3(KDoorX0, 2.18f, iz1), new Vector3(KDoorX1, KTop, KZ1));
        Solid(g, "Casino wall collider", new Vector3(KEntX0, KEntH, iz1), new Vector3(KEntX1, KTop, KZ1));
        // 左・右・奥
        Box(g, "Casino side wall", KX0, ix0, 0, KTop, KZ0, KZ1, wall);
        Box(g, "Casino side wall", ix1, KX1, 0, KTop, KZ0, KZ1, wall);
        Box(g, "Casino back wall", ix0, ix1, 0, KTop, KZ0, iz0, wall);
        Solid(g, "Casino wall collider", new Vector3(KX0, 0, KZ0), new Vector3(ix0, KTop, KZ1));
        Solid(g, "Casino wall collider", new Vector3(ix1, 0, KZ0), new Vector3(KX1, KTop, KZ1));
        Solid(g, "Casino wall collider", new Vector3(ix0, 0, KZ0), new Vector3(ix1, KTop, iz0));
        // 屋根（天井の上の板と、屋上の防水面・パラペット・笠木）
        Box(g, "Casino roof slab", KX0, KX1, KCeil, KTop - .35f, KZ0, KZ1, Mat("roof_flat"));
        Solid(g, "Casino roof collider", new Vector3(KX0, KCeil, KZ0), new Vector3(KX1, KTop - .35f, KZ1));
        Box(g, "Casino roof deck", KX0 + .2f, KX1 - .2f, KTop - .35f, KTop - .3f, KZ0 + .2f, KZ1 - .2f, Mat("roof_flat"));
        Box(g, "Casino coping", KX0 - .08f, KX1 + .08f, KTop, KTop + .1f, KZ1 - .2f, KZ1 + .08f, gold);
        Box(g, "Casino coping", KX0 - .08f, KX1 + .08f, KTop, KTop + .1f, KZ0 - .08f, KZ0 + .2f, cream);
        Box(g, "Casino coping", KX0 - .08f, KX0 + .2f, KTop, KTop + .1f, KZ0, KZ1, cream);
        Box(g, "Casino coping", KX1 - .2f, KX1 + .08f, KTop, KTop + .1f, KZ0, KZ1, cream);
        // 外壁の足元（黒い石）と、側面・裏の帯
        Box(g, "Casino plinth", KX0 - .03f, KX1 + .03f, 0, .5f, KZ0 - .03f, KZ0 + .02f, black);
        Box(g, "Casino plinth", KX0 - .03f, KX0 + .02f, 0, .5f, KZ0, KZ1, black);
        Box(g, "Casino plinth", KX1 - .02f, KX1 + .03f, 0, .5f, KZ0, KZ1, black);
        foreach (float y in new[] { 3.6f })
        {
            Box(g, "Casino band", KX0 - .04f, KX0 + .01f, y, y + .25f, KZ0, KZ1, cream);
            Box(g, "Casino band", KX1 - .01f, KX1 + .04f, y, y + .25f, KZ0, KZ1, cream);
            Box(g, "Casino band", KX0, KX1, y, y + .25f, KZ0 - .04f, KZ0 + .01f, cream);
        }
        // 屋上の室外機
        foreach (var (x, z) in new[] { (2.5f, -16.5f), (5.0f, -16.5f), (13.5f, -12f) })
        {
            Box(g, "AC unit", x - .7f, x + .7f, KTop - .3f, KTop + .55f, z - .5f, z + .5f, Mat("ac_unit"));
            Cyl(g, "AC fan", new Vector3(x, KTop + .57f, z), new Vector3(.8f, .02f, .8f), Mat("metal_dark"));
        }
    }

    // ================================================================ 外観：入口・ひさし・看板・ネオン
    static void CasinoFacade(Transform g, Transform work)
    {
        var gold = Mat("casino_gold"); var cream = Mat("casino_cream"); var black = Mat("casino_black"); var bulb = Mat("bulb");
        float face = KZ1;   // 正面の外の面
        // 足元の黒い石と、上の金の帯・ネオンの線
        foreach (var (x0, x1) in new[] { (KX0 - .03f, KDoorX0), (KDoorX1, KEntX0), (KEntX1, KX1 + .03f) })
            Box(g, "Casino plinth", x0, x1, 0, .5f, face, face + .05f, black);
        Box(g, "Casino cornice", KX0 - .1f, KX1 + .1f, 4.55f, 4.85f, face, face + .2f, gold);
        Box(g, "Casino cornice", KX0 - .1f, KX1 + .1f, 4.85f, 4.95f, face, face + .28f, cream);
        Box(g, "Casino neon line", KX0, KX1, 4.47f, 4.52f, face + .1f, face + .14f, Mat("neon_pink"));
        // 柱（金とクリーム）
        foreach (float x in new[] { KX0 + .1f, 3.6f, 12.0f, KX1 - .1f })
        {
            Box(g, "Casino pilaster", x - .22f, x + .22f, .5f, 4.55f, face, face + .12f, cream);
            Box(g, "Casino pilaster cap", x - .28f, x + .28f, 4.35f, 4.55f, face, face + .18f, gold);
            Box(g, "Casino pilaster base", x - .28f, x + .28f, .5f, .7f, face, face + .18f, gold);
        }
        // 入口：金の額縁、開け放したガラスの扉（左右に開いた形。当たり判定なし）、上の欄間
        Box(g, "Entrance portal", KEntX0 - .3f, KEntX0, 0, KEntH + .3f, face, face + .16f, gold);
        Box(g, "Entrance portal", KEntX1, KEntX1 + .3f, 0, KEntH + .3f, face, face + .16f, gold);
        Box(g, "Entrance portal", KEntX0 - .3f, KEntX1 + .3f, KEntH, KEntH + .3f, face, face + .16f, gold);
        Box(g, "Entrance transom", KEntX0, KEntX1, 2.45f, KEntH, face - .2f, face - .14f, Mat("slot_screen"));
        for (float x = KEntX0 + .3f; x < KEntX1 - .1f; x += .5f) Box(g, "Transom fan", x - .02f, x + .02f, 2.45f, KEntH, face - .14f, face - .1f, gold);
        foreach (var (hx, dir) in new[] { (KEntX0, 1f), (KEntX1, -1f) })
        {
            var leaf = new GameObject("Entrance door leaf").transform;
            leaf.SetParent(g, false); leaf.localPosition = new Vector3(hx, KFloor, face - .2f); leaf.localRotation = Quaternion.Euler(0, dir > 0 ? -100 : 100, 0);
            Box(leaf, "Door glass", 0, dir * 1.4f, .1f, 2.35f, -.025f, .025f, Mat("slot_screen"));
            Box(leaf, "Door rail", 0, dir * 1.4f, 0, .14f, -.04f, .04f, gold);
            Box(leaf, "Door rail", 0, dir * 1.4f, 2.3f, 2.42f, -.04f, .04f, gold);
            Box(leaf, "Door stile", 0, dir * .08f, 0, 2.42f, -.04f, .04f, gold);
            Box(leaf, "Door stile", dir * 1.32f, dir * 1.4f, 0, 2.42f, -.04f, .04f, gold);
            Box(leaf, "Door handle", dir * 1.2f, dir * 1.25f, .8f, 1.6f, .04f, .08f, gold);
        }
        // 通用口（シーンの DoorPivot がはまる所）の枠と小さな看板の板
        Box(g, "Side door frame", KDoorX0 - .1f, KDoorX0, 0, 2.28f, face, face + .1f, gold);
        Box(g, "Side door frame", KDoorX1, KDoorX1 + .1f, 0, 2.28f, face, face + .1f, gold);
        Box(g, "Side door frame", KDoorX0 - .1f, KDoorX1 + .1f, 2.18f, 2.28f, face, face + .1f, gold);
        Box(g, "Side door lamp", 2.05f, 2.25f, 2.0f, 2.3f, face, face + .16f, black);
        Box(g, "Side door lamp lens", 2.08f, 2.22f, 2.02f, 2.28f, face + .16f, face + .17f, bulb);
        // ひさし（マーキー）：入口の上に張り出す。下に電球、前に看板（AFTER HOURS）、金の柱2本
        float mz0 = face, mz1 = face + 1.55f, my0 = 3.3f, my1 = 3.75f, mx0 = 5.0f, mx1 = 10.6f;
        Box(g, "Marquee body", mx0, mx1, my0, my1, mz0, mz1, Mat("casino_red"));
        Box(g, "Marquee trim", mx0 - .05f, mx1 + .05f, my1, my1 + .06f, mz0, mz1 + .05f, gold);
        Box(g, "Marquee trim", mx0 - .05f, mx1 + .05f, my0 - .05f, my0, mz0, mz1 + .05f, gold);
        Box(g, "Marquee face", mx0 + .1f, mx1 - .1f, my0 + .06f, my1 - .06f, mz1, mz1 + .03f, black);
        for (float x = mx0 + .15f; x < mx1 - .1f; x += .3f)
            for (float z = mz0 + .25f; z < mz1 - .1f; z += .35f)
                Cyl(g, "Marquee bulb", new Vector3(x, my0 - .02f, z), new Vector3(.08f, .02f, .08f), bulb);
        for (float x = mx0 + .1f; x < mx1; x += .28f)
        {
            Cyl(g, "Marquee chaser", new Vector3(x, my1 + .1f, mz1 + .02f), new Vector3(.07f, .04f, .07f), bulb);
            Cyl(g, "Marquee chaser", new Vector3(x, my0 - .08f, mz1 + .02f), new Vector3(.07f, .04f, .07f), bulb);
        }
        foreach (float x in new[] { mx0 + .2f, mx1 - .2f })
        {
            Cyl(g, "Marquee column", new Vector3(x, my0 / 2, mz1 - .2f), new Vector3(.18f, my0 / 2, .18f), gold);
            Cyl(g, "Marquee column base", new Vector3(x, .06f, mz1 - .2f), new Vector3(.32f, .06f, .32f), black);
        }
        // 看板の文字：AFTER HOURS をひさしの前へ（もとから通りの側から読める向き）
        MoveSign(work, "Sign - AFTER HOURS", new Vector3(7.8f, (my0 + my1) / 2, mz1 + .04f), 180, 1f);
        // 屋上の大きな看板 CASINO（電球の枠）
        Box(g, "Rooftop sign board", 4.4f, 11.2f, KTop + .1f, KTop + 1.6f, face + .05f, face + .2f, black);
        Box(g, "Rooftop sign post", 5.0f, 5.15f, KTop, KTop + .2f, face + .2f, face + .9f, Mat("metal_dark"));
        Box(g, "Rooftop sign post", 10.45f, 10.6f, KTop, KTop + .2f, face + .2f, face + .9f, Mat("metal_dark"));
        for (float x = 4.5f; x < 11.15f; x += .3f)
            foreach (float y in new[] { KTop + .2f, KTop + 1.5f })
                Cyl(g, "Rooftop sign bulb", new Vector3(x, y, face + .22f), new Vector3(.09f, .03f, .09f), bulb).transform.localRotation = Quaternion.Euler(90, 0, 0);
        NewSign(g, work, "Sign - CASINO", "CASINO", new Vector3(7.8f, KTop + .85f, face + .215f), 180, 2.6f, new Color(1f, .82f, .35f));
        // 壁の光る絵（トランプ・サイコロ・コイン）
        foreach (var (x0, x1, m) in new[] { (2.3f, 3.2f, "poster_cards"), (4.0f, 5.0f, "poster_dice"), (10.4f, 11.5f, "poster_coins"), (12.6f, 13.7f, "poster_cards"), (14.4f, 15.5f, "poster_dice") })
        {
            Box(g, "Poster frame", x0 - .06f, x1 + .06f, 1.15f, 2.75f, face, face + .08f, gold);
            Box(g, "Poster light box", x0, x1, 1.21f, 2.69f, face + .08f, face + .1f, Mat(m));
            string label = m == "poster_cards" ? "21" : m == "poster_dice" ? "777" : "$$$";
            NewSign(g, work, "Sign - poster " + label, label, new Vector3((x0 + x1) / 2, 1.95f, face + .11f), 180, .9f, new Color(.35f, .05f, .1f));
        }
        // 赤い絨毯と、入口の両わきのロープ（通り道はふさがない）
        Box(g, "Entrance carpet", KEntX0 + .1f, KEntX1 - .1f, 0, .03f, face, face + 1.55f, Mat("casino_red"));
        foreach (float x in new[] { KEntX0 - .7f, KEntX1 + .7f })
        {
            foreach (float z in new[] { face + .4f, face + 1.3f })
            {
                Cyl(g, "Stanchion base", new Vector3(x, .02f, z), new Vector3(.3f, .02f, .3f), gold);
                Cyl(g, "Stanchion post", new Vector3(x, .5f, z), new Vector3(.06f, .48f, .06f), gold);
                Cyl(g, "Stanchion top", new Vector3(x, 1.0f, z), new Vector3(.1f, .04f, .1f), gold);
            }
            Box(g, "Velvet rope", x - .03f, x + .03f, .82f, .88f, face + .4f, face + 1.3f, Mat("casino_red"));
        }
        // 夜に正面を照らす光（朝は消える：Shop light）
        SpotLight(g, "Shop light - CASINO", new Vector3(7.8f, 6.4f, -3.4f), new Vector3(7.8f, 2.2f, KZ1), 6f, 12f, 110f, new Color(1f, .78f, .55f));
        PointLight(g, "Shop light - CASINO marquee", new Vector3(7.8f, 2.9f, face + .9f), 3f, 7f, new Color(1f, .82f, .6f));
    }

    // ================================================================ 室内
    static void CasinoInterior(Transform g, Transform work)
    {
        var gold = Mat("casino_gold"); var wood = Mat("casino_wood"); var fabric = Mat("casino_wall"); var black = Mat("casino_black"); var bulb = Mat("bulb");
        float ix0 = KX0 + KT, ix1 = KX1 - KT, iz0 = KZ0 + KT, iz1 = KZ1 - KT;
        // 床：絨毯
        var carpet = GameObject.CreatePrimitive(PrimitiveType.Quad);
        carpet.name = "Casino carpet"; Object.DestroyImmediate(carpet.GetComponent<Collider>());
        carpet.transform.SetParent(g, false);
        carpet.transform.localPosition = new Vector3((ix0 + ix1) / 2, KFloor + .006f, (iz0 + iz1) / 2);
        carpet.transform.localRotation = Quaternion.Euler(90, 0, 0);
        carpet.transform.localScale = new Vector3(ix1 - ix0, iz1 - iz0, 1);
        carpet.GetComponent<MeshRenderer>().sharedMaterial = CasinoCarpet(ix1 - ix0, iz1 - iz0);
        carpet.isStatic = true;
        // 壁：木の腰壁・布張り・金の柱・見切り・額（4面）
        var walls = new (string name, Vector3 n, float plane, float a0, float a1)[]
        {
            ("back", Vector3.forward, iz0, ix0, ix1), ("left", Vector3.right, ix0, iz0, iz1), ("right", Vector3.left, ix1, iz0, iz1), ("front", Vector3.back, iz1, ix0, ix1),
        };
        foreach (var w in walls)
        {
            FaceBox(g, "Wainscot", w.n, w.plane, w.a0, w.a1, KFloor, 1.1f, 0, .05f, wood);
            FaceBox(g, "Chair rail", w.n, w.plane, w.a0, w.a1, 1.08f, 1.16f, 0, .09f, gold);
            FaceBox(g, "Wall fabric", w.n, w.plane, w.a0, w.a1, 1.16f, KCeil, 0, .02f, fabric);
            FaceBox(g, "Crown molding", w.n, w.plane, w.a0, w.a1, KCeil - .22f, KCeil, 0, .16f, gold);
            for (float a = w.a0 + 1.2f; a < w.a1 - .5f; a += 2.4f)
            {
                if (w.name == "front" && ((a > KDoorX0 - .4f && a < KDoorX1 + .4f) || (a > KEntX0 - .4f && a < KEntX1 + .4f))) continue;
                FaceBox(g, "Wall pilaster", w.n, w.plane, a - .14f, a + .14f, 1.16f, KCeil - .22f, 0, .08f, gold);
                FaceBox(g, "Wainscot panel", w.n, w.plane, a - .9f, a + .9f, .3f, .95f, .05f, .065f, wood);
            }
        }
        // 正面の内側の入口の上：PLAY / UNWIND / REPEAT は左の壁のバーの上へ（部屋の中から読める向き）
        MoveSign(work, "Sign - PLAY / UNWIND / REPEAT", new Vector3(ix0 + .06f, 3.25f, -15.6f), 270, 1.2f);
        // 格天井（金の梁）と、まわりの間接照明・埋め込みの灯
        Box(g, "Ceiling", ix0, ix1, KCeil - .02f, KCeil, iz0, iz1, Mat("casino_ceiling"));
        for (float x = ix0 + 2.9f; x < ix1; x += 2.9f) Box(g, "Coffer beam", x - .09f, x + .09f, KCeil - .22f, KCeil, iz0, iz1, gold);
        for (float z = iz0 + 2.8f; z < iz1; z += 2.8f) Box(g, "Coffer beam", ix0, ix1, KCeil - .22f, KCeil, z - .09f, z + .09f, gold);
        foreach (var w in walls) FaceBox(g, "Cove light", w.n, w.plane, w.a0 + .2f, w.a1 - .2f, KCeil - .3f, KCeil - .26f, .17f, .2f, Mat("neon_pink"));
        // シャンデリア：入口・スロットの列の上・ブラックジャック台の上
        Chandelier(g, new Vector3(7.8f, KCeil, -9.9f), .9f);
        Chandelier(g, new Vector3(7.8f, KCeil, -13.6f), 1.15f);
        Chandelier(g, new Vector3(5.76f, KCeil, -16.4f), 1.05f);
        PointLight(g, "Casino light - entrance", new Vector3(7.8f, 3.3f, -9.9f), 4.8f, 9f, new Color(1f, .84f, .64f));
        PointLight(g, "Casino light - slots", new Vector3(7.8f, 3.1f, -13.6f), 5.4f, 11f, new Color(1f, .82f, .62f));
        PointLight(g, "Casino light - blackjack", new Vector3(5.76f, 3.0f, -16.4f), 5.0f, 8f, new Color(1f, .86f, .66f));

        // ---- バー（左の壁）：奥の棚・鏡・光る瓶、カウンター（当たり判定）、スツール
        float bz0 = -18.9f, bz1 = -12.8f, cx0 = .45f, cx1 = 1.05f;
        Box(g, "Back bar cabinet", ix0, ix0 + .5f, KFloor, 1.0f, bz0, bz1, wood);
        Box(g, "Back bar mirror", ix0, ix0 + .03f, 1.3f, 2.8f, bz0 + .3f, bz1 - .3f, Mat("jail_mirror"));
        foreach (float y in new[] { 1.4f, 1.85f, 2.3f })
        {
            Box(g, "Back bar shelf", ix0, ix0 + .32f, y - .03f, y, bz0 + .3f, bz1 - .3f, Mat("slot_screen"));
            Box(g, "Back bar shelf light", ix0 + .02f, ix0 + .3f, y - .035f, y - .03f, bz0 + .3f, bz1 - .3f, Mat("neon_cyan"));
            int k = 0;
            for (float z = bz0 + .45f; z < bz1 - .4f; z += .22f, k++)
            {
                string[] bottles = { "bottle_amber", "bottle_green", "bottle_clear", "bottle_red" };
                float h = .26f + (k % 3) * .04f;
                Cyl(g, "Bottle", new Vector3(ix0 + .16f, y + h / 2, z), new Vector3(.07f, h / 2, .07f), Mat(bottles[(k + (int)(y * 3)) % 4]));
                Cyl(g, "Bottle neck", new Vector3(ix0 + .16f, y + h + .04f, z), new Vector3(.025f, .04f, .025f), Mat(bottles[(k + (int)(y * 3)) % 4]));
            }
        }
        Box(g, "Back bar crown", ix0, ix0 + .5f, 2.85f, 3.05f, bz0, bz1, gold);
        Box(g, "Bar counter", cx0, cx1, KFloor, 1.0f, bz0, bz1, wood);
        Box(g, "Bar counter front", cx1, cx1 + .03f, .2f, .95f, bz0 + .05f, bz1 - .05f, Mat("casino_red"));
        Box(g, "Bar top", cx0 - .08f, cx1 + .12f, 1.0f, 1.08f, bz0 - .05f, bz1 + .05f, Mat("casino_marble"));
        Box(g, "Bar foot rail", cx1 + .12f, cx1 + .16f, .2f, .26f, bz0, bz1, gold);
        Box(g, "Bar light strip", cx1 + .03f, cx1 + .05f, .92f, .96f, bz0 + .05f, bz1 - .05f, Mat("neon_pink"));
        Solid(g, "Bar collider", new Vector3(cx0 - .08f, 0, bz0 - .05f), new Vector3(cx1 + .12f, 1.08f, bz1 + .05f));
        Solid(g, "Back bar collider", new Vector3(ix0, 0, bz0), new Vector3(ix0 + .5f, 1.0f, bz1));
        for (float z = bz0 + .5f; z < bz1 - .3f; z += .95f)
        {
            Cyl(g, "Bar stool base", new Vector3(cx1 + .55f, KFloor + .02f, z), new Vector3(.36f, .02f, .36f), gold);
            Cyl(g, "Bar stool post", new Vector3(cx1 + .55f, KFloor + .38f, z), new Vector3(.06f, .36f, .06f), gold);
            Cyl(g, "Bar stool seat", new Vector3(cx1 + .55f, KFloor + .78f, z), new Vector3(.42f, .05f, .42f), Mat("casino_red"));
        }
        // カウンターの上の小物（グラスとシェーカー）
        foreach (float z in new[] { -17.6f, -16.1f, -14.4f }) Cyl(g, "Glass", new Vector3(.75f, 1.13f, z), new Vector3(.08f, .05f, .08f), Mat("glass_shop"));
        Cyl(g, "Shaker", new Vector3(.6f, 1.18f, -15.2f), new Vector3(.09f, .1f, .09f), Mat("slot_chrome"));
        PointLight(g, "Casino light - bar", new Vector3(.9f, 2.6f, -15.8f), 3.2f, 5f, new Color(1f, .7f, .55f));

        // ---- 両替所（右奥）：カウンターと真鍮の格子、窓口、看板
        float qx0 = 12.7f, qz1 = -16.9f;
        Box(g, "Cashier counter", qx0, ix1, KFloor, 1.05f, qz1 - .45f, qz1, wood);
        Box(g, "Cashier counter top", qx0 - .05f, ix1, 1.05f, 1.12f, qz1 - .5f, qz1 + .08f, Mat("casino_marble"));
        Box(g, "Cashier partition", qx0 - .12f, qx0, KFloor, KCeil - .22f, iz0, qz1, wood);
        Box(g, "Cashier header", qx0, ix1, 2.6f, KCeil - .22f, qz1 - .1f, qz1, Mat("casino_red"));
        for (float x = qx0 + .1f; x < ix1 - .05f; x += .12f) Cyl(g, "Cashier grille bar", new Vector3(x, 1.86f, qz1 - .05f), new Vector3(.025f, .74f, .025f), gold);
        foreach (float x in new[] { 13.8f, 15.4f })
        {
            Box(g, "Cashier window", x - .35f, x + .35f, 1.12f, 1.55f, qz1 - .07f, qz1 - .03f, Mat("slot_screen"));
            Box(g, "Cashier tray", x - .25f, x + .25f, 1.12f, 1.16f, qz1 - .1f, qz1 + .15f, gold);
            Box(g, "Cashier lamp", x - .12f, x + .12f, 2.45f, 2.6f, qz1 - .25f, qz1 - .1f, Mat("casino_green_shade"));
        }
        Box(g, "Chip racks", qx0 + .3f, ix1 - .3f, 1.12f, 1.3f, qz1 - .45f, qz1 - .3f, black);
        Solid(g, "Cashier collider", new Vector3(qx0 - .12f, 0, iz0), new Vector3(ix1, KCeil, qz1 + .08f));
        NewSign(g, work, "Sign - CASHIER", "CASHIER", new Vector3((qx0 + ix1) / 2, 3.1f, qz1 + .02f), 180, .9f, new Color(1f, .86f, .45f));

        // ---- スロットの列（シーンの3台は動かさない）：通路の敷物、間仕切り、スツール、上の看板
        float sz = -11.36f;
        Box(g, "Slot aisle rug", ix0 + .4f, ix1 - .4f, KFloor + .007f, KFloor + .012f, sz - 1.1f, sz + 1.3f, Mat("casino_red"));
        foreach (float x in new[] { 2.67f, 7.66f, 12.36f })
        {
            Cyl(g, "Slot stool base", new Vector3(x, KFloor + .02f, sz + .95f), new Vector3(.34f, .02f, .34f), gold);
            Cyl(g, "Slot stool post", new Vector3(x, KFloor + .32f, sz + .95f), new Vector3(.06f, .3f, .06f), gold);
            Cyl(g, "Slot stool seat", new Vector3(x, KFloor + .66f, sz + .95f), new Vector3(.4f, .05f, .4f), Mat("casino_red"));
        }
        // 列の両端の柱（金の柱と光る頭）
        foreach (float x in new[] { .2f, 15.3f })
        {
            Cyl(g, "Slot row post", new Vector3(x, KFloor + .7f, sz - .3f), new Vector3(.22f, .7f, .22f), gold);
            Cyl(g, "Slot row lamp", new Vector3(x, KFloor + 1.5f, sz - .3f), new Vector3(.3f, .1f, .3f), Mat("neon_cyan"));
        }
        HangingSign(g, work, "Sign - SLOTS", "SLOTS", new Vector3(7.8f, 3.25f, sz - .6f), 1.0f);

        // ---- ブラックジャック台のまわり：丸い敷物・ロープ（親の側）・看板・チップの台
        var bj = new Vector3(5.76f, KFloor, -16.34f);
        Cyl(g, "Table rug", new Vector3(bj.x, KFloor + .009f, bj.z), new Vector3(5.2f, .003f, 4.0f), Mat("casino_felt"));
        Cyl(g, "Table rug border", new Vector3(bj.x, KFloor + .007f, bj.z), new Vector3(5.5f, .003f, 4.3f), gold);
        for (int k = 0; k <= 6; k++)
        {
            float a = Mathf.Lerp(200, 340, k / 6f) * Mathf.Deg2Rad;
            var p = bj + new Vector3(Mathf.Cos(a) * 2.6f, 0, Mathf.Sin(a) * 2.0f);
            Cyl(g, "Stanchion base", new Vector3(p.x, KFloor + .02f, p.z), new Vector3(.28f, .02f, .28f), gold);
            Cyl(g, "Stanchion post", new Vector3(p.x, KFloor + .48f, p.z), new Vector3(.055f, .46f, .055f), gold);
            if (k > 0)
            {
                float a0 = Mathf.Lerp(200, 340, (k - 1) / 6f) * Mathf.Deg2Rad;
                var q = bj + new Vector3(Mathf.Cos(a0) * 2.6f, 0, Mathf.Sin(a0) * 2.0f);
                var mid = (p + q) / 2; var d = p - q;
                RBox(g, "Velvet rope", new Vector3(mid.x, KFloor + .82f, mid.z), new Vector3(.05f, .05f, d.magnitude), new Vector3(0, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 0), Mat("casino_red"));
            }
        }
        HangingSign(g, work, "Sign - BLACKJACK", "BLACKJACK", new Vector3(bj.x, 3.0f, bj.z - 1.6f), 1.0f);
        // 植木（金の鉢）と、壁ぎわのソファ
        foreach (var (x, z) in new[] { (ix0 + .5f, iz1 - .5f), (ix1 - .5f, iz1 - 1.6f), (ix1 - .5f, -15.8f), (9.6f, iz0 + .5f) })
        {
            Cyl(g, "Gold planter", new Vector3(x, KFloor + .3f, z), new Vector3(.5f, .3f, .5f), gold);
            var bush = GameObject.CreatePrimitive(PrimitiveType.Sphere); bush.name = "Planter leaves";
            Object.DestroyImmediate(bush.GetComponent<Collider>()); bush.transform.SetParent(g, false);
            bush.transform.localPosition = new Vector3(x, KFloor + 1.0f, z); bush.transform.localScale = new Vector3(.8f, 1.0f, .8f);
            bush.GetComponent<MeshRenderer>().sharedMaterial = Mat("planter_leaf"); bush.isStatic = true;
        }
        Box(g, "Lounge sofa", 9.2f, 12.4f, KFloor, .5f, iz0 + .05f, iz0 + .9f, Mat("casino_red"));
        Box(g, "Lounge sofa back", 9.2f, 12.4f, .5f, 1.15f, iz0 + .05f, iz0 + .3f, Mat("casino_red"));
        Box(g, "Lounge sofa arm", 9.1f, 9.3f, KFloor, .8f, iz0 + .05f, iz0 + .9f, Mat("casino_red"));
        Box(g, "Lounge sofa arm", 12.3f, 12.5f, KFloor, .8f, iz0 + .05f, iz0 + .9f, Mat("casino_red"));
        Solid(g, "Lounge sofa collider", new Vector3(9.1f, 0, iz0), new Vector3(12.5f, .8f, iz0 + .9f));
    }

    /// <summary>シャンデリア：金の輪3段・ろうそくの灯・水晶のしずく。天井から吊る。</summary>
    static void Chandelier(Transform g, Vector3 top, float size)
    {
        var gold = Mat("casino_gold"); var crystal = Mat("crystal"); var bulb = Mat("bulb");
        float y0 = top.y - .6f;
        Box(g, "Chandelier rod", top.x - .02f, top.x + .02f, y0, top.y, top.z - .02f, top.z + .02f, gold);
        Cyl(g, "Chandelier canopy", new Vector3(top.x, top.y - .03f, top.z), new Vector3(.3f, .03f, .3f), gold);
        float[] rings = { 1f, .72f, .42f };
        for (int i = 0; i < rings.Length; i++)
        {
            float r = size * rings[i] * .5f, y = y0 - i * .28f + (i == 0 ? -.3f : 0);
            Cyl(g, "Chandelier ring", new Vector3(top.x, y, top.z), new Vector3(r * 2, .02f, r * 2), gold);
            int n = 6 + (2 - i) * 3;
            for (int k = 0; k < n; k++)
            {
                float a = k * Mathf.PI * 2 / n + i * .3f;
                var p = new Vector3(top.x + Mathf.Cos(a) * r, y, top.z + Mathf.Sin(a) * r);
                Cyl(g, "Chandelier candle", p + Vector3.up * .07f, new Vector3(.035f, .06f, .035f), Mat("casino_cream"));
                Cyl(g, "Chandelier flame", p + Vector3.up * .16f, new Vector3(.05f, .03f, .05f), bulb);
                RBox(g, "Chandelier crystal", p + Vector3.down * .12f, new Vector3(.04f, .14f, .04f), new Vector3(0, a * Mathf.Rad2Deg, 45), crystal);
            }
        }
        RBox(g, "Chandelier crystal", new Vector3(top.x, y0 - .95f, top.z), new Vector3(.08f, .24f, .08f), new Vector3(0, 45, 0), crystal);
        Cyl(g, "Chandelier glow", new Vector3(top.x, y0 - .55f, top.z), new Vector3(.22f, .1f, .22f), bulb);
    }

    /// <summary>天井から吊った看板（黒い板・金の縁・ネオンの縁）と文字（町のプレハブの看板の文字を写して作る）。</summary>
    static void HangingSign(Transform g, Transform work, string name, string text, Vector3 c, float scale)
    {
        float w = .32f * text.Length * scale + .5f, h = .55f * scale;
        foreach (float dx in new[] { -w / 2 + .15f, w / 2 - .15f }) Box(g, "Sign chain", c.x + dx - .01f, c.x + dx + .01f, c.y + h / 2, KCeil, c.z - .01f, c.z + .01f, Mat("metal_dark"));
        Box(g, "Hanging sign board", c.x - w / 2, c.x + w / 2, c.y - h / 2, c.y + h / 2, c.z - .04f, c.z + .04f, Mat("casino_black"));
        Box(g, "Hanging sign trim", c.x - w / 2 - .03f, c.x + w / 2 + .03f, c.y + h / 2, c.y + h / 2 + .04f, c.z - .05f, c.z + .05f, Mat("casino_gold"));
        Box(g, "Hanging sign trim", c.x - w / 2 - .03f, c.x + w / 2 + .03f, c.y - h / 2 - .04f, c.y - h / 2, c.z - .05f, c.z + .05f, Mat("casino_gold"));
        Box(g, "Hanging sign neon", c.x - w / 2 + .05f, c.x + w / 2 - .05f, c.y - h / 2 + .03f, c.y - h / 2 + .06f, c.z + .04f, c.z + .05f, Mat("neon_pink"));
        NewSign(g, work, name, text, new Vector3(c.x, c.y, c.z + .045f), 180, scale, new Color(1f, .9f, .6f));
    }

    /// <summary>看板の文字を新しく作る：町のプレハブにある AFTER HOURS の文字を写し、文言・位置・大きさ・色を変える。</summary>
    static void NewSign(Transform g, Transform work, string name, string text, Vector3 pos, float yaw, float scale, Color color)
    {
        var src = work != null ? work.Find("Sign - AFTER HOURS") : null;
        if (src == null) return;
        var copy = Object.Instantiate(src.gameObject, g);
        copy.name = name;
        var tmp = copy.GetComponent<TMPro.TextMeshPro>();
        tmp.text = text; tmp.color = color;
        copy.transform.localPosition = pos; copy.transform.localRotation = Quaternion.Euler(0, yaw, 0); copy.transform.localScale = Vector3.one * scale;
        var rt = (RectTransform)copy.transform; rt.sizeDelta = new Vector2(Mathf.Max(2f, text.Length * .55f), .6f);
    }

    static void MoveSign(Transform work, string name, Vector3 pos, float yaw, float scale)
    {
        var t = work != null ? work.Find(name) : null;
        if (t == null) return;
        t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0); t.localScale = Vector3.one * scale;
    }
}
#endif
