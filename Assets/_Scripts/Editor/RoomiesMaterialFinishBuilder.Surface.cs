#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 2 質感：1色で塗った面に、手続きで作った質感テクスチャとノーマルマップを足す。床と壁に汚れのデカールを置く。
/// テクスチャは Assets/_Art/Restyle/Textures/finish_*.png（決まった乱数なので何度作っても同じ絵）。
/// デカールはシーンの「Roomies_Decals」に置く（見た目だけ。スクリプトなし）。位置を直したら下の表もそろえること。
/// </summary>
internal static partial class RoomiesMaterialFinishBuilder
{
    const string TexDir = "Assets/_Art/Restyle/Textures/";
    const string DecalRootName = "Roomies_Decals";
    const float FloorY = 3.2725f;

    // 床の汚れ：x, z, 大きさ, 向き, 種類（0 しみ / 1 すり跡）
    static readonly float[,] FloorDecals =
    {
        { 19.6f, 18.2f, .9f, 20, 0 }, { 20.8f, 20.6f, .6f, 75, 0 }, { 15.3f, 17.2f, .7f, 140, 0 }, { 17.2f, 22.4f, .55f, 30, 0 },
        { 13.9f, 20.5f, .8f, 0, 0 }, { 22.0f, 21.5f, .7f, 10, 0 }, { 16.9f, 19.2f, .6f, 200, 0 },
        { 18.9f, 16.0f, 1.6f, 0, 1 }, { 21.3f, 16.9f, 1.3f, 90, 1 }, { 19.4f, 20.9f, 1.2f, 35, 1 },
    };
    // 壁の下の黒ずみ：x, z, 幅, 外向き（壁の方向）の角度
    static readonly float[,] WallDecals =
    {
        { 15.0f, 23.43f, 2.6f, 0 }, { 18.6f, 23.43f, 2.2f, 0 }, { 21.4f, 23.43f, 2.0f, 0 },
        { 15.6f, 13.93f, 2.4f, 180 }, { 19.2f, 13.93f, 2.6f, 180 },
        { 22.68f, 17.0f, 2.4f, 90 }, { 22.68f, 20.6f, 2.2f, 90 },
        { 13.18f, 16.2f, 2.4f, 270 }, { 13.18f, 21.0f, 2.2f, 270 },
    };

    [MenuItem("Roomies/Art/Restyle/Material Finish/2 Surface Detail")]
    internal static void ApplySurfaceDetail()
    {
        ApplySurfaceTextures();
        foreach (var scene in Enumerable.Range(0, SceneManager.sceneCount).Select(SceneManager.GetSceneAt))
            if (scene.isLoaded && scene.GetRootGameObjects().Any(g => g.name == "Roomies_RoomRestyle")) PlaceDecals(scene);
    }

    /// <summary>素材にテクスチャとノーマルマップを付ける（シーンは触らない）。配置ツールの最後にも呼ばれる。</summary>
    internal static void ApplySurfaceTextures()
    {
        var floor = Make("finish_floor", 1024, FloorPixel, out var floorN, 5f);
        var plaster = Make("finish_plaster", 512, PlasterPixel, out var plasterN, 2.2f);
        var weave = Make("finish_weave", 512, WeavePixel, out var weaveN, 3.5f);
        var board = Make("finish_cardboard", 512, CardboardPixel, out var boardN, 3f);

        Surface("floor_planks", floor, floorN, .9f);
        Surface("wall_plaster", plaster, plasterN, .7f);
        Surface("ceiling", plaster, plasterN, .5f, new Vector2(4, 4));
        foreach (var n in new[] { "sofa_yellow", "sofa_yellow_dark", "fabric_grey", "rug", "rug_edge", "curtain",
                     "bed_duvet", "bed_duvet_light", "bed_sheet", "brown", "shark_blue", "shark_belly" })
            Surface(n, weave, weaveN, .8f);
        foreach (var n in new[] { "cloth_blue", "cloth_cream", "pillow_teal", "plush_grey" })
            Surface(n, weave, weaveN, .8f, new Vector2(2, 2));
        Surface("cardboard", board, boardN, .8f);
        Surface("cardboard_dark", board, boardN, .8f);
        AssetDatabase.SaveAssets();
    }

    static void Surface(string material, Texture2D map, Texture2D normal, float bump, Vector2? tiling = null)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Restyle + material + ".mat");
        if (mat == null) { Debug.LogWarning("[Material Finish] not found: " + material); return; }
        mat.SetTexture("_BaseMap", map);
        if (tiling.HasValue) mat.SetTextureScale("_BaseMap", tiling.Value);
        mat.SetTexture("_BumpMap", normal);
        mat.SetFloat("_BumpScale", bump);
        mat.EnableKeyword("_NORMALMAP");
        EditorUtility.SetDirty(mat);
    }

    // ================================================================ デカール
    static void PlaceDecals(Scene scene)
    {
        var stain = DecalMat("decal_stain", Make("finish_decal_stain", 512, StainPixel, out _, 0));
        var scuff = DecalMat("decal_scuff", Make("finish_decal_scuff", 512, ScuffPixel, out _, 0));
        var grime = DecalMat("decal_grime", Make("finish_decal_grime", 512, GrimePixel, out _, 0));

        foreach (var old in scene.GetRootGameObjects().Where(g => g.name == DecalRootName).ToArray()) Object.DestroyImmediate(old);
        var root = new GameObject(DecalRootName) { isStatic = true };
        SceneManager.MoveGameObjectToScene(root, scene);

        for (int i = 0; i < FloorDecals.GetLength(0); i++)
        {
            float size = FloorDecals[i, 2];
            bool isScuff = FloorDecals[i, 4] > 0;
            Decal(root.transform, isScuff ? "Floor scuff" : "Floor stain", new Vector3(FloorDecals[i, 0], FloorY + .05f, FloorDecals[i, 1]),
                Quaternion.Euler(90, FloorDecals[i, 3], 0), new Vector3(size, isScuff ? size * .45f : size, .4f), isScuff ? scuff : stain, isScuff ? .55f : .8f);
        }
        for (int i = 0; i < WallDecals.GetLength(0); i++)
            Decal(root.transform, "Wall grime", new Vector3(WallDecals[i, 0], FloorY + .42f, WallDecals[i, 1]),
                Quaternion.Euler(0, WallDecals[i, 3], 0), new Vector3(WallDecals[i, 2], .8f, .7f), grime, .85f);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[Material Finish] decals placed in {scene.name}");
    }

    static void Decal(Transform parent, string name, Vector3 pos, Quaternion rot, Vector3 size, Material mat, float opacity)
    {
        var go = new GameObject(name) { isStatic = true };
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(pos, rot);
        var d = go.AddComponent<DecalProjector>();
        d.material = mat; d.size = size; d.pivot = Vector3.zero; d.fadeFactor = opacity;
        d.drawDistance = 30; d.startAngleFade = 60; d.endAngleFade = 80;
    }

    static Material DecalMat(string name, Texture2D map)
    {
        string path = Restyle + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Shader Graphs/Decal")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("Base_Map", map);
        mat.SetFloat("Normal_Blend", 0);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    // ================================================================ テクスチャづくり
    delegate void Pixel(float u, float v, out Color albedo, out float height);

    /// <summary>色と高さを作り、色の png と（bump &gt; 0 なら）高さから作ったノーマルマップの png を保存する。</summary>
    static Texture2D Make(string name, int size, Pixel pixel, out Texture2D normalMap, float bump)
    {
        var colors = new Color[size * size]; var heights = new float[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            pixel((x + .5f) / size, (y + .5f) / size, out var c, out var h);
            colors[y * size + x] = c; heights[y * size + x] = h;
        }
        var albedo = Save(name, size, colors, false);
        normalMap = null;
        if (bump <= 0) return albedo;
        var normals = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            float H(int dx, int dy) => heights[((y + dy + size) % size) * size + (x + dx + size) % size];
            var n = new Vector3((H(-1, 0) - H(1, 0)) * bump, (H(0, -1) - H(0, 1)) * bump, 1).normalized;
            normals[y * size + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
        }
        normalMap = Save(name + "_n", size, normals, true);
        return albedo;
    }

    static Texture2D Save(string name, int size, Color[] px, bool normal)
    {
        string path = TexDir + name + ".png";
        Directory.CreateDirectory(TexDir);
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false, normal);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        bool decal = name.Contains("decal");
        if (importer.textureType != type || importer.wrapMode != (decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat) || importer.anisoLevel != 4 || importer.alphaIsTransparency != decal)
        {
            importer.textureType = type; importer.mipmapEnabled = true; importer.anisoLevel = 4;
            importer.wrapMode = decal ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
            importer.alphaIsTransparency = decal;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    static float N(float u, float v, float scale, float seed) => RoomiesRestyleKit.Noise(u, v, scale, seed);
    static float S(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
    static float Hash(float a, float b) { float h = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f; return h - Mathf.Floor(h); }

    // 床：元の floor_planks と同じ割り付け（横に6枚・縦に2枚、板ごとにずらす）。木目・節・継ぎ目の溝・すり減り
    static void FloorPixel(float u, float v, out Color c, out float h)
    {
        float fu = u * 6; int plank = Mathf.FloorToInt(fu); float inPlank = fu - plank;
        float offset = Mathf.PerlinNoise(plank * 3.7f, 9.1f);
        float vv = Mathf.Repeat(v + offset, 1);
        float board = Mathf.Floor(vv * 2), inBoard = Mathf.Repeat(vv * 2, 1);
        float id = plank * 2 + board;
        float tone = .7f + .3f * Hash(id, 1.7f);
        // 木目：板ごとにずらした縦の筋と、ゆるい年輪
        float warp = N(u, v, 6, id * .37f) * .08f;
        float rings = Mathf.Sin((inPlank + warp + Hash(id, 4.1f)) * 38f + Mathf.Sin(vv * 9 + id) * 1.5f) * .5f + .5f;
        float streak = Mathf.PerlinNoise(u * 420, v * 14 + id) * .6f + Mathf.PerlinNoise(u * 1500, v * 40) * .4f;
        tone *= .82f + .1f * rings + .14f * streak;
        // 節（ときどき）
        float knotU = Hash(id, 8.3f), knotV = Hash(id, 2.9f);
        float kd = new Vector2((inPlank - knotU) * 1.0f, (inBoard - knotV) * 6f).magnitude;
        bool hasKnot = Hash(id, 5.5f) > .65f;
        float knot = hasKnot ? 1 - S(.04f, .12f, kd) : 0;
        tone *= 1 - .35f * knot;
        // すり減り・汚れ（通る所は明るく、すみは暗く）
        float wear = S(.55f, .85f, N(u, v, 3, 21));
        float grime = S(.5f, .8f, N(u, v, 7, 13));
        tone *= 1 + .1f * wear - .18f * grime;
        // 継ぎ目
        float seamPx = Mathf.Min(inPlank, 1 - inPlank) * 1024f / 6f;
        float jointPx = Mathf.Min(inBoard, 1 - inBoard) * 512f;
        float groove = Mathf.Min(S(0, 2.5f, seamPx), S(0, 2f, jointPx));
        tone *= Mathf.Lerp(.45f, 1, groove);
        c = new Color(tone, tone * .96f, tone * .9f, 1);
        // 高さ：溝・板の角の丸み・木目のでこぼこ
        float bevel = Mathf.Min(S(0, 7f, seamPx), S(0, 6f, jointPx));
        h = .6f * groove + .25f * bevel + .05f * rings + .04f * streak - .1f * knot;
    }

    // 壁：しっくいのでこぼこ、色むら、うっすらした汚れ
    static void PlasterPixel(float u, float v, out Color c, out float h)
    {
        float bumps = N(u, v, 24, 2) * .5f + N(u, v, 60, 5) * .3f + Mathf.PerlinNoise(u * 200, v * 200) * .2f;
        float mottle = N(u, v, 4, 7);
        float stain = S(.58f, .8f, N(u, v, 9, 31)) * .5f + S(.62f, .78f, N(u, v, 3, 41)) * .5f;
        float t = .86f + .1f * mottle + .05f * bumps - .14f * stain;
        c = new Color(t, t * .98f, t * .95f, 1);
        h = bumps * .6f + mottle * .1f;
    }

    // 布：たて糸とよこ糸が交互に上になる平織り
    static void WeavePixel(float u, float v, out Color c, out float h)
    {
        const float threads = 96;
        float gu = u * threads, gv = v * threads;
        int iu = Mathf.FloorToInt(gu), iv = Mathf.FloorToInt(gv);
        float fu = gu - iu, fv = gv - iv;
        bool warpOnTop = ((iu + iv) & 1) == 0;
        float warp = Mathf.Sin(fu * Mathf.PI), weft = Mathf.Sin(fv * Mathf.PI);
        float top = warpOnTop ? warp * (.6f + .4f * Mathf.Sin(fv * Mathf.PI)) : weft * (.6f + .4f * Mathf.Sin(fu * Mathf.PI));
        float fuzz = Mathf.PerlinNoise(u * 700, v * 700);
        float slub = N(u, v, 12, 9);
        h = top * .8f + fuzz * .15f;
        float t = .78f + .16f * top + .06f * fuzz + .05f * (slub - .5f);
        t *= 1 - .12f * S(.6f, .85f, N(u, v, 4, 17));
        c = new Color(t, t, t, 1);
    }

    // 段ボール：波板のすじ、紙の繊維、うすい折り跡
    static void CardboardPixel(float u, float v, out Color c, out float h)
    {
        float ridge = Mathf.Sin(v * 72 * Mathf.PI * 2) * .5f + .5f;
        float fiber = Mathf.PerlinNoise(u * 600, v * 90) * .5f + Mathf.PerlinNoise(u * 150, v * 150) * .5f;
        float crease = 1 - S(0, .006f, Mathf.Abs(Mathf.Repeat(u * 2 + .02f * N(u, v, 6, 3), 1) - .5f));
        float t = .86f + .06f * ridge + .08f * fiber - .1f * crease - .1f * S(.6f, .85f, N(u, v, 5, 23));
        c = new Color(t, t, t, 1);
        h = ridge * .5f + fiber * .15f - crease * .4f;
    }

    // デカール：こぼした跡（ふちが少し濃い）
    static void StainPixel(float u, float v, out Color c, out float h)
    {
        float d = new Vector2(u - .5f, v - .5f).magnitude * 2;
        float edge = d + (N(u, v, 5, 51) - .5f) * .55f + (Mathf.PerlinNoise(u * 30, v * 30) - .5f) * .15f;
        float inside = 1 - S(.55f, .7f, edge);
        float rim = inside * S(.35f, .62f, edge) * .6f;
        float a = Mathf.Clamp01(inside * (.35f + .25f * N(u, v, 9, 7)) + rim);
        c = new Color(.22f, .15f, .09f, a * .75f);
        h = 0;
    }

    // デカール：すり跡（一方向に伸びた筋）
    static void ScuffPixel(float u, float v, out Color c, out float h)
    {
        float mask = (1 - S(.25f, .5f, Mathf.Abs(u - .5f))) * (1 - S(.3f, .5f, Mathf.Abs(v - .5f)));
        float streaks = Mathf.PerlinNoise(u * 4, v * 60) * Mathf.PerlinNoise(u * 12 + 5, v * 25);
        float a = Mathf.Clamp01(mask * S(.12f, .4f, streaks) * (.6f + .4f * N(u, v, 4, 61)));
        c = new Color(.16f, .12f, .1f, a * .55f);
        h = 0;
    }

    // デカール：壁の下の黒ずみ（下ほど濃く、上へまだらに薄くなる）
    static void GrimePixel(float u, float v, out Color c, out float h)
    {
        float up = v + (N(u, v, 6, 71) - .5f) * .35f;
        float side = 1 - S(.38f, .5f, Mathf.Abs(u - .5f));
        float a = Mathf.Clamp01((1 - S(.05f, .75f, up)) * side * (.5f + .5f * N(u, v, 14, 77)));
        c = new Color(.13f, .11f, .1f, a * .6f);
        h = 0;
    }
}
#endif
