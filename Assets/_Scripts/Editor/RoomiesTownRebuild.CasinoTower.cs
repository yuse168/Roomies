#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カジノを 3 階建てにする（2026/11/23 ゆせさんの依頼）。Rebuild/3 Casino から呼ぶ。
/// 幅と奥行きは 1 階建てのときと同じ（x -1.2〜16.8、z -19.6〜-7.8）。上に 2 層を積む。
///
/// 高さ（床の上面／天井の下面）：1 階 0.118／4.4、2 階 4.9／8.3、3 階 8.8／12.6、屋上の面 13.05、笠木 13.4。
/// 1 階の床と天井は前のままなので、スロット・ブラックジャック台・通用口は動かさない。
///
/// 置く場所（町のプレハブ）：
///  ・「Town casino」     … 外壁・床の板・屋根・窓と、1 階の中身（通りから見えるので、いつも出しておく）
///  ・「Town casino 2F」  … 2 階の中身（床と壁の仕上げ・明かり・家具）
///  ・「Town casino 3F」  … 3 階の中身
/// 2 階・3 階の中身は、その階にいる人の画面でだけ出す（GameRoom の「Roomies_Casino」の CasinoFloorVisibility）。
///
/// 階の行き来はエレベーターだけ（Assets/_Prefabs/Casino/CasinoElevator.prefab。奥の壁ぎわ x 9.5〜12.3、z 〜-17.1 を空けてある）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    internal const string CasinoGroup2F = "Town casino 2F", CasinoGroup3F = "Town casino 3F";
    internal const float KF2 = 4.9f, KC2 = 8.3f, KF3 = 8.8f, KC3 = 12.6f, KRoofDeck = 13.05f, KRoof = 13.4f;
    // エレベーターが入る所（奥の壁の内側から手前へ）。中身はエレベーターのプレハブの側
    internal const float KEvX0 = 9.5f, KEvX1 = 12.3f, KEvZ1 = -17.1f;
    // 店の人が立つ踏み台の高さ（カウンターの上に体が出るように）
    internal const float KClerkStep = .45f;

    // 通り側の窓（x の範囲と高さ）。2 階は 4 つ、3 階は夜景の見える大きな窓を 3 つ
    internal static readonly (float x0, float x1)[] KWin2F = { (.6f, 3.0f), (4.6f, 7.0f), (8.6f, 11.0f), (12.6f, 15.0f) };
    internal const float KWin2Y0 = 5.8f, KWin2Y1 = 7.7f;
    internal static readonly (float x0, float x1)[] KWin3F = { (.4f, 4.8f), (5.6f, 10.0f), (10.8f, 15.2f) };
    internal const float KWin3Y0 = 9.6f, KWin3Y1 = 12.1f;

    internal static float KFloorY(int floor) => floor == 1 ? KFloor : floor == 2 ? KF2 : KF3;
    internal static float KCeilY(int floor) => floor == 1 ? KCeil : floor == 2 ? KC2 : KC3;

    // ================================================================ 外壁・床の板・屋根（当たり判定つき）
    static void CasinoTowerShell(Transform g)
    {
        var wall = Mat("casino_facade"); var cream = Mat("casino_cream"); var gold = Mat("casino_gold"); var black = Mat("casino_black");
        float ix0 = KX0 + KT, ix1 = KX1 - KT, iz0 = KZ0 + KT, iz1 = KZ1 - KT;

        // 正面の 1 階（入口と通用口を空ける）
        foreach (var (x0, x1) in new[] { (KX0, KDoorX0), (KDoorX1, KEntX0), (KEntX1, KX1) })
        {
            Box(g, "Casino front wall", x0, x1, 0, KF2, iz1, KZ1, wall);
            Solid(g, "Casino wall collider", new Vector3(x0, 0, iz1), new Vector3(x1, KF2, KZ1));
        }
        Box(g, "Casino front wall", KDoorX0, KDoorX1, 2.18f, KF2, iz1, KZ1, wall);
        Box(g, "Casino front wall", KEntX0, KEntX1, KEntH, KF2, iz1, KZ1, wall);
        Solid(g, "Casino wall collider", new Vector3(KDoorX0, 2.18f, iz1), new Vector3(KDoorX1, KF2, KZ1));
        Solid(g, "Casino wall collider", new Vector3(KEntX0, KEntH, iz1), new Vector3(KEntX1, KF2, KZ1));
        // 正面の 2 階・3 階（窓を空けた壁。ガラスは通れないので、当たり判定は 1 枚）
        FrontBand(g, KF2, KF3, KWin2F, KWin2Y0, KWin2Y1, wall);
        FrontBand(g, KF3, KRoof, KWin3F, KWin3Y0, KWin3Y1, wall);
        Solid(g, "Casino wall collider", new Vector3(KX0, KF2, iz1), new Vector3(KX1, KRoof, KZ1));

        // 左・右・奥
        Box(g, "Casino side wall", KX0, ix0, 0, KRoof, KZ0, KZ1, wall);
        Box(g, "Casino side wall", ix1, KX1, 0, KRoof, KZ0, KZ1, wall);
        Box(g, "Casino back wall", ix0, ix1, 0, KRoof, KZ0, iz0, wall);
        Solid(g, "Casino wall collider", new Vector3(KX0, 0, KZ0), new Vector3(ix0, KRoof, KZ1));
        Solid(g, "Casino wall collider", new Vector3(ix1, 0, KZ0), new Vector3(KX1, KRoof, KZ1));
        Solid(g, "Casino wall collider", new Vector3(ix0, 0, KZ0), new Vector3(ix1, KRoof, iz0));

        // 床の板（下の階の天井の上〜上の階の床）と屋根
        foreach (var (y0, y1, name) in new[] { (KCeil, KF2, "2F"), (KC2, KF3, "3F"), (KC3, KRoofDeck, "roof") })
        {
            Box(g, "Casino slab " + name, ix0, ix1, y0, y1, iz0, iz1, Mat("roof_flat"));
            Solid(g, "Casino slab collider " + name, new Vector3(KX0, y0, KZ0), new Vector3(KX1, y1, KZ1));
        }
        // 笠木
        Box(g, "Casino coping", KX0 - .08f, KX1 + .08f, KRoof, KRoof + .1f, KZ1 - .2f, KZ1 + .08f, gold);
        Box(g, "Casino coping", KX0 - .08f, KX1 + .08f, KRoof, KRoof + .1f, KZ0 - .08f, KZ0 + .2f, cream);
        Box(g, "Casino coping", KX0 - .08f, KX0 + .2f, KRoof, KRoof + .1f, KZ0, KZ1, cream);
        Box(g, "Casino coping", KX1 - .2f, KX1 + .08f, KRoof, KRoof + .1f, KZ0, KZ1, cream);
        // 外壁の足元（黒い石）と、階の境の帯（側面・裏）
        Box(g, "Casino plinth", KX0 - .03f, KX1 + .03f, 0, .5f, KZ0 - .03f, KZ0 + .02f, black);
        Box(g, "Casino plinth", KX0 - .03f, KX0 + .02f, 0, .5f, KZ0, KZ1, black);
        Box(g, "Casino plinth", KX1 - .02f, KX1 + .03f, 0, .5f, KZ0, KZ1, black);
        foreach (float y in new[] { KF2 - .3f, KF3 - .3f })
        {
            Box(g, "Casino band", KX0 - .04f, KX0 + .01f, y, y + .25f, KZ0, KZ1, cream);
            Box(g, "Casino band", KX1 - .01f, KX1 + .04f, y, y + .25f, KZ0, KZ1, cream);
            Box(g, "Casino band", KX0, KX1, y, y + .25f, KZ0 - .04f, KZ0 + .01f, cream);
        }
        // 屋上の室外機
        foreach (var (x, z) in new[] { (2.5f, -16.5f), (5.0f, -16.5f), (13.5f, -12f) })
        {
            Box(g, "AC unit", x - .7f, x + .7f, KRoofDeck, KRoofDeck + .85f, z - .5f, z + .5f, Mat("ac_unit"));
            Cyl(g, "AC fan", new Vector3(x, KRoofDeck + .87f, z), new Vector3(.8f, .02f, .8f), Mat("metal_dark"));
        }
    }

    /// <summary>a0〜a1 を、開口（x の範囲）で区切る。開口でない区間は open=false。</summary>
    static IEnumerable<(float a0, float a1, bool open)> Spans(float a0, float a1, (float x0, float x1)[] openings)
    {
        float a = a0;
        foreach (var (o0, o1) in openings)
        {
            if (o0 > a) yield return (a, o0, false);
            yield return (o0, o1, true);
            a = o1;
        }
        if (a1 > a) yield return (a, a1, false);
    }

    /// <summary>正面の壁の 1 層ぶん：窓を空けた壁と、窓のガラス・枠。</summary>
    static void FrontBand(Transform g, float y0, float y1, (float x0, float x1)[] wins, float wy0, float wy1, Material wall)
    {
        float iz1 = KZ1 - KT;
        var frame = Mat("casino_gold");
        foreach (var (a0, a1, open) in Spans(KX0, KX1, wins))
        {
            if (!open) { Box(g, "Casino front wall", a0, a1, y0, y1, iz1, KZ1, wall); continue; }
            Box(g, "Casino front wall", a0, a1, y0, wy0, iz1, KZ1, wall);
            Box(g, "Casino front wall", a0, a1, wy1, y1, iz1, KZ1, wall);
            Box(g, "Casino window glass", a0, a1, wy0, wy1, KZ1 - .15f, KZ1 - .11f, Mat("slot_screen"));
            Box(g, "Casino window frame", a0 - .08f, a0, wy0 - .08f, wy1 + .08f, KZ1 - .02f, KZ1 + .06f, frame);
            Box(g, "Casino window frame", a1, a1 + .08f, wy0 - .08f, wy1 + .08f, KZ1 - .02f, KZ1 + .06f, frame);
            Box(g, "Casino window frame", a0, a1, wy1, wy1 + .08f, KZ1 - .02f, KZ1 + .06f, frame);
            Box(g, "Casino window sill", a0 - .12f, a1 + .12f, wy0 - .1f, wy0, KZ1 - .02f, KZ1 + .12f, frame);
        }
    }

    // ================================================================ 2 階・3 階の中（いまは仮の仕上げ。段階 6 で作り直す）
    static void CasinoUpperFloor(Transform g, int floor)
    {
        float ix0 = KX0 + KT, ix1 = KX1 - KT, iz0 = KZ0 + KT, iz1 = KZ1 - KT;
        float f = KFloorY(floor), c = KCeilY(floor);
        var wins = floor == 2 ? KWin2F : KWin3F;
        float wy0 = floor == 2 ? KWin2Y0 : KWin3Y0, wy1 = floor == 2 ? KWin2Y1 : KWin3Y1;
        var fabric = Mat("casino_wall"); var wood = Mat("casino_wood");

        Box(g, "Floor finish", ix0, ix1, f, f + .012f, iz0, iz1, wood);
        Box(g, "Ceiling", ix0, ix1, c - .02f, c, iz0, iz1, Mat("casino_ceiling"));
        // 壁の仕上げ（奥・左・右は 1 枚、通り側は窓を空ける）
        FaceBox(g, "Wall finish", Vector3.forward, iz0, ix0, ix1, f, c, 0, .02f, fabric);
        FaceBox(g, "Wall finish", Vector3.right, ix0, iz0, iz1, f, c, 0, .02f, fabric);
        FaceBox(g, "Wall finish", Vector3.left, ix1, iz0, iz1, f, c, 0, .02f, fabric);
        foreach (var (a0, a1, open) in Spans(ix0, ix1, wins))
        {
            if (!open) { FaceBox(g, "Wall finish", Vector3.back, iz1, a0, a1, f, c, 0, .02f, fabric); continue; }
            FaceBox(g, "Wall finish", Vector3.back, iz1, a0, a1, f, wy0, 0, .02f, fabric);
            FaceBox(g, "Wall finish", Vector3.back, iz1, a0, a1, wy1, c, 0, .02f, fabric);
        }
        // 2 階：店の人のカウンター（1 階の両替所の真上。エレベーターの右）
        if (floor == 2)
        {
            Box(g, "Clerk counter", 12.8f, ix1, f, f + 1.05f, -17.4f, -16.9f, wood);
            Box(g, "Clerk counter top", 12.75f, ix1, f + 1.05f, f + 1.12f, -17.45f, -16.82f, Mat("casino_marble"));
            Solid(g, "Clerk counter collider", new Vector3(12.75f, f, -17.45f), new Vector3(ix1, f + 1.12f, -16.82f));
            Box(g, "Clerk step", 12.8f, ix1, f, f + KClerkStep, iz0, -17.4f, wood);
        }
        // 明かり（その階にいるときだけ点く。CasinoFloorVisibility がこのグループごと出し入れする）
        int n = 0;
        foreach (float x in new[] { 3.2f, 7.8f, 12.9f })
            foreach (float z in new[] { -16.2f, -11.2f })
                PointLight(g, $"Casino light - {floor}F {++n}", new Vector3(x, c - .7f, z), 4.6f, 9f, new Color(1f, .82f, .62f));
    }
}
#endif
