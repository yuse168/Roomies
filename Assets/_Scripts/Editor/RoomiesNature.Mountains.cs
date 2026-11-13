#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 自然物 2：山。遠景の配置ツール（RoomiesBackdropBuilder.BuildMountains）が使う。
/// ・形：尾根の出るノイズ（ridged）を重ねた、なめらかな起伏と稜線のある高さの場（48×48 の格子）。細かい起伏はノーマルマップに焼く。
/// ・色：斜面の角度と高さで、草地・森・岩肌・雪を切り替えたテクスチャを焼く（形ごとに雪あり／なしの2枚）。
/// ・遠い山ほど青みを足す（空気の遠近。町のかわいい色合いのまま）。素材は輪（近・中・遠）ごと。
/// メッシュは Assets/_Art/Restyle/Nature/Mountains.asset。原点は足元の真ん中、幅1・高さ1（配置で大きさを掛ける）。
/// </summary>
internal static partial class RoomiesNature
{
    const string MountainMeshPath = ArtDir + "Mountains.asset";
    const int MountainGrid = 48;
    const float MountainAspect = 1f / 1.6f;   // 高さ ÷ 幅（配置のだいたいの比。斜面の角度の計算に使う）
    static readonly Dictionary<int, float> mountainMax = new Dictionary<int, float>();

    /// <summary>形 shape（0〜2）の高さ（0〜1）。x, z は −0.5〜0.5。外の縁は地面より下。</summary>
    static float MountainH(int shape, float x, float z)
    {
        int seed = 1000 + shape * 977;
        float u = x + .5f, v = z + .5f;
        float wx = (Fbm(u, v, 2, 3, seed + 1) - .5f) * .28f, wz = (Fbm(u, v, 2, 3, seed + 2) - .5f) * .28f;
        float r = Mathf.Sqrt((x + wx) * (x + wx) + (z + wz) * (z + wz)) * 2f;
        if (r >= 1f) return -.03f;
        // 裾はなだらか、上ほど急（凹んだ形の山）
        float baseP = Mathf.Pow(1f - r, 1.75f);
        float ridged = 0, amp = .5f, f = 3;
        for (int o = 0; o < 6; o++)
        {
            float n = VNoise(u * f + 17 * shape, v * f, 0, seed + o * 13);
            n = 1 - Mathf.Abs(2 * n - 1); n *= n;
            ridged += n * amp; amp *= .5f; f *= 2.03f;
        }
        // 形ごとに、肩（もう一つの小さな頂）を足す
        Vector2 sp = shape == 0 ? new Vector2(.16f, -.1f) : shape == 1 ? new Vector2(-.18f, .12f) : new Vector2(.05f, .2f);
        float shoulder = Mathf.Exp(-((x - sp.x) * (x - sp.x) + (z - sp.y) * (z - sp.y)) / .018f) * .32f;
        return baseP * (.55f + .6f * ridged) + shoulder * Mathf.Sqrt(baseP);
    }

    static float MountainMax(int shape)
    {
        if (mountainMax.TryGetValue(shape, out var m)) return m;
        m = 0;
        for (int i = 0; i <= 160; i++) for (int j = 0; j <= 160; j++) m = Mathf.Max(m, MountainH(shape, i / 160f - .5f, j / 160f - .5f));
        mountainMax[shape] = m;
        return m;
    }

    static float MH(int shape, float x, float z) { float h = MountainH(shape, x, z); return h < 0 ? h : h / MountainMax(shape); }

    internal static Mesh MountainMesh(int shape)
    {
        string name = "mountain_" + shape;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MountainMeshPath)) if (o is Mesh m && m.name == name) return m;
        var mb = new MB(1);
        int N = MountainGrid;
        for (int j = 0; j <= N; j++)
            for (int i = 0; i <= N; i++)
            {
                float x = i / (float)N - .5f, z = j / (float)N - .5f;
                mb.v.Add(new Vector3(x, Mathf.Max(MH(shape, x, z), -.03f), z)); mb.n.Add(Vector3.up); mb.uv.Add(new Vector2(x + .5f, z + .5f));
            }
        for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                int a = j * (N + 1) + i, b = a + 1, c = a + N + 1, d = c + 1;
                mb.t[0].Add(a); mb.t[0].Add(c); mb.t[0].Add(b);
                mb.t[0].Add(b); mb.t[0].Add(c); mb.t[0].Add(d);
            }
        var mesh = mb.ToMesh(name);
        mesh.RecalculateNormals(); mesh.RecalculateTangents();
        return StoreMesh(MountainMeshPath, mesh);
    }

    static readonly Dictionary<string, Material> mountainMats = new Dictionary<string, Material>();

    /// <summary>山の素材：形・雪の有無・輪（0 近い〜2 遠い）。</summary>
    internal static Material MountainMaterial(int shape, bool snow, int ring)
    {
        string key = $"M_Mountain_{shape}{(snow ? "_snow" : "")}_r{ring}";
        if (mountainMats.TryGetValue(key, out var m) && m != null) return m;
        string tex = $"nature_mountain_{shape}{(snow ? "_snow" : "")}";
        var albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex + ".png");
        var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + tex + "_n.png");
        if (albedo == null || normal == null || rebakeMountains.Add(tex)) albedo = Tex(tex, 1024, 1024, (float u, float v, out Color c, out float h) => MountainPixel(shape, snow, u, v, out c, out h), 5f, out normal, false, true);
        Color[] tints = { new Color(.92f, 1f, .97f), new Color(.8f, .88f, 1f), new Color(.7f, .8f, 1f) };
        m = Lit(key, albedo, normal, tints[Mathf.Clamp(ring, 0, 2)], .08f, false, .8f);
        mountainMats[key] = m;
        return m;
    }

    static readonly HashSet<string> rebakeMountains = new HashSet<string>();

    /// <summary>山を作り直す前に呼ぶ：メッシュとテクスチャを焼き直す。</summary>
    internal static void ResetMountains()
    {
        mountainMats.Clear(); rebakeMountains.Clear(); mountainMax.Clear();
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(MountainMeshPath))
            if (o is Mesh m && !AssetDatabase.IsMainAsset(o)) Object.DestroyImmediate(m, true);
    }

    static void MountainPixel(int shape, bool snow, float u, float v, out Color c, out float h)
    {
        float x = u - .5f, z = v - .5f, e = 1f / 512f;
        float hc = MH(shape, x, z);
        if (hc <= 0) { c = new Color(.42f, .58f, .33f); h = 0; return; }
        float gx = (MH(shape, x + e, z) - MH(shape, x - e, z)) / (2 * e) * MountainAspect;
        float gz = (MH(shape, x, z + e) - MH(shape, x, z - e)) / (2 * e) * MountainAspect;
        float slope = Mathf.Sqrt(gx * gx + gz * gz);                     // 1 で 45°
        float n1 = Fbm(u, v, 8, 4, 31 + shape), n2 = Fbm(u, v, 32, 3, 47 + shape);
        // 岩の層（横の筋）
        float strata = Mathf.Sin((hc * 38f + n1 * 3f) * Mathf.PI) * .5f + .5f;
        Color grass = Color.Lerp(new Color(.4f, .58f, .3f), new Color(.5f, .64f, .34f), n2);
        Color forest = Color.Lerp(new Color(.17f, .33f, .21f), new Color(.24f, .4f, .25f), n2);
        Color rock = Color.Lerp(new Color(.5f, .5f, .52f), new Color(.58f, .57f, .58f), strata * .25f + n2 * .5f);
        Color scree = new Color(.55f, .53f, .5f);
        Color snowC = Color.Lerp(new Color(.86f, .9f, .97f), new Color(.98f, .99f, 1f), n2);

        float wForest = Smooth(.04f, .14f, hc) * Smooth(.66f, .48f, hc) * Smooth(.38f, .55f, Fbm(u, v, 6, 3, 59 + shape)) * Smooth(1.4f, 1.0f, slope);
        float wRock = Mathf.Max(Smooth(1.4f, 2.0f, slope + (n1 - .5f) * .5f), Smooth(.66f, .86f, hc + (n1 - .5f) * .18f));
        float wScree = Smooth(.8f, 1.2f, slope) * Smooth(.35f, .6f, hc) * (1 - wRock) * .6f;
        c = Color.Lerp(grass, forest, wForest);
        c = Color.Lerp(c, scree, wScree);
        c = Color.Lerp(c, rock, wRock);
        if (snow)
        {
            float line = .68f + (n1 - .5f) * .14f;
            float wSnow = Smooth(line, line + .06f, hc) * Smooth(1.9f, 1.2f, slope);
            // 谷筋に雪が残る
            wSnow = Mathf.Max(wSnow, Smooth(line - .12f, line, hc) * Smooth(.6f, .3f, slope) * .8f);
            c = Color.Lerp(c, snowC, wSnow);
        }
        c *= .95f + .1f * n2;
        c.a = 1;
        // ノーマルマップは細かい起伏だけ（山全体の傾きはメッシュが持つ）
        h = Fbm(u, v, 24, 4, 61 + shape) * .45f + Fbm(u, v, 96, 2, 71 + shape) * .25f * (.4f + wRock);
    }
}
#endif
