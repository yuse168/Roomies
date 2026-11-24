#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// コンビニ（CORNER STORE。2026/11/24 ゆせさんの依頼）の形：商品・棚・冷蔵ケース・レジ・看板など。
/// カジノと同じ道具（Lp）とアトラス・マテリアルを使う（形の決まりも同じ：角は丸く、丸い物は辺を多く、やわらかい物はふくらみ）。
/// どの形も「足元の真ん中が原点、正面が +Z」。置くのは RoomiesTownRebuild.Store.cs（建物）と RoomiesCasinoBuilder.Store.cs（プレハブ）。
///
/// 店の色はさんご色・クリーム・墨色。実在のコンビニの色の組み合わせやロゴはまねしない。
/// 商品のラベルは、いまは色の帯だけ（絵が届いたらアトラスに入れて差し替える）。
/// </summary>
internal static partial class RoomiesCasinoArt
{
    /// <summary>売っている物の種類（StoreShop の番号と同じ順）。</summary>
    internal static readonly string[] StoreKinds = { "onigiri", "bento", "bread", "snack", "cup", "drink", "milk", "ice" };

    // ================================================================ 道具
    /// <summary>凸の多角形の角を、半径 r で丸める（角 1 つを seg に分ける）。</summary>
    internal static Vector2[] RoundPoly(Vector2[] pts, float r, int seg)
    {
        var o = new List<Vector2>();
        int n = pts.Length;
        for (int i = 0; i < n; i++)
        {
            Vector2 p0 = pts[(i + n - 1) % n], p1 = pts[i], p2 = pts[(i + 1) % n];
            Vector2 d0 = (p0 - p1).normalized, d2 = (p2 - p1).normalized;
            float theta = Mathf.Acos(Mathf.Clamp(Vector2.Dot(d0, d2), -1f, 1f));
            float t = r / Mathf.Tan(theta / 2f);
            float maxT = Mathf.Min((p0 - p1).magnitude, (p2 - p1).magnitude) * .45f;
            float rr = t > maxT ? r * maxT / t : r;
            t = Mathf.Min(t, maxT);
            Vector2 bis = (d0 + d2).normalized;
            Vector2 c = p1 + bis * (rr / Mathf.Sin(theta / 2f));
            Vector2 a = p1 + d0 * t, b = p1 + d2 * t;
            float aa = Mathf.Atan2(a.y - c.y, a.x - c.x), ab = Mathf.Atan2(b.y - c.y, b.x - c.x);
            float delta = Mathf.DeltaAngle(aa * Mathf.Rad2Deg, ab * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            for (int k = 0; k <= seg; k++)
            {
                float ang = aa + delta * k / seg;
                o.Add(c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * rr);
            }
        }
        return o.ToArray();
    }

    /// <summary>上から見た形：奥の辺（z = 0）はまっすぐ、前の 2 つの角だけ半径 r で丸めた四角（z は 0〜depth）。</summary>
    internal static Vector2[] FrontRounded(float w, float depth, float r, int seg = 6)
    {
        var p = new List<Vector2> { new Vector2(-w / 2, 0), new Vector2(w / 2, 0) };
        for (int k = 0; k <= seg; k++) { float a = Mathf.PI / 2 * k / seg; p.Add(new Vector2(w / 2 - r + Mathf.Cos(a) * r, depth - r + Mathf.Sin(a) * r)); }
        for (int k = 0; k <= seg; k++) { float a = Mathf.PI / 2 + Mathf.PI / 2 * k / seg; p.Add(new Vector2(-w / 2 + r + Mathf.Cos(a) * r, depth - r + Mathf.Sin(a) * r)); }
        return p.ToArray();
    }

    /// <summary>
    /// 立てた板（正面が +Z）。poly は正面から見た形（x が横、y が上）。thick は前後の厚み（真ん中が原点）。
    /// 前の面の縁は丸める（Prism の上の角の丸み）。
    /// </summary>
    internal static void StandingSlab(Lp lp, Vector2[] poly, float thick, string color, string front = null)
    {
        var flat = new Vector2[poly.Length];
        for (int i = 0; i < poly.Length; i++) flat[i] = new Vector2(poly[i].x, -poly[i].y);
        lp.Push(Vector3.zero, new Vector3(90, 0, 0));
        lp.Prism(flat, -thick / 2f, thick / 2f, color, front ?? color, true);
        lp.Pop();
    }

    // ================================================================ 商品（足元の真ん中が原点、正面が +Z）
    internal static void Onigiri(Lp lp, Vector3 at, float yaw = 0)
    {
        lp.Push(at, new Vector3(0, yaw, 0));
        var tri = RoundPoly(new[] { new Vector2(-.045f, 0), new Vector2(.045f, 0), new Vector2(0, .076f) }, .016f, 3);
        StandingSlab(lp, tri, .034f, "rice");
        lp.Box(new Vector3(-.019f, 0, -.0185f), new Vector3(.019f, .035f, .0185f), "nori");                  // のり
        lp.FrontImage(new Vector3(0, .026f, .0192f), .034f, .034f, ImageRect(Img.StoreLabelOnigiri));          // 前のシール（Codex の絵）
        lp.Pop();
    }

    internal static void Bento(Lp lp, Vector3 at, float yaw = 0, int variant = 0)
    {
        lp.Push(at, new Vector3(0, yaw, 0));
        var tray = Chamfer(.17f, .12f, .022f);
        lp.Prism(tray, 0, .024f, "ink");
        lp.Loft(tray, new[] { (.024f, 1f), (.044f, .97f), (.05f, .9f) }, "bento_lid", null, false, .005f);
        // ふたを上から見た絵（中身が透けて見える。Codex の絵）
        lp.TopRectImage(new Vector3(0, .0562f, 0), .16f, .11f, ImageRect(variant % 2 == 0 ? Img.StoreLidBento1 : Img.StoreLidBento2));
        lp.Pop();
    }

    internal static void Bread(Lp lp, Vector3 at, float yaw = 0, string color = "bread", int img = 0)
    {
        lp.Push(at, new Vector3(0, yaw, 0));
        lp.Cushion(Chamfer(.15f, .09f, .035f), 0, .066f, color, color);
        // 袋の上の絵（ふくらみの上へ少しだけ浮かせる。軽い形は平らなので、ほぼ上の面）
        lp.TopRectImage(new Vector3(0, lp.Lite ? .0668f : .0748f, 0), .124f, .074f, ImageRect(img % 2 == 0 ? Img.StoreLabelBread1 : Img.StoreLabelBread2));
        lp.Pop();
    }

    internal static void Snack(Lp lp, Vector3 at, float yaw = 0, string color = "label_orange", int img = 0)
    {
        lp.Push(at, new Vector3(0, yaw, 0));
        lp.Push(Vector3.zero, new Vector3(90, 0, 0));
        lp.Cushion(Offset(Chamfer(.11f, .15f, .03f), new Vector2(0, -.075f)), -.022f, .022f, color, color);
        lp.Pop();
        Img[] art = { Img.StoreLabelSnack1, Img.StoreLabelSnack2, Img.StoreLabelSnack3, Img.StoreLabelSnack4 };
        lp.FrontImage(new Vector3(0, .075f, lp.Lite ? .0228f : .0276f), .096f, .12f, ImageRect(art[img % art.Length]));
        lp.Pop();
    }

    internal static void CupNoodle(Lp lp, Vector3 at, float yaw = 0, string color = "label_red", int img = 0)
    {
        lp.Push(at, new Vector3(0, yaw, 0));
        lp.Lathe(new[] { (.031f, 0f), (.036f, .03f), (.04f, .06f), (.043f, .08f), (.046f, .086f) }, 10,
            b => b == 1 ? "shelf_white" : b == 3 ? "milk_white" : color, 0, true, true);
        Img[] art = { Img.StoreLabelCup1, Img.StoreLabelCup2, Img.StoreLabelCup3 };
        lp.Wrap(.0436f, .03f, .078f, 10, ImageRect(art[img % art.Length]));
        lp.Pop();
    }

    static readonly Img[] DrinkArt = { Img.StoreLabelDrink1, Img.StoreLabelDrink2, Img.StoreLabelDrink3, Img.StoreLabelDrink4, Img.StoreLabelDrink5, Img.StoreLabelDrink6 };

    internal static void Bottle(Lp lp, Vector3 at, string cap = "label_blue", string label = "label_sky", string liquid = "tea", int img = 0)
    {
        lp.Push(at);
        lp.Lathe(new[] { (.025f, 0f), (.028f, .012f), (.028f, .06f), (.028f, .14f), (.02f, .178f), (.011f, .198f), (.011f, .214f) }, 8,
            b => b == 2 ? label : b == 5 ? cap : liquid, 0, true, true);
        lp.Wrap(.0286f, .062f, .138f, 8, ImageRect(DrinkArt[img % DrinkArt.Length]));      // ラベル（Codex の絵）
        lp.Pop();
    }

    internal static void Can(Lp lp, Vector3 at, string color = "label_red", int img = 0)
    {
        lp.Push(at);
        lp.Lathe(new[] { (.021f, 0f), (.024f, .007f), (.024f, .084f), (.019f, .092f) }, 8, b => b == 1 ? color : "steel", 0, true, true);
        lp.Wrap(.0246f, .012f, .08f, 8, ImageRect(DrinkArt[img % DrinkArt.Length]));
        lp.Pop();
    }

    internal static void Milk(Lp lp, Vector3 at, float yaw = 0, string band = "label_blue")
    {
        lp.Push(at, new Vector3(0, yaw, 0));
        lp.Sharp = true;
        lp.Box(new Vector3(-.035f, 0, -.035f), new Vector3(.035f, .13f, .035f), "milk_white");
        lp.Sharp = false;
        // 前と後ろの面に、牛乳パックの絵（Codex の絵）
        foreach (float yawFace in new[] { 0f, 180f })
        {
            lp.Push(Vector3.zero, new Vector3(0, yawFace, 0));
            lp.FrontImage(new Vector3(0, .066f, .0356f), .064f, .118f, ImageRect(Img.StoreLabelMilk));
            lp.Pop();
        }
        // 三角の屋根
        foreach (float s in new[] { 1f, -1f })
            lp.Quad(new Vector3(-.035f, .13f, s * .035f), new Vector3(.035f, .13f, s * .035f), new Vector3(.035f, .155f, 0), new Vector3(-.035f, .155f, 0), "milk_white", new Vector3(0, .5f, s));
        foreach (float s in new[] { 1f, -1f })
            lp.Tri(new Vector3(s * .035f, .13f, -.035f), new Vector3(s * .035f, .13f, .035f), new Vector3(s * .035f, .155f, 0), Sw("milk_white"), Sw("milk_white"), Sw("milk_white"), new Vector3(s, 0, 0));
        lp.Box(new Vector3(-.035f, .153f, -.006f), new Vector3(.035f, .168f, .006f), "milk_white");
        lp.Pop();
    }

    internal static void IceCup(Lp lp, Vector3 at, string color = "ice_pink")
    {
        lp.Push(at);
        lp.Lathe(new[] { (.03f, 0f), (.036f, .045f), (.038f, .05f) }, 10, b => b == 0 ? color : "milk_white", 0, true, true);
        lp.Pop();
    }

    /// <summary>種類の番号の商品を 1 つ（色の見本 v で少し変える）。</summary>
    internal static void StoreItem(Lp lp, int kind, Vector3 at, float yaw, int v)
    {
        string[] bottles = { "label_sky", "label_green", "label_orange", "label_red", "label_purple", "label_yellow" };
        string[] caps = { "label_blue", "label_red", "milk_white", "label_green" };
        string[] liquids = { "tea", "label_sky", "label_orange", "milk_white", "veg_green" };
        string[] snacks = { "label_orange", "label_red", "label_yellow", "label_blue", "label_purple", "label_green" };
        string[] cups = { "label_red", "label_orange", "label_yellow", "label_blue" };
        string[] breads = { "bread", "bread_dark", "egg", "bread" };
        string[] ices = { "ice_pink", "ice_choc", "milk_white", "label_green" };
        switch (kind)
        {
            case 0: Onigiri(lp, at, yaw); break;
            case 1: Bento(lp, at, yaw, v); break;
            case 2: Bread(lp, at, yaw, breads[v % breads.Length], v); break;
            case 3: Snack(lp, at, yaw, snacks[v % snacks.Length], v); break;
            case 4: CupNoodle(lp, at, yaw, cups[v % cups.Length], v); break;
            case 5: if (v % 3 == 2) Can(lp, at, bottles[(v / 3) % bottles.Length], v / 3 + 4); else Bottle(lp, at, caps[v % caps.Length], bottles[v % bottles.Length], liquids[v % liquids.Length], v); break;
            case 6: Milk(lp, at, yaw, v % 2 == 0 ? "label_blue" : "label_green"); break;
            case 7: IceCup(lp, at, ices[v % ices.Length]); break;
        }
    }

    /// <summary>1 つだけの商品（手に取ったときに飛ぶ物・レジの上に置く物）。1.3 倍。</summary>
    internal static Mesh StoreItemSolo(int kind) => Build("store_item_" + StoreKinds[kind], lp =>
    {
        lp.Push(Vector3.zero, default, Vector3.one * 1.3f);
        StoreItem(lp, kind, Vector3.zero, 0, kind);
        lp.Pop();
    });

    /// <summary>棚の 1 段に、商品を横へ並べる（x0〜x1、手前の z、奥へ rows 列）。</summary>
    static void Row(Lp lp, int kind, float x0, float x1, float y, float zFront, int rows, float yaw, int seed, float step = .1f, float rowStep = .1f)
    {
        int count = Mathf.Max(1, Mathf.FloorToInt((x1 - x0) / step));
        float pad = ((x1 - x0) - (count - 1) * step) / 2f;
        // 棚に並ぶ物は、軽い形（たくさん並ぶので）。奥の列は 2 列まで（手前の列に隠れて、ほとんど見えない）
        bool wasLite = lp.Lite;
        lp.Lite = true;
        rows = Mathf.Min(rows, 2);
        for (int r = 0; r < rows; r++)
            for (int i = 0; i < count; i++)
            {
                // 同じ種類が 2〜3 個ずつ並ぶ（棚らしく）。抜けを少し作る
                int v = seed + (i / 3) * 7 + r;
                if ((seed + i * 13 + r * 5) % 17 == 0) continue;
                float z = zFront - r * rowStep;
                StoreItem(lp, kind, new Vector3(x0 + pad + i * step, y, z), yaw, v);
            }
        lp.Lite = wasLite;
    }

    // ================================================================ 売り場の什器（足元の真ん中が原点、正面が +Z、長さは x）
    /// <summary>扉のない冷蔵ケース（おにぎり・お弁当）。上の 2 段がおにぎり、下の 2 段がお弁当。</summary>
    internal static Mesh StoreOpenChiller(float len, float h, float d) => Build($"store_chiller_{Mathf.RoundToInt(len * 100)}", lp =>
    {
        float x0 = -len / 2, x1 = len / 2, zb = -d / 2, zf = d / 2;
        lp.Box(new Vector3(x0 + .03f, 0, zb + .04f), new Vector3(x1 - .03f, .12f, zf - .03f), "ink");
        lp.Box(new Vector3(x0, .12f, zb), new Vector3(x1, .5f, zf), "shelf_white");
        lp.Box(new Vector3(x0 + .02f, .2f, zf - .005f), new Vector3(x1 - .02f, .42f, zf + .008f), "coral");
        foreach (float x in new[] { x0, x1 - .05f }) lp.Box(new Vector3(x, .12f, zb), new Vector3(x + .05f, h, zf), "shelf_white");
        lp.Box(new Vector3(x0 + .05f, .5f, zb), new Vector3(x1 - .05f, h - .18f, zb + .05f), "steel_dark");
        lp.Box(new Vector3(x0 + .06f, .55f, zb + .05f), new Vector3(x1 - .06f, h - .22f, zb + .06f), "cooler_glow", true);
        lp.Box(new Vector3(x0, h - .18f, zb), new Vector3(x1, h, zf - .1f), "shelf_white");
        lp.Box(new Vector3(x0, h - .2f, zf - .14f), new Vector3(x1, h - .04f, zf - .08f), "coral");
        lp.Box(new Vector3(x0 + .06f, h - .21f, zb + .2f), new Vector3(x1 - .06f, h - .19f, zf - .16f), "panel_glow", true);
        float[] levels = { .5f, .92f, 1.26f, 1.58f };
        for (int k = 1; k < levels.Length; k++)
        {
            float y = levels[k];
            float front = zf - .1f - k * .04f;      // 上ほど奥（下の段が見える）
            lp.Box(new Vector3(x0 + .05f, y - .02f, zb + .06f), new Vector3(x1 - .05f, y, front), "shelf_white");
            lp.Box(new Vector3(x0 + .05f, y - .05f, front - .01f), new Vector3(x1 - .05f, y + .012f, front + .012f), "label_yellow");
        }
        for (int k = 0; k < levels.Length; k++)
        {
            float front = (k == 0 ? zf - .12f : zf - .1f - k * .04f) - .06f;
            int kind = k < 2 ? 1 : 0;
            Row(lp, kind, x0 + .1f, x1 - .1f, levels[k] + .002f, front, kind == 1 ? 2 : 3, 0, 11 * k + 3, kind == 1 ? .19f : .1f, kind == 1 ? .14f : .055f);
        }
    });

    /// <summary>壁の棚（パン）。</summary>
    internal static Mesh StoreBreadShelf(float len, float h, float d) => Build($"store_bread_{Mathf.RoundToInt(len * 100)}", lp =>
    {
        float x0 = -len / 2, x1 = len / 2, zb = -d / 2, zf = d / 2;
        lp.Box(new Vector3(x0, 0, zb), new Vector3(x1, .14f, zf), "ink");
        lp.Box(new Vector3(x0, .14f, zb), new Vector3(x1, h, zb + .04f), "shelf_white");
        foreach (float x in new[] { x0, x1 - .04f }) lp.Box(new Vector3(x, .14f, zb), new Vector3(x + .04f, h, zf), "shelf_white");
        lp.Box(new Vector3(x0, h - .06f, zb), new Vector3(x1, h, zf + .02f), "coral");
        float[] levels = { .14f, .48f, .82f, 1.16f };
        foreach (float y in levels)
        {
            lp.Box(new Vector3(x0 + .04f, y - .02f, zb + .04f), new Vector3(x1 - .04f, y, zf - .02f), "shelf_white");
            lp.Box(new Vector3(x0 + .04f, y - .045f, zf - .03f), new Vector3(x1 - .04f, y + .01f, zf - .01f), "label_yellow");
        }
        for (int k = 0; k < levels.Length; k++) Row(lp, 2, x0 + .12f, x1 - .12f, levels[k] + .002f, zf - .1f, 2, 0, 5 * k + 1, .17f, .14f);
    });

    /// <summary>両側に商品が並ぶ棚（ゴンドラ）。kind の商品を両側に。端は丸い。</summary>
    internal static Mesh StoreGondola(float len, float h, float w, int kind) => Build($"store_gondola_{StoreKinds[kind]}_{Mathf.RoundToInt(len * 100)}", lp =>
    {
        float x0 = -len / 2, x1 = len / 2;
        lp.Prism(Chamfer(len, w, .12f), 0, .13f, "ink");
        lp.Box(new Vector3(x0 + .04f, .13f, -.03f), new Vector3(x1 - .04f, h - .12f, .03f), "shelf_white");
        // 端：丸い板
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s < 0 ? x0 : x1;
            lp.Push(new Vector3(x - s * .03f, 0, 0), new Vector3(0, 90, 0));
            StandingSlab(lp, RoundPoly(new[] { new Vector2(-w / 2, .13f), new Vector2(w / 2, .13f), new Vector2(w / 2, h), new Vector2(-w / 2, h) }, .1f, 4), .06f, "shelf_white");
            lp.Pop();
            lp.Box(new Vector3(x - (s < 0 ? -.005f : .065f), h - .3f, -w / 2 + .06f), new Vector3(x + (s < 0 ? .065f : -.005f), h - .06f, w / 2 - .06f), "coral");
        }
        // 端の小さな棚（エンド）：外へ 0.28m 出して、2 段に同じ商品を並べる
        foreach (float s in new[] { -1f, 1f })
        {
            float x = s < 0 ? x0 : x1;
            lp.Push(new Vector3(x, 0, 0), new Vector3(0, s > 0 ? 90 : -90, 0));
            lp.Box(new Vector3(-w / 2 + .05f, 0, 0), new Vector3(w / 2 - .05f, .12f, .3f), "ink");
            foreach (float y in new[] { .12f, .58f })
            {
                lp.Box(new Vector3(-w / 2 + .05f, y, .03f), new Vector3(w / 2 - .05f, y + .02f, .3f), "shelf_white");
                lp.Box(new Vector3(-w / 2 + .05f, y - .03f, .29f), new Vector3(w / 2 - .05f, y + .025f, .31f), "label_yellow");
                Row(lp, kind, -w / 2 + .12f, w / 2 - .12f, y + .022f, .2f, kind == 3 ? 1 : 2, 0, s > 0 ? 31 : 37, kind == 3 ? .13f : .1f, .1f);
            }
            lp.Pop();
        }
        float[] levels = { .13f, .45f, .77f, 1.09f };
        foreach (float side in new[] { 1f, -1f })
        {
            foreach (float y in levels)
            {
                float zIn = side * .03f, zOut = side * (w / 2 - .02f);
                lp.Box(new Vector3(x0 + .06f, y - .02f, Mathf.Min(zIn, zOut)), new Vector3(x1 - .06f, y, Mathf.Max(zIn, zOut)), "shelf_white");
                float zr = side * (w / 2 - .01f);
                lp.Box(new Vector3(x0 + .06f, y - .045f, zr - .012f), new Vector3(x1 - .06f, y + .01f, zr + .012f), "label_yellow");
            }
            for (int k = 0; k < levels.Length; k++)
            {
                float front = side * (w / 2 - .08f);
                lp.Push(Vector3.zero, new Vector3(0, side > 0 ? 0 : 180, 0));
                Row(lp, kind, x0 + .12f, x1 - .12f, levels[k] + .002f, w / 2 - .08f, kind == 3 ? 2 : 3, 0, 7 * k + (side > 0 ? 2 : 9), kind == 3 ? .13f : .1f, .1f);
                lp.Pop();
            }
        }
    });

    /// <summary>扉つきの冷蔵ケース（飲み物・牛乳）。doors 枚の扉。いちばん右の扉は牛乳。ガラスは StoreCoolerGlass。</summary>
    internal static Mesh StoreDrinkCooler(int doors, float doorW, float h, float d) => Build($"store_cooler_{doors}", lp =>
    {
        float len = doors * doorW, x0 = -len / 2, x1 = len / 2, zb = -d / 2, zf = d / 2;
        lp.Box(new Vector3(x0, 0, zb), new Vector3(x1, .2f, zf), "ink");
        lp.Box(new Vector3(x0, h - .32f, zb), new Vector3(x1, h, zf), "shelf_white");
        lp.Box(new Vector3(x0 + .02f, h - .27f, zf - .005f), new Vector3(x1 - .02f, h - .1f, zf + .01f), "coral");
        lp.Box(new Vector3(x0, .2f, zb), new Vector3(x1, h - .32f, zb + .05f), "steel_dark");
        lp.Box(new Vector3(x0 + .04f, .25f, zb + .05f), new Vector3(x1 - .04f, h - .36f, zb + .06f), "cooler_glow", true);
        foreach (float x in new[] { x0, x1 - .06f }) lp.Box(new Vector3(x, .2f, zb), new Vector3(x + .06f, h - .32f, zf), "shelf_white");
        // 扉の枠と取っ手
        for (int i = 0; i <= doors; i++)
        {
            float x = x0 + i * doorW;
            lp.Box(new Vector3(x - .03f, .2f, zf - .05f), new Vector3(x + .03f, h - .32f, zf), "steel");
            if (i < doors) lp.Box(new Vector3(x + doorW - .14f, .8f, zf), new Vector3(x + doorW - .11f, 1.5f, zf + .035f), "steel_dark");
        }
        lp.Box(new Vector3(x0, .2f, zf - .05f), new Vector3(x1, .26f, zf), "steel");
        float[] levels = { .28f, .64f, 1.0f, 1.36f, 1.72f };
        foreach (float y in levels)
            lp.Box(new Vector3(x0 + .06f, y - .015f, zb + .06f), new Vector3(x1 - .06f, y, zf - .08f), "steel");
        for (int i = 0; i < doors; i++)
        {
            float dx0 = x0 + i * doorW + .08f, dx1 = x0 + (i + 1) * doorW - .08f;
            int kind = i == doors - 1 ? 6 : 5;
            for (int k = 0; k < levels.Length; k++)
                Row(lp, kind, dx0, dx1, levels[k] + .001f, zf - .14f, 3, 0, i * 19 + k * 5, kind == 6 ? .09f : .075f, .085f);
        }
    });

    /// <summary>冷蔵ケースの扉のガラス（透けるマテリアルで置く）。</summary>
    internal static Mesh StoreCoolerGlass(int doors, float doorW, float h, float d) => Build($"store_cooler_glass_{doors}", lp =>
    {
        lp.Sharp = true;
        float len = doors * doorW, x0 = -len / 2, zf = d / 2;
        for (int i = 0; i < doors; i++)
            lp.Box(new Vector3(x0 + i * doorW + .03f, .26f, zf - .03f), new Vector3(x0 + (i + 1) * doorW - .03f, h - .32f, zf - .02f), "glass_dark");
    });

    /// <summary>アイスのケース（ふたはガラス。StoreFreezerGlass）。</summary>
    internal static Mesh StoreFreezer(float w, float d, float h) => Build("store_freezer", lp =>
    {
        var outer = Chamfer(w, d, .12f);
        lp.Prism(Chamfer(w - .06f, d - .06f, .1f), 0, .1f, "ink");
        lp.Prism(outer, .1f, h - .06f, "shelf_white");
        lp.Prism(Chamfer(w + .01f, d + .01f, .125f), .2f, .32f, "coral");
        lp.Ring(Chamfer(w, d, .12f), Chamfer(w - .1f, d - .1f, .08f), h - .06f, h, "steel");
        lp.Prism(Chamfer(w - .1f, d - .1f, .08f), h - .07f, h - .065f, "cooler_glow", null, false, 1f, true);
        for (int r = 0; r < 4; r++)
            for (int i = 0; i < 9; i++)
            {
                float x = -w / 2 + .14f + i * ((w - .28f) / 8f), z = -d / 2 + .16f + r * ((d - .32f) / 3f);
                if ((i + r * 3) % 11 == 5) continue;
                lp.Lite = true;
                IceCup(lp, new Vector3(x, h - .065f, z), new[] { "ice_pink", "ice_choc", "milk_white", "label_green" }[(i / 2 + r) % 4]);
                lp.Lite = false;
            }
    });

    internal static Mesh StoreFreezerGlass(float w, float d, float h) => Build("store_freezer_glass", lp =>
    {
        lp.Sharp = true;
        lp.Box(new Vector3(-w / 2 + .05f, h + .005f, -d / 2 + .05f), new Vector3(w / 2 - .05f, h + .012f, d / 2 - .05f), "glass_dark");
    });

    /// <summary>レジのカウンター（前の面にさんご色の板、上はクリームの天板）。</summary>
    internal static Mesh StoreCounter(float len, float d, float h) => Build($"store_counter_{Mathf.RoundToInt(len * 100)}", lp =>
    {
        float x0 = -len / 2, x1 = len / 2;
        lp.Prism(Chamfer(len - .06f, d - .1f, .05f), 0, .1f, "ink");
        lp.Prism(Chamfer(len, d, .06f), .1f, h - .05f, "store_cream");
        lp.Box(new Vector3(x0 + .08f, .22f, d / 2 - .01f), new Vector3(x1 - .08f, h - .18f, d / 2 + .012f), "coral");
        lp.Box(new Vector3(x0 + .08f, h - .16f, d / 2 - .01f), new Vector3(x1 - .08f, h - .12f, d / 2 + .014f), "coral_pale");
        lp.Prism(Chamfer(len + .06f, d + .1f, .07f), h - .05f, h, "shelf_white");
    });

    /// <summary>L 字の脇のカウンター（奥へのびる短い部分）。</summary>
    internal static Mesh StoreSideCounter(float len, float d, float h) => Build($"store_sidecounter_{Mathf.RoundToInt(len * 100)}", lp =>
    {
        lp.Prism(Chamfer(len, d, .05f), 0, h - .05f, "store_cream");
        lp.Prism(Chamfer(len + .04f, d + .06f, .06f), h - .05f, h, "shelf_white");
    });

    /// <summary>レジ（台・お客さん向けの画面・お店の人向けの画面・読み取りの台）。原点はカウンターの上。正面（+Z）がお客さん。</summary>
    internal static Mesh StoreRegister() => Build("store_register", lp =>
    {
        lp.Box(new Vector3(-.2f, 0, -.16f), new Vector3(.2f, .1f, .16f), "ink");
        lp.Box(new Vector3(-.17f, .1f, -.14f), new Vector3(.17f, .13f, .1f), "metal_dark");
        // 支柱と、お客さん向けの画面（+Z）・お店の人向けの画面（-Z）
        lp.Box(new Vector3(-.02f, .1f, -.03f), new Vector3(.02f, .34f, .01f), "metal_dark");
        lp.Push(new Vector3(0, .38f, .03f), new Vector3(-12, 0, 0));
        lp.Box(new Vector3(-.15f, -.08f, -.015f), new Vector3(.15f, .08f, .015f), "ink");
        lp.Pop();
        lp.Push(new Vector3(0, .28f, -.08f), new Vector3(20, 0, 0));
        lp.Box(new Vector3(-.14f, -.07f, -.012f), new Vector3(.14f, .07f, .012f), "ink");
        lp.Box(new Vector3(-.12f, -.055f, -.0135f), new Vector3(.12f, .055f, -.011f), "sky", true);
        lp.Pop();
        // お店の人の側のキー
        for (int i = 0; i < 4; i++)
            for (int j = 0; j < 3; j++)
                lp.Box(new Vector3(-.12f + i * .07f, .13f, -.12f + j * .05f), new Vector3(-.08f + i * .07f, .145f, -.09f + j * .05f), i == 3 ? "coral" : "steel");
    });

    /// <summary>レジの引き出し（お金の入れ物）。原点は引き出しの真ん中の下。+Z へ開く。</summary>
    internal static Mesh StoreDrawer() => Build("store_drawer", lp =>
    {
        lp.Box(new Vector3(-.17f, 0, -.14f), new Vector3(.17f, .07f, .14f), "ink");
        for (int i = 0; i < 5; i++) lp.Box(new Vector3(-.15f + i * .06f, .06f, -.02f), new Vector3(-.105f + i * .06f, .072f, .11f), i < 3 ? "gold" : "paper");
    });

    /// <summary>読み取りの台（黒いガラスの面）。光る線は StoreScanGlow。</summary>
    internal static Mesh StoreScanner() => Build("store_scanner", lp =>
    {
        lp.Box(new Vector3(-.14f, 0, -.11f), new Vector3(.14f, .03f, .11f), "metal_dark");
        lp.Box(new Vector3(-.12f, .03f, -.09f), new Vector3(.12f, .036f, .09f), "glass_dark");
    });

    internal static Mesh StoreScanGlow() => Build("store_scan_glow", lp =>
    {
        lp.Sharp = true;
        lp.Box(new Vector3(-.11f, .037f, -.004f), new Vector3(.11f, .041f, .004f), "scan_glow", true);
        lp.Box(new Vector3(-.004f, .037f, -.08f), new Vector3(.004f, .041f, .08f), "scan_glow", true);
    });

    /// <summary>レシート（細長い紙）。原点は下の真ん中。</summary>
    internal static Mesh StoreReceipt() => Build("store_receipt", lp =>
    {
        lp.Sharp = true;
        lp.Box(new Vector3(-.032f, 0, -.001f), new Vector3(.032f, .14f, .001f), "white");
        for (int i = 0; i < 6; i++) lp.Box(new Vector3(-.024f, .02f + i * .018f, .0011f), new Vector3(i % 2 == 0 ? .02f : .008f, .026f + i * .018f, .0016f), "grey");
    });

    /// <summary>レジ袋（白。さんご色の印）。原点は底の真ん中。</summary>
    internal static Mesh StoreBag() => Build("store_bag", lp =>
    {
        var poly = Chamfer(.26f, .14f, .05f);
        lp.Loft(poly, new[] { (0f, .86f), (.04f, 1f), (.2f, 1.03f), (.27f, .96f) }, "milk_white", null, true, .02f);
        // 中から見える商品の頭
        lp.Cushion(Offset(Chamfer(.12f, .07f, .03f), new Vector2(-.05f, 0)), .25f, .3f, "bread", "bread");
        lp.Lathe(new[] { (.012f, .24f), (.012f, .31f) }, 8, "label_blue", 0, true);
        // 取っ手（白い輪）
        foreach (float s in new[] { -1f, 1f })
        {
            Vector3 a = new Vector3(-.07f, .27f, s * .045f), b = new Vector3(-.05f, .36f, s * .045f), c = new Vector3(.05f, .36f, s * .045f), e = new Vector3(.07f, .27f, s * .045f);
            lp.Bar(a, b, .012f, "milk_white"); lp.Bar(b, c, .012f, "milk_white"); lp.Bar(c, e, .012f, "milk_white");
        }
        // 店の印（Codex の絵）
        lp.FaceImage(Offset(Chamfer(.07f, .07f, .014f), new Vector2(0, .135f)), .0745f, ImageRect(Img.StoreStoreLogo), new Rect(-.035f, .1f, .07f, .07f));
    });

    /// <summary>温かい物のケース（ガラスは StoreHotCaseGlass）。</summary>
    internal static Mesh StoreHotCase() => Build("store_hotcase", lp =>
    {
        lp.Box(new Vector3(-.36f, 0, -.22f), new Vector3(.36f, .08f, .22f), "steel_dark");
        lp.Box(new Vector3(-.36f, .42f, -.22f), new Vector3(.36f, .48f, .22f), "coral");
        lp.Box(new Vector3(-.3f, .405f, -.16f), new Vector3(.3f, .42f, .16f), "warm", true);
        foreach (float x in new[] { -.36f, .33f }) lp.Box(new Vector3(x, .08f, -.22f), new Vector3(x + .03f, .42f, .22f), "steel");
        lp.Box(new Vector3(-.33f, .2f, -.18f), new Vector3(.33f, .215f, .16f), "steel");
        for (int i = 0; i < 6; i++)
        {
            lp.Cushion(Offset(Chamfer(.08f, .06f, .025f), new Vector2(-.25f + i * .1f, .05f)), .08f, .13f, "bread_dark", "bread");
            lp.Cushion(Offset(Chamfer(.07f, .05f, .02f), new Vector2(-.25f + i * .1f, -.02f)), .215f, .26f, i % 2 == 0 ? "bread" : "label_orange", "bread");
        }
    });

    internal static Mesh StoreHotCaseGlass() => Build("store_hotcase_glass", lp =>
    {
        lp.Sharp = true;
        lp.Box(new Vector3(-.33f, .08f, .2f), new Vector3(.33f, .42f, .21f), "glass_dark");
        foreach (float x in new[] { -.345f, .345f }) lp.Box(new Vector3(x - .005f, .08f, -.2f), new Vector3(x + .005f, .42f, .2f), "glass_dark");
    });

    /// <summary>うしろの棚（電池・くすり・文房具など。色の小箱）。お店の人の側にある。</summary>
    internal static Mesh StoreBackShelf(float len, float h, float d) => Build($"store_backshelf_{Mathf.RoundToInt(len * 100)}", lp =>
    {
        float x0 = -len / 2, x1 = len / 2;
        lp.Box(new Vector3(x0, 0, -d / 2), new Vector3(x1, h, -d / 2 + .04f), "shelf_white");
        lp.Box(new Vector3(x0, 0, -d / 2), new Vector3(x1, .9f, d / 2), "store_cream");
        lp.Box(new Vector3(x0 - .01f, .9f, -d / 2), new Vector3(x1 + .01f, .94f, d / 2 + .02f), "shelf_white");
        lp.Box(new Vector3(x0, h - .14f, -d / 2), new Vector3(x1, h, d / 2 - .1f), "coral");
        // 3 段：小箱（お菓子のラベル）・小さいカップめん・小さいボトル（届いた絵を使い回す）。たくさん並ぶので軽い形
        Img[] snackArt = { Img.StoreLabelSnack1, Img.StoreLabelSnack2, Img.StoreLabelSnack3, Img.StoreLabelSnack4 };
        string[] boxColors = { "label_orange", "label_red", "label_yellow", "coral_pale" };
        lp.Lite = true;
        for (int row = 0; row < 3; row++)
        {
            float y = 1.0f + row * .24f;
            lp.Box(new Vector3(x0 + .03f, y - .015f, -d / 2 + .04f), new Vector3(x1 - .03f, y, -d / 2 + .22f), "shelf_white");
            lp.Box(new Vector3(x0 + .03f, y - .035f, -d / 2 + .215f), new Vector3(x1 - .03f, y + .008f, -d / 2 + .225f), "label_yellow");
            float zc = -d / 2 + .13f;
            if (row == 0)
            {
                int n = (int)((len - .1f) / .07f);
                for (int k = 0; k < n; k++)
                {
                    float x = x0 + .08f + k * .07f;
                    int v = (k / 2 + 3) % 4;
                    lp.Box(new Vector3(x - .027f, y, zc - .04f), new Vector3(x + .027f, y + .075f, zc + .04f), boxColors[v]);
                    lp.FrontImage(new Vector3(x, y + .0375f, zc + .0405f), .054f, .07f, ImageRect(snackArt[v]));
                }
            }
            else if (row == 1)
            {
                int n = (int)((len - .1f) / .075f);
                for (int k = 0; k < n; k++)
                {
                    lp.Push(new Vector3(x0 + .08f + k * .075f, y, zc), default, Vector3.one * .78f);
                    CupNoodle(lp, Vector3.zero, 0, new[] { "label_red", "label_orange", "label_yellow" }[(k / 2) % 3], k / 2);
                    lp.Pop();
                }
            }
            else
            {
                int n = (int)((len - .1f) / .055f);
                for (int k = 0; k < n; k++)
                {
                    lp.Push(new Vector3(x0 + .07f + k * .055f, y, zc), default, Vector3.one * .62f);
                    Bottle(lp, Vector3.zero, new[] { "label_blue", "label_red", "milk_white" }[k % 3], "label_sky", new[] { "tea", "label_sky", "label_orange" }[(k / 2) % 3], k / 2);
                    lp.Pop();
                }
            }
        }
        lp.Lite = false;
    });

    /// <summary>セルフのコーヒーの台（台・機械・カップ）。</summary>
    internal static Mesh StoreCoffee(float w, float d, float h) => Build("store_coffee", lp =>
    {
        lp.Prism(Chamfer(w, d, .06f), 0, h - .04f, "store_cream");
        lp.Box(new Vector3(-w / 2 + .06f, .18f, d / 2 - .01f), new Vector3(w / 2 - .06f, h - .2f, d / 2 + .012f), "coral");
        lp.Prism(Chamfer(w + .04f, d + .06f, .06f), h - .04f, h, "shelf_white");
        // 機械（角の丸い黒い箱、光る画面、カップを置く所）
        lp.Push(new Vector3(-.1f, h, -.05f));
        lp.Prism(Chamfer(.42f, .44f, .08f), 0, .58f, "ink");
        lp.Box(new Vector3(-.15f, .38f, .2f), new Vector3(.15f, .5f, .225f), "sky", true);
        lp.Box(new Vector3(-.12f, .06f, .14f), new Vector3(.12f, .08f, .23f), "metal_dark");
        lp.Box(new Vector3(-.05f, .2f, .16f), new Vector3(.05f, .3f, .23f), "metal_dark");
        lp.Lathe(new[] { (.03f, .08f), (.038f, .17f) }, 10, "milk_white", 0, true);
        lp.Pop();
        // カップの山とふた
        lp.Push(new Vector3(.3f, h, 0));
        lp.Lathe(new[] { (.03f, 0f), (.04f, .22f), (.042f, .23f) }, 10, b => b == 0 ? "milk_white" : "coral", 0, true);
        lp.Pop();
        lp.Push(new Vector3(.3f, h, -.13f));
        lp.Lathe(new[] { (.04f, 0f), (.04f, .08f) }, 10, "ink", 0, true);
        lp.Pop();
    });

    /// <summary>窓ぎわの雑誌の棚（3 段、斜めに立てた雑誌）。</summary>
    internal static Mesh StoreMagazineRack(float len, float h, float d) => Build($"store_magazine_{Mathf.RoundToInt(len * 100)}", lp =>
    {
        float x0 = -len / 2, x1 = len / 2;
        lp.Box(new Vector3(x0, 0, -d / 2), new Vector3(x1, .3f, d / 2), "store_cream");
        lp.Box(new Vector3(x0, .3f, -d / 2), new Vector3(x1, h, -d / 2 + .04f), "shelf_white");
        foreach (float x in new[] { x0, x1 - .04f }) lp.Box(new Vector3(x, 0, -d / 2), new Vector3(x + .04f, h, d / 2), "shelf_white");
        string[] colors = { "label_red", "label_sky", "label_yellow", "coral_pale", "label_green", "label_purple", "milk_white", "label_orange" };
        for (int t = 0; t < 3; t++)
        {
            float y = .32f + t * .22f, z = d / 2 - .06f - t * .08f;
            lp.Box(new Vector3(x0 + .04f, y - .02f, z - .06f), new Vector3(x1 - .04f, y, z + .04f), "shelf_white");
            int n = (int)((len - .12f) / .23f);
            for (int i = 0; i < n; i++)
            {
                float x = x0 + .14f + i * .23f;
                lp.Push(new Vector3(x, y, z - .02f), new Vector3(-14, 0, 0));
                lp.Sharp = true;
                lp.Box(new Vector3(-.1f, 0, -.004f), new Vector3(.1f, .27f, .004f), colors[(i * 3 + t) % colors.Length]);
                lp.Box(new Vector3(-.08f, .18f, .0041f), new Vector3(.08f, .24f, .006f), "white");
                lp.Sharp = false;
                lp.Pop();
            }
        }
    });

    /// <summary>重ねたカゴ（さんご色）。</summary>
    internal static Mesh StoreBasketStack() => Build("store_baskets", lp =>
    {
        var outer = Chamfer(.44f, .32f, .05f);
        lp.Prism(Chamfer(.4f, .28f, .04f), 0, .04f, "coral_dark");
        lp.Loft(outer, new[] { (.04f, .9f), (.36f, 1f) }, "coral", null, false);
        for (int i = 0; i < 6; i++) lp.Ring(Chamfer(.445f, .325f, .052f), Chamfer(.43f, .31f, .05f), .1f + i * .045f, .112f + i * .045f, "coral_dark");
        lp.Ring(Chamfer(.45f, .33f, .055f), Chamfer(.4f, .28f, .04f), .36f, .38f, "coral_dark");
        lp.Prism(Chamfer(.4f, .28f, .04f), .3f, .31f, "coral_dark");
        foreach (float s in new[] { -1f, 1f })
        {
            lp.Bar(new Vector3(-.16f, .38f, s * .13f), new Vector3(-.08f, .46f, s * .13f), .014f, "ink");
            lp.Bar(new Vector3(-.08f, .46f, s * .13f), new Vector3(.08f, .46f, s * .13f), .014f, "ink");
            lp.Bar(new Vector3(.08f, .46f, s * .13f), new Vector3(.16f, .38f, s * .13f), .014f, "ink");
        }
    });

    /// <summary>天井の明かり（角の丸い枠と、光る面）。原点は天井の面（下へのびる）。</summary>
    internal static Mesh StoreCeilingPanel(float w, float d) => Build($"store_panel_{Mathf.RoundToInt(w * 100)}", lp =>
    {
        lp.Prism(Chamfer(w, d, .08f), -.06f, 0, "steel");
        lp.Prism(Chamfer(w - .08f, d - .08f, .05f), -.068f, -.06f, "panel_glow", null, true, 1f, true);
    });

    /// <summary>お店の人の側への扉（窓の丸い開き戸。見た目だけ）。</summary>
    internal static Mesh StoreStaffDoor() => Build("store_staffdoor", lp =>
    {
        lp.Box(new Vector3(-.5f, 0, -.04f), new Vector3(.5f, 2.15f, .04f), "metal_dark");
        lp.Box(new Vector3(-.44f, .06f, .04f), new Vector3(.44f, 2.09f, .05f), "steel_dark");
        lp.Push(new Vector3(0, 1.55f, .05f));
        lp.Push(Vector3.zero, new Vector3(90, 0, 0));
        lp.Lathe(new[] { (.15f, 0f), (.15f, .01f) }, 16, "steel", 0, true);
        lp.Pop();
        lp.Pop();
        lp.Box(new Vector3(.3f, .95f, .05f), new Vector3(.36f, 1.05f, .09f), "steel");
    });

    /// <summary>お店の印（オリジナル。Codex の絵 store_logo）：角の丸い濃いさんご色の板の前に、印の絵を、同じ角丸の形で貼る。正面が +Z。原点は下の真ん中。</summary>
    internal static Mesh StoreEmblem(float size) => Build($"store_emblem_{Mathf.RoundToInt(size * 100)}", lp =>
    {
        float s = size / 2;
        StandingSlab(lp, Offset(Chamfer(size * 1.04f, size * 1.04f, size * .22f), new Vector2(0, s)), .05f, "coral_dark");
        lp.FaceImage(Offset(Chamfer(size, size, size * .2f), new Vector2(0, s)), .0262f, ImageRect(Img.StoreStoreLogo), new Rect(-s, 0, size, size));
    });

    // ---- ポスター（2026/11/24）：近くで見てもくっきりするよう、ポスター 2 枚だけを別のテクスチャにする（1024×1024 に左右 2 枚、512×683）
    const string PosterTexturePath = "Assets/_Art/Store/store_posters.png";
    const string PosterSheetSource = "ArtSource/Store/atlas/store_posters.png";

    /// <summary>ポスターの台紙（クリームの紙。アトラスの色）。原点は下の真ん中、+Z が表。絵の面は StorePosterFace で別に置く。</summary>
    internal static Mesh StorePoster(float w, float h) => Build($"store_poster_board_{Mathf.RoundToInt(w * 100)}", lp =>
    {
        lp.Sharp = true;
        lp.Box(new Vector3(-w / 2 - .015f, -.015f, -.006f), new Vector3(w / 2 + .015f, h + .015f, 0), "store_cream");
    });

    /// <summary>ポスターの絵の面（StorePosterMaterial で置く）。img は 0 か 1。</summary>
    internal static Mesh StorePosterFace(float w, float h, int img) => Build($"store_poster_face_{img}_{Mathf.RoundToInt(w * 100)}", lp =>
    {
        const float inset = 2f / 1024f, v1 = 683f / 1024f;
        var r = new Rect(img * .5f + inset, inset, .5f - inset * 2, v1 - inset * 2);
        lp.FrontImage(new Vector3(0, h / 2, .001f), w, h, r);
    });

    /// <summary>ポスター専用のマテリアル（Assets/_Materials/Store/store_posters.mat）。絵は ArtSource/Store/make_store_art.py が作った 1 枚を写す。</summary>
    internal static Material StorePosterMaterial()
    {
        System.IO.Directory.CreateDirectory("Assets/_Art/Store");
        System.IO.Directory.CreateDirectory("Assets/_Materials/Store");
        if (System.IO.File.Exists(PosterSheetSource)) CopyIfChanged(PosterSheetSource, PosterTexturePath);
        var importer = (UnityEditor.TextureImporter)UnityEditor.AssetImporter.GetAtPath(PosterTexturePath);
        if (importer != null && (importer.maxTextureSize != 1024 || !importer.mipmapEnabled || importer.anisoLevel != 8 || importer.filterMode != FilterMode.Trilinear
            || importer.wrapMode != TextureWrapMode.Clamp || importer.textureCompression != UnityEditor.TextureImporterCompression.CompressedHQ))
        {
            importer.maxTextureSize = 1024; importer.mipmapEnabled = true; importer.anisoLevel = 8; importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp; importer.textureCompression = UnityEditor.TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        const string path = "Assets/_Materials/Store/store_posters.mat";
        var mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); UnityEditor.AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>(PosterTexturePath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", .18f); mat.SetFloat("_Metallic", 0);
        mat.SetFloat("_EnvironmentReflections", 0); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        mat.enableInstancing = true;
        UnityEditor.EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>のぼり（柱・重り・上の横棒・旗）。旗は両面に絵。原点は柱の足元。旗は柱の -X の側（手前から見て右）に下がり、+Z を向く。</summary>
    internal static Mesh StoreNobori(int img) => Build($"store_nobori_{img}", lp =>
    {
        const float h = 2.45f, fw = .46f, fh = 1.84f;
        lp.Lathe(new[] { (.11f, 0f), (.12f, .05f), (.1f, .14f), (.03f, .18f) }, 16, "ink", 0, true);
        lp.Lathe(new[] { (.017f, .14f), (.017f, h) }, 10, "steel_dark", 0, true);
        lp.Bar(new Vector3(0, h - .05f, 0), new Vector3(-(fw + .03f), h - .05f, 0), .016f, "steel_dark");
        Img art = img == 0 ? Img.StoreNobori1 : Img.StoreNobori2;
        Vector3 c = new Vector3(-(.03f + fw / 2), h - .07f - fh / 2, 0);
        lp.FrontImage(c + new Vector3(0, 0, .002f), fw, fh, ImageRect(art));
        lp.Push(Vector3.zero, new Vector3(0, 180, 0));
        lp.FrontImage(new Vector3(-c.x, c.y, .002f), fw, fh, ImageRect(art));
        lp.Pop();
    });

    /// <summary>看板の光る板（クリーム。角が丸い）。原点は下の真ん中。正面が +Z。</summary>
    internal static Mesh StoreSignPanel(float w, float h) => Build($"store_signpanel_{Mathf.RoundToInt(w * 100)}_{Mathf.RoundToInt(h * 100)}", lp =>
    {
        StandingSlab(lp, Offset(Chamfer(w + .08f, h + .08f, .14f), new Vector2(0, h / 2)), .08f, "coral_dark");
        lp.Push(new Vector3(0, 0, .04f));
        StandingSlab(lp, Offset(Chamfer(w, h, .1f), new Vector2(0, h / 2)), .02f, "sign_glow");
        lp.Pop();
    });

    /// <summary>入口のひさし（前の角が丸い板。上はさんご色、下はクリームで、下へ光る丸い明かり 2 つ）。原点は壁の面の下の真ん中。</summary>
    internal static Mesh StoreCanopy(float w, float depth, float thick) => Build($"store_canopy_{Mathf.RoundToInt(w * 100)}", lp =>
    {
        // 壁の面（z = 0）から前だけの形：前の 2 つの角だけを丸める（店の中へ入りこまない）
        lp.Prism(FrontRounded(w, depth, .45f), 0, thick, "store_cream", "coral");
        lp.Prism(FrontRounded(w + .04f, depth + .02f, .47f), thick - .03f, thick + .01f, "coral_dark", "coral");
        foreach (float x in new[] { -w * .25f, w * .25f })
        {
            lp.Push(new Vector3(x, -.002f, depth * .5f));
            lp.Cap(.11f, 0, 16, "panel_glow", false, 0, true);
            lp.Pop();
        }
    });

    /// <summary>正面の角の柱（角の丸い四角の柱）。</summary>
    internal static Mesh StorePillar(float w, float d, float h) => Build($"store_pillar_{Mathf.RoundToInt(w * 100)}_{Mathf.RoundToInt(h * 100)}", lp =>
        lp.Prism(Chamfer(w, d, .12f), 0, h, "store_cream"));

    /// <summary>看板の帯（上から見て角の丸い長い板。前と両脇へ回りこむ）。</summary>
    internal static Mesh StoreFascia(float w, float d, float h) => Build($"store_fascia_{Mathf.RoundToInt(w * 100)}", lp =>
        lp.Prism(Chamfer(w, d, .35f), 0, h, "coral"));

    /// <summary>店先のごみ箱（3 つ並び。ふたの色で分ける）。</summary>
    internal static Mesh StoreBins() => Build("store_bins", lp =>
    {
        string[] lids = { "label_blue", "label_yellow", "label_green" };
        for (int i = 0; i < 3; i++)
        {
            float x = (i - 1) * .52f;
            lp.Prism(Offset(Chamfer(.46f, .4f, .08f), new Vector2(x, 0)), 0, .82f, "store_cream");
            lp.Prism(Offset(Chamfer(.48f, .42f, .09f), new Vector2(x, 0)), .82f, .92f, lids[i]);
            lp.Box(new Vector3(x - .12f, .86f, .14f), new Vector3(x + .12f, .93f, .215f), "ink");
            lp.Box(new Vector3(x - .14f, .5f, .2f), new Vector3(x + .14f, .66f, .21f), lids[i]);
        }
    });

    /// <summary>屋上の室外機（角の丸い箱、ファンの丸）。</summary>
    internal static Mesh StoreRoofUnit() => Build("store_roof_unit", lp =>
    {
        lp.Prism(Chamfer(1.2f, .6f, .1f), 0, .8f, "steel");
        lp.Push(new Vector3(.18f, .4f, .3f));
        lp.Push(Vector3.zero, new Vector3(90, 0, 0));
        lp.Lathe(new[] { (.26f, 0f), (.26f, .01f) }, 20, "metal_dark", 0, true);
        lp.Pop();
        lp.Pop();
        lp.Box(new Vector3(-.5f, .1f, .3f), new Vector3(-.2f, .7f, .31f), "steel_dark");
    });

    // ================================================================ レジ打ちのバイト（2026/11/24）
    /// <summary>お客さん（NPC）。店の人と同じ丸い体で、服・顔・帽子の色と形を変えた 3 人（look 0〜2）。原点は足元、+Z が正面。</summary>
    internal static Mesh StoreShopper(int look) => Build($"store_shopper_{look}", lp =>
    {
        string[] coats = { "label_blue", "label_green", "label_purple" };
        string[] faces = { "teal_pale", "coral_pale", "label_sky" };
        string coat = coats[look % 3], face = faces[look % 3];
        lp.Lathe(new[] { (.22f, 0f), (.36f, .08f), (.44f, .38f), (.45f, .72f), (.42f, 1.0f), (.34f, 1.24f), (.2f, 1.4f), (0f, 1.46f) }, 10,
            b => b == 0 ? "ink" : b <= 2 ? coat : face, Mathf.PI / 10, false, true);
        // 上着の前の合わせと、ポケット
        lp.Box(new Vector3(-.015f, .14f, .43f), new Vector3(.015f, .8f, .455f), "ink");
        foreach (float x in new[] { -.22f, .22f }) lp.Box(new Vector3(x - .07f, .3f, .4f), new Vector3(x + .07f, .4f, .43f), "ink");
        // 目とほっぺ
        foreach (float x in new[] { -.14f, .14f })
        {
            lp.Box(new Vector3(x - .03f, 1.08f, .4f), new Vector3(x + .03f, 1.2f, .43f), "black");
            lp.Box(new Vector3(x * 1.75f - .035f, 1.0f, .37f), new Vector3(x * 1.75f + .035f, 1.04f, .4f), "pink_pale");
        }
        if (look % 3 == 0)
        {
            // つば付きの帽子
            lp.Lathe(new[] { (.37f, 1.27f), (.36f, 1.37f), (.28f, 1.46f), (.14f, 1.51f), (0f, 1.52f) }, 10, "label_red", Mathf.PI / 10, true);
            lp.Push(new Vector3(0, 1.29f, .22f), new Vector3(8, 0, 0));
            lp.Prism(Offset(Ellipse(.3f, .2f, 8), new Vector2(0, .14f)), 0, .025f, "label_red", "label_red", true);
            lp.Pop();
        }
        else if (look % 3 == 1)
        {
            // ニット帽と、てっぺんの玉
            lp.Lathe(new[] { (.38f, 1.24f), (.38f, 1.32f), (.33f, 1.44f), (.2f, 1.53f), (0f, 1.56f) }, 10, "store_cream", Mathf.PI / 10, true);
            lp.Lathe(new[] { (.39f, 1.22f), (.39f, 1.3f) }, 10, "label_orange", Mathf.PI / 10, false);
            lp.Lathe(new[] { (0f, 1.54f), (.07f, 1.57f), (.08f, 1.62f), (.06f, 1.67f), (0f, 1.69f) }, 8, "label_orange", 0, true);
        }
        else
        {
            // マフラー（首に巻いて、前に垂らす）
            lp.Lathe(new[] { (.43f, .96f), (.45f, 1.0f), (.43f, 1.06f) }, 10, "label_yellow", Mathf.PI / 10, false);
            lp.Box(new Vector3(.08f, .7f, .4f), new Vector3(.2f, .98f, .45f), "label_yellow");
        }
        // 短い手
        foreach (float sd in new[] { -1f, 1f })
        {
            lp.Push(new Vector3(sd * .44f, .72f, .1f), new Vector3(-30, 0, sd * -28));
            lp.Lathe(new[] { (.075f, 0f), (.09f, .1f), (.08f, .22f), (0f, .27f) }, 6, b => b == 2 ? face : coat, 0, false, true);
            lp.Pop();
        }
    });

    /// <summary>タイムカードの機械（時計の窓・カードの差し口・横にカード差し）。原点は下の真ん中、+Z が正面。</summary>
    internal static Mesh StoreTimeCard() => Build("store_timecard", lp =>
    {
        lp.Box(new Vector3(-.13f, 0, -.09f), new Vector3(.13f, .34f, .09f), "store_cream");
        lp.Box(new Vector3(-.13f, 0, -.09f), new Vector3(.13f, .05f, .092f), "coral");
        lp.Box(new Vector3(-.1f, .2f, .085f), new Vector3(.1f, .31f, .1f), "ink");
        lp.Box(new Vector3(-.075f, .225f, .1f), new Vector3(.075f, .285f, .104f), "label_sky", true);
        lp.Box(new Vector3(-.07f, .13f, .085f), new Vector3(.07f, .155f, .1f), "black");
        lp.Box(new Vector3(-.035f, .145f, .09f), new Vector3(.035f, .2f, .095f), "white");
        lp.Box(new Vector3(.14f, 0, -.07f), new Vector3(.25f, .24f, .07f), "steel_dark");
        string[] cards = { "white", "label_yellow", "white", "coral_pale" };
        for (int i = 0; i < cards.Length; i++)
            lp.Box(new Vector3(.152f + i * .024f, .14f, -.05f), new Vector3(.166f + i * .024f, .31f + (i % 2) * .02f, .05f), cards[i]);
    });

    /// <summary>店員の側の小さな画面（お釣りの候補を出す）。原点は画面の真ん中、-Z が表（店員の側）。下に支え。</summary>
    internal static Mesh StoreJobScreen() => Build("store_job_screen", lp =>
    {
        lp.Box(new Vector3(-.25f, -.13f, 0), new Vector3(.25f, .13f, .035f), "ink");
        lp.Sharp = true;
        lp.Box(new Vector3(-.02f, -.3f, .01f), new Vector3(.02f, -.13f, .03f), "steel_dark");
        lp.Box(new Vector3(-.08f, -.31f, -.04f), new Vector3(.08f, -.29f, .08f), "steel_dark");
    });

    /// <summary>「アルバイト募集」の札（さんご色の板と柱）。原点は柱の下、+Z が表。文字は別に置く。</summary>
    internal static Mesh StoreJobSign() => Build("store_job_sign", lp =>
    {
        lp.Box(new Vector3(-.28f, .32f, -.015f), new Vector3(.28f, .66f, .015f), "coral");
        lp.Box(new Vector3(-.25f, .35f, .015f), new Vector3(.25f, .63f, .02f), "store_cream");
        lp.Bar(new Vector3(0, 0, -.02f), new Vector3(0, .33f, -.02f), .025f, "steel_dark");
        lp.Box(new Vector3(-.08f, 0, -.08f), new Vector3(.08f, .02f, .04f), "steel_dark");
    });
}
#endif
