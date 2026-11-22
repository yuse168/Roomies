#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static RoomiesRestyleKit;
using Object = UnityEngine.Object;

/// <summary>
/// 部屋の小物（缶・瓶・観葉植物・電球・ピザと箱・雑誌の表紙・メモ・紙くず・ネコのネオン）の形（2026/11/23）。
/// 箱・球・円柱を積むのをやめ、小物ごとに 1 つの形にした（少し角の立った面で、なめらかにしない）。
/// 色と絵はアトラス（RoomiesRoomRestyleBuilder.PropsAtlas.cs）から取る。
/// </summary>
internal static partial class RoomiesRoomRestyleBuilder
{
    // ================================================================ メッシュを組む道具（面ごとに頂点を分けて、角の立った陰影にする）
    sealed class LP
    {
        readonly List<Vector3> v = new List<Vector3>(); readonly List<Vector2> uv = new List<Vector2>(); readonly List<int>[] tris;
        public LP(int submeshes = 1) { tris = new List<int>[submeshes]; for (int i = 0; i < submeshes; i++) tris[i] = new List<int>(); }

        /// <summary>三角形。outward の向きを表にする。</summary>
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward, int sub = 0)
        {
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { (b, c) = (c, b); (ub, uc) = (uc, ub); }
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); uv.Add(ua); uv.Add(ub); uv.Add(uc);
            tris[sub].Add(i); tris[sub].Add(i + 1); tris[sub].Add(i + 2);
        }
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 uvAll, Vector3 outward, int sub = 0) => Tri(a, b, c, uvAll, uvAll, uvAll, outward, sub);
        /// <summary>四角形（a→b→c→d が周の順）。</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward, int sub = 0)
        { Tri(a, b, c, ua, ub, uc, outward, sub); Tri(a, c, d, ua, uc, ud, outward, sub); }
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uvAll, Vector3 outward, int sub = 0) => Quad(a, b, c, d, uvAll, uvAll, uvAll, uvAll, outward, sub);
        public void Fill(Mesh m)
        {
            m.SetVertices(v); m.SetUVs(0, uv); m.subMeshCount = tris.Length;
            for (int i = 0; i < tris.Length; i++) m.SetTriangles(tris[i], i);
            m.RecalculateNormals(); m.RecalculateTangents(); m.RecalculateBounds();
        }
    }

    static Vector3 Ring(float r, float y, float ang) => V(Mathf.Cos(ang) * r, y, Mathf.Sin(ang) * r);

    /// <summary>回転体の帯。rings は下から (半径, 高さ)。外側は下→上→内へ、内側の壁は上→下の順に並べると、面は自動で外を向く。
    /// uvOf(帯の番号, 0〜1 の周, 0〜1 の高さ) で色・柄を決める。</summary>
    static void LatheBands(LP lp, Vector2[] rings, int sides, Func<int, float, float, Vector2> uvOf, int sub = 0, float phase = 0)
    {
        for (int b = 0; b < rings.Length - 1; b++)
        {
            Vector2 r0 = rings[b], r1 = rings[b + 1];
            float dr = r1.x - r0.x, dy = r1.y - r0.y;
            for (int k = 0; k < sides; k++)
            {
                float a0 = phase + k * Mathf.PI * 2 / sides, a1 = phase + (k + 1) * Mathf.PI * 2 / sides;
                Vector3 p0 = Ring(r0.x, r0.y, a0), p1 = Ring(r0.x, r0.y, a1), p2 = Ring(r1.x, r1.y, a1), p3 = Ring(r1.x, r1.y, a0);
                Vector3 outward = Ring(1, 0, (a0 + a1) / 2) * dy - Vector3.up * dr;   // 断面の進む向きを 90 度回した向き
                if (outward.sqrMagnitude < 1e-12f) continue;
                float u0 = (float)k / sides, u1 = (float)(k + 1) / sides;
                lp.Quad(p0, p1, p2, p3, uvOf(b, u0, 0), uvOf(b, u1, 0), uvOf(b, u1, 1), uvOf(b, u0, 1), outward, sub);
            }
        }
    }

    /// <summary>水平な円（多角形）のふた。up = true で上向き。</summary>
    static void Cap(LP lp, float r, float y, int sides, Vector2 uv, bool up, int sub = 0, float phase = 0)
    {
        var c = V(0, y, 0);
        for (int k = 0; k < sides; k++)
            lp.Tri(c, Ring(r, y, phase + k * Mathf.PI * 2 / sides), Ring(r, y, phase + (k + 1) * Mathf.PI * 2 / sides), uv, up ? Vector3.up : Vector3.down, sub);
    }

    /// <summary>向きのある箱（中心・大きさ・回転）。</summary>
    static void Block(LP lp, Vector3 center, Vector3 size, Quaternion rot, Vector2 uv, int sub = 0)
    {
        Vector3 h = size * .5f;
        Vector3 P(float x, float y, float z) => center + rot * Vector3.Scale(V(x, y, z), h);
        Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (var f in n)
        {
            Vector3 a = Mathf.Abs(f.x) > 0 ? Vector3.up : Vector3.right, b = Vector3.Cross(f, a);
            Vector3 c0 = f + a + b, c1 = f + a - b, c2 = f - a - b, c3 = f - a + b;
            lp.Quad(P(c0.x, c0.y, c0.z), P(c1.x, c1.y, c1.z), P(c2.x, c2.y, c2.z), P(c3.x, c3.y, c3.z), uv, rot * f, sub);
        }
    }

    // ================================================================ 缶（6 種。ラベルは周り 1 周に貼る UV。ラベルの帯は 2.75:1）
    static readonly Vector2[] CanRings = { new Vector2(.029f, 0), new Vector2(.033f, .008f), new Vector2(.033f, .018f), new Vector2(.033f, .096f), new Vector2(.033f, .112f), new Vector2(.027f, .122f), new Vector2(.0245f, .119f) };

    static Mesh CanMesh(int color) => CustomMesh("can_" + color, m =>
    {
        var lp = new LP();
        Vector2 body = SwUV(color), rim = SwUV(SwMetalDark), lid = SwUV(SwMetal);
        var label = CanLabelRect(color);
        LatheBands(lp, CanRings, 8, (b, u, v) => b == 2 ? InRect(label, u, v) : b == 1 || b == 3 ? body : b == 0 ? rim : lid, 0, Mathf.PI / 8);
        Cap(lp, .0245f, .119f, 8, lid, true, 0, Mathf.PI / 8);
        Cap(lp, .029f, 0, 8, rim, false, 0, Mathf.PI / 8);
        Block(lp, V(0, .1205f, .009f), V(.012f, .003f, .02f), Quaternion.identity, rim);   // プルタブ
        lp.Fill(m);
    });

    // ================================================================ 瓶（4 種。ガラスの色とラベル。ラベルの帯は 5.5:1）
    static readonly Vector2[] BottleRings = { new Vector2(.030f, 0), new Vector2(.035f, .01f), new Vector2(.035f, .055f), new Vector2(.035f, .095f), new Vector2(.035f, .13f), new Vector2(.018f, .175f), new Vector2(.014f, .19f), new Vector2(.013f, .23f) };
    static readonly Vector2[] BottleCapRings = { new Vector2(.013f, .23f), new Vector2(.016f, .23f), new Vector2(.016f, .247f) };

    static Mesh BottleMesh(int j) => CustomMesh("bottle_" + j, m =>
    {
        var lp = new LP();
        Vector2 glass = SwUV(BottleGlass[j]), gold = SwUV(SwGold);
        var label = BottleLabelRect(BottleLabel[j]);
        LatheBands(lp, BottleRings, 8, (b, u, v) => b == 2 ? InRect(label, u, v) : glass, 0, Mathf.PI / 8);
        LatheBands(lp, BottleCapRings, 8, (b, u, v) => gold, 0, Mathf.PI / 8);
        Cap(lp, .016f, .247f, 8, gold, true, 0, Mathf.PI / 8);
        Cap(lp, .030f, 0, 8, glass, false, 0, Mathf.PI / 8);
        lp.Fill(m);
    });

    static int BottleVariant(Vector3 pos) => Mathf.Abs(Mathf.RoundToInt(pos.x * 100) + Mathf.RoundToInt(pos.z * 37) + Mathf.RoundToInt(pos.y * 53)) % BottleKinds;

    // ================================================================ ピザ（8 切れ。上面は 1 枚のテクスチャを真上から貼る UV）
    static IEnumerable<Vector2> PepperoniSpots()
    {
        for (int s = 0; s < 8; s++)
        {
            float mid = (s + .5f) * Mathf.PI * 2 / 8;
            yield return new Vector2(Mathf.Cos(mid), Mathf.Sin(mid)) * .1f;
            if (s % 2 == 0) { float a = mid + .12f; yield return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .047f; }
        }
    }

    static IEnumerable<Vector2> PepperSpots()
    {
        for (int s = 0; s < 8; s++) { float a = (s + .2f) * Mathf.PI * 2 / 8; yield return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * .125f; }
    }

    static int SliceOf(Vector2 p) => Mathf.FloorToInt(Mathf.Repeat(Mathf.Atan2(p.y, p.x), Mathf.PI * 2) / (Mathf.PI * 2 / 8)) % 8;

    /// <summary>ピザを足す。slices に入っている切れだけ作る。toLocal で箱の中の位置へ。</summary>
    static void AddPizza(LP lp, Func<Vector3, Vector3> toLocal, Func<Vector3, Vector3> dirToLocal, ICollection<int> slices)
    {
        const int S = 8, K = 3; const float gap = .028f;
        const float yC = .012f, yRp = .021f, yRo = .014f, rRp = .165f;
        Vector2 top(Vector3 p) => InRect(PizzaTopRect, p.x / (2 * PizzaCheeseR) + .5f, p.z / (2 * PizzaCheeseR) + .5f);
        Vector2 crust = SwUV(SwCrust), crustDark = SwUV(SwCrustDark);
        void T(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 o) => lp.Tri(toLocal(a), toLocal(b), toLocal(c), ua, ub, uc, dirToLocal(o));
        void Q(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 uv, Vector3 o) { T(a, b, c, uv, uv, uv, o); T(a, c, d, uv, uv, uv, o); }
        foreach (int s in slices)
        {
            float t0 = s * Mathf.PI * 2 / S + gap / 2, t1 = (s + 1) * Mathf.PI * 2 / S - gap / 2, mid = (t0 + t1) / 2;
            Vector3 apexB = Ring(.006f, 0, mid), apexT = Ring(.006f, yC, mid);
            Vector2[] section = { new Vector2(PizzaCheeseR, yC), new Vector2(rRp, yRp), new Vector2(PizzaR, yRo), new Vector2(PizzaR, 0) };
            for (int k = 0; k < K; k++)
            {
                float a0 = Mathf.Lerp(t0, t1, (float)k / K), a1 = Mathf.Lerp(t0, t1, (float)(k + 1) / K);
                Vector3 c0 = Ring(PizzaCheeseR, yC, a0), c1 = Ring(PizzaCheeseR, yC, a1);
                T(apexT, c0, c1, top(apexT), top(c0), top(c1), Vector3.up);
                for (int b = 0; b < section.Length - 1; b++)
                {
                    Vector3 p0 = Ring(section[b].x, section[b].y, a0), p1 = Ring(section[b].x, section[b].y, a1), p2 = Ring(section[b + 1].x, section[b + 1].y, a1), p3 = Ring(section[b + 1].x, section[b + 1].y, a0);
                    Vector3 o = b == 2 ? Ring(1, 0, (a0 + a1) / 2) : (b == 0 ? Vector3.up + Ring(-.3f, 0, (a0 + a1) / 2) : Vector3.up + Ring(.6f, 0, (a0 + a1) / 2));
                    Q(p0, p1, p2, p3, b == 2 ? crustDark : crust, o);
                }
            }
            // 切り口（両側）
            foreach (var (ang, sign) in new[] { (t0, -1f), (t1, 1f) })
            {
                Vector3 o = V(-Mathf.Sin(ang), 0, Mathf.Cos(ang)) * sign;
                Vector3 q1 = Ring(PizzaCheeseR, yC, ang), q2 = Ring(rRp, yRp, ang), q3 = Ring(PizzaR, yRo, ang), q4 = Ring(PizzaR, 0, ang);
                Vector2 cheese = SwUV(SwCheese);
                T(apexB, apexT, q1, crust, cheese, cheese, o); T(apexB, q1, q2, crust, cheese, crust, o);
                T(apexB, q2, q3, crust, crust, crust, o); T(apexB, q3, q4, crust, crust, crust, o);
            }
        }
        if (!PizzaToppingDiscs) return;
        foreach (var p in PepperoniSpots())
        {
            if (!slices.Contains(SliceOf(p))) continue;
            const int n = 6; const float r = .02f, y0 = yC - .001f, y1 = yC + .003f;
            Vector2 pep = SwUV(SwPepperoni);
            var c = V(p.x, y1, p.y);
            for (int k = 0; k < n; k++)
            {
                float a0 = k * Mathf.PI * 2 / n, a1 = (k + 1) * Mathf.PI * 2 / n;
                Vector3 e0 = c + Ring(r, 0, a0), e1 = c + Ring(r, 0, a1);
                T(c, e0, e1, top(c), top(e0), top(e1), Vector3.up);
                Q(e0, e1, V(e1.x, y0, e1.z), V(e0.x, y0, e0.z), pep, Ring(1, 0, (a0 + a1) / 2));
            }
        }
        foreach (var p in PepperSpots())
        {
            if (!slices.Contains(SliceOf(p))) continue;
            Vector3 c = V(p.x, yC + .0015f, p.y), d = V(.8f, 0, .6f) * .012f, w = V(-.6f, 0, .8f) * .004f;
            var g = SwUV(SwPepper);
            T(c - d, c + w, c + d, g, g, g, Vector3.up); T(c - d, c + d, c - w, g, g, g, Vector3.up);
        }
    }

    // ================================================================ ピザの箱（開いて中身あり・開いて空・閉じた）
    static Mesh PizzaBoxMesh(string variant) => CustomMesh("pizzabox_" + variant, m =>
    {
        var lp = new LP();
        const float h = .21f, H = .04f, t = .01f, floorY = .006f, lt = .008f;
        Vector2 outer = SwUV(SwBoxOuter), inner = SwUV(SwBoxInner), edge = SwUV(SwBoxEdge);
        // 受け皿：外側の壁・壁の上・内側の壁・底
        for (int side = 0; side < 4; side++)
        {
            var rot = Quaternion.Euler(0, side * 90, 0);
            Vector3 P(float x, float y, float z) => rot * V(x, y, z);
            lp.Quad(P(-h, 0, h), P(h, 0, h), P(h, H, h), P(-h, H, h), outer, rot * Vector3.forward);
            lp.Quad(P(-h, H, h), P(h, H, h), P(h - t, H, h - t), P(-h + t, H, h - t), edge, Vector3.up);
            lp.Quad(P(-h + t, floorY, h - t), P(h - t, floorY, h - t), P(h - t, H, h - t), P(-h + t, H, h - t), inner, rot * Vector3.back);
        }
        lp.Quad(V(-h + t, floorY, -h + t), V(h - t, floorY, -h + t), V(h - t, floorY, h - t), V(-h + t, floorY, h - t), inner, Vector3.up);

        // ふた（後ろの辺がちょうつがい）。閉じた箱は少しずらして置く（生活感）
        bool closed = variant == "closed";
        Quaternion lidRot = closed ? Quaternion.Euler(0, 6, 0) : Quaternion.Euler(-102, 0, 0);
        Vector3 hinge = closed ? V(0, H, 0) + Quaternion.Euler(0, 6, 0) * V(0, 0, -h) : V(0, H, -h);
        Vector3 L(float x, float y, float z) => hinge + lidRot * V(x, y, z);
        Vector3 Ld(Vector3 d) => lidRot * d;
        float w = h, len = 2 * h;
        lp.Quad(L(-w, lt, 0), L(w, lt, 0), L(w, lt, len), L(-w, lt, len), InRect(BoxPrintRect, 0, 1), InRect(BoxPrintRect, 1, 1), InRect(BoxPrintRect, 1, 0), InRect(BoxPrintRect, 0, 0), Ld(Vector3.up));
        lp.Quad(L(-w, 0, 0), L(w, 0, 0), L(w, 0, len), L(-w, 0, len), inner, Ld(Vector3.down));
        lp.Quad(L(-w, 0, len), L(w, 0, len), L(w, lt, len), L(-w, lt, len), edge, Ld(Vector3.forward));
        lp.Quad(L(-w, 0, 0), L(-w, 0, len), L(-w, lt, len), L(-w, lt, 0), edge, Ld(Vector3.left));
        lp.Quad(L(w, 0, 0), L(w, 0, len), L(w, lt, len), L(w, lt, 0), edge, Ld(Vector3.right));
        if (!closed)
        {
            // ふたの前の折り返し（開いた箱だけ。両面）
            Vector3 f0 = L(-w + .01f, 0, len), f1 = L(w - .01f, 0, len), f2 = L(w - .01f, -.03f, len - .004f), f3 = L(-w + .01f, -.03f, len - .004f);
            lp.Quad(f0, f1, f2, f3, edge, Ld(Vector3.forward)); lp.Quad(f0, f1, f2, f3, inner, Ld(Vector3.back));
        }

        if (variant == "open_full")
        {
            var all = new List<int> { 0, 1, 2, 3, 4, 6, 7 };   // 1 切れ食べたあと（手前右）
            AddPizza(lp, p => p + V(0, floorY, .01f), d => d, all);
        }
        else if (variant == "open_empty")
        {
            Cap(lp, .14f, floorY + .0005f, 10, SwUV(SwGrease), true);   // 油じみ
            var rot = Quaternion.Euler(0, 40, 0);
            AddPizza(lp, p => rot * (p - V(.09f, 0, .04f)) + V(.03f, floorY + .001f, .02f), d => rot * d, new List<int> { 0 });   // 残った 1 切れ
        }
        lp.Fill(m);
    });

    // ================================================================ 観葉植物（鉢・土・茎・葉を 1 つの形に）
    static readonly Vector2[] PotRings = { new Vector2(.11f, 0), new Vector2(.145f, .2f), new Vector2(.158f, .205f), new Vector2(.158f, .25f), new Vector2(.13f, .25f), new Vector2(.125f, .215f) };

    static Mesh PlantMesh(float height, int leaves, bool cards) => CustomMesh($"plant_{Mathf.RoundToInt(height * 100)}_{leaves}_{(cards ? "card" : "mesh")}", m =>
    {
        var lp = new LP(cards ? 2 : 1);
        Vector2 pot = SwUV(SwPot), potDark = SwUV(SwPotDark), stem = SwUV(SwStem);
        LatheBands(lp, PotRings, 8, (b, u, v) => b == 0 || b == 2 ? pot : potDark);
        Cap(lp, .125f, .215f, 8, SwUV(SwSoil), true);
        Cap(lp, .11f, 0, 8, potDark, false);

        for (int i = 0; i < leaves; i++)
        {
            // 茎の向きと長さは前の作り（棒と球）と同じ式
            float a = i * 137.5f, tilt = 25 + (i * 29 % 40), len = height * (.45f + (i * 17 % 10) * .04f);
            Vector3 d = Quaternion.Euler(tilt, a, 0) * Vector3.up;
            Vector3 outH = V(d.x, 0, d.z).normalized;
            Vector3 p0 = V(0, .215f, 0) + outH * .025f, p1 = p0 + d * len * .6f;
            Vector3 d2 = (d + outH * .35f - Vector3.up * .05f).normalized, p2 = p1 + d2 * len * .4f;
            Stalk(lp, p0, p1, .008f, stem); Stalk(lp, p1, p2, .007f, stem);
            float L = .28f + .04f * (i % 3), W = .26f + .04f * (i % 3);
            Vector3 side = Vector3.Cross(Vector3.up, outH).normalized;
            float sgn = i % 2 == 0 ? 1 : -1;
            int kind = cards ? i % 2 : 0;   // 板の葉はポトスとマドカズラを交互に。メッシュの葉は形の合うポトス
            Leaf(lp, p2, (d2 + outH * .5f - Vector3.up * .25f).normalized, outH, L, W, i % 2 == 0, cards, kind);
            Leaf(lp, p1, (outH * .9f + Vector3.up * .2f + side * .35f * sgn).normalized, outH, L * .72f, W * .72f, i % 2 != 0, cards, kind);
            Leaf(lp, p0 + d * len * .32f, (outH * .8f + Vector3.up * .35f - side * .4f * sgn).normalized, outH, L * .55f, W * .55f, i % 2 == 0, cards, kind);
        }
        lp.Fill(m);
    });

    /// <summary>三角柱の茎。</summary>
    static void Stalk(LP lp, Vector3 a, Vector3 b, float r, Vector2 uv)
    {
        Vector3 dir = (b - a).normalized, s = Vector3.Cross(dir, Mathf.Abs(dir.y) > .9f ? Vector3.right : Vector3.up).normalized, u = Vector3.Cross(s, dir);
        for (int k = 0; k < 3; k++)
        {
            float a0 = k * Mathf.PI * 2 / 3, a1 = (k + 1) * Mathf.PI * 2 / 3;
            Vector3 o0 = (s * Mathf.Cos(a0) + u * Mathf.Sin(a0)) * r, o1 = (s * Mathf.Cos(a1) + u * Mathf.Sin(a1)) * r;
            lp.Quad(a + o0, a + o1, b + o1, b + o0, uv, (o0 + o1).normalized);
        }
    }

    /// <summary>葉。メッシュの葉は中央の葉脈で折れたハート形（表と裏）。板の葉は 2 枚の四角（アトラスの葉の区画を貼る）。</summary>
    static void Leaf(LP lp, Vector3 basePos, Vector3 f, Vector3 outH, float L, float W, bool light, bool card, int kind = 0)
    {
        Vector3 s = Vector3.Cross(Vector3.up, outH).normalized;
        s = (s - f * Vector3.Dot(s, f)).normalized;
        if (s.sqrMagnitude < .01f) s = Vector3.Cross(f, Vector3.forward).normalized;
        Vector3 n = Vector3.Cross(f, s).normalized; if (n.y < 0) n = -n;
        Vector3 P(float x, float y, float z) => basePos + s * x + f * y + n * z;
        var rect = LeafRect(kind);
        if (card)
        {
            // 板の葉：葉の絵の区画（縦長 256×320）をそのまま 2 枚の四角に。幅は区画の縦横比に合わせる
            float cw = L * rect.width / rect.height;
            Vector3 b0 = P(-.5f * cw, 0, 0), b1 = P(.5f * cw, 0, 0), m0 = P(0, 0, .06f * cw), t0 = P(-.5f * cw, L, -.05f * L), t1 = P(.5f * cw, L, -.05f * L), tm = P(0, L, .03f * cw - .05f * L);
            lp.Quad(b0, m0, tm, t0, InRect(rect, 0, 0), InRect(rect, .5f, 0), InRect(rect, .5f, 1), InRect(rect, 0, 1), n, 1);
            lp.Quad(m0, b1, t1, tm, InRect(rect, .5f, 0), InRect(rect, 1, 0), InRect(rect, 1, 1), InRect(rect, .5f, 1), n, 1);
            return;
        }
        Vector3 B = P(0, 0, 0), T = P(0, L, -.06f * L), M1 = P(0, .28f * L, .07f * W), M2 = P(0, .66f * L, .05f * W);
        Vector3 L1 = P(-.48f * W, .3f * L, 0), L2 = P(-.4f * W, .68f * L, -.02f * L), R1 = P(.48f * W, .3f * L, 0), R2 = P(.4f * W, .68f * L, -.02f * L);
        // 表は葉の絵を貼る（葉の付け根 = 区画の下、先 = 上。横は区画の縦横比で合わせる）。裏は濃い緑の単色
        Vector2 back = SwUV(SwLeafDark);
        float cwidth = L * rect.width / rect.height;
        Vector2 Uv(Vector3 p) { var d = p - basePos; return InRect(rect, .5f + Vector3.Dot(d, s) / cwidth, Vector3.Dot(d, f) / L); }
        void Both(Vector3 a, Vector3 b, Vector3 c) { lp.Tri(a, b, c, Uv(a), Uv(b), Uv(c), n); lp.Tri(a, b, c, back, -n); }
        Both(B, L1, M1); Both(M1, L1, L2); Both(M1, L2, M2); Both(M2, L2, T);
        Both(B, M1, R1); Both(M1, R2, R1); Both(M1, M2, R2); Both(M2, T, R2);
    }

    // ================================================================ 電球（電飾・吊りランプ共通。原点がソケットの上＝コードに付く点）
    static Mesh BulbMesh() => CustomMesh("bulb", m =>
    {
        var lp = new LP(2);   // 0 = 光る玉、1 = ソケット（アトラスの色）
        var socket = SwUV(SwSocket);
        LatheBands(lp, new[] { new Vector2(.0055f, -.013f), new Vector2(.0055f, 0) }, 6, (b, u, v) => socket, 1);
        Cap(lp, .0055f, 0, 6, socket, true, 1);
        var glow = new Vector2(.5f, .5f);
        LatheBands(lp, new[] { new Vector2(0, -.052f), new Vector2(.0085f, -.043f), new Vector2(.0125f, -.031f), new Vector2(.0105f, -.019f), new Vector2(.0055f, -.013f) }, 6, (b, u, v) => glow, 0, Mathf.PI / 6);
        lp.Fill(m);
    });

    // ================================================================ 絵を貼る板（雑誌の表紙・メモ・紙くず・ネオン）
    /// <summary>四角い板。horizontal = true は上向き（床・机の上に置く物）、false は +Z 向き（壁・扉に貼る物。+Z 側から読める向き）。</summary>
    static void AddQuad(LP lp, Vector2 size, Func<float, float, Vector2> uv, bool horizontal)
    {
        float w = size.x / 2, h = size.y / 2;
        if (horizontal) lp.Quad(V(-w, 0, -h), V(w, 0, -h), V(w, 0, h), V(-w, 0, h), uv(0, 0), uv(1, 0), uv(1, 1), uv(0, 1), Vector3.up);
        else lp.Quad(V(-w, -h, 0), V(w, -h, 0), V(w, h, 0), V(-w, h, 0), uv(1, 0), uv(0, 0), uv(0, 1), uv(1, 1), Vector3.forward);   // +Z から見ると左右が逆になるので u を反転
    }

    static Mesh CoverMesh(int k) => CustomMesh("cover_" + k, m => { var lp = new LP(); AddQuad(lp, new Vector2(.212f, .287f), (u, v) => InRect(CoverRect(k), u, v), true); lp.Fill(m); });
    /// <summary>メモ（高さ 1m。置くときに大きさをかける）。</summary>
    static Mesh NoteMesh(int n) => CustomMesh("note_" + n, m => { var lp = new LP(); var r = NoteRect(n); AddQuad(lp, new Vector2((float)r.width / r.height, 1), (u, v) => InRect(r, u, v), false); lp.Fill(m); });
    /// <summary>紙くず（1m 四方。置くときに縦横の大きさをかける）。</summary>
    static Mesh ScrapMesh(int k) => CustomMesh("scrap_" + k, m => { var lp = new LP(); AddQuad(lp, Vector2.one, (u, v) => InRect(ScrapRect(k), u, v), true); lp.Fill(m); });
    /// <summary>ネコのネオン（幅 0.56m。UV はネオンのテクスチャの絵の範囲）。</summary>
    static Mesh NeonCatMesh() => CustomMesh("neon_cat", m =>
    {
        var lp = new LP(); var r = SrcNeonCat; const float texW = 1024, texH = 683;
        AddQuad(lp, new Vector2(.56f, .56f * r.height / r.width), (u, v) => new Vector2((r.x + u * r.width) / texW, 1 - (r.yMax - v * r.height) / texH), false);
        lp.Fill(m);
    });
}
#endif
