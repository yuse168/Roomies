#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// テンプレマテリアル（2026/11/13）。質感ごとの親マテリアル15個と、色だけを上書きした子（マテリアルバリアント）。
/// ・テンプレ：Assets/_Materials/Templates/T_〇〇.mat（シェーダー Roomies/Triplanar Lit。キャラの肌だけ URP Lit）
/// ・子：Assets/_Materials/Templates/Variants/テンプレ名/元の名前.mat（色だけ上書き。床だけ板のテクスチャも上書き）
///   名前を元と同じにしているのは、足音（FootstepAudio）がマテリアル名で床の種類を決めているため
/// 質感を直すときはテンプレ（親）を直す。子は色しか持たないので、親の変更がそのまま全部に効く。
/// テクスチャは手続きで作る（Assets/_Art/Restyle/Textures/tpl_*.png）。何度実行しても同じ結果。
/// </summary>
internal static partial class RoomiesMaterialFinishBuilder
{
    const string TemplateDir = "Assets/_Materials/Templates/";
    const string VariantDir = TemplateDir + "Variants/";
    const string TriplanarShader = "Roomies/Triplanar Lit";

    internal enum Tpl { Wood, Fabric, Plastic, PaintedMetal, Metal, Paper, Cardboard, Glass, Plaster, Concrete, Asphalt, Roof, Foliage, Ground, Skin }

    // テンプレの設定：テクスチャ名, 作り方, ノーマルの強さ(作るとき), 模様1枚の大きさ(m), Smoothness, Metallic, ノーマル強さ, ハイライト, 映り込み
    readonly struct TplDef
    {
        public readonly string Tex; public readonly Pixel Make; public readonly float Bake, Tile, Smooth, Metal, Bump; public readonly bool Spec, Env;
        public TplDef(string tex, Pixel make, float bake, float tile, float smooth, float metal, float bump, bool spec = true, bool env = true)
        { Tex = tex; Make = make; Bake = bake; Tile = tile; Smooth = smooth; Metal = metal; Bump = bump; Spec = spec; Env = env; }
    }

    static readonly Dictionary<Tpl, TplDef> TplDefs = new Dictionary<Tpl, TplDef>
    {
        [Tpl.Wood] = new TplDef("tpl_wood", WoodPixel, 4f, .9f, .16f, 0, .8f),
        [Tpl.Fabric] = new TplDef("finish_weave", WeavePixel, 3.5f, .18f, .03f, 0, .6f, false, false),
        [Tpl.Plastic] = new TplDef("tpl_plastic", PlasticPixel, 1.5f, .5f, .3f, 0, .3f),
        [Tpl.PaintedMetal] = new TplDef("tpl_paint", PaintPixel, 2.5f, 1.2f, .3f, .1f, .5f),
        [Tpl.Metal] = new TplDef("tpl_metal", BrushedPixel, 2f, .5f, .45f, .7f, .35f),
        [Tpl.Paper] = new TplDef("tpl_paper", PaperPixel, 2f, .35f, .03f, 0, .35f, false, false),
        [Tpl.Cardboard] = new TplDef("finish_cardboard", CardboardPixel, 3f, .5f, .03f, 0, .8f, false, false),
        [Tpl.Glass] = new TplDef("tpl_glass", GlassPixel, .6f, 1.2f, .82f, 0, .15f),
        [Tpl.Plaster] = new TplDef("finish_plaster", PlasterPixel, 2.2f, 1.6f, .03f, 0, .7f, false, false),
        [Tpl.Concrete] = new TplDef("tpl_concrete", ConcretePixel, 3f, 1.6f, .1f, 0, .9f),
        [Tpl.Asphalt] = new TplDef("tpl_asphalt", AsphaltPixel, 3f, 2f, .12f, 0, .8f),
        [Tpl.Roof] = new TplDef("tpl_roof", RoofPixel, 5f, 1.2f, .15f, 0, 1f),
        [Tpl.Foliage] = new TplDef("tpl_foliage", FoliagePixel, 3f, .7f, .2f, 0, .8f),
        [Tpl.Ground] = new TplDef("tpl_ground", GroundPixel, 3f, 2.5f, .05f, 0, .7f, false, true),
    };

    [MenuItem("Roomies/Art/Restyle/Material Templates/1 Build Templates")]
    internal static void BuildTemplates()
    {
        Directory.CreateDirectory(VariantDir);
        foreach (Tpl t in Enum.GetValues(typeof(Tpl))) Template(t);
        int n = 0;
        foreach (var pair in OldToTemplate) if (VariantFor(pair.Key) != null) n++;
        AssetDatabase.SaveAssets();
        Debug.Log($"[Material Templates] {Enum.GetValues(typeof(Tpl)).Length} templates, {n} variants");
    }

    static string TemplatePath(Tpl t) => $"{TemplateDir}T_{t}.mat";

    /// <summary>テンプレ（親）を作る・整える。</summary>
    internal static Material Template(Tpl t)
    {
        string path = TemplatePath(t);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (t == Tpl.Skin)
        {
            // キャラの肌：URP Lit（動くので UV で貼る。Bobs.fbx には UV がある）。少しマットに、ごく弱い粒感
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = lit;
            var skin = Make("tpl_skin", 512, SkinPixel, out var skinN, 1.2f);
            mat.SetTexture("_BaseMap", skin); mat.SetTextureScale("_BaseMap", new Vector2(3, 3));
            mat.SetTexture("_BumpMap", skinN); mat.SetFloat("_BumpScale", .35f); mat.EnableKeyword("_NORMALMAP");
            mat.SetColor("_BaseColor", new Color(.98f, .55f, .62f));
            mat.SetFloat("_Smoothness", .22f); mat.SetFloat("_Metallic", 0);
            mat.SetFloat("_SpecularHighlights", 1); mat.SetFloat("_EnvironmentReflections", 1);
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF"); mat.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(mat);
            return mat;
        }
        var d = TplDefs[t];
        var shader = Shader.Find(TriplanarShader);
        if (shader == null) throw new InvalidOperationException("Shader not found: " + TriplanarShader);
        if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = shader;
        var map = Make(d.Tex, 512, d.Make, out var normal, d.Bake);
        mat.SetTexture("_BaseMap", map); mat.SetTexture("_BumpMap", normal);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_TileSize", d.Tile); mat.SetFloat("_BumpScale", d.Bump);
        mat.SetFloat("_Smoothness", d.Smooth); mat.SetFloat("_Metallic", d.Metal);
        mat.SetFloat("_BlendSharpness", 6); mat.SetFloat("_DetailContrast", 1);
        mat.SetFloat("_SpecularHighlights", d.Spec ? 1 : 0); mat.SetFloat("_EnvironmentReflections", d.Env ? 1 : 0);
        SetKeyword(mat, "_SPECULARHIGHLIGHTS_OFF", !d.Spec); SetKeyword(mat, "_ENVIRONMENTREFLECTIONS_OFF", !d.Env);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>テンプレの子（マテリアルバリアント）。色だけ上書きする。extra でほかの上書き（床の板など）を足せる。</summary>
    internal static Material Variant(Tpl t, string name, Color color, Action<Material> extra = null)
    {
        var parent = AssetDatabase.LoadAssetAtPath<Material>(TemplatePath(t)) ?? Template(t);
        // 名前は元のマテリアルと同じにする（足音 FootstepAudio がマテリアル名の言葉で床を判定しているため）
        Directory.CreateDirectory(VariantDir + t);
        string path = $"{VariantDir}{t}/{name}.mat";
        var v = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (v == null) { v = new Material(parent); AssetDatabase.CreateAsset(v, path); }
        v.parent = parent;
        v.RevertAllPropertyOverrides();
        v.SetColor("_BaseColor", color);
        extra?.Invoke(v);
        EditorUtility.SetDirty(v);
        return v;
    }

    // ================================================================ 元のマテリアル → テンプレ
    // 部屋とタイトル（Restyle の素材）。町・鉱山（RoomiesArt の色の素材）は段階3で部品の名前ごとに決める
    static readonly Dictionary<string, Tpl> OldToTemplate = new Dictionary<string, Tpl>
    {
        ["wood_dark"] = Tpl.Wood, ["wood_mid"] = Tpl.Wood, ["wood_light"] = Tpl.Wood, ["wood_back"] = Tpl.Wood, ["brown"] = Tpl.Wood,
        ["floor_planks"] = Tpl.Wood,
        ["sofa_yellow"] = Tpl.Fabric, ["sofa_yellow_dark"] = Tpl.Fabric, ["curtain"] = Tpl.Fabric, ["rug"] = Tpl.Fabric, ["rug_edge"] = Tpl.Fabric,
        ["bed_duvet"] = Tpl.Fabric, ["bed_duvet_light"] = Tpl.Fabric, ["bed_sheet"] = Tpl.Fabric, ["cloth_blue"] = Tpl.Fabric, ["cloth_cream"] = Tpl.Fabric,
        ["pillow_teal"] = Tpl.Fabric, ["plush_grey"] = Tpl.Fabric, ["fabric_grey"] = Tpl.Fabric, ["shark_blue"] = Tpl.Fabric, ["shark_belly"] = Tpl.Fabric,
        ["plastic_black"] = Tpl.Plastic, ["plastic_white"] = Tpl.Plastic, ["red"] = Tpl.Plastic, ["orange"] = Tpl.Plastic, ["yellow"] = Tpl.Plastic,
        ["green"] = Tpl.Plastic, ["teal"] = Tpl.Plastic, ["blue"] = Tpl.Plastic, ["pink"] = Tpl.Plastic, ["purple"] = Tpl.Plastic, ["purple_dark"] = Tpl.Plastic,
        ["bag_blue"] = Tpl.Plastic, ["bin_blue"] = Tpl.Plastic, ["fan_blade"] = Tpl.Plastic, ["mw_panel"] = Tpl.Plastic, ["slot_panel"] = Tpl.Plastic,
        ["dart_dark"] = Tpl.Plastic, ["tape"] = Tpl.Plastic, ["pot"] = Tpl.Plastic, ["cream"] = Tpl.Plastic,
        ["fridge_green"] = Tpl.PaintedMetal, ["fridge_green_dark"] = Tpl.PaintedMetal, ["cabinet_green"] = Tpl.PaintedMetal,
        ["cabinet_green_light"] = Tpl.PaintedMetal, ["slot_body"] = Tpl.PaintedMetal, ["lamp_shade"] = Tpl.PaintedMetal,
        ["lamp_shade_green"] = Tpl.PaintedMetal, ["worktop"] = Tpl.PaintedMetal,
        ["metal_dark"] = Tpl.Metal, ["metal_grey"] = Tpl.Metal, ["gold"] = Tpl.Metal,
        ["paper"] = Tpl.Paper, ["page_grey"] = Tpl.Paper, ["page_green"] = Tpl.Paper, ["page_ink"] = Tpl.Paper, ["page_red"] = Tpl.Paper,
        ["page_yellow"] = Tpl.Paper, ["poster_red"] = Tpl.Paper, ["poster_teal"] = Tpl.Paper, ["ink_brown"] = Tpl.Paper, ["tape_brown"] = Tpl.Paper,
        ["cardboard"] = Tpl.Cardboard, ["cardboard_dark"] = Tpl.Cardboard,
        ["glass_brown"] = Tpl.Glass,
        ["wall_plaster"] = Tpl.Plaster, ["ceiling"] = Tpl.Plaster,
        ["leaf"] = Tpl.Foliage, ["leaf_dark"] = Tpl.Foliage,
        ["soil"] = Tpl.Ground, ["gravel"] = Tpl.Ground,
    };

    /// <summary>元の Restyle マテリアルに対応する子を作る（色は元の色を写す）。対応がなければ null。</summary>
    internal static Material VariantFor(string oldName)
    {
        if (!OldToTemplate.TryGetValue(oldName, out var t)) return null;
        var old = AssetDatabase.LoadAssetAtPath<Material>(Restyle + oldName + ".mat");
        if (old == null) return null;
        Color c = old.GetColor("_BaseColor");
        Action<Material> extra = null;
        var def = TplDefs[t];
        if (oldName == "floor_planks")
            extra = v =>
            {
                v.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "finish_floor.png"));
                v.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + "finish_floor_n.png"));
                v.SetFloat("_TileSize", 1.4f); v.SetFloat("_Smoothness", .18f); v.SetFloat("_BumpScale", .9f);
            };
        else if (oldName == "ceiling") extra = v => v.SetFloat("_TileSize", 1.2f);
        else if (oldName == "gold") extra = v => { v.SetFloat("_Metallic", .9f); v.SetFloat("_Smoothness", .6f); };
        else if (oldName == "rug" || oldName == "rug_edge") extra = v => v.SetFloat("_TileSize", .3f);
        return Variant(t, oldName, c, extra);
    }

    // ================================================================ テクスチャ（色は灰色の濃淡だけ。色は _BaseColor で付く）
    static void WoodPixel(float u, float v, out Color c, out float h)
    {
        float warp = N(u, v, 4, 3) * .12f + N(u, v, 9, 7) * .04f;
        float rings = Mathf.Sin((u + warp) * 52f) * .5f + .5f;
        float streak = Mathf.PerlinNoise(u * 380, v * 12) * .6f + Mathf.PerlinNoise(u * 1200, v * 30) * .4f;
        float knot = 1 - S(.03f, .09f, new Vector2((u - .3f) * 1.0f, (v - .62f) * 3f).magnitude);
        float t = .78f + .1f * rings + .14f * streak - .25f * knot - .08f * S(.55f, .8f, N(u, v, 3, 19));
        c = new Color(t, t * .97f, t * .92f, 1);
        h = .06f * rings + .05f * streak - .1f * knot;
    }

    static void PlasticPixel(float u, float v, out Color c, out float h)
    {
        float speck = Mathf.PerlinNoise(u * 400, v * 400);
        float mottle = N(u, v, 5, 4);
        float t = .95f + .03f * mottle + .02f * speck;
        c = new Color(t, t, t, 1);
        h = speck * .15f + mottle * .1f;
    }

    static void PaintPixel(float u, float v, out Color c, out float h)
    {
        float mottle = N(u, v, 6, 8);
        float orange = Mathf.PerlinNoise(u * 260, v * 260);                  // 塗りの細かいでこぼこ
        float scratch = Mathf.Max(0, Mathf.Sin((u * .8f + v * .3f) * 220 + N(u, v, 3, 2) * 30) - .985f) * 40;
        float chip = S(.8f, .84f, N(u, v, 23, 41));                           // 小さなはげ（ごく少し）
        float t = .93f + .04f * mottle - .08f * chip + .06f * Mathf.Clamp01(scratch);
        c = new Color(t, t, t, 1);
        h = orange * .25f - chip * .4f - Mathf.Clamp01(scratch) * .2f;
    }

    static void BrushedPixel(float u, float v, out Color c, out float h)
    {
        float brush = Mathf.PerlinNoise(u * 4, v * 900) * .6f + Mathf.PerlinNoise(u * 12 + 3, v * 300) * .4f;
        float smudge = N(u, v, 4, 6);
        float t = .86f + .1f * brush + .05f * smudge;
        c = new Color(t, t, t, 1);
        h = brush * .3f;
    }

    static void PaperPixel(float u, float v, out Color c, out float h)
    {
        float fiber = Mathf.PerlinNoise(u * 500, v * 500) * .5f + Mathf.PerlinNoise(u * 120, v * 120) * .5f;
        float t = .93f + .05f * fiber - .05f * S(.6f, .85f, N(u, v, 4, 12));
        c = new Color(t, t, t, 1);
        h = fiber * .4f;
    }

    static void GlassPixel(float u, float v, out Color c, out float h)
    {
        float smudge = S(.55f, .85f, N(u, v, 5, 9));
        float t = 1 - .06f * smudge;
        c = new Color(t, t, t, 1);
        h = smudge * .2f;
    }

    static void ConcretePixel(float u, float v, out Color c, out float h)
    {
        float coarse = N(u, v, 6, 2), fine = Mathf.PerlinNoise(u * 300, v * 300);
        float pits = S(.78f, .85f, Mathf.PerlinNoise(u * 140 + 7, v * 140 + 3));
        float crackLine = Mathf.Abs(N(u, v, 3, 33) - .5f);
        float crack = 1 - S(0, .008f, crackLine);
        float stain = S(.6f, .85f, N(u, v, 2, 55));
        float t = .82f + .1f * coarse + .06f * fine - .2f * pits - .25f * crack - .1f * stain;
        c = new Color(t, t, t, 1);
        h = coarse * .3f + fine * .25f - pits * .5f - crack * .6f;
    }

    static void AsphaltPixel(float u, float v, out Color c, out float h)
    {
        float grit = Mathf.PerlinNoise(u * 700, v * 700);
        float stone = S(.7f, .8f, Mathf.PerlinNoise(u * 260 + 11, v * 260 + 5));
        float patch = S(.55f, .8f, N(u, v, 3, 61));
        float t = .78f + .12f * grit + .12f * stone - .12f * patch;
        c = new Color(t, t, t, 1);
        h = grit * .4f + stone * .35f;
    }

    // 屋根：ずらして重ねた瓦（板）の段
    static void RoofPixel(float u, float v, out Color c, out float h)
    {
        const float rows = 8, cols = 6;
        float gv = v * rows; int row = Mathf.FloorToInt(gv); float fv = gv - row;
        float gu = u * cols + (row % 2) * .5f; int col = Mathf.FloorToInt(gu); float fu = gu - col;
        float id = Hash(col, row);
        float overlap = S(0, .85f, fv);                   // 下の段に向かって厚くなる
        float edgeU = Mathf.Min(fu, 1 - fu) * cols * 64;
        float gap = S(0, 2.5f, edgeU) * S(0, .04f, fv);
        float t = (.78f + .2f * id) * (.85f + .15f * overlap) * Mathf.Lerp(.55f, 1, gap) * (.95f + .05f * Mathf.PerlinNoise(u * 300, v * 300));
        c = new Color(t, t, t, 1);
        h = overlap * .7f * gap + .05f * Mathf.PerlinNoise(u * 200, v * 200);
    }

    static void FoliagePixel(float u, float v, out Color c, out float h)
    {
        float clumps = N(u, v, 10, 3) * .6f + N(u, v, 24, 9) * .4f;
        float leaves = Mathf.PerlinNoise(u * 90, v * 90);
        float t = .7f + .3f * clumps + .08f * leaves;
        c = new Color(t, t, t, 1);
        h = clumps * .7f + leaves * .3f;
    }

    static void GroundPixel(float u, float v, out Color c, out float h)
    {
        float patches = N(u, v, 4, 5), tufts = Mathf.PerlinNoise(u * 220, v * 220), pebbles = S(.8f, .86f, Mathf.PerlinNoise(u * 160 + 2, v * 160 + 9));
        float t = .78f + .14f * patches + .1f * tufts + .1f * pebbles;
        c = new Color(t, t, t, 1);
        h = tufts * .4f + pebbles * .4f + patches * .2f;
    }

    // キャラの肌：ソフビ感を消すための、ごく弱い粒とむら
    static void SkinPixel(float u, float v, out Color c, out float h)
    {
        float pore = Mathf.PerlinNoise(u * 220, v * 220), mottle = N(u, v, 5, 13);
        float t = .95f + .03f * mottle + .02f * pore;
        c = new Color(t, t, t, 1);
        h = pore * .5f + mottle * .2f;
    }

    // ================================================================ 段階2：部屋とタイトルに付け替える
    // Roomies_RoomRestyle の中と、元の家（床・壁）の部品だけ。プレハブのインスタンス（ベッド・冷蔵庫など）は段階3で扱うので触らない
    const string HouseGroup = "Shared house - lounge and terrace";

    [MenuItem("Roomies/Art/Restyle/Material Templates/2 Apply To Room And Title")]
    internal static void ApplyTemplatesToRoom()
    {
        var cache = new Dictionary<Material, Material>();
        foreach (var scene in Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(UnityEngine.SceneManagement.SceneManager.GetSceneAt).ToArray())
        {
            if (!scene.isLoaded) continue;
            var roots = scene.GetRootGameObjects();
            var room = roots.FirstOrDefault(g => g.name == "Roomies_RoomRestyle");
            if (room == null) continue;
            var targets = room.GetComponentsInChildren<Renderer>(true).ToList();
            var house = roots.SelectMany(g => g.GetComponentsInChildren<Transform>(true)).FirstOrDefault(t => t.name == HouseGroup);
            if (house != null) targets.AddRange(house.GetComponentsInChildren<Renderer>(true));
            int swapped = 0;
            foreach (var r in targets)
            {
                if (PrefabUtility.IsPartOfPrefabInstance(r)) continue;
                var mats = r.sharedMaterials; bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var old = mats[i];
                    if (old == null || old.parent != null) continue;   // もう子マテリアルなら何もしない
                    if (!cache.TryGetValue(old, out var v))
                    {
                        v = AssetDatabase.GetAssetPath(old).StartsWith(Restyle) ? VariantFor(old.name) : null;
                        cache[old] = v;
                    }
                    if (v != null) { mats[i] = v; changed = true; }
                }
                if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); swapped++; }
            }

            // タイトルのキャラ（見た目だけの BobBody）：肌のテンプレに（色は MainMenuRoomShowcase が実行時に付ける）
            var skin = SkinBody();
            foreach (var r in roots.SelectMany(g => g.GetComponentsInChildren<Renderer>(true)).Where(r => r.name == "BobBody" && !PrefabUtility.IsPartOfPrefabInstance(r)))
            {
                if (r.sharedMaterial == skin) continue;
                r.sharedMaterial = skin; EditorUtility.SetDirty(r); swapped++;
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Material Templates] {scene.name}: {swapped} renderers now use template variants");
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>キャラの体：BobBody.mat を肌テンプレの子にする（プレイヤー・NPC もこれを使う。プレハブは触らない）。</summary>
    internal static Material SkinBody()
    {
        var body = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/BobBody.mat");
        if (body == null) return null;
        var parent = AssetDatabase.LoadAssetAtPath<Material>(TemplatePath(Tpl.Skin)) ?? Template(Tpl.Skin);
        Color c = body.GetColor("_BaseColor");
        body.parent = parent;
        body.RevertAllPropertyOverrides();
        body.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(body);
        return body;
    }
}
#endif

