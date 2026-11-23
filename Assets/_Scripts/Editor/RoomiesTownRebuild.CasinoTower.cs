#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カジノの建物の骨組み（2026/11/23 ゆせさんの依頼）：外壁・床の板・屋根・窓と、その当たり判定。Rebuild/3 Casino から呼ぶ。
/// 幅と奥行きは 1 階建てのときと同じ（x -1.2〜16.8、z -19.6〜-7.8）。上に 2 層を積んである。
///
/// 高さ（床の上面／天井の下面）：1 階 0.118／4.4、2 階 4.9／8.3、3 階 8.8／12.6、屋上の面 13.05、笠木 13.4。
/// 1 階の床の高さは前のままなので、通用口（シーンの DoorPivot）は動かしていない。
///
/// 階の行き来はエレベーターだけ（Assets/_Prefabs/Casino/CasinoElevator.prefab。奥の壁ぎわ x 9.5〜12.3、z 〜-17.1 を空けてある）。
/// 通り側の窓は本当に透ける。上の階の中身は、その階にいる人の画面でだけ出すので、
/// 外から見たときは、窓のすぐ内側に「明かりの点いた窓」の板（Town casino 2F windows / 3F windows）を出しておく。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    // 外形・壁の厚さ・1 階の床と天井・入口と通用口の開口
    const float KX0 = -1.2f, KX1 = 16.8f, KZ0 = -19.6f, KZ1 = -7.8f, KT = .25f, KFloor = .118f, KCeil = 4.4f;
    const float KDoorX0 = .76f, KDoorX1 = 1.9f, KDoorH = 2.18f, KEntX0 = 6.3f, KEntX1 = 9.3f, KEntH = 3.0f;
    internal const float KF2 = 4.9f, KC2 = 8.3f, KF3 = 8.8f, KC3 = 12.6f, KRoofDeck = 13.05f, KRoof = 13.4f;
    // エレベーターが入る所（奥の壁の内側から手前へ）。中身はエレベーターのプレハブの側
    internal const float KEvX0 = 9.5f, KEvX1 = 12.3f, KEvZ1 = -17.1f;
    // 店の人が立つ踏み台の高さ（カウンターの上に体が出るように）
    internal const float KClerkStep = .45f;
    // 壁の内側の面
    const float KIX0 = KX0 + KT, KIX1 = KX1 - KT, KIZ0 = KZ0 + KT, KIZ1 = KZ1 - KT;

    // 通り側の窓（x の範囲と高さ）。2 階は 4 つ、3 階は夜景の見える大きな窓を 3 つ
    internal static readonly (float x0, float x1)[] KWin2F = { (.6f, 3.0f), (4.6f, 7.0f), (8.6f, 11.0f), (12.6f, 15.0f) };
    internal const float KWin2Y0 = 5.8f, KWin2Y1 = 7.7f;
    internal static readonly (float x0, float x1)[] KWin3F = { (.4f, 4.8f), (5.6f, 10.0f), (10.8f, 15.2f) };
    internal const float KWin3Y0 = 9.6f, KWin3Y1 = 12.1f;

    internal static float KFloorY(int floor) => floor == 1 ? KFloor : floor == 2 ? KF2 : KF3;
    internal static float KCeilY(int floor) => floor == 1 ? KCeil : floor == 2 ? KC2 : KC3;
    static (float x0, float x1)[] KWindows(int floor) => floor == 2 ? KWin2F : KWin3F;
    static float KWinY0(int floor) => floor == 2 ? KWin2Y0 : KWin3Y0;
    static float KWinY1(int floor) => floor == 2 ? KWin2Y1 : KWin3Y1;

    // ================================================================ 外壁・床の板・屋根（当たり判定つき）
    static void CasinoTowerShell(Transform g)
    {
        var wall = Mat("casino_brick"); var timber = Mat("casino_timber"); var stone = Mat("plinth");

        // 正面の 1 階（入口と通用口を空ける）
        foreach (var (x0, x1) in new[] { (KX0, KDoorX0), (KDoorX1, KEntX0), (KEntX1, KX1) })
        {
            Box(g, "Casino front wall", x0, x1, 0, KF2, KIZ1, KZ1, wall);
            Solid(g, "Casino wall collider", new Vector3(x0, 0, KIZ1), new Vector3(x1, KF2, KZ1));
        }
        Box(g, "Casino front wall", KDoorX0, KDoorX1, KDoorH, KF2, KIZ1, KZ1, wall);
        Box(g, "Casino front wall", KEntX0, KEntX1, KEntH, KF2, KIZ1, KZ1, wall);
        Solid(g, "Casino wall collider", new Vector3(KDoorX0, KDoorH, KIZ1), new Vector3(KDoorX1, KF2, KZ1));
        Solid(g, "Casino wall collider", new Vector3(KEntX0, KEntH, KIZ1), new Vector3(KEntX1, KF2, KZ1));
        // 正面の 2 階・3 階（窓を空けた壁。ガラスは通れないので、当たり判定は 1 枚）
        FrontBand(g, KF2, KF3, 2, wall);
        FrontBand(g, KF3, KRoof, 3, wall);
        Solid(g, "Casino wall collider", new Vector3(KX0, KF2, KIZ1), new Vector3(KX1, KRoof, KZ1));

        // 左・右・奥
        Box(g, "Casino side wall", KX0, KIX0, 0, KRoof, KZ0, KZ1, wall);
        Box(g, "Casino side wall", KIX1, KX1, 0, KRoof, KZ0, KZ1, wall);
        Box(g, "Casino back wall", KIX0, KIX1, 0, KRoof, KZ0, KIZ0, wall);
        Solid(g, "Casino wall collider", new Vector3(KX0, 0, KZ0), new Vector3(KIX0, KRoof, KZ1));
        Solid(g, "Casino wall collider", new Vector3(KIX1, 0, KZ0), new Vector3(KX1, KRoof, KZ1));
        Solid(g, "Casino wall collider", new Vector3(KIX0, 0, KZ0), new Vector3(KIX1, KRoof, KIZ0));

        // 床の板（下の階の天井の上〜上の階の床）と屋根
        foreach (var (y0, y1, name) in new[] { (KCeil, KF2, "2F"), (KC2, KF3, "3F"), (KC3, KRoofDeck, "roof") })
        {
            Box(g, "Casino slab " + name, KIX0, KIX1, y0, y1, KIZ0, KIZ1, Mat("roof_flat"));
            Solid(g, "Casino slab collider " + name, new Vector3(KX0, y0, KZ0), new Vector3(KX1, y1, KZ1));
        }

        // 外の木組み：四隅の柱、階の境の帯、笠木。足元は石
        float o = .07f;
        foreach (var (x, z) in new[] { (KX0, KZ0), (KX1, KZ0), (KX0, KZ1), (KX1, KZ1) })
            Box(g, "Casino corner post", x - .2f, x + .2f, 0, KRoof, z - .2f, z + .2f, timber);
        foreach (float y in new[] { KF2 - .32f, KF3 - .32f, KRoof - .3f })
        {
            Box(g, "Casino band", KX0 - o, KX1 + o, y, y + .3f, KZ1, KZ1 + o, timber);
            Box(g, "Casino band", KX0 - o, KX1 + o, y, y + .3f, KZ0 - o, KZ0, timber);
            Box(g, "Casino band", KX0 - o, KX0, y, y + .3f, KZ0, KZ1, timber);
            Box(g, "Casino band", KX1, KX1 + o, y, y + .3f, KZ0, KZ1, timber);
        }
        Box(g, "Casino coping", KX0 - .12f, KX1 + .12f, KRoof, KRoof + .12f, KZ0 - .12f, KZ1 + .12f, timber);
        Box(g, "Casino roof deck", KX0 + .3f, KX1 - .3f, KRoofDeck, KRoofDeck + .02f, KZ0 + .3f, KZ1 - .3f, Mat("roof_flat"));
        Box(g, "Casino plinth", KX0 - .04f, KX1 + .04f, 0, .45f, KZ0 - .04f, KZ0, stone);
        Box(g, "Casino plinth", KX0 - .04f, KX0, 0, .45f, KZ0, KZ1, stone);
        Box(g, "Casino plinth", KX1, KX1 + .04f, 0, .45f, KZ0, KZ1, stone);
        // 屋上の室外機
        foreach (var (x, z) in new[] { (2.5f, -16.5f), (5.0f, -16.5f), (13.5f, -12f) })
            Box(g, "AC unit", x - .7f, x + .7f, KRoofDeck, KRoofDeck + .85f, z - .5f, z + .5f, stone);
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

    /// <summary>正面の壁の 1 層ぶん：窓を空けた壁と、窓のガラス・木の枠・桟。</summary>
    static void FrontBand(Transform g, float y0, float y1, int floor, Material wall)
    {
        var frame = Mat("casino_timber");
        var glass = RoomiesCasinoArt.GlassMaterial();
        float wy0 = KWinY0(floor), wy1 = KWinY1(floor);
        foreach (var (a0, a1, open) in Spans(KX0, KX1, KWindows(floor)))
        {
            if (!open) { Box(g, "Casino front wall", a0, a1, y0, y1, KIZ1, KZ1, wall); continue; }
            Box(g, "Casino front wall", a0, a1, y0, wy0, KIZ1, KZ1, wall);
            Box(g, "Casino front wall", a0, a1, wy1, y1, KIZ1, KZ1, wall);
            Box(g, "Casino window glass", a0, a1, wy0, wy1, KZ1 - .14f, KZ1 - .11f, glass);
            // 枠（外と内の両方から見える厚さ）と、桟
            Box(g, "Casino window frame", a0 - .1f, a0 + .04f, wy0 - .1f, wy1 + .1f, KIZ1 - .03f, KZ1 + .07f, frame);
            Box(g, "Casino window frame", a1 - .04f, a1 + .1f, wy0 - .1f, wy1 + .1f, KIZ1 - .03f, KZ1 + .07f, frame);
            Box(g, "Casino window frame", a0, a1, wy1 - .04f, wy1 + .1f, KIZ1 - .03f, KZ1 + .07f, frame);
            Box(g, "Casino window sill", a0 - .16f, a1 + .16f, wy0 - .12f, wy0 + .03f, KIZ1 - .08f, KZ1 + .14f, frame);
            int panes = Mathf.Max(2, Mathf.RoundToInt((a1 - a0) / 1.1f));
            for (int i = 1; i < panes; i++)
            {
                float x = Mathf.Lerp(a0, a1, (float)i / panes);
                Box(g, "Casino window mullion", x - .03f, x + .03f, wy0, wy1, KZ1 - .17f, KZ1 - .08f, frame);
            }
            Box(g, "Casino window mullion", a0, a1, wy1 - .62f, wy1 - .56f, KZ1 - .17f, KZ1 - .08f, frame);
        }
    }

    /// <summary>外から見た窓の明かり（その階にいないときだけ出す）。窓のすぐ内側の、暖色に光る板。</summary>
    static void CasinoWindowGlow(Transform g, int floor)
    {
        var glow = Mat("casino_window_glow");
        foreach (var (x0, x1) in KWindows(floor))
            Box(g, "Window glow", x0, x1, KWinY0(floor), KWinY1(floor), KIZ1 - .05f, KIZ1 - .03f, glow);
    }
}
#endif
