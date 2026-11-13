#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 自然物 1：木。何度実行しても同じ結果。
/// ・幹と枝（先へ細くなる管）＋葉のかたまり（アルファで切り抜いた葉の板を3枚ずつ十字に重ねる）。針葉樹は枝ごとに垂れた葉の板。
/// ・近く・中くらい・遠くで作りの細かさを変える（LODGroup。遠いと消える）。当たり判定は付けない（見た目だけ）。
/// ・プレハブ：Assets/_Prefabs/Environment/Nature/Tree_〇〇.prefab。メッシュは Assets/_Art/Restyle/Nature/Trees.asset。
/// ・置き場所：町のプレハブの庭8か所（「Town trees」。元の球の木を消す）と、遠景の林・街路樹（遠景の配置ツールが置く）。
/// </summary>
internal static partial class RoomiesNature
{
    const string TreeMeshPath = ArtDir + "Trees.asset";
    const string TownTreeGroup = "Town trees";
    const string Gardens = "Pocket gardens and distant silhouettes";

    internal enum TreeKind { GardenA, GardenB, Street, Oak, Pine }

    // 種類ごとの形：幹の太さ, 樹冠の下, 樹冠の中心の高さ, 樹冠の横と縦の半径, 葉のかたまりの数と大きさ, 主枝の数, 葉の素材
    struct TreeShape
    {
        public float trunkR, crownBase, crownC, R, Ry, card; public int clusters, primaries; public string leaves; public int seed; public bool pine; public float height;
    }

    static TreeShape Shape(TreeKind k)
    {
        switch (k)
        {
            case TreeKind.GardenA: return new TreeShape { trunkR = .12f, crownBase = 1.4f, crownC = 2.85f, R = 1.55f, Ry = 1.35f, card = .95f, clusters = 70, primaries = 4, leaves = "M_Leaves_Green", seed = 11 };
            case TreeKind.GardenB: return new TreeShape { trunkR = .12f, crownBase = 1.5f, crownC = 2.95f, R = 1.45f, Ry = 1.4f, card = .95f, clusters = 70, primaries = 5, leaves = "M_Leaves_Teal", seed = 23 };
            case TreeKind.Street: return new TreeShape { trunkR = .17f, crownBase = 2.3f, crownC = 4.5f, R = 2.2f, Ry = 2.0f, card = 1.25f, clusters = 85, primaries = 5, leaves = "M_Leaves_Green", seed = 37 };
            case TreeKind.Oak: return new TreeShape { trunkR = .3f, crownBase = 2.8f, crownC = 6.2f, R = 3.6f, Ry = 3.0f, card = 1.7f, clusters = 110, primaries = 6, leaves = "M_Leaves_Deep", seed = 53 };
            default: return new TreeShape { trunkR = .24f, height = 11f, R = 2.6f, card = 1f, leaves = "M_Needles", seed = 71, pine = true };
        }
    }

    internal static float TreeHeight(TreeKind k) { var s = Shape(k); return s.pine ? s.height : s.crownC + s.Ry; }

    [MenuItem("Roomies/Art/Nature/1 Trees")]
    internal static void BuildTrees()
    {
        BuildTreeAssets();
        ApplyWind();
        PlaceTownTrees();
        AssetDatabase.SaveAssets();
        Debug.Log("[Nature] trees built and placed in the town (backdrop: run Roomies/Environment/Build Distant Backdrop)");
    }

    // ================================================================ 素材とプレハブ
    static readonly Dictionary<TreeKind, GameObject> treePrefabs = new Dictionary<TreeKind, GameObject>();

    internal static GameObject TreePrefab(TreeKind k)
    {
        if (treePrefabs.TryGetValue(k, out var p) && p != null) return p;
        p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Tree_" + k + ".prefab");
        if (p == null) { BuildTreeAssets(); p = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "Tree_" + k + ".prefab"); }
        treePrefabs[k] = p;
        return p;
    }

    static void BuildTreeAssets()
    {
        var bark = Tex("nature_bark", 512, 512, BarkPixel, 6f, out var barkN);
        var leaves = LeafTexture(out var leavesN);
        var needles = NeedleTexture(out var needlesN);
        var mBark = Lit("M_Bark", bark, barkN, new Color(.92f, .86f, .8f), .12f);
        var mats = new Dictionary<string, Material>
        {
            ["M_Leaves_Green"] = Lit("M_Leaves_Green", leaves, leavesN, new Color(1f, 1f, .95f), .22f, true),
            ["M_Leaves_Teal"] = Lit("M_Leaves_Teal", leaves, leavesN, new Color(.72f, 1f, 1.05f), .22f, true),
            ["M_Leaves_Deep"] = Lit("M_Leaves_Deep", leaves, leavesN, new Color(.78f, .9f, .76f), .2f, true),
            ["M_Needles"] = Lit("M_Needles", needles, needlesN, new Color(.85f, .95f, .88f), .18f, true),
        };
        Directory.CreateDirectory(PrefabDir);
        foreach (TreeKind k in System.Enum.GetValues(typeof(TreeKind)))
        {
            var s = Shape(k);
            var meshes = new Mesh[3];
            for (int lod = 0; lod < 3; lod++) meshes[lod] = StoreMesh(TreeMeshPath, TreeMesh(s, lod, $"tree_{k}_lod{lod}"));
            var root = new GameObject("Tree_" + k) { isStatic = true };
            var lods = new LOD[3];
            float[] heights = { .32f, .1f, .018f };
            for (int lod = 0; lod < 3; lod++)
            {
                var go = new GameObject("LOD" + lod) { isStatic = true };
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes[lod];
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterials = new[] { mBark, mats[s.leaves] };
                r.shadowCastingMode = lod == 2 ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                lods[lod] = new LOD(heights[lod], new Renderer[] { r });
            }
            var group = root.AddComponent<LODGroup>();
            group.SetLODs(lods); group.RecalculateBounds();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "Tree_" + k + ".prefab");
            Object.DestroyImmediate(root);
        }
        treePrefabs.Clear();
    }

    // ================================================================ 木のメッシュ
    /// <summary>木を1本。lod 0〜2 で同じ乱数の骨組みを使い、細かさだけ変える。submesh 0 が幹と枝、1 が葉。</summary>
    static Mesh TreeMesh(TreeShape s, int lod, string name)
    {
        var rnd = new System.Random(s.seed);
        float R() => (float)rnd.NextDouble();
        var mb = new MB(2);
        int seg = lod == 0 ? 8 : lod == 1 ? 6 : 4;
        Rect full = new Rect(0, 0, 1, 1);

        if (s.pine)
        {
            // 幹：まっすぐ先まで細く
            var tp = new List<Vector3>(); var tr = new List<float>();
            for (int i = 0; i <= 6; i++) { float t = i / 6f; tp.Add(new Vector3(Mathf.Sin(t * 3f) * .05f, t * s.height, 0)); tr.Add(Mathf.Lerp(s.trunkR * 1.25f, .02f, t)); }
            mb.Tube(tp, tr, seg, 2, 1.5f);
            // 段ごとの枝：垂れた葉の板
            float step = lod == 0 ? .5f : lod == 1 ? .95f : 1.9f;
            int perWhorl = lod == 0 ? 7 : lod == 1 ? 6 : 4;
            float widthMul = lod == 0 ? 1 : lod == 1 ? 1.35f : 1.9f;
            int whorl = 0;
            for (float y = s.height * .2f; y < s.height * .97f; y += step, whorl++)
            {
                float t = Mathf.InverseLerp(s.height * .2f, s.height, y);
                float len = Mathf.Lerp(s.R, .35f, Mathf.Pow(t, .9f)) + .2f;
                for (int i = 0; i < perWhorl; i++)
                {
                    float a = (i / (float)perWhorl + whorl * .382f) * Mathf.PI * 2 + (R() - .5f) * .4f;
                    var outD = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    float droop = .35f + R() * .25f;
                    var dir = (outD - Vector3.up * droop).normalized;
                    var side = Vector3.Cross(Vector3.up, outD).normalized;
                    float w = Mathf.Max(.55f, len * .75f) * widthMul * .5f;
                    var basePt = new Vector3(0, y, 0);
                    var tip = basePt + dir * len * (lod == 2 ? 1.1f : 1f);
                    var up = Vector3.Cross(dir, side).normalized;
                    var nBase = (outD * .4f + Vector3.up * .9f).normalized; var nTip = (outD * .9f + Vector3.up * .45f).normalized;
                    mb.Quad(basePt - side * w * .4f, basePt + side * w * .4f, tip + side * w, tip - side * w, nBase, nBase, nTip, nTip, 1, full);
                    if (lod < 2) // 横から見ても薄くならないよう、縦向きの板も1枚
                        mb.Quad(basePt - up * w * .3f, basePt + up * w * .3f, tip + up * w * .7f, tip - up * w * .7f, nBase, nBase, nTip, nTip, 1, full);
                }
            }
            return mb.ToMesh(name);
        }

        // ---- 広葉樹：幹 → 主枝 → 小枝。骨組みは lod に関係なく同じ乱数で作る
        Vector3 crown = new Vector3(0, s.crownC, 0);
        var lean = new Vector3(R() - .5f, 0, R() - .5f) * .25f;
        var trunkPts = new List<Vector3>(); var trunkRad = new List<float>();
        for (int i = 0; i <= 5; i++)
        {
            float t = i / 5f;
            trunkPts.Add(new Vector3(0, t * s.crownBase, 0) + lean * t * t + new Vector3(Mathf.Sin(t * 5 + s.seed) * .04f, 0, Mathf.Cos(t * 4 + s.seed) * .04f));
            trunkRad.Add(s.trunkR * (i == 0 ? 1.45f : Mathf.Lerp(1.1f, .8f, t)));
        }
        mb.Tube(trunkPts, trunkRad, seg, 2, 1.2f);
        Vector3 top = trunkPts[trunkPts.Count - 1];

        var tips = new List<Vector3>();
        for (int p = 0; p < s.primaries; p++)
        {
            float a = (p / (float)s.primaries) * Mathf.PI * 2 + R() * .6f;
            var dir = new Vector3(Mathf.Cos(a), .55f + R() * .55f, Mathf.Sin(a)).normalized;
            var end = crown + Vector3.Scale(dir, new Vector3(s.R, s.Ry, s.R)) * .62f;
            var start = top - Vector3.up * (R() * .35f);
            var mid = Vector3.Lerp(start, end, .5f) + Vector3.up * .25f * s.Ry * .3f;
            var pts = new List<Vector3> { start, Vector3.Lerp(start, mid, .5f), mid, Vector3.Lerp(mid, end, .6f), end };
            float r0 = s.trunkR * .62f;
            var rad = pts.Select((_, i) => Mathf.Lerp(r0, r0 * .25f, i / 4f)).ToList();
            if (lod < 2) mb.Tube(pts, rad, Mathf.Max(4, seg - 2), 1, 1.2f);
            tips.Add(end);
            int subs = 2 + (int)(R() * 2);
            for (int q = 0; q < subs; q++)
            {
                float t = .45f + R() * .45f;
                var from = Vector3.Lerp(mid, end, t);
                var d2 = (dir + new Vector3(R() - .5f, R() * .6f, R() - .5f)).normalized;
                var to = crown + Vector3.Scale(d2, new Vector3(s.R, s.Ry, s.R)) * (.8f + R() * .12f);
                var p2 = new List<Vector3> { from, Vector3.Lerp(from, to, .5f) + Vector3.up * .1f, to };
                var r2 = new List<float> { r0 * .35f, r0 * .2f, .015f };
                if (lod == 0) mb.Tube(p2, r2, 4, 1, 1f);
                tips.Add(to);
            }
        }

        // 葉のかたまり：小枝の先と、樹冠の殻の中。数は lod で減らし、そのぶん板を大きく
        var pts2 = new List<Vector3>(tips);
        int extra = s.clusters - tips.Count;
        for (int i = 0; i < extra; i++)
        {
            Vector3 d;
            do d = new Vector3(R() * 2 - 1, R() * 2 - 1, R() * 2 - 1); while (d.sqrMagnitude > 1 || d.sqrMagnitude < .08f);
            float r = Mathf.Lerp(.55f, 1f, Mathf.Pow(R(), .5f));
            d = d.normalized * r;
            if (d.y < -.55f) d.y = -.55f + R() * .2f;   // 下側は少しすかす
            pts2.Add(crown + Vector3.Scale(d, new Vector3(s.R, s.Ry, s.R)));
        }
        // 並びをばらす（lod で前から使う）
        var order = pts2.Select((p, i) => (p, k: Rand01(i, s.seed))).OrderBy(x => x.k).Select(x => x.p).ToList();
        float keep = lod == 0 ? 1 : lod == 1 ? .38f : .14f;
        float size = s.card * (lod == 0 ? 1 : lod == 1 ? 1.55f : 2.5f);
        int count = Mathf.Max(6, Mathf.RoundToInt(order.Count * keep));
        int planes = lod == 2 ? 2 : 3;
        for (int i = 0; i < count; i++)
        {
            var c = order[i];
            float sz = size * (.8f + Rand01(i, s.seed + 5) * .45f);
            var outN = (c - crown); outN = new Vector3(outN.x / s.R, outN.y / s.Ry, outN.z / s.R).normalized;
            var q = Quaternion.Euler(Rand01(i, 1) * 360, Rand01(i, 2) * 360, Rand01(i, 3) * 360);
            for (int pl = 0; pl < planes; pl++)
            {
                var rot = q * (pl == 0 ? Quaternion.identity : pl == 1 ? Quaternion.Euler(0, 90, 0) : Quaternion.Euler(90, 0, 0));
                var right = rot * Vector3.right * sz * .5f; var up = rot * Vector3.up * sz * .5f;
                Vector3 N(Vector3 p) { var d = p - crown; d = new Vector3(d.x / s.R, d.y / s.Ry, d.z / s.R).normalized; return (d * .8f + Vector3.up * .25f).normalized; }
                var a = c - right - up; var b = c + right - up; var cc = c + right + up; var dd = c - right + up;
                mb.Quad(a, b, cc, dd, N(a), N(b), N(cc), N(dd), 1, new Rect(0, 0, 1, 1));
            }
        }
        return mb.ToMesh(name);
    }

    // ================================================================ テクスチャ
    // 樹皮：縦のひだと割れ目
    static void BarkPixel(float u, float v, out Color c, out float h)
    {
        float warp = Fbm(u, v, 3, 3, 5) * .35f;
        float ridge = Mathf.Abs(Mathf.Sin((u * 9 + warp * 4) * Mathf.PI));
        float crack = Smooth(.12f, .02f, ridge) * (.6f + .4f * Fbm(u, v, 6, 2, 9));
        float grain = Fbm(u * 1, v * 1, 16, 3, 13);
        h = ridge * .7f + grain * .3f - crack * .5f;
        float tone = .55f + .35f * ridge + .2f * grain - .35f * crack;
        float moss = Smooth(.62f, .78f, Fbm(u, v, 2, 3, 21)) * .35f;
        c = Color.Lerp(new Color(.62f, .5f, .4f) * tone, new Color(.32f, .38f, .2f), moss);
        c.a = 1;
    }

    // 葉のかたまり：小さな葉を丸く寄せて描く（外ほど明るく、内と下は暗い）
    static Texture2D LeafTexture(out Texture2D normal)
    {
        const int W = 1024;
        var col = new Color[W * W]; var hei = new float[W * W];
        var rnd = new System.Random(4242);
        float R() => (float)rnd.NextDouble();
        Color[] greens = { new Color(.36f, .58f, .22f), new Color(.30f, .52f, .2f), new Color(.44f, .64f, .26f), new Color(.26f, .47f, .2f), new Color(.5f, .68f, .3f) };
        int leaves = 230;
        var list = new List<(Vector2 p, float ang, float len, float wid, Color c, float depth)>();
        for (int i = 0; i < leaves; i++)
        {
            float rr = Mathf.Sqrt(R()) * .42f, a = R() * Mathf.PI * 2;
            var p = new Vector2(.5f + Mathf.Cos(a) * rr, .5f + Mathf.Sin(a) * rr);
            float ang = a + (R() - .5f) * 1.6f;
            float depth = rr / .42f;
            list.Add((p, ang, .06f + R() * .045f, .028f + R() * .018f, greens[(int)(R() * greens.Length)], depth));
        }
        // 内側（暗い）から外側へ描く
        foreach (var lf in list.OrderBy(l => l.depth))
        {
            var dir = new Vector2(Mathf.Cos(lf.ang), Mathf.Sin(lf.ang)); var perp = new Vector2(-dir.y, dir.x);
            var basePt = lf.p - dir * lf.len * .5f;
            int x0 = Mathf.Max(0, (int)((lf.p.x - lf.len) * W)), x1 = Mathf.Min(W - 1, (int)((lf.p.x + lf.len) * W));
            int y0 = Mathf.Max(0, (int)((lf.p.y - lf.len) * W)), y1 = Mathf.Min(W - 1, (int)((lf.p.y + lf.len) * W));
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var q = new Vector2((x + .5f) / W, (y + .5f) / W) - basePt;
                    float t = Vector2.Dot(q, dir) / lf.len; if (t < 0 || t > 1) continue;
                    float s = Vector2.Dot(q, perp) / (lf.wid * .5f);
                    float prof = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, .85f)), .75f);
                    if (Mathf.Abs(s) > prof) continue;
                    float edge = Mathf.Abs(s) / Mathf.Max(prof, 1e-3f);
                    float vein = Mathf.Abs(s) < .08f ? .82f : 1f;
                    float light = Mathf.Lerp(.55f, 1.05f, lf.depth) * (.86f + .22f * t) * vein * (1 - .18f * edge * edge);
                    light *= Mathf.Lerp(.85f, 1.08f, (y + .5f) / W);    // 上が明るい
                    var c = lf.c * light; c.a = 1;
                    int i = y * W + x; col[i] = c; hei[i] = (1 - edge * edge) * .6f + lf.depth * .4f;
                }
        }
        return Save("nature_leaves", W, W, col, hei, 4f, out normal, true, true);
    }

    // 針葉樹の枝：真ん中の軸から、先へ向かって斜めに針が出る扇形
    static Texture2D NeedleTexture(out Texture2D normal)
    {
        const int W = 512;
        var col = new Color[W * W]; var hei = new float[W * W];
        var rnd = new System.Random(777);
        float R() => (float)rnd.NextDouble();
        void Line(Vector2 a, Vector2 b, float width, Color c, float hh)
        {
            int x0 = Mathf.Max(0, (int)(Mathf.Min(a.x, b.x) * W) - 3), x1 = Mathf.Min(W - 1, (int)(Mathf.Max(a.x, b.x) * W) + 3);
            int y0 = Mathf.Max(0, (int)(Mathf.Min(a.y, b.y) * W) - 3), y1 = Mathf.Min(W - 1, (int)(Mathf.Max(a.y, b.y) * W) + 3);
            var ab = b - a; float l2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
            for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++)
            {
                var p = new Vector2((x + .5f) / W, (y + .5f) / W);
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                float d = (p - (a + ab * t)).magnitude * W;
                if (d > width) continue;
                int i = y * W + x; col[i] = c * (1 - .25f * d / width); col[i].a = 1; hei[i] = hh * (1 - d / width);
            }
        }
        // 軸（下の真ん中 → 上）
        for (int side = -1; side <= 1; side += 2)
            for (int k = 0; k < 70; k++)
            {
                float t = .05f + k / 70f * .9f;
                var on = new Vector2(.5f + Mathf.Sin(t * 2.5f) * .02f, t);
                float len = Mathf.Lerp(.32f, .1f, t) * (.75f + R() * .4f);
                var dir = new Vector2(side * .8f, .55f + R() * .25f).normalized;
                var c = Color.Lerp(new Color(.16f, .33f, .19f), new Color(.3f, .48f, .27f), R()) * Mathf.Lerp(.75f, 1.1f, t);
                Line(on, on + dir * len, 2.2f, c, .6f);
            }
        Line(new Vector2(.5f, .02f), new Vector2(.52f, .97f), 3.5f, new Color(.3f, .22f, .15f), 1f);
        return Save("nature_needles", W, W, col, hei, 3f, out normal, true, true);
    }

    // ================================================================ 町の庭の木
    // 庭（花壇）の木の位置（元の球の木の幹の位置。土の上 y=0.3）と種類
    static readonly (Vector3 pos, TreeKind kind, float yaw, float scale)[] GardenTrees =
    {
        (new Vector3(-33.05f, .3f, 15.05f), TreeKind.GardenA, 20, 1.0f),
        (new Vector3(-26.05f, .3f, 20.05f), TreeKind.GardenB, 140, .95f),
        (new Vector3(-15.05f, .3f, 16.05f), TreeKind.GardenA, 250, 1.05f),
        (new Vector3(-13.05f, .3f, 21.05f), TreeKind.GardenB, 80, 1.0f),
        (new Vector3(31.05f, .3f, 19.05f), TreeKind.GardenA, 310, .95f),
        (new Vector3(35.05f, .3f, 11.05f), TreeKind.GardenB, 200, 1.05f),
        (new Vector3(-36.05f, .3f, -20.05f), TreeKind.GardenA, 60, 1.0f),
        (new Vector3(36.05f, .3f, -21.05f), TreeKind.GardenB, 170, .95f),
    };

    static void PlaceTownTrees()
    {
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            var gardens = root.transform.Find(Gardens);
            if (gardens != null)
                foreach (Transform c in gardens.Cast<Transform>().ToArray())
                    if (c.name == "Tree trunk" || c.name == "Rounded tree canopy") Object.DestroyImmediate(c.gameObject);
            var old = root.transform.Find(TownTreeGroup);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(TownTreeGroup) { isStatic = true }.transform;
            group.SetParent(root.transform, false);
            foreach (var (pos, kind, yaw, scale) in GardenTrees)
            {
                var t = (GameObject)PrefabUtility.InstantiatePrefab(TreePrefab(kind), group);
                t.name = "Garden tree";
                t.transform.localPosition = pos; t.transform.localRotation = Quaternion.Euler(0, yaw, 0); t.transform.localScale = Vector3.one * scale;
            }
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    /// <summary>遠景の配置ツールから呼ぶ：parent の下に木を1本（プレハブ）。高さ height に合わせて大きさを決める。</summary>
    internal static void PlaceTree(Transform parent, TreeKind kind, float height, float yaw)
    {
        var t = (GameObject)PrefabUtility.InstantiatePrefab(TreePrefab(kind), parent);
        t.transform.localPosition = Vector3.zero;
        t.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        t.transform.localScale = Vector3.one * Mathf.Clamp(height / TreeHeight(kind), .6f, 1.5f);
    }
}
#endif
