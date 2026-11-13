#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 作り直し2-1：牢屋を0から作る（2026/11/13）。何度実行しても同じ結果。
/// ・ゲームの仕組みはそのまま：移送先（シーンの JailPoint）、作業台（JailLabor_Placeholder）、閉じ込める当たり判定
///   （「Community Jail」の床・奥の壁・左右の壁・屋根・前の梁・鉄格子・横棒・寝台・ベンチ）は位置も大きさも変えない。
///   これらの部品からは見た目（メッシュ）だけを外し、見た目は「Town jail」に0から組む。看板の文字と Cell light は残す。
/// ・外壁が厚くなった分だけ当たり判定を足す（外の面に合わせる）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string JailOld = "Community Jail";
    const string JailGroup = "Town jail";
    // 牢屋の内側（元の当たり判定の面）
    const float JX0 = 4.89f, JX1 = 11.91f, JZ0 = 15.1f, JZ1 = 22.21f, JFloor = .16f, JCeil = 3.45f;

    [MenuItem("Roomies/Art/Rebuild/1 Jail")]
    internal static void BuildJail()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            var old = root.transform.Find(JailOld);
            StripOldJail(old);
            var g = Fresh(root.transform, JailGroup);
            JailShell(g);
            JailFront(g);
            JailInterior(g);
            JailOutside(g);
            RoomiesBevelBuilder.ApplyTownUnder(g);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        RoomiesMaterialFinishBuilder.BuildTimeOfDay();   // 新しい光を朝夜の切り替えに登録する
        CombineForSpeed();
        Debug.Log("[Rebuild] jail built");
    }

    /// <summary>元の牢屋：当たり判定と文字と明かりは残し、見た目だけの部品とメッシュを外す。</summary>
    static void StripOldJail(Transform old)
    {
        if (old == null) return;
        foreach (Transform c in old.Cast<Transform>().ToArray())
        {
            if (c.GetComponent<TMPro.TextMeshPro>() != null || c.GetComponent<Light>() != null) continue;
            if (c.GetComponent<Collider>() == null) { Object.DestroyImmediate(c.gameObject); continue; }
            Object.DestroyImmediate(c.GetComponent<MeshRenderer>());
            Object.DestroyImmediate(c.GetComponent<MeshFilter>());
            foreach (Transform k in c.Cast<Transform>().ToArray()) Object.DestroyImmediate(k.gameObject);   // 寝台の見た目など
        }
    }

    static void Solid(Transform g, string name, Vector3 min, Vector3 max)
    {
        var go = new GameObject(name) { isStatic = true }; go.transform.SetParent(g, false);
        var c = go.AddComponent<BoxCollider>(); c.center = (min + max) / 2; c.size = max - min;
    }

    static void JailShell(Transform g)
    {
        var block = Mat("jail_block"); var low = Mat("jail_paint_low"); var high = Mat("jail_paint_high"); var floor = Mat("jail_floor");
        var cap = Mat("sill"); var slab = Mat("roof_flat");
        const float T = .32f, ox0 = JX0 - T - .14f, ox1 = JX1 + .26f, oz1 = JZ1 + T + .12f;
        // 外壁（コンクリートブロック）：左・右・奥。前は梁と柱
        Box(g, "Jail wall", ox0, JX0, 0, JCeil + .2f, JZ0 - .15f, oz1, block);
        Box(g, "Jail wall", JX1, ox1, 0, JCeil + .2f, JZ0 - .15f, oz1, block);
        Box(g, "Jail wall", JX0, JX1, 0, JCeil + .2f, JZ1, oz1, block);
        Box(g, "Jail plinth", ox0 - .04f, ox1 + .04f, 0, .28f, JZ0 - .19f, oz1 + .04f, Mat("plinth"));
        Solid(g, "Jail outer wall collider", new Vector3(ox0, 0, JZ0 - .15f), new Vector3(JX0, JCeil + .2f, oz1));
        Solid(g, "Jail outer wall collider", new Vector3(JX1, 0, JZ0 - .15f), new Vector3(ox1, JCeil + .2f, oz1));
        Solid(g, "Jail outer wall collider", new Vector3(JX0, 0, JZ1), new Vector3(JX1, JCeil + .2f, oz1));
        // 前の柱（格子の両わき）
        Box(g, "Jail pillar", ox0, JX0 + .12f, 0, JCeil + .2f, JZ0 - .24f, JZ0 + .1f, block);
        Box(g, "Jail pillar", JX1 - .12f, ox1, 0, JCeil + .2f, JZ0 - .24f, JZ0 + .1f, block);
        // 屋根（コンクリートの厚い板・水切り・前の庇）
        Box(g, "Jail roof slab", ox0 - .2f, ox1 + .2f, JCeil + .2f, JCeil + .42f, JZ0 - .8f, oz1 + .2f, slab);
        Box(g, "Jail roof parapet", ox0 - .2f, ox1 + .2f, JCeil + .42f, JCeil + .62f, oz1, oz1 + .2f, cap);
        Box(g, "Jail roof parapet", ox0 - .2f, ox0 + .05f, JCeil + .42f, JCeil + .62f, JZ0 - .8f, oz1 + .2f, cap);
        Box(g, "Jail roof parapet", ox1 - .05f, ox1 + .2f, JCeil + .42f, JCeil + .62f, JZ0 - .8f, oz1 + .2f, cap);
        Box(g, "Jail eave fascia", ox0 - .2f, ox1 + .2f, JCeil + .12f, JCeil + .42f, JZ0 - .86f, JZ0 - .78f, Mat("frame_h_navy"));
        // 内側：床（コンクリート・継ぎ目・排水口）、腰の塗り分け、天井
        Box(g, "Jail floor", JX0, JX1, JFloor - .02f, JFloor + .005f, JZ0, JZ1, floor);
        for (float z = JZ0 + 1.8f; z < JZ1; z += 1.8f) Box(g, "Floor joint", JX0, JX1, JFloor + .005f, JFloor + .008f, z - .01f, z + .01f, Mat("plinth"));
        Cyl(g, "Floor drain", new Vector3(8.4f, JFloor + .006f, 17.2f), new Vector3(.28f, .003f, .28f), Mat("metal_dark"));
        float wainscot = 1.25f;
        Box(g, "Jail wall paint", JX0, JX0 + .01f, JFloor, wainscot, JZ0, JZ1, low);
        Box(g, "Jail wall paint", JX1 - .01f, JX1, JFloor, wainscot, JZ0, JZ1, low);
        Box(g, "Jail wall paint", JX0, JX1, JFloor, wainscot, JZ1 - .01f, JZ1, low);
        Box(g, "Jail wall paint", JX0, JX0 + .01f, wainscot, JCeil, JZ0, JZ1, high);
        Box(g, "Jail wall paint", JX1 - .01f, JX1, wainscot, JCeil, JZ0, JZ1, high);
        Box(g, "Jail wall paint", JX0, JX1, wainscot, JCeil, JZ1 - .01f, JZ1, high);
        // 塗り分けの線（少し出た見切り）
        Box(g, "Wall trim", JX0, JX0 + .03f, wainscot - .03f, wainscot + .03f, JZ0, JZ1, Mat("metal_dark"));
        Box(g, "Wall trim", JX1 - .03f, JX1, wainscot - .03f, wainscot + .03f, JZ0, JZ1, Mat("metal_dark"));
        Box(g, "Wall trim", JX0, JX1, wainscot - .03f, wainscot + .03f, JZ1 - .03f, JZ1, Mat("metal_dark"));
        Box(g, "Jail ceiling", JX0, JX1, JCeil - .02f, JCeil, JZ0 - .1f, JZ1, Mat("jail_paint_high"));
        // 正の字（壁のいたずら書き）
        for (int k = 0; k < 4; k++) Box(g, "Tally mark", JX0 + .01f, JX0 + .015f, 1.55f, 1.85f, 18.2f + k * .07f, 18.22f + k * .07f, Mat("metal_dark"));
        RBox(g, "Tally mark", new Vector3(JX0 + .013f, 1.7f, 18.3f), new Vector3(.005f, .02f, .34f), new Vector3(30, 0, 0), Mat("metal_dark"));
    }

    static void JailFront(Transform g)
    {
        var bar = Mat("jail_bars"); var frame = Mat("metal_dark");
        // 上の梁（看板の板）
        Box(g, "Jail lintel", JX0 - .1f, JX1 + .1f, 2.78f, JCeil + .2f, 14.975f, JZ0 + .14f, Mat("jail_block"));
        // 文字（Sign - COMMUNITY JAIL, z 14.95）の後ろに板
        Box(g, "Jail sign plate", 6.45f, 10.35f, 2.9f, 3.34f, 14.955f, 14.975f, Mat("frame_h_navy"));
        Box(g, "Jail sign rivet", 6.5f, 6.56f, 3.09f, 3.15f, 14.945f, 14.955f, Mat("handle"));
        Box(g, "Jail sign rivet", 10.24f, 10.3f, 3.09f, 3.15f, 14.945f, 14.955f, Mat("handle"));
        // 枠・横の平鋼（元の横棒と同じ高さ）・丸い縦格子（元の格子と同じ間隔）
        Box(g, "Bar frame", JX0, JX1, .16f, .26f, JZ0 - .05f, JZ0 + .05f, frame);
        Box(g, "Bar frame", JX0, JX1, 2.68f, 2.8f, JZ0 - .05f, JZ0 + .05f, frame);
        foreach (float y in new[] { 1.18f }) Box(g, "Bar band", JX0, JX1, y - .04f, y + .04f, JZ0 - .035f, JZ0 + .035f, bar);
        for (float x = 4.95f; x <= 11.9f; x += .2775f)
        {
            if (x > 7.7f && x < 9.08f) continue;   // 扉の所は扉で描く
            Cyl(g, "Jail bar", new Vector3(x, 1.47f, JZ0), new Vector3(.045f, 1.22f, .045f), bar);
        }
        // 扉（蝶番・錠・差し入れ口）。開かない（閉じ込めるのは元の格子の当たり判定）
        float dx0 = 7.72f, dx1 = 9.08f;
        Box(g, "Jail door frame", dx0, dx0 + .07f, .16f, 2.68f, JZ0 - .06f, JZ0 + .06f, frame);
        Box(g, "Jail door frame", dx1 - .07f, dx1, .16f, 2.68f, JZ0 - .06f, JZ0 + .06f, frame);
        Box(g, "Jail door frame", dx0, dx1, 2.6f, 2.68f, JZ0 - .06f, JZ0 + .06f, frame);
        Box(g, "Jail door frame", dx0, dx1, .2f, .3f, JZ0 - .06f, JZ0 + .06f, frame);
        Box(g, "Jail door frame", dx0, dx1, 1.1f, 1.26f, JZ0 - .06f, JZ0 + .06f, frame);
        for (float x = dx0 + .2f; x < dx1 - .1f; x += .19f) Cyl(g, "Jail door bar", new Vector3(x, 1.45f, JZ0), new Vector3(.04f, 1.18f, .04f), bar);
        Box(g, "Jail door slot", dx0 + .38f, dx1 - .38f, 1.1f, 1.26f, JZ0 - .09f, JZ0 - .06f, Mat("plinth"));
        Box(g, "Jail lock box", dx1 - .3f, dx1 - .06f, 1.05f, 1.42f, JZ0 - .14f, JZ0 - .06f, Mat("frame_express"));
        Box(g, "Jail keyhole", dx1 - .2f, dx1 - .16f, 1.18f, 1.26f, JZ0 - .15f, JZ0 - .14f, Mat("metal_dark"));
        foreach (float y in new[] { .55f, 2.2f }) Cyl(g, "Jail hinge", new Vector3(dx0 - .03f, y, JZ0 - .08f), new Vector3(.08f, .12f, .08f), frame);
        // 警告の黄色い線（格子の内側）
        Box(g, "Warning line", JX0, JX1, JFloor + .005f, JFloor + .009f, JZ0 + .35f, JZ0 + .45f, Mat("frame_express"));
    }

    static void JailInterior(Transform g)
    {
        var steel = Mat("jail_bars"); var dark = Mat("metal_dark");
        // 鋼の2段ベッド（下段は元の寝台の当たり判定の上：x 5.15〜6.25, z 19.6〜21.6, 上面 0.63）
        float bx0 = 5.15f, bx1 = 6.25f, bz0 = 19.6f, bz1 = 21.6f;
        foreach (var (x, z) in new[] { (bx0 + .03f, bz0 + .03f), (bx1 - .03f, bz0 + .03f), (bx0 + .03f, bz1 - .03f), (bx1 - .03f, bz1 - .03f) })
            Box(g, "Bunk post", x - .03f, x + .03f, JFloor, 2.05f, z - .03f, z + .03f, steel);
        foreach (float y in new[] { .45f, 1.55f })
        {
            Box(g, "Bunk frame", bx0, bx1, y, y + .06f, bz0, bz1, steel);
            Box(g, "Bunk mattress", bx0 + .05f, bx1 - .05f, y + .06f, y + .18f, bz0 + .05f, bz1 - .05f, Mat("jail_mattress"));
            Box(g, "Bunk blanket", bx0 + .04f, bx1 - .04f, y + .18f, y + .21f, bz0 + .55f, bz1 - .06f, Mat("jail_blanket"));
            Box(g, "Bunk pillow", bx0 + .15f, bx1 - .15f, y + .18f, y + .28f, bz1 - .45f, bz1 - .12f, Mat("jail_pillow"));
        }
        Box(g, "Bunk rail", bx1 - .03f, bx1 + .02f, 1.85f, 1.9f, bz0, bz1, steel);
        for (float y = .75f; y < 1.5f; y += .25f) Box(g, "Bunk ladder rung", bx1 - .02f, bx1 + .03f, y, y + .03f, bz0 + .05f, bz0 + .45f, steel);
        // 壁付けのベンチ（元のベンチの当たり判定：x 11.1〜11.7, 上面 0.61）と持ち送り
        Box(g, "Steel bench", 11.1f, 11.7f, .53f, .61f, 19.45f, 21.65f, steel);
        foreach (float z in new[] { 19.7f, 20.55f, 21.4f }) RBox(g, "Bench bracket", new Vector3(11.65f, .4f, z), new Vector3(.4f, .05f, .05f), new Vector3(0, 0, -40), dark);
        // 便器と洗面台（ステンレスの一体型）。奥の右の角
        float tx = 10.65f, tz = JZ1 - .02f;
        Box(g, "Toilet tank", tx - .3f, tx + .3f, JFloor, 1.05f, tz - .28f, tz, steel);
        Box(g, "Toilet bowl", tx - .22f, tx + .22f, JFloor, .45f, tz - .72f, tz - .28f, steel);
        Box(g, "Toilet seat", tx - .2f, tx + .2f, .45f, .48f, tz - .7f, tz - .3f, dark);
        Box(g, "Sink basin", tx - .26f, tx + .26f, .98f, 1.08f, tz - .5f, tz - .28f, steel);
        Cyl(g, "Sink tap", new Vector3(tx, 1.13f, tz - .22f), new Vector3(.04f, .05f, .04f), Mat("handle"));
        Box(g, "Steel mirror", tx - .22f, tx + .22f, 1.45f, 1.85f, tz - .015f, tz, Mat("jail_mirror"));
        // 作業台の上の掲示（元の看板の文字の後ろの板）
        Box(g, "Notice board", 5.95f, 10.85f, 2.18f, 2.93f, JZ1 - .04f, JZ1, Mat("frame_h_navy"));
        Box(g, "Notice board frame", 5.9f, 10.9f, 2.13f, 2.18f, JZ1 - .05f, JZ1, dark);
        Box(g, "Notice board frame", 5.9f, 10.9f, 2.93f, 2.98f, JZ1 - .05f, JZ1, dark);
        // 高い所の鉄格子の窓（奥の壁。外へは抜けない）
        foreach (float x in new[] { 5.42f, 11.38f })
        {
            Box(g, "Cell window glass", x - .3f, x + .3f, 2.45f, 2.95f, JZ1 - .02f, JZ1 - .005f, Mat("jail_frosted"));
            Box(g, "Cell window frame", x - .34f, x + .34f, 2.4f, 2.45f, JZ1 - .06f, JZ1, dark);
            Box(g, "Cell window frame", x - .34f, x + .34f, 2.95f, 3f, JZ1 - .06f, JZ1, dark);
            for (float bx = x - .22f; bx <= x + .23f; bx += .11f) Box(g, "Cell window bar", bx - .012f, bx + .012f, 2.45f, 2.95f, JZ1 - .05f, JZ1 - .025f, steel);
        }
        // かごの付いた天井灯（明かりは元の Cell light）
        var cx = 8.4f; var cz = 19.4f;
        Cyl(g, "Cage light base", new Vector3(cx, JCeil - .05f, cz), new Vector3(.36f, .03f, .36f), dark);
        Cyl(g, "Cage light bulb", new Vector3(cx, JCeil - .17f, cz), new Vector3(.2f, .08f, .2f), Mat("bulb"));
        for (int k = 0; k < 6; k++)
        {
            float a = k * Mathf.PI / 3;
            RBox(g, "Cage light wire", new Vector3(cx + Mathf.Cos(a) * .15f, JCeil - .17f, cz + Mathf.Sin(a) * .15f), new Vector3(.012f, .22f, .012f), Vector3.zero, dark);
        }
        Cyl(g, "Cage light ring", new Vector3(cx, JCeil - .27f, cz), new Vector3(.32f, .006f, .32f), dark);
    }

    static void JailOutside(Transform g)
    {
        var dark = Mat("metal_dark");
        // 入口の上の灯具と、角の監視カメラ
        foreach (float x in new[] { 5.3f, 11.5f })
        {
            Box(g, "Jail wall lamp", x - .14f, x + .14f, 3.0f, 3.24f, JZ0 - .5f, JZ0 - .24f, dark);
            Box(g, "Jail wall lamp lens", x - .1f, x + .1f, 2.99f, 3.0f, JZ0 - .46f, JZ0 - .28f, Mat("bulb"));
        }
        Box(g, "Camera bracket", 12.2f, 12.26f, 3.1f, 3.16f, JZ0 - .1f, JZ0 + .25f, dark);
        RBox(g, "Security camera", new Vector3(12.22f, 3.05f, JZ0 - .2f), new Vector3(.16f, .14f, .34f), new Vector3(20, -25, 0), Mat("ac_unit"));
        Box(g, "Camera lens", 12.16f, 12.26f, 2.99f, 3.07f, JZ0 - .4f, JZ0 - .36f, dark);
        // 前を照らす光（夜だけ。名前の頭 Shop light で朝に消える）
        SpotLight(g, "Shop light - JAIL", new Vector3(8.4f, 3.4f, 13.6f), new Vector3(8.4f, .6f, 15.6f), 4.5f, 9f, 115f, LampColor);
        // 外の壁の注意書きの板
        Box(g, "Notice plate", JX1 + .26f, JX1 + .28f, 1.3f, 1.8f, 16f, 16.7f, Mat("frame_express"));
        // 雨どい
        Box(g, "Downpipe", JX0 - .5f, JX0 - .42f, 0, JCeil + .3f, JZ1 + .3f, JZ1 + .38f, dark);
    }
}
#endif
