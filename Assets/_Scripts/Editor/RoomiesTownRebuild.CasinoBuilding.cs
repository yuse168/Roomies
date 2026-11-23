#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using Art = RoomiesCasinoArt;

/// <summary>
/// カジノの外観の飾りと、3 つの階の中（2026/11/23。参考画像の「木の梁・暖色の吊り照明・ピンクのネオンの溜まり場」）。Rebuild/3 Casino から呼ぶ。
///
/// 1 階 ロビー：入口の敷物、右の壁ぎわにスロットの列、奥の左にブラックジャック、左の壁にバー、奥の右に両替所（店の人）、その左にエレベーター
/// 2 階 ハイローラー：真ん中にルーレット、奥の左にブラックジャック、右の壁ぎわにスロット、奥の右に店の人のカウンター、通り側の窓の下にソファ
/// 3 階 VIP：夜景の大きな窓とソファ、ルーレット、ブラックジャック
///
/// 明かりの決まり（art_rules.md）：暖色の吊り照明が主役（1）。ネオンは壁の近くだけ（0.15〜0.3）、窓の青は弱い補い（0.2〜0.35）。
/// 位置を直すときは、ここの数字を直して Rebuild/3 Casino を実行し直す（シーンで直した位置は、作り直すと消える）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    static readonly Color KWarm = new Color(1f, .76f, .48f), KPink = new Color(.97f, .40f, .73f), KNight = new Color(.30f, .46f, .72f);

    // ================================================================ 置く道具
    static GameObject Prop(Transform g, string name, Mesh mesh, float x, float y, float z, float yaw = 0)
        => Art.Place(g, name, mesh, new Vector3(x, y, z), yaw, true);

    /// <summary>形を置いて、町の素材（レンガ・塗り壁など。三方向から貼るので、どんな形にも模様が合う）に替える。</summary>
    static GameObject PropMat(Transform g, string name, Mesh mesh, float x, float y, float z, Material material)
    {
        var go = Prop(g, name, mesh, x, y, z);
        go.GetComponent<MeshRenderer>().sharedMaterial = material;
        return go;
    }

    /// <summary>吊り照明（傘と電球）と、その明かり。</summary>
    static void Pendant(Transform g, float x, float ceilY, float z, float drop, float intensity = 6.5f, float range = 9.5f)
    {
        Prop(g, "Pendant lamp", Art.PendantLamp(drop), x, ceilY, z);
        if (intensity > 0f) 
        {
            // 明かりは傘の中（内側の面より上）に置く。面のすぐ下に置くと、内側の面が近すぎる明かりで真っ白に飛ぶ
            PointLight(g, "Casino light - pendant", new Vector3(x, ceilY - drop + .12f, z), intensity, range, KWarm);
        }
    }

    /// <summary>ソファ（通れない）。yaw は座る人の向く方（0 = +Z、90 = +X、180 = -Z）。</summary>
    static void SofaAt(Transform g, float width, string cloth, string clothDark, float x, float y, float z, float yaw)
    {
        Prop(g, "Sofa", Art.Sofa(width, cloth, clothDark), x, y, z, yaw);
        bool alongZ = Mathf.Abs(Mathf.DeltaAngle(yaw, 90)) < 1f || Mathf.Abs(Mathf.DeltaAngle(yaw, 270)) < 1f;
        float hx = (alongZ ? .9f : width) / 2, hz = (alongZ ? width : .9f) / 2;
        Solid(g, "Sofa collider", new Vector3(x - hx, y, z - hz), new Vector3(x + hx, y + .7f, z + hz));
    }

    /// <summary>低い机・丸い机・植木（通れない）。</summary>
    static void LowTableAt(Transform g, float width, float depth, float x, float y, float z)
    {
        Prop(g, "Low table", Art.LowTable(width, depth), x, y, z);
        Solid(g, "Low table collider", new Vector3(x - width / 2, y, z - depth / 2), new Vector3(x + width / 2, y + .43f, z + depth / 2));
    }

    static void RoundTableAt(Transform g, float x, float y, float z)
    {
        Prop(g, "Round table", Art.RoundTable(.42f, 1.05f), x, y, z);
        Solid(g, "Round table collider", new Vector3(x - .3f, y, z - .3f), new Vector3(x + .3f, y + 1.05f, z + .3f));
    }

    static void PlanterAt(Transform g, float x, float y, float z, float yaw = 0)
    {
        Prop(g, "Planter", Art.Planter(), x, y, z, yaw);
        Solid(g, "Planter collider", new Vector3(x - .24f, y, z - .24f), new Vector3(x + .24f, y + .5f, z + .24f));
    }

    /// <summary>その階の真ん中の、やわらかい補いの明かり（吊り照明の光だまりの間が暗くなりすぎないように）。</summary>
    static void FillLight(Transform g, float fy, float cy)
        => PointLight(g, "Casino light - fill", new Vector3(7.8f, Mathf.Min(cy - .6f, fy + 3.0f), -13.4f), 3.0f, 17f, new Color(1f, .86f, .70f));

    /// <summary>ロープの柵：点を順につなぐ（柱とロープ）。</summary>
    static void RopeLine(Transform g, float y, params Vector2[] points)
    {
        for (int i = 0; i < points.Length; i++)
        {
            Prop(g, "Stanchion", Art.Stanchion(), points[i].x, y, points[i].y);
            if (i == 0) continue;
            Vector2 a = points[i - 1], d = points[i] - a;
            Prop(g, "Rope", Art.Rope(d.magnitude), a.x, y + .9f, a.y, -Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
        }
    }

    static void MoveSign(Transform work, string name, Vector3 pos, float yaw, float scale)
    {
        var t = work != null ? work.Find(name) : null;
        if (t == null) return;
        t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0); t.localScale = Vector3.one * scale;
    }

    /// <summary>
    /// 部屋の殻の内側：床・天井・梁・柱・壁の仕上げ（腰の板・見切り・塗り壁）。
    /// frontOpenings は通り側の壁の開口（x0, x1, 下, 上）。奥の壁は、エレベーターの所を空ける。
    /// </summary>
    static void CasinoRoomShell(Transform g, int floor, Material floorMat, (float x0, float x1, float y0, float y1)[] frontOpenings)
    {
        float fy = KFloorY(floor), cy = KCeilY(floor);
        var timber = Mat("casino_timber"); var wain = Swatch("wood"); var plaster = Mat("casino_plaster");

        Box(g, "Floor finish", KIX0, KIX1, fy, fy + .012f, KIZ0, KIZ1, floorMat);
        Box(g, "Ceiling boards", KIX0, KIX1, cy - .03f, cy, KIZ0, KIZ1, Mat("casino_timber"));
        // 梁：奥行きの向きに 3 本（太い）、幅の向きに 2 本
        foreach (float x in new[] { 3.4f, 7.8f, 12.2f }) Box(g, "Ceiling beam", x - .15f, x + .15f, cy - .36f, cy - .03f, KIZ0, KIZ1, timber);
        foreach (float z in new[] { -15.7f, -12.1f }) Box(g, "Ceiling beam", KIX0, KIX1, cy - .27f, cy - .03f, z - .11f, z + .11f, timber);

        // 壁の仕上げ（面に沿って a0〜a1。開口は下と上に分けて貼る）
        void Finish(Vector3 n, float plane, float a0, float a1, float oy0 = float.MaxValue, float oy1 = float.MinValue)
        {
            void Piece(string name, float y0, float y1, float d, Material m)
            {
                if (oy0 == float.MaxValue) { FaceBox(g, name, n, plane, a0, a1, y0, y1, 0, d, m); return; }
                if (oy0 > y0) FaceBox(g, name, n, plane, a0, a1, y0, Mathf.Min(y1, oy0), 0, d, m);
                if (oy1 < y1) FaceBox(g, name, n, plane, a0, a1, Mathf.Max(y0, oy1), y1, 0, d, m);
            }
            Piece("Wainscot", fy, fy + 1.0f, .04f, wain);
            Piece("Wainscot rail", fy + 1.0f, fy + 1.08f, .07f, timber);
            Piece("Wall plaster", fy + 1.08f, cy - .03f, .02f, plaster);
        }
        Finish(Vector3.right, KIX0, KIZ0, KIZ1);
        Finish(Vector3.left, KIX1, KIZ0, KIZ1);
        Finish(Vector3.forward, KIZ0, KIX0, KEvX0);
        Finish(Vector3.forward, KIZ0, KEvX1, KIX1);
        float a = KIX0;
        foreach (var o in frontOpenings)
        {
            if (o.x0 > a) Finish(Vector3.back, KIZ1, a, o.x0);
            Finish(Vector3.back, KIZ1, o.x0, o.x1, o.y0, o.y1);
            a = o.x1;
        }
        if (a < KIX1) Finish(Vector3.back, KIZ1, a, KIX1);

        // 柱（壁から少し出る木の柱）：梁の下。通り側は開口にかからない所だけ
        foreach (float x in new[] { 3.4f, 7.8f })
            Box(g, "Wall post", x - .13f, x + .13f, fy, cy - .03f, KIZ0, KIZ0 + .12f, timber);
        foreach (float x in new[] { 3.4f, 7.8f, 12.2f })
        {
            bool blocked = false;
            foreach (var o in frontOpenings) if (x + .13f > o.x0 - .1f && x - .13f < o.x1 + .1f) blocked = true;
            if (!blocked) Box(g, "Wall post", x - .13f, x + .13f, fy, cy - .03f, KIZ1 - .12f, KIZ1, timber);
        }
        foreach (float z in new[] { -15.7f, -12.1f })
        {
            Box(g, "Wall post", KIX0, KIX0 + .12f, fy, cy - .03f, z - .13f, z + .13f, timber);
            Box(g, "Wall post", KIX1 - .12f, KIX1, fy, cy - .03f, z - .13f, z + .13f, timber);
        }
    }

    static (float, float, float, float)[] WindowOpenings(int floor)
    {
        var list = new List<(float, float, float, float)>();
        foreach (var (x0, x1) in KWindows(floor)) list.Add((x0, x1, KWinY0(floor) - .12f, KWinY1(floor) + .1f));
        return list.ToArray();
    }

    /// <summary>スロットの前の腰かけと、壁のネオンの線・看板の文字。</summary>
    static void SlotCorner(Transform g, float fy, float[] zs, string sign)
    {
        float z0 = zs[zs.Length - 1] - 1.0f, z1 = zs[0] + 1.0f;
        Box(g, "Slot rug", 13.3f, KIX1 - .02f, fy + .012f, fy + .028f, z0, z1, Mat("casino_carpet"));
        foreach (float z in zs) Prop(g, "Slot stool", Art.Stool("maroon"), 15.02f, fy, z);
        Box(g, "Slot neon line", KIX1 - .05f, KIX1 - .02f, fy + 2.62f, fy + 2.68f, z0 + .1f, z1 - .1f, Swatch("pink", true));
        Art.NeonText(g, "Sign - " + sign, sign, new Vector3(KIX1 - .06f, fy + 3.02f, (z0 + z1) / 2), 270, .42f);
        PointLight(g, "Casino light - slot neon", new Vector3(14.9f, fy + 2.4f, (z0 + z1) / 2), 1.5f, 6f, KPink);
    }

    /// <summary>ブラックジャック台のまわり：敷物・椅子 3 つ・吊り照明。台そのものはプレハブ（シーンに置いてある）。</summary>
    static void BlackjackCorner(Transform g, float fy, float cy, float x, float z)
    {
        Box(g, "Table rug", x - 2.1f, x + 2.1f, fy + .012f, fy + .028f, z - 1.5f, z + 2.3f, Mat("casino_carpet"));
        // 椅子は丸い側の左右に 1 つずつ（真ん中は、立って遊ぶ人のために空ける）
        foreach (float s in new[] { -1f, 1f })
            Prop(g, "Table chair", Art.Chair("maroon", "maroon_dark"), x + s * 1.5f, fy, z + .95f, 180 + s * 52);
        Pendant(g, x, cy - .03f, z + .1f, cy - fy - 2.45f);
    }

    /// <summary>店の人のカウンター（奥の右）：カウンター・踏み台・仕切り・上の看板・吊り照明。当たり判定はカウンターの高さまで。</summary>
    static void ClerkCounter(Transform g, float fy, float cy, string sign)
    {
        float x0 = 12.7f, z1 = -16.9f;
        var wain = Swatch("wood"); var timber = Mat("casino_timber");
        Box(g, "Clerk counter", x0, KIX1, fy, fy + 1.05f, z1 - .5f, z1, wain);
        Box(g, "Clerk counter top", x0 - .06f, KIX1, fy + 1.05f, fy + 1.12f, z1 - .56f, z1 + .08f, timber);
        Box(g, "Clerk counter kick", x0, KIX1, fy, fy + .12f, z1, z1 + .02f, timber);
        Solid(g, "Clerk counter collider", new Vector3(x0 - .06f, fy, z1 - .56f), new Vector3(KIX1, fy + 1.12f, z1 + .08f));
        Box(g, "Clerk step", x0, KIX1, fy, fy + KClerkStep, KIZ0, z1 - .5f, wain);
        // エレベーターとの間の仕切り（柱 2 本と上の板）と、上の看板
        Box(g, "Clerk post", x0 - .12f, x0 + .04f, fy, cy - .03f, z1 - .12f, z1 + .04f, timber);
        Box(g, "Clerk sign board", x0 - .12f, KIX1, fy + 2.45f, fy + 3.05f, z1 - .08f, z1, timber);
        Art.NeonText(g, "Sign - " + sign, sign, new Vector3((x0 + KIX1) / 2, fy + 2.75f, z1 + .01f), 0, .34f, "FFD08A", 2.4f);
        Solid(g, "Clerk partition collider", new Vector3(x0 - .12f, fy, KIZ0), new Vector3(x0 - .04f, fy + 1.12f, z1 - .56f));
        Box(g, "Clerk partition", x0 - .12f, x0 - .04f, fy, fy + 1.12f, KIZ0, z1 - .56f, wain);
        Prop(g, "Counter chips", Art.ChipPile(), 15.7f, fy + 1.12f, z1 - .2f);
        Prop(g, "Counter lantern", Art.Lantern(), 13.2f, fy + 1.3f, z1 - .2f, 0);
        PointLight(g, "Casino light - clerk", new Vector3(14.6f, fy + 2.3f, z1 - .5f), 3.4f, 5f, KWarm);
    }

    // ================================================================ 外観：入口・ひさし・ネオン・垂れ幕・屋上の看板
    static void CasinoFacade(Transform g, Transform work)
    {
        var timber = Mat("casino_timber"); var stone = Swatch("stone");
        float face = KZ1;
        // 足元の石（開口は空ける）と、正面の柱
        foreach (var (x0, x1) in new[] { (KX0 - .04f, KDoorX0), (KDoorX1, KEntX0), (KEntX1, KX1 + .04f) })
            Box(g, "Casino plinth", x0, x1, 0, .45f, face, face + .05f, stone);
        // 正面の付け柱（白い柱に金の柱頭）。入口の両わきと、その外
        foreach (float x in new[] { 3.6f, 5.3f, 10.3f, 12.0f })
            Prop(g, "Casino pilaster", Art.Pilaster(KRoof - .3f), x, 0, face);

        // 正面の 2 つの角：丸い塔（胴はレンガ、帯と丸屋根は金）。建物の幅からは出さず、前へ 0.45m だけ張り出す
        float turretHeight = KRoof + .5f;
        foreach (float x in new[] { KX0 + Art.TurretRadius - .12f, KX1 - Art.TurretRadius + .12f })
        {
            float tz = face - Art.TurretRadius + .45f;
            PropMat(g, "Casino turret", Art.TurretBody(turretHeight), x, 0, tz, Mat("casino_brick"));
            Prop(g, "Casino turret trim", Art.TurretTrim(turretHeight, new[] { KF2 - .34f, KF3 - .34f }), x, 0, tz);
            Solid(g, "Casino turret collider", new Vector3(x - .8f, 0, face), new Vector3(x + .8f, KRoof, face + .45f));
        }

        // 入口：木の額縁、開け放した扉（木枠とガラス。当たり判定なし）
        Box(g, "Entrance portal", KEntX0 - .28f, KEntX0, 0, KEntH + .28f, face - .02f, face + .16f, timber);
        Box(g, "Entrance portal", KEntX1, KEntX1 + .28f, 0, KEntH + .28f, face - .02f, face + .16f, timber);
        Box(g, "Entrance portal", KEntX0 - .28f, KEntX1 + .28f, KEntH, KEntH + .28f, face - .02f, face + .16f, timber);
        var glass = Art.GlassMaterial();
        foreach (var (hx, dir) in new[] { (KEntX0, 1f), (KEntX1, -1f) })
        {
            var leaf = new GameObject("Entrance door leaf") { isStatic = true }.transform;
            leaf.SetParent(g, false); leaf.localPosition = new Vector3(hx, KFloor, face - .2f); leaf.localRotation = Quaternion.Euler(0, dir > 0 ? -100 : 100, 0);
            Box(leaf, "Door glass", dir * .1f, dir * 1.3f, .5f, 2.3f, -.015f, .015f, glass);
            Box(leaf, "Door rail", 0, dir * 1.4f, 0, .5f, -.04f, .04f, timber);
            Box(leaf, "Door rail", 0, dir * 1.4f, 2.3f, 2.44f, -.04f, .04f, timber);
            Box(leaf, "Door stile", 0, dir * .1f, 0, 2.44f, -.04f, .04f, timber);
            Box(leaf, "Door stile", dir * 1.3f, dir * 1.4f, 0, 2.44f, -.04f, .04f, timber);
        }
        // 通用口（シーンの DoorPivot がはまる所）の枠
        Box(g, "Side door frame", KDoorX0 - .12f, KDoorX0, 0, KDoorH + .12f, face, face + .1f, timber);
        Box(g, "Side door frame", KDoorX1, KDoorX1 + .12f, 0, KDoorH + .12f, face, face + .1f, timber);
        Box(g, "Side door frame", KDoorX0 - .12f, KDoorX1 + .12f, KDoorH, KDoorH + .12f, face, face + .1f, timber);
        Prop(g, "Side door lantern", Art.Lantern(), 2.25f, 2.1f, face + .01f);

        // 入口：半円に張り出すひさし（ふちに電球、下の面が光る）と、その上に立つ金のアーチ（中に AFTER HOURS の看板）
        float marqueeY = 3.35f;
        Prop(g, "Casino marquee", Art.Marquee(), 7.8f, marqueeY, face);
        float archY = marqueeY + Art.MarqueeThickness + .08f;
        Prop(g, "Casino entrance arch", Art.EntranceArch(), 7.8f, archY, face);
        MoveSign(work, "Sign - AFTER HOURS", new Vector3(7.8f, archY + .62f, face + .14f), 180, .62f);
        Prop(g, "Canopy lantern", Art.Lantern(), KEntX0 - .5f, 2.2f, face + .16f);
        Prop(g, "Canopy lantern", Art.Lantern(), KEntX1 + .5f, 2.2f, face + .16f);

        // 2 階の窓の日よけ（えんじと白のしま）
        foreach (var (x0, x1) in KWin2F)
            Prop(g, "Casino awning", Art.Awning(x1 - x0 + .3f), (x0 + x1) / 2, KWin2Y1 + .12f, face + .08f);

        // 軒の縁取り：屋上のふちと、2 階・3 階の境に、電球の列
        foreach (float y in new[] { KRoof + .02f, KF3 - .17f, KF2 - .17f })
        {
            float x0 = KX0 + 2f * Art.TurretRadius - .1f, x1 = KX1 - 2f * Art.TurretRadius + .1f;
            if (y < KF3 - 1f)
            {
                // 2 階の境は、入口のアーチの左右に分ける
                Prop(g, "Casino bulb row", Art.BulbRow(7.8f - Art.ArchOuter - .2f - x0, 12), x0, y, face + .1f);
                Prop(g, "Casino bulb row", Art.BulbRow(x1 - (7.8f + Art.ArchOuter + .2f), 12), 7.8f + Art.ArchOuter + .2f, y, face + .1f);
            }
            else Prop(g, "Casino bulb row", Art.BulbRow(x1 - x0, 36), x0, y, face + .1f);
        }

        // 2 階の窓の間：王冠のネオン。1 階の壁：紫の垂れ幕 4 本
        Art.NeonSign(g, "Neon sign - crown", new Vector3(7.8f, (KWin2Y0 + KWin2Y1) / 2 + .05f, face + .1f), 0, 1.5f);
        foreach (float x in new[] { 2.75f, 4.75f, 11.0f, 13.6f })
            Prop(g, "Banner", Art.Banner(.56f), x, 3.75f, face + .08f);

        // 屋上の段々の冠：角の丸い段を 2 つ積み、その上に看板の板。前のふちは電球。てっぺんに王冠のネオンと星
        float sy = KRoof + .12f;
        float t1w = 12.4f, t1d = 2.6f, t1h = .9f, t2w = 8.8f, t2d = 2.2f, t2h = 1.1f, capH = .14f;
        float t1z = face - t1d / 2 + .1f, t2z = face - t2d / 2 + .12f;
        PropMat(g, "Casino crown tier", Art.Tier(t1w, t1d, t1h, .6f), 7.8f, sy, t1z, Mat("casino_brick"));
        Prop(g, "Casino crown tier cap", Art.TierCap(t1w, t1d, .6f, 30), 7.8f, sy + t1h, t1z);
        float y2 = sy + t1h + capH;
        PropMat(g, "Casino crown tier", Art.Tier(t2w, t2d, t2h, .5f), 7.8f, y2, t2z, Mat("casino_plaster"));
        Prop(g, "Casino crown tier cap", Art.TierCap(t2w, t2d, .5f, 22), 7.8f, y2 + t2h, t2z);
        float y3 = y2 + t2h + capH, signH = 2.0f, signW = 6.8f;
        PropMat(g, "Rooftop sign board", Art.Tier(signW, .5f, signH, .22f), 7.8f, y3, face - .2f, Mat("casino_timber"));
        Prop(g, "Rooftop sign cap", Art.TierCap(signW, .5f, .22f, 18), 7.8f, y3 + signH, face - .2f);
        Art.NeonText(g, "Sign - CASINO", "CASINO", new Vector3(7.8f, y3 + signH / 2 + .02f, face + .07f), 0, 1.25f);
        Art.NeonSign(g, "Neon sign - rooftop crown", new Vector3(7.8f, y3 + signH + capH + .95f, face - .2f), 0, 2.0f);
        foreach (float x in new[] { 7.8f - t2w / 2 + .5f, 7.8f + t2w / 2 - .5f })
            Prop(g, "Casino star", Art.StarFinial(), x, y2 + t2h + capH, face - .5f);
        foreach (float x in new[] { 7.8f - t1w / 2 + .6f, 7.8f + t1w / 2 - .6f })
            Prop(g, "Casino star", Art.StarFinial(), x, sy + t1h + capH, face - .5f);

        // 入口の前：紫の敷物、ロープの柵、植木
        Box(g, "Entrance carpet", KEntX0 + .1f, KEntX1 - .1f, 0, .03f, face, face + 1.6f, Mat("casino_carpet"));
        foreach (float x in new[] { KEntX0 - .75f, KEntX1 + .75f })
            RopeLine(g, 0, new Vector2(x, face + .45f), new Vector2(x, face + 1.35f));
        PlanterAt(g, 4.2f, 0, face + .55f);
        PlanterAt(g, 11.4f, 0, face + .55f, 40);

        // 夜に正面を照らす光（朝は消える：名前の頭が Shop light）
        SpotLight(g, "Shop light - CASINO", new Vector3(7.8f, 6.4f, -3.4f), new Vector3(7.8f, 3.0f, KZ1), 5.5f, 13f, 110f, new Color(1f, .78f, .55f));
        PointLight(g, "Shop light - CASINO canopy", new Vector3(7.8f, 2.8f, face + .9f), 3.2f, 7f, KWarm);
        PointLight(g, "Shop light - CASINO neon", new Vector3(7.8f, 6.7f, face + 1.2f), 2.2f, 7f, KPink);
    }

    // ================================================================ 1 階 ロビー
    static void CasinoFloor1(Transform g, Transform work)
    {
        float fy = KFloor, cy = KCeil;
        CasinoRoomShell(g, 1, Mat("casino_brick_floor"), new[] { (KDoorX0 - .12f, KDoorX1 + .12f, 0f, KDoorH + .12f), (KEntX0 - .28f, KEntX1 + .28f, 0f, KEntH + .28f) });
        var carpet = Mat("casino_carpet"); var wain = Swatch("wood"); var timber = Mat("casino_timber");
        FillLight(g, fy, cy);

        // 入口から奥への敷物、入口の内側の植木
        Box(g, "Entrance runner", 6.5f, 9.1f, fy + .012f, fy + .028f, -14.0f, KIZ1 + .02f, carpet);
        PlanterAt(g, 5.55f, fy, -8.75f);
        PlanterAt(g, 10.05f, fy, -8.75f, 60);

        // ---- スロットの列（右の壁ぎわ。台はシーンに置いてある）
        SlotCorner(g, fy, new[] { -10.9f, -12.95f, -15.0f }, "SLOTS");
        Pendant(g, 14.2f, cy - .03f, -12.95f, 1.5f, 4.2f, 7f);

        // ---- ブラックジャック（奥の左）と、奥の壁の王冠のネオン・垂れ幕
        BlackjackCorner(g, fy, cy, 5.76f, -16.34f);
        RopeLine(g, fy, new Vector2(3.1f, -15.2f), new Vector2(3.1f, -17.6f));
        RopeLine(g, fy, new Vector2(8.45f, -15.2f), new Vector2(8.45f, -17.6f));
        Art.NeonSign(g, "Neon sign - crown", new Vector3(5.76f, fy + 2.75f, KIZ0 + .08f), 0, 2.0f);
        PointLight(g, "Casino light - neon", new Vector3(5.76f, fy + 2.6f, KIZ0 + 1.0f), 1.6f, 6f, KPink);
        Prop(g, "Banner", Art.Banner(.5f), 4.15f, fy + 3.7f, KIZ0 + .16f);
        Prop(g, "Banner", Art.Banner(.5f), 7.1f, fy + 3.7f, KIZ0 + .16f);

        // ---- バー（左の壁）：奥の棚と瓶、カウンター（当たり判定）、腰かけ
        float bz0 = -18.9f, bz1 = -13.0f, cx0 = .45f, cx1 = 1.05f;
        Box(g, "Back bar", KIX0, KIX0 + .42f, fy, fy + 1.0f, bz0, bz1, wain);
        Box(g, "Back bar board", KIX0, KIX0 + .06f, fy + 1.0f, fy + 2.5f, bz0, bz1, timber);
        string[] bottles = { "gold_dark", "felt", "red_dark", "blue", "pink_dark", "teal" };
        int bi = 0;
        foreach (float y in new[] { 1.0f, 1.5f, 2.0f })
        {
            if (y > 1.0f) Box(g, "Back bar shelf", KIX0 + .04f, KIX0 + .34f, fy + y - .04f, fy + y, bz0 + .2f, bz1 - .2f, timber);
            for (float z = bz0 + .5f; z < bz1 - .4f; z += .46f, bi++)
                Prop(g, "Bottle", Art.Bottle(bottles[(bi * 5 + (int)(y * 2)) % bottles.Length]), KIX0 + .2f, fy + y, z, bi * 47);
        }
        Box(g, "Bar counter", cx0, cx1, fy, fy + 1.0f, bz0, bz1, wain);
        Box(g, "Bar top", cx0 - .08f, cx1 + .12f, fy + 1.0f, fy + 1.08f, bz0 - .05f, bz1 + .05f, timber);
        Box(g, "Bar foot rail", cx1 + .1f, cx1 + .15f, fy + .2f, fy + .25f, bz0, bz1, timber);
        Solid(g, "Bar collider", new Vector3(cx0 - .08f, fy, bz0 - .05f), new Vector3(cx1 + .12f, fy + 1.08f, bz1 + .05f));
        Solid(g, "Back bar collider", new Vector3(KIX0, fy, bz0), new Vector3(KIX0 + .42f, fy + 1.0f, bz1));
        for (float z = bz0 + .6f; z < bz1 - .3f; z += 1.15f) Prop(g, "Bar stool", Art.Stool("maroon"), cx1 + .62f, fy, z);
        Prop(g, "Bar glass", Art.Glass(), .75f, fy + 1.08f, -17.5f); Prop(g, "Bar glass", Art.Glass(), .8f, fy + 1.08f, -15.3f);
        Prop(g, "Bar popcorn", Art.Popcorn(), .7f, fy + 1.08f, -14.0f);
        Pendant(g, .85f, cy - .03f, -17.4f, 1.9f, 0f);
        Pendant(g, .85f, cy - .03f, -14.6f, 1.9f, 4.6f, 7f);
        MoveSign(work, "Sign - PLAY / UNWIND / REPEAT", new Vector3(KIX0 + .08f, fy + 3.3f, -15.95f), 270, 1.1f);

        // ---- 両替所（店の人）
        ClerkCounter(g, fy, cy, "CASHIER");

        // ---- ひと休みの場所（左の壁の手前）：ソファ・低い机・植木・額
        SofaAt(g, 2.2f, "maroon", "maroon_dark", KIX0 + .5f, fy, -11.15f, 90);
        LowTableAt(g, .6f, 1.2f, 1.05f, fy, -11.15f);
        Prop(g, "Table popcorn", Art.Popcorn(), 1.0f, fy + .43f, -10.8f);
        Prop(g, "Table chips", Art.ChipPile(), 1.1f, fy + .43f, -11.5f);
        PlanterAt(g, KIX0 + .45f, fy, -12.5f, 20);
        Prop(g, "Frame", Art.Frame(.5f, .4f, "purple"), KIX0 + .03f, fy + 1.95f, -10.6f, 90);
        Prop(g, "Frame", Art.Frame(.36f, .5f, "teal"), KIX0 + .03f, fy + 2.0f, -11.5f, 90);
        Pendant(g, 1.3f, cy - .03f, -11.15f, 1.7f, 3.8f, 6.5f);

        // ---- 真ん中：立ち飲みの丸い机 2 つ
        foreach (var (x, z) in new[] { (10.6f, -11.3f), (11.6f, -14.2f) })
        {
            RoundTableAt(g, x, fy, z);
            Prop(g, "Table glass", Art.Glass(), x + .1f, fy + 1.05f, z - .08f);
            Prop(g, "Round table stool", Art.Stool("purple"), x - .7f, fy, z + .25f);
            Prop(g, "Round table stool", Art.Stool("purple"), x + .55f, fy, z - .55f);
        }

        // ---- 吊り照明（入口・真ん中）と、通り側の壁の内側の電球の列
        Pendant(g, 7.8f, cy - .03f, -9.9f, 1.6f);
        Pendant(g, 7.8f, cy - .03f, -13.3f, 1.6f);
        Prop(g, "Bulb string", Art.BulbString(4.4f, 9), 9.6f, cy - .5f, KIZ1 - .2f);
        Prop(g, "Bulb string", Art.BulbString(4.2f, 9), 2.0f, cy - .5f, KIZ1 - .2f);
        Prop(g, "Bulb string", Art.BulbString(4.6f, 9), KIX0 + .3f, cy - .6f, KIZ0 + .25f);
    }

    // ================================================================ 2 階 ハイローラー
    static void CasinoFloor2(Transform g)
    {
        float fy = KF2, cy = KC2;
        CasinoRoomShell(g, 2, Mat("casino_plank"), WindowOpenings(2));
        var carpet = Mat("casino_carpet"); var timber = Mat("casino_timber"); var wain = Swatch("wood");
        FillLight(g, fy, cy);

        // ---- ルーレット（真ん中。台はシーンに置いてある）：大きな敷物と吊り照明 2 つ
        Box(g, "Roulette rug", 4.9f, 10.3f, fy + .012f, fy + .028f, -14.3f, -10.3f, carpet);
        Pendant(g, 6.5f, cy - .03f, -12.0f, .85f, 5.0f, 8f);
        Pendant(g, 8.7f, cy - .03f, -12.0f, .85f, 0f);

        // ---- ブラックジャック（奥の左）と、奥の壁のネオンの文字
        BlackjackCorner(g, fy, cy, 4.2f, -16.6f);
        Art.NeonText(g, "Sign - HIGH ROLLERS", "HIGH ROLLERS", new Vector3(4.6f, fy + 2.55f, KIZ0 + .05f), 0, .5f);
        PointLight(g, "Casino light - neon", new Vector3(4.6f, fy + 2.3f, KIZ0 + .9f), 1.5f, 6f, KPink);
        Prop(g, "Banner", Art.Banner(.46f), 1.2f, fy + 3.0f, KIZ0 + .16f);
        Prop(g, "Banner", Art.Banner(.46f), 8.4f, fy + 3.0f, KIZ0 + .16f);

        // ---- スロット 2 台（右の壁ぎわ）
        SlotCorner(g, fy, new[] { -10.9f, -12.95f }, "SLOTS");
        Pendant(g, 14.2f, cy - .03f, -11.9f, .85f, 3.8f, 6.5f);

        // ---- ハイ＆ロー（右寄り。台はシーンに置いてある）：敷物と吊り照明
        Box(g, "HiLo rug", 10.9f, 13.5f, fy + .012f, fy + .028f, -11.9f, -9.2f, carpet);
        Pendant(g, 12.2f, cy - .03f, -10.3f, .85f, 4.2f, 7f);

        // ---- 店の人のカウンター（3 階の鍵）
        ClerkCounter(g, fy, cy, "VIP KEY");

        // ---- 通り側の窓の下：ソファ 2 つと低い机、植木
        SofaAt(g, 2.4f, "purple", "purple_dark", 2.6f, fy, KIZ1 - .5f, 180);
        LowTableAt(g, 1.2f, .6f, 2.6f, fy, KIZ1 - 1.5f);
        Prop(g, "Table chips", Art.ChipPile(), 2.3f, fy + .43f, KIZ1 - 1.5f);
        Prop(g, "Table glass", Art.Glass(), 2.95f, fy + .43f, KIZ1 - 1.45f);
        SofaAt(g, 2.0f, "maroon", "maroon_dark", 11.6f, fy, KIZ1 - .5f, 180);
        PlanterAt(g, KIX0 + .45f, fy, KIZ1 - .45f);
        PlanterAt(g, 7.8f, fy, KIZ1 - .45f, 30);
        Pendant(g, 2.6f, cy - .03f, KIZ1 - 1.3f, .85f, 3.6f, 6f);
        PointLight(g, "Casino light - window", new Vector3(9.8f, fy + 1.9f, KIZ1 - .7f), 1.1f, 6f, KNight);

        // ---- 左の壁：飲み物の棚と額
        Box(g, "Drink shelf", KIX0, KIX0 + .4f, fy, fy + 1.0f, -15.2f, -12.9f, wain);
        Box(g, "Drink shelf top", KIX0, KIX0 + .46f, fy + 1.0f, fy + 1.06f, -15.25f, -12.85f, timber);
        Solid(g, "Drink shelf collider", new Vector3(KIX0, fy, -15.25f), new Vector3(KIX0 + .46f, fy + 1.06f, -12.85f));
        string[] bottles = { "gold_dark", "felt", "red_dark", "blue", "pink_dark" };
        for (int i = 0; i < 5; i++) Prop(g, "Bottle", Art.Bottle(bottles[i]), KIX0 + .2f, fy + 1.06f, -14.9f + i * .42f, i * 61);
        Prop(g, "Frame", Art.Frame(.7f, .5f, "pink_dark"), KIX0 + .03f, fy + 2.05f, -14.05f, 90);
        Prop(g, "Bulb string", Art.BulbString(5.2f, 11), KIX0 + .15f, cy - .5f, -15.2f, -90);
    }

    // ================================================================ 3 階 VIP
    static void CasinoFloor3(Transform g)
    {
        float fy = KF3, cy = KC3;
        CasinoRoomShell(g, 3, Mat("casino_plank"), WindowOpenings(3));
        ClerkCounter(g, fy, cy, "HOST");      // 3 階の店の人（鍵は売らない。話すだけ）
        var carpet = Mat("casino_carpet"); var purple = Swatch("wall_purple");
        FillLight(g, fy, cy);

        // 奥と左右の壁の上のほうは紫の布張り（VIP の階だけ）
        FaceBox(g, "VIP wall cloth", Vector3.right, KIX0, KIZ0, KIZ1, fy + 1.08f, cy - .03f, .02f, .035f, purple);
        FaceBox(g, "VIP wall cloth", Vector3.left, KIX1, KIZ0, KIZ1, fy + 1.08f, cy - .03f, .02f, .035f, purple);
        FaceBox(g, "VIP wall cloth", Vector3.forward, KIZ0, KIX0, KEvX0, fy + 1.08f, cy - .03f, .02f, .035f, purple);
        FaceBox(g, "VIP wall cloth", Vector3.forward, KIZ0, KEvX1, KIX1, fy + 1.08f, cy - .03f, .02f, .035f, purple);

        // ---- 真ん中の大きな敷物、ルーレット（台はシーン）の吊り照明
        Box(g, "VIP rug", 3.2f, 12.8f, fy + .012f, fy + .028f, -14.6f, -9.9f, carpet);
        Pendant(g, 5.3f, cy - .03f, -12.2f, 1.2f, 5.0f, 8f);
        Pendant(g, 7.5f, cy - .03f, -12.2f, 1.2f, 0f);

        // ---- ブラックジャック（奥の左）と、奥の壁の王冠のネオン
        BlackjackCorner(g, fy, cy, 4.2f, -16.8f);
        Art.NeonSign(g, "Neon sign - crown", new Vector3(4.2f, fy + 2.75f, KIZ0 + .1f), 0, 1.8f);
        PointLight(g, "Casino light - neon", new Vector3(4.2f, fy + 2.5f, KIZ0 + 1.0f), 1.6f, 6f, KPink);
        Prop(g, "Banner", Art.Banner(.5f), 1.4f, fy + 3.4f, KIZ0 + .16f);
        Prop(g, "Banner", Art.Banner(.5f), 7.9f, fy + 3.4f, KIZ0 + .16f);
        Art.NeonText(g, "Sign - VIP", "VIP", new Vector3(KIX1 - .06f, fy + 2.7f, -16.0f), 270, .7f);
        PointLight(g, "Casino light - neon", new Vector3(KIX1 - 1.0f, fy + 2.4f, -16.0f), 1.3f, 5.5f, KPink);

        // ---- 夜景の窓の前：ソファと低い机、植木。窓からの青い光
        SofaAt(g, 2.6f, "purple", "purple_dark", 2.6f, fy, KIZ1 - 1.6f, 0);
        LowTableAt(g, 1.3f, .6f, 2.6f, fy, KIZ1 - .7f);
        Prop(g, "Table glass", Art.Glass(), 2.3f, fy + .43f, KIZ1 - .7f);
        Prop(g, "Table popcorn", Art.Popcorn(), 2.95f, fy + .43f, KIZ1 - .72f);
        SofaAt(g, 2.6f, "maroon", "maroon_dark", 13.0f, fy, KIZ1 - 1.6f, 0);
        LowTableAt(g, 1.3f, .6f, 13.0f, fy, KIZ1 - .7f);
        Prop(g, "Table chips", Art.ChipPile(), 13.1f, fy + .43f, KIZ1 - .7f);
        PlanterAt(g, 5.2f, fy, KIZ1 - .45f);
        PlanterAt(g, 10.4f, fy, KIZ1 - .45f, 50);
        PlanterAt(g, KIX0 + .45f, fy, -15.0f, 15);
        PointLight(g, "Casino light - window", new Vector3(4.0f, fy + 2.0f, KIZ1 - .8f), 1.3f, 7f, KNight);
        PointLight(g, "Casino light - window", new Vector3(11.6f, fy + 2.0f, KIZ1 - .8f), 1.3f, 7f, KNight);
        Pendant(g, 2.6f, cy - .03f, KIZ1 - 1.3f, 1.2f, 3.4f, 6f);
        Pendant(g, 13.0f, cy - .03f, KIZ1 - 1.3f, 1.2f, 3.4f, 6f);

        // ---- チンチロ（右寄り。台はシーンに置いてある）：吊り照明
        Pendant(g, 12.8f, cy - .03f, -13.4f, 1.2f, 4.6f, 7f);

        // ---- 右の壁ぎわ：丸い机と腰かけ
        RoundTableAt(g, 15.2f, fy, -10.6f);
        Prop(g, "Round table stool", Art.Stool("purple"), 14.5f, fy, -10.3f);
        Prop(g, "Table glass", Art.Glass(), 15.25f, fy + 1.05f, -10.55f);
        Prop(g, "Bulb string", Art.BulbString(5.4f, 11), KIX0 + .15f, cy - .55f, -15.4f, -90);
        Prop(g, "Bulb string", Art.BulbString(5.4f, 11), KIX1 - .15f, cy - .55f, -10.0f, 90);
        Prop(g, "Frame", Art.Frame(.6f, .8f, "gold"), KIX0 + .055f, fy + 2.2f, -12.2f, 90);
    }
}
#endif
