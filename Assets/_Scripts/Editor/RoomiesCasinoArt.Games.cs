#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// ハイ＆ローの台と、チンチロの台・どんぶり・サイコロの形。どれも「足元が原点、遊ぶ人の立つ側が +Z」。
/// </summary>
internal static partial class RoomiesCasinoArt
{
    // ================================================================ ハイ＆ローの台
    internal const float HlLength = 1.7f, HlDepth = .95f, HlTop = .92f;
    // 立てたカードの真ん中（文字を置く所）と、その大きさ
    internal static readonly Vector3 HlCardCenter = new Vector3(0, 1.36f, -.3f);
    internal const float HlCardW = .36f, HlCardH = .5f;

    internal static Mesh HiLoTable() => Build("hilo_table", lp =>
    {
        foreach (float x in new[] { -.55f, .55f }) lp.Prism(Offset(Chamfer(.3f, .5f, .07f), new Vector2(x, 0)), .06f, .6f, "wood_dark");
        lp.Prism(Chamfer(1.5f, .7f, .1f), 0, .06f, "ink");
        lp.Prism(Chamfer(HlLength - .12f, HlDepth - .12f, .1f), .6f, .84f, "wood", null, true);
        lp.Ring(Chamfer(HlLength, HlDepth, .12f), Chamfer(HlLength - .14f, HlDepth - .14f, .08f), .84f, HlTop + .03f, "maroon", "maroon_dark");
        lp.Prism(Chamfer(HlLength - .14f, HlDepth - .14f, .08f), .84f, HlTop, "felt_dark", "felt");
        // カードを立てる台と、立てたカード（文字はプレハブの Anchor_Card に置く）
        lp.Prism(Offset(Chamfer(.56f, .16f, .04f), new Vector2(0, -.3f)), HlTop, HlTop + .16f, "wood_dark", "gold_dark");
        lp.Push(new Vector3(HlCardCenter.x, HlCardCenter.y, HlCardCenter.z - .012f));
        lp.Box(new Vector3(-HlCardW / 2 - .02f, -HlCardH / 2 - .02f, -.014f), new Vector3(HlCardW / 2 + .02f, HlCardH / 2 + .02f, 0), "gold_dark");
        lp.Box(new Vector3(-HlCardW / 2, -HlCardH / 2, 0), new Vector3(HlCardW / 2, HlCardH / 2, .01f), "card");
        lp.Pop();
        // 上（ピンク）と下（水色）の矢印：カードの左右で光る
        foreach (var (x, up, c) in new[] { (.42f, true, "pink"), (-.42f, false, "cyan") })
        {
            float y = HlCardCenter.y, s = up ? 1 : -1; float z = HlCardCenter.z;
            Vector2 u = Sw(c);
            Vector3 tip = new Vector3(x, y + s * .16f, z), l = new Vector3(x - .11f, y - s * .04f, z), r = new Vector3(x + .11f, y - s * .04f, z);
            lp.Tri(tip, l, r, u, u, u, Vector3.forward, true);
            lp.Box(new Vector3(x - .04f, Mathf.Min(y - s * .04f, y - s * .16f), z - .005f), new Vector3(x + .04f, Mathf.Max(y - s * .04f, y - s * .16f), z + .005f), c, true);
        }
        // 札の束とチップ
        lp.Box(new Vector3(.5f, HlTop, .08f), new Vector3(.64f, HlTop + .05f, .28f), "card");
        lp.Box(new Vector3(.5f, HlTop + .05f, .08f), new Vector3(.64f, HlTop + .054f, .28f), "maroon");
        foreach (var (x, z, n, c) in new[] { (-.56f, .16f, 6, "chip_red"), (-.44f, .2f, 4, "chip_blue"), (-.5f, .06f, 8, "chip_green") })
            lp.Cyl(new Vector3(x, HlTop, z), .04f, n * .012f, 8, c, false, "chip_white");
        // 手持ちの額の札（手前のふちの上）
        lp.Prism(Offset(Chamfer(.9f, .05f, .015f), new Vector2(0, -.12f)), HlTop, HlTop + .12f, "ink", "gold_dark");
        // 上の小さな看板
        lp.Box(new Vector3(-.03f, HlTop + .16f, -.4f), new Vector3(.03f, 1.74f, -.36f), "gold_dark");
        lp.Prism(Offset(Chamfer(.86f, .06f, .02f), new Vector2(0, -.38f)), 1.72f, 1.94f, "ink", "gold_dark");
    });

    // ================================================================ チンチロの台（丸い台と、どんぶり）
    internal const float CcRadius = .62f, CcTop = .88f, CcBowlFloor = .04f, CcDie = .075f;

    internal static Mesh ChinchiroTable() => Build("chinchiro_table", lp =>
    {
        lp.Lathe(new[] { (.42f, 0f), (.38f, .05f), (.11f, .1f), (.1f, CcTop - .12f), (.3f, CcTop - .06f) }, 8, "wood_dark", Mathf.PI / 8, false, true);
        lp.Prism(Ellipse(CcRadius, CcRadius, 12, Mathf.PI / 12), CcTop - .06f, CcTop - .01f, "wood", "wood", true);
        lp.Prism(Ellipse(CcRadius - .06f, CcRadius - .06f, 12, Mathf.PI / 12), CcTop - .01f, CcTop, "purple_dark", "purple");
        // どんぶり：外は黒、内は赤。底は平ら
        lp.Push(new Vector3(0, CcTop, 0));
        lp.Lathe(new[] { (.12f, 0f), (.25f, .11f), (.27f, .15f), (.255f, .15f), (.13f, CcBowlFloor) }, 10, b => b < 2 ? "ink" : b == 2 ? "gold_dark" : "maroon", 0, false);
        lp.Cap(.13f, CcBowlFloor, 10, "maroon_dark", true);
        lp.Pop();
        // 立て札（奥）
        lp.Box(new Vector3(-.03f, CcTop, -.56f), new Vector3(.03f, 1.5f, -.52f), "gold_dark");
        lp.Prism(Offset(Chamfer(1.0f, .06f, .02f), new Vector2(0, -.54f)), 1.44f, 1.96f, "ink", "gold_dark");
        // チップ
        foreach (var (x, z, n, c) in new[] { (.4f, .2f, 5, "chip_black"), (.34f, .32f, 7, "chip_red"), (-.42f, .22f, 4, "chip_white") })
            lp.Cyl(new Vector3(x, CcTop, z), .04f, n * .012f, 8, c, false, "chip_white");
    });

    /// <summary>サイコロ（上=1・下=6・前(+Z)=2・後ろ=5・右(+X)=3・左=4。1 の目だけ赤）。原点は真ん中。</summary>
    internal static Mesh Die() => Build("die", lp =>
    {
        float h = CcDie / 2, d = CcDie * .24f, p = CcDie * .09f;
        lp.Box(new Vector3(-h, -h, -h), new Vector3(h, h, h), "white");
        // 面ごとの目の位置（面の中の横 a・縦 b）
        Vector2[][] pips =
        {
            new[] { Vector2.zero },
            new[] { new Vector2(-d, -d), new Vector2(d, d) },
            new[] { new Vector2(-d, -d), Vector2.zero, new Vector2(d, d) },
            new[] { new Vector2(-d, -d), new Vector2(d, -d), new Vector2(-d, d), new Vector2(d, d) },
            new[] { new Vector2(-d, -d), new Vector2(d, -d), Vector2.zero, new Vector2(-d, d), new Vector2(d, d) },
            new[] { new Vector2(-d, -d), new Vector2(d, -d), new Vector2(-d, 0), new Vector2(d, 0), new Vector2(-d, d), new Vector2(d, d) },
        };
        // 目の数 → 面の向き（法線・面の中の横と縦）
        (int value, Vector3 n, Vector3 a, Vector3 b)[] faces =
        {
            (1, Vector3.up, Vector3.right, Vector3.forward), (6, Vector3.down, Vector3.right, Vector3.forward),
            (2, Vector3.forward, Vector3.right, Vector3.up), (5, Vector3.back, Vector3.right, Vector3.up),
            (3, Vector3.right, Vector3.forward, Vector3.up), (4, Vector3.left, Vector3.forward, Vector3.up),
        };
        foreach (var f in faces)
            foreach (var q in pips[f.value - 1])
            {
                float s = f.value == 1 ? p * 1.7f : p;
                Vector3 c = f.n * (h + .0008f) + f.a * q.x + f.b * q.y;
                Vector2 u = Sw(f.value == 1 ? "chip_red" : "black");
                lp.Quad(c - f.a * s - f.b * s, c + f.a * s - f.b * s, c + f.a * s + f.b * s, c - f.a * s + f.b * s, u, u, u, u, f.n);
            }
    });
}
#endif
