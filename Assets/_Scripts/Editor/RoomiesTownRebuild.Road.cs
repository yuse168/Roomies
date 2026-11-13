#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 作り直し2-4：町の道路を0から作る（2026/11/13）。何度実行しても同じ結果。
/// ・車道（Demo/pCube8 の上）：骨材・ひび・補修の跡・タイヤの跡のあるアスファルト（自作の手続きテクスチャとノーマルマップ）。
/// ・路面の線：中央の黄色の2本線、両端の白線、横断歩道（x ±18。元と同じ位置）と停止線。
/// ・縁石（1m ごとの石）と側溝の帯、平板の歩道、横断歩道の所の下げた縁石と黄色の点字ブロック。
/// ・元の見た目（歩道の板・縁石・破線・横断歩道の板・目地）は消す。どれも当たり判定はなかった（歩く面は Demo/pPlane1）。
/// ・マンホール・雨水ます・街灯・ベンチなどはそのまま。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string RoadGroup = "Town road";
    const float RoadZ = 3.98f, CurbZ = 4.13f, WalkZ = 6.4f, RoadTop = .1f, WalkTop = .15f, TownHalf = 40f;
    static readonly float[] CrosswalkX = { -18f, 18f };

    [MenuItem("Roomies/Art/Rebuild/4 Road")]
    internal static void BuildRoad()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            var street = root.transform.Find("Street - sidewalks and wayfinding");
            if (street != null)
                foreach (Transform c in street.Cast<Transform>().ToArray())
                    if (new[] { "Lane dash", "Crosswalk stripe", "Paving joint", "Kerb", "Sidewalk" }.Contains(c.name) && c.GetComponent<Collider>() == null)
                        Object.DestroyImmediate(c.gameObject);
            var g = Fresh(root.transform, RoadGroup);
            RoadSurface(g);
            RoadMarkings(g);
            CurbsAndWalks(g);
            RoomiesBevelBuilder.ApplyTownUnder(g);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        CombineForSpeed();
        Debug.Log("[Rebuild] road built");
    }

    static GameObject TiledQuad(Transform g, string name, float x0, float x1, float z0, float z1, float y, Material m)
    {
        var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
        q.name = name; Object.DestroyImmediate(q.GetComponent<Collider>());
        q.transform.SetParent(g, false);
        q.transform.localPosition = new Vector3((x0 + x1) / 2, y, (z0 + z1) / 2);
        q.transform.localRotation = Quaternion.Euler(90, 0, 0);
        q.transform.localScale = new Vector3(x1 - x0, z1 - z0, 1);
        q.GetComponent<MeshRenderer>().sharedMaterial = m;
        q.isStatic = true;
        return q;
    }

    static void RoadSurface(Transform g)
    {
        // テクスチャ1枚が車道の幅いっぱい（v）× 8m（u）
        var tex = RoomiesNature.Tex("road_asphalt", 1024, 1024, AsphaltPixel, 6f, out var n);
        var m = RoomiesNature.Lit("M_Road_Asphalt", tex, n, Color.white, .12f, false, 1f);
        m.SetTextureScale("_BaseMap", new Vector2(TownHalf * 2 / 8f, 1));
        TiledQuad(g, "Road asphalt", -TownHalf, TownHalf, -RoadZ, RoadZ, RoadTop + .003f, m);
        // 側溝の帯（縁石の手前）
        foreach (float s in new[] { -1f, 1f })
            Box(g, "Gutter", -TownHalf, TownHalf, RoadTop, RoadTop + .005f, s > 0 ? RoadZ - .38f : -RoadZ, s > 0 ? RoadZ : -RoadZ + .38f, Mat("road_gutter"));
    }

    // アスファルト：細かい骨材のつぶつぶ、タイヤの通る所の黒ずみ、ひび、補修の跡、油じみ（u：道の向き 8m、v：道の幅）
    static void AsphaltPixel(float u, float v, out Color c, out float h)
    {
        float grain = RoomiesNature.VNoise(u * 900, v * 900, 900, 11) * .6f + RoomiesNature.VNoise(u * 400, v * 400, 400, 13) * .4f;
        float big = RoomiesNature.Fbm(u, v, 3, 4, 17);
        float tone = .3f + .06f * (big - .5f) + .05f * (grain - .5f);
        // タイヤの跡（各車線の2本ずつ）
        foreach (float lane in new[] { .18f, .32f, .68f, .82f }) tone -= .03f * Mathf.Exp(-Mathf.Pow((v - lane) / .035f, 2)) * (.6f + .4f * RoomiesNature.Fbm(u, v, 12, 2, 19));
        // 補修の跡（四角いつぎはぎ、少し色が違う）
        float pu = Mathf.Floor(u * 3), pv = Mathf.Floor(v * 4);
        bool patch = RoomiesNature.Rand01((int)pu, (int)pv + 40) > .8f;
        float pfu = u * 3 - pu, pfv = v * 4 - pv;
        if (patch && pfu > .15f && pfu < .75f && pfv > .2f && pfv < .8f) tone = tone * .82f + .02f;
        // ひび（細い線）
        float crack = 0;
        for (int k = 0; k < 2; k++)
        {
            float w = RoomiesNature.Fbm(u * (2 + k), v * (2 + k), 4, 4, 23 + k * 7);
            crack = Mathf.Max(crack, RoomiesNature.Smooth(.004f, 0f, Mathf.Abs(w - .5f)) * RoomiesNature.Smooth(.68f, .8f, RoomiesNature.Fbm(u, v, 3, 2, 31 + k)));
        }
        tone *= 1 - .3f * crack;
        // 油じみ（車線の真ん中にときどき）
        float oil = RoomiesNature.Smooth(.74f, .86f, RoomiesNature.Fbm(u, v, 6, 3, 37)) * (Mathf.Exp(-Mathf.Pow((v - .25f) / .08f, 2)) + Mathf.Exp(-Mathf.Pow((v - .75f) / .08f, 2)));
        tone *= 1 - .25f * oil;
        c = new Color(tone * .96f, tone * .98f, tone * 1.05f, 1);
        h = grain * .7f - crack * .4f + big * .2f;
    }

    static void RoadMarkings(Transform g)
    {
        var yellow = Mat("road_paint_yellow"); var white = Mat("road_paint_white");
        float y0 = RoadTop + .004f, y1 = RoadTop + .007f;
        // 中央の黄色の2本線（横断歩道の所は切る）
        float[] cuts = CrosswalkX.SelectMany(x => new[] { x - 1.6f, x + 1.6f }).ToArray();
        var xs = new[] { -TownHalf }.Concat(cuts).Concat(new[] { TownHalf }).OrderBy(x => x).ToArray();
        for (int i = 0; i < xs.Length - 1; i += 2)
            foreach (float z in new[] { -.09f, .09f }) Box(g, "Center line", xs[i], xs[i + 1], y0, y1, z - .05f, z + .05f, yellow);
        // 両端の白線
        foreach (float s in new[] { -1f, 1f }) Box(g, "Edge line", -TownHalf, TownHalf, y0, y1, s * 3.4f - .06f, s * 3.4f + .06f, white);
        // 横断歩道（元と同じ：2.3m の白い帯を 1m おきに）と停止線
        foreach (float cx in CrosswalkX)
        {
            for (float z = -3f; z <= 3.01f; z += 1f) Box(g, "Crosswalk bar", cx - 1.15f, cx + 1.15f, y0, y1 + .001f, z - .25f, z + .25f, white);
            Box(g, "Stop line", cx - 2.1f, cx - 1.8f, y0, y1, .2f, 3.35f, white);
            Box(g, "Stop line", cx + 1.8f, cx + 2.1f, y0, y1, -3.35f, -.2f, white);
        }
    }

    static void CurbsAndWalks(Transform g)
    {
        var curb = Mat("curb_granite"); var lowCurb = Mat("curb_granite");
        var tex = RoomiesNature.Tex("sidewalk_slabs", 512, 512, SlabPixel, 4f, out var n);
        var walk = RoomiesNature.Lit("M_Sidewalk_Slabs", tex, n, Color.white, .08f, false, .8f);
        float depth = WalkZ - CurbZ;
        walk.SetTextureScale("_BaseMap", new Vector2(TownHalf * 2 / 2.4f, depth / 2.4f * 2));
        foreach (float s in new[] { -1f, 1f })
        {
            float zr = s * RoadZ, zc = s * CurbZ, zw = s * WalkZ;
            // 縁石：1m の石。横断歩道の所は低く（段差を下げる）
            for (float x = -TownHalf; x < TownHalf - .01f; x += 1f)
            {
                bool lowered = CrosswalkX.Any(cx => Mathf.Abs(x + .5f - cx) < 1.3f);
                float top = lowered ? RoadTop + .03f : WalkTop + .02f;
                Box(g, "Curb stone", x + .005f, x + .995f, 0, top, Mathf.Min(zr, zc), Mathf.Max(zr, zc), lowered ? lowCurb : curb);
            }
            // 歩道の平板（板の上に模様の面）
            Box(g, "Sidewalk base", -TownHalf, TownHalf, 0, WalkTop - .005f, Mathf.Min(zc, zw), Mathf.Max(zc, zw), Mat("paving"));
            TiledQuad(g, "Sidewalk slabs", -TownHalf, TownHalf, Mathf.Min(zc, zw), Mathf.Max(zc, zw), WalkTop, walk);
            // 横断歩道の所の点字ブロック（歩道の縁）
            foreach (float cx in CrosswalkX)
            {
                float tz0 = s > 0 ? CurbZ + .05f : -CurbZ - .65f, tz1 = tz0 + .6f;
                Box(g, "Tactile paving", cx - 1.1f, cx + 1.1f, WalkTop, WalkTop + .008f, tz0, tz1, Mat("tactile_yellow"));
                // 点状の突起（10cm おき）
                for (float x = cx - 1.05f; x < cx + 1.05f; x += .1f)
                    for (float z = tz0 + .05f; z < tz1 - .02f; z += .1f)
                        Box(g, "Tactile dot", x - .025f, x + .025f, WalkTop + .008f, WalkTop + .016f, z - .025f, z + .025f, Mat("tactile_yellow"));
            }
        }
    }

    // 歩道の平板：60cm 角の板を目地で区切り、板ごとに少し色を変える。小さなしみ（1枚 = 2.4m 四方の 4×4 枚）
    static void SlabPixel(float u, float v, out Color c, out float h)
    {
        float gu = u * 4, gv = v * 4; int iu = Mathf.FloorToInt(gu), iv = Mathf.FloorToInt(gv);
        float fu = gu - iu, fv = gv - iv;
        float joint = Mathf.Max(RoomiesNature.Smooth(.03f, .0f, Mathf.Min(fu, 1 - fu)), RoomiesNature.Smooth(.03f, .0f, Mathf.Min(fv, 1 - fv)));
        float tone = .74f + .08f * (RoomiesNature.Rand01(iu, iv + 77) - .5f) + .05f * (RoomiesNature.Fbm(u, v, 16, 3, 41) - .5f);
        float stain = RoomiesNature.Smooth(.72f, .82f, RoomiesNature.Fbm(u, v, 5, 3, 43));
        tone *= 1 - .12f * stain;
        tone = Mathf.Lerp(tone, .5f, joint);
        c = new Color(tone * 1.0f, tone * .98f, tone * .95f, 1);
        h = (1 - joint) * .6f + RoomiesNature.VNoise(u * 300, v * 300, 300, 47) * .2f;
    }
}
#endif
