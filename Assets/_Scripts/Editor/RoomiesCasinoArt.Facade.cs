#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カジノの外観の飾り（2026/11/24 ゆせさんの依頼：箱の形をやめて、派手なカジノらしい形に）。
/// 角の丸い塔・段々の冠・丸いひさし・入口のアーチ・付け柱・窓の日よけ・電球の列。
/// どれも「正面が +Z、足元（または壁の面）が原点」。置く所と寸法の組み合わせは RoomiesTownRebuild.CasinoBuilding.cs。
/// レンガや塗り壁にしたい形は、置いたあとで町の素材に替える（町の素材は三方向から貼るので、どんな形にも模様が合う）。
/// </summary>
internal static partial class RoomiesCasinoArt
{
    // ================================================================ 角の丸い塔
    // 塔の中心は、正面の壁の面より TurretInset だけ奥にある。屋根より下は「壁の面より前に出ている弧」だけを作る
    // （丸ごとの円柱にすると、建物の中の部屋へ突き出る）。屋根より上（てっぺんの帯と丸屋根）は、丸ごと。
    internal const float TurretRadius = .9f, TurretInset = .45f;

    /// <summary>壁の面より前に出ている範囲（角度）。Lathe と同じ向き（+Z が正面）。壁の厚みの中で止まるよう、ほんの少しだけ奥へ回す。</summary>
    static (float a0, float a1) TurretFrontArc(float r)
    {
        float a = Mathf.Asin(Mathf.Clamp((TurretInset - .06f) / r, -1f, 1f));
        return (a, Mathf.PI - a);
    }

    /// <summary>回転体の一部（角度 a0〜a1 の弧）。rings は下から (半径, 高さ)。</summary>
    static void LatheArc(Lp lp, (float r, float y)[] rings, int sides, float a0, float a1, System.Func<int, string> colorOfBand, bool glow = false)
    {
        Vector3 R(float r, float y, float a) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
        for (int b = 0; b < rings.Length - 1; b++)
        {
            var r0 = rings[b]; var r1 = rings[b + 1];
            float dr = r1.r - r0.r, dy = r1.y - r0.y;
            Vector2 u = Sw(colorOfBand(b));
            for (int k = 0; k < sides; k++)
            {
                float t0 = Mathf.Lerp(a0, a1, (float)k / sides), t1 = Mathf.Lerp(a0, a1, (float)(k + 1) / sides);
                Vector3 outward = R(1, 0, (t0 + t1) / 2) * dy - Vector3.up * dr;
                if (outward.sqrMagnitude < 1e-12f) continue;
                lp.Quad(R(r0.r, r0.y, t0), R(r0.r, r0.y, t1), R(r1.r, r1.y, t1), R(r1.r, r1.y, t0), u, u, u, u, outward, glow);
            }
        }
    }

    /// <summary>塔の胴（レンガにする部分）。足元から roofY までは前の弧だけ、roofY から height までは丸ごと。</summary>
    internal static Mesh TurretBody(float height, float roofY) => Build("facade_turret_body_" + Mathf.RoundToInt(height * 10), lp =>
    {
        var (a0, a1) = TurretFrontArc(TurretRadius);
        LatheArc(lp, new[] { (TurretRadius, 0f), (TurretRadius, roofY) }, 14, a0, a1, _ => "brick");
        lp.Lathe(new[] { (TurretRadius, roofY), (TurretRadius, height) }, 20, "brick", 0, false, false);
    });

    /// <summary>塔の飾り：足元の石、階の境の金の帯とピンクのネオンの輪（どれも前の弧だけ）、てっぺんの白い帯・金の丸屋根・飾り玉・電球（丸ごと）。</summary>
    internal static Mesh TurretTrim(float height, float[] bands) => Build("facade_turret_trim_" + Mathf.RoundToInt(height * 10), lp =>
    {
        float r = TurretRadius;
        var (a0, a1) = TurretFrontArc(r + .08f);
        LatheArc(lp, new[] { (r + .1f, 0f), (r + .1f, .42f), (r + .03f, .5f) }, 14, a0, a1, _ => "stone");
        foreach (float y in bands)
        {
            LatheArc(lp, new[] { (r + .01f, y - .04f), (r + .08f, y), (r + .08f, y + .2f), (r + .01f, y + .24f) }, 14, a0, a1, _ => "gold");
            LatheArc(lp, new[] { (r + .03f, y + .3f), (r + .07f, y + .33f), (r + .03f, y + .36f) }, 14, a0, a1, _ => "pink", true);
        }
        // てっぺん：白い帯 → 金のふち → 丸屋根 → 飾り玉
        lp.Lathe(new[] { (r + .02f, height - .02f), (r + .1f, height + .06f), (r + .1f, height + .62f), (r + .16f, height + .7f), (r + .16f, height + .82f), (r + .04f, height + .86f) },
            20, b => b >= 3 ? "gold" : "cream", 0, false, false);
        lp.Lathe(new[] { (r + .04f, height + .86f), (r * .98f, height + 1.12f), (r * .8f, height + 1.42f), (r * .5f, height + 1.68f), (r * .2f, height + 1.82f), (.06f, height + 1.9f), (.05f, height + 2.2f) },
            20, "gold", 0, false, false);
        lp.Push(new Vector3(0, height + 2.34f, 0));
        lp.Lathe(new[] { (0f, -.16f), (.12f, -.11f), (.16f, 0f), (.12f, .11f), (0f, .16f) }, 10, "gold_dark", 0, false, false);
        lp.Pop();
        // 白い帯のまわりの電球
        const int bulbs = 14;
        for (int i = 0; i < bulbs; i++)
        {
            float a = i * Mathf.PI * 2 / bulbs;
            lp.Push(new Vector3(Mathf.Cos(a) * (r + .12f), height + .34f, Mathf.Sin(a) * (r + .12f)));
            lp.Lathe(new[] { (0f, -.05f), (.045f, -.02f), (.045f, .02f), (0f, .05f) }, 6, i % 2 == 0 ? "warm" : "pink", 0, false, false, true);
            lp.Pop();
        }
    });

    // ================================================================ 段々の冠（屋上に積む、角の丸い段）
    /// <summary>角の丸い段（幅・奥行き・高さ・角の丸み）。原点は底の真ん中。置いたあとで町の素材に替える。</summary>
    internal static Mesh Tier(float width, float depth, float height, float corner) =>
        Build($"facade_tier_{Mathf.RoundToInt(width * 10)}_{Mathf.RoundToInt(depth * 10)}_{Mathf.RoundToInt(height * 10)}", lp =>
            lp.Prism(Chamfer(width, depth, corner), 0, height, "plaster", null, false));

    /// <summary>段の上にのせる金のふち（段より少し大きい、薄い板）と、前のふちの電球の列。</summary>
    internal static Mesh TierCap(float width, float depth, float corner, int bulbs) =>
        Build($"facade_tier_cap_{Mathf.RoundToInt(width * 10)}_{Mathf.RoundToInt(depth * 10)}", lp =>
        {
            lp.Prism(Chamfer(width + .2f, depth + .2f, corner + .1f), 0, .14f, "gold", "gold_dark", true);
            for (int i = 0; i < bulbs; i++)
            {
                float x = Mathf.Lerp(-width / 2 + corner, width / 2 - corner, bulbs > 1 ? (float)i / (bulbs - 1) : .5f);
                lp.Push(new Vector3(x, -.07f, depth / 2 + .11f));
                lp.Lathe(new[] { (0f, -.06f), (.05f, -.025f), (.05f, .025f), (0f, .06f) }, 6, i % 2 == 0 ? "warm" : "pink", 0, false, false, true);
                lp.Pop();
            }
        });

    // ================================================================ 入口の丸いひさし（半円に張り出す）
    internal const float MarqueeHalfWidth = 3.2f, MarqueeDepth = 1.5f, MarqueeThickness = .36f;

    /// <summary>半円のひさし。原点は壁の面の真ん中・下の面。+Z へ張り出す。ふちに電球、下の面は暖色に光る。</summary>
    internal static Mesh Marquee() => Build("facade_marquee", lp =>
    {
        Vector2[] Half(float rx, float rz)
        {
            const int n = 20;
            var p = new List<Vector2>();
            for (int i = 0; i <= n; i++) { float a = Mathf.PI * i / n; p.Add(new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz)); }
            return p.ToArray();
        }
        lp.Prism(Half(MarqueeHalfWidth, MarqueeDepth), 0, MarqueeThickness, "gold", "maroon_dark", false);
        lp.Prism(Half(MarqueeHalfWidth - .14f, MarqueeDepth - .14f), MarqueeThickness, MarqueeThickness + .1f, "maroon", "maroon", false);
        lp.Sharp = true;
        lp.Prism(Half(MarqueeHalfWidth - .2f, MarqueeDepth - .2f), -.02f, 0f, "lamp_under", null, true, 1f, true);      // 下の面（光る）
        lp.Sharp = false;
        // ふちの電球（暖色とピンクが交互）
        const int bulbs = 23;
        for (int i = 0; i < bulbs; i++)
        {
            float a = Mathf.PI * (i + .5f) / bulbs;
            lp.Push(new Vector3(Mathf.Cos(a) * (MarqueeHalfWidth + .04f), MarqueeThickness * .5f, Mathf.Sin(a) * (MarqueeDepth + .04f)));
            lp.Lathe(new[] { (0f, -.07f), (.06f, -.03f), (.06f, .03f), (0f, .07f) }, 6, i % 2 == 0 ? "warm" : "pink", 0, false, false, true);
            lp.Pop();
        }
    });

    // ================================================================ 入口のアーチ（ひさしの上に立つ半円）
    internal const float ArchOuter = 2.2f, ArchInner = 1.72f;

    /// <summary>半円のアーチ。原点はアーチの中心（半円の底の真ん中）・壁の面。中は放射状の飾り、内側のふちにピンクのネオン。</summary>
    internal static Mesh EntranceArch() => Build("facade_arch", lp =>
    {
        const int n = 20;
        const float depth = .24f;
        Vector3 P(float r, float a, float z) => new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
        Vector2 gold = Sw("gold"), dark = Sw("gold_dark");
        for (int i = 0; i < n; i++)
        {
            float a0 = Mathf.PI * i / n, a1 = Mathf.PI * (i + 1) / n, am = (a0 + a1) / 2;
            Vector3 radial = P(1, am, 0);
            lp.Quad(P(ArchInner, a0, depth), P(ArchOuter, a0, depth), P(ArchOuter, a1, depth), P(ArchInner, a1, depth), gold, gold, gold, gold, Vector3.forward);   // 前の面
            lp.Quad(P(ArchOuter, a0, 0), P(ArchOuter, a0, depth), P(ArchOuter, a1, depth), P(ArchOuter, a1, 0), dark, dark, dark, dark, radial);                    // 外のふち
            lp.Quad(P(ArchInner, a0, 0), P(ArchInner, a0, depth), P(ArchInner, a1, depth), P(ArchInner, a1, 0), dark, dark, dark, dark, -radial);                   // 内のふち
            // ピンクのネオンの管（アーチの前の面の真ん中を走る）
            lp.Bar(P((ArchInner + ArchOuter) / 2, a0, depth + .03f), P((ArchInner + ArchOuter) / 2, a1, depth + .03f), .06f, "pink", true);
        }
        // 中の放射状の飾り（えんじと金が交互）
        const int rays = 10;
        for (int i = 0; i < rays; i++)
        {
            float a0 = Mathf.PI * i / rays, a1 = Mathf.PI * (i + 1) / rays;
            Vector2 u = Sw(i % 2 == 0 ? "maroon" : "gold_dark");
            lp.Tri(new Vector3(0, 0, .05f), P(ArchInner, a0, .05f), P(ArchInner, a1, .05f), u, u, u, Vector3.forward);
        }
        // かなめ石
        lp.Box(new Vector3(-.2f, ArchOuter - .1f, 0), new Vector3(.2f, ArchOuter + .34f, depth + .06f), "gold_dark");
    });

    // ================================================================ 付け柱（正面の壁の外の面に付く、白い柱と金の柱頭）
    /// <summary>付け柱。原点は柱の足元・壁の面。壁の面より前（+Z）にだけ形がある。onGround のときは足元に石の台。</summary>
    internal static Mesh Pilaster(float height, bool onGround = true) => Build($"facade_pilaster_{Mathf.RoundToInt(height * 10)}_{(onGround ? "g" : "w")}", lp =>
    {
        const float back = .01f;          // 壁の面からのすき間（壁の中へは入れない）
        var shaft = Offset(Chamfer(.46f, .2f, .07f), new Vector2(0, back + .1f));
        float y0 = 0;
        if (onGround) { lp.Prism(Offset(Chamfer(.62f, .28f, .06f), new Vector2(0, back + .14f)), 0, .5f, "stone"); y0 = .5f; }
        else lp.Loft(shaft, new[] { (0f, 1.25f), (.1f, 1.2f), (.22f, 1f) }, "gold", null, true);     // 壁の途中から立つ柱の、下の受け
        lp.Prism(shaft, y0 + (onGround ? 0 : .22f), height - .5f, "cream");
        lp.Loft(shaft, new[] { (height - .5f, 1f), (height - .38f, 1.1f), (height - .12f, 1.22f), (height, 1.25f) }, "gold", "gold_dark");
    });

    // ================================================================ 窓の日よけ（えんじと白のしま）
    internal static Mesh Awning(float width) => Build("facade_awning_" + Mathf.RoundToInt(width * 100), lp =>
    {
        const float depth = .62f, rise = .46f, drop = .16f;
        int stripes = Mathf.Max(4, Mathf.RoundToInt(width / .34f));
        float hw = width / 2;
        for (int i = 0; i < stripes; i++)
        {
            float x0 = Mathf.Lerp(-hw, hw, (float)i / stripes), x1 = Mathf.Lerp(-hw, hw, (float)(i + 1) / stripes);
            string c = i % 2 == 0 ? "maroon" : "cream";
            lp.Quad(new Vector3(x0, rise, 0), new Vector3(x1, rise, 0), new Vector3(x1, 0, depth), new Vector3(x0, 0, depth), c, new Vector3(0, .8f, .6f));      // 斜めの面
            lp.Quad(new Vector3(x0, 0, depth), new Vector3(x1, 0, depth), new Vector3(x1, -drop, depth), new Vector3(x0, -drop, depth), c, Vector3.forward);      // 前の垂れ
        }
        foreach (float s in new[] { -1f, 1f })
        {
            Vector2 u = Sw("maroon_dark");
            lp.Tri(new Vector3(s * hw, rise, 0), new Vector3(s * hw, 0, depth), new Vector3(s * hw, 0, 0), u, u, u, Vector3.right * s);
        }
        lp.Bar(new Vector3(-hw - .03f, 0, depth), new Vector3(hw + .03f, 0, depth), .04f, "gold");
    });

    // ================================================================ 電球の列（まっすぐ。軒の縁取り用）。原点は左の端、+X へ伸びる
    internal static Mesh BulbRow(float length, int bulbs) => Build($"facade_bulb_row_{Mathf.RoundToInt(length * 10)}_{bulbs}", lp =>
    {
        lp.Sharp = true;
        lp.Box(new Vector3(0, -.03f, -.03f), new Vector3(length, .03f, .03f), "gold_dark");
        lp.Sharp = false;
        for (int i = 0; i < bulbs; i++)
        {
            lp.Push(new Vector3(length * (i + .5f) / bulbs, 0, .07f));
            lp.Lathe(new[] { (0f, -.065f), (.055f, -.03f), (.055f, .03f), (0f, .065f) }, 6, i % 2 == 0 ? "warm" : "pink", 0, false, false, true);
            lp.Pop();
        }
    });

    // ================================================================ 屋上の星の飾り（金の柱の上に、光る星）
    internal static Mesh StarFinial() => Build("facade_star", lp =>
    {
        lp.Lathe(new[] { (.12f, 0f), (.06f, .1f), (.045f, 1.0f) }, 8, "gold_dark", 0, false, false);
        Vector2 u = Sw("warm");
        const int points = 5;
        for (int i = 0; i < points * 2; i++)
        {
            float a0 = Mathf.PI / 2 + i * Mathf.PI / points, a1 = a0 + Mathf.PI / points;
            float r0 = i % 2 == 0 ? .42f : .18f, r1 = i % 2 == 0 ? .18f : .42f;
            foreach (float z in new[] { .05f, -.05f })
                lp.Tri(new Vector3(0, 1.35f, z), new Vector3(Mathf.Cos(a0) * r0, 1.35f + Mathf.Sin(a0) * r0, 0), new Vector3(Mathf.Cos(a1) * r1, 1.35f + Mathf.Sin(a1) * r1, 0), u, u, u,
                    z > 0 ? Vector3.forward : Vector3.back, true);
        }
    });
}
#endif
