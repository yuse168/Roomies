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

    internal enum Tpl { Wood, Fabric, Plastic, PaintedMetal, Metal, Paper, Cardboard, Glass, Plaster, Concrete, Asphalt, Roof, Foliage, Ground, Skin, Rubber, Ceramic, Emissive, Brick }

    // テンプレの設定：テクスチャ名, 作り方, ノーマルの強さ(作るとき), 模様1枚の大きさ(m), Smoothness, Metallic, ノーマル強さ, ハイライト, 映り込み
    readonly struct TplDef
    {
        public readonly string Tex; public readonly Pixel Make; public readonly float Bake, Tile, Smooth, Metal, Bump; public readonly bool Spec, Env;
        public TplDef(string tex, Pixel make, float bake, float tile, float smooth, float metal, float bump, bool spec = true, bool env = true)
        { Tex = tex; Make = make; Bake = bake; Tile = tile; Smooth = smooth; Metal = metal; Bump = bump; Spec = spec; Env = env; }
    }

    static readonly Dictionary<Tpl, TplDef> TplDefs = new Dictionary<Tpl, TplDef>
    {
        [Tpl.Wood] = new TplDef("tpl_wood", WoodPixel, 4f, .9f, .1f, 0, .8f),
        [Tpl.Fabric] = new TplDef("finish_weave", WeavePixel, 3.5f, .18f, .03f, 0, .6f, false, false),
        [Tpl.Plastic] = new TplDef("tpl_plastic", PlasticPixel, 1.5f, .5f, .18f, 0, .3f),
        [Tpl.PaintedMetal] = new TplDef("tpl_paint", PaintPixel, 2.5f, 1.2f, .2f, .1f, .5f),
        [Tpl.Metal] = new TplDef("tpl_metal", BrushedPixel, 2f, .5f, .35f, .7f, .35f),
        [Tpl.Paper] = new TplDef("tpl_paper", PaperPixel, 2f, .35f, .03f, 0, .35f, false, false),
        [Tpl.Cardboard] = new TplDef("finish_cardboard", CardboardPixel, 3f, .5f, .03f, 0, .8f, false, false),
        [Tpl.Glass] = new TplDef("tpl_glass", GlassPixel, .6f, 1.2f, .75f, 0, .15f),
        [Tpl.Plaster] = new TplDef("finish_plaster", PlasterPixel, 2.2f, 1.6f, .03f, 0, .7f, false, false),
        [Tpl.Concrete] = new TplDef("tpl_concrete", ConcretePixel, 3f, 1.6f, .05f, 0, .9f),
        [Tpl.Asphalt] = new TplDef("tpl_asphalt", AsphaltPixel, 3f, 2f, .06f, 0, .8f),
        [Tpl.Roof] = new TplDef("tpl_roof", RoofPixel, 5f, 1.2f, .08f, 0, 1f),
        [Tpl.Foliage] = new TplDef("tpl_foliage", FoliagePixel, 3f, .7f, .12f, 0, .8f),
        [Tpl.Ground] = new TplDef("tpl_ground", GroundPixel, 3f, 2.5f, .05f, 0, .7f, false, true),
        [Tpl.Rubber] = new TplDef("tpl_rubber", RubberPixel, 2f, .3f, .22f, 0, .35f),
        [Tpl.Ceramic] = new TplDef("tpl_ceramic", CeramicPixel, 1f, .3f, .55f, 0, .12f),
        [Tpl.Brick] = new TplDef("tpl_brick", BrickPixel, 4f, 1.2f, .08f, 0, .9f),
    };

    // 微細な色ムラの強さ（テンプレごと）。テクスチャの色をわずかに暖色・寒色へ揺らす
    static readonly Dictionary<Tpl, float> TplTint = new Dictionary<Tpl, float>
    {
        [Tpl.Wood] = .12f, [Tpl.Fabric] = .08f, [Tpl.Plastic] = .04f, [Tpl.PaintedMetal] = .06f, [Tpl.Metal] = .03f,
        [Tpl.Paper] = .06f, [Tpl.Cardboard] = .1f, [Tpl.Glass] = 0, [Tpl.Plaster] = .08f, [Tpl.Concrete] = .08f,
        [Tpl.Asphalt] = .05f, [Tpl.Roof] = .1f, [Tpl.Foliage] = .15f, [Tpl.Ground] = .15f, [Tpl.Rubber] = .03f, [Tpl.Ceramic] = .04f, [Tpl.Brick] = .14f,
    };

    static Pixel Tinted(Pixel pixel, float amount, int seed) => (float u, float v, out Color c, out float h) =>
    {
        pixel(u, v, out c, out h);
        if (amount <= 0) return;
        float warm = N(u, v, 3, 70 + seed) - .5f, green = N(u, v, 7, 90 + seed) - .5f;
        c = new Color(c.r * (1 + amount * warm), c.g * (1 + amount * .5f * green), c.b * (1 - amount * warm), 1);
    };

    [MenuItem("Roomies/Art/Restyle/Material Templates/1 Build Templates")]
    internal static void BuildTemplates()
    {
        Directory.CreateDirectory(VariantDir);
        foreach (Tpl t in Enum.GetValues(typeof(Tpl))) Template(t);
        int n = 0;
        foreach (var pair in OldToTemplate) if (VariantFor(pair.Key) != null) n++;
        foreach (var rule in NameRules) foreach (var old in RuleColors) if (VariantFor(old, rule.tpl) != null) n++;
        AssetDatabase.SaveAssets();
        Debug.Log($"[Material Templates] {Enum.GetValues(typeof(Tpl)).Length} templates, {n} variants");
    }

    static string TemplatePath(Tpl t) => $"{TemplateDir}T_{t}.mat";

    /// <summary>テンプレ（親）を作る・整える。</summary>
    internal static Material Template(Tpl t)
    {
        string path = TemplatePath(t);
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (t == Tpl.Emissive)
        {
            // 光る物：URP Lit の発光。色と光の強さは子で上書きする
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null) { mat = new Material(lit); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = lit;
            mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", .3f); mat.SetFloat("_Metallic", 0);
            mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.white);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }
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
            mat.SetFloat("_Smoothness", .16f); mat.SetFloat("_Metallic", 0);
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
        var map = Make(d.Tex, 512, Tinted(d.Make, TplTint.TryGetValue(t, out var tint) ? tint : 0, (int)t), out var normal, d.Bake);
        mat.SetTexture("_BaseMap", map); mat.SetTexture("_BumpMap", normal);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_TileSize", d.Tile); mat.SetFloat("_BumpScale", d.Bump);
        mat.SetFloat("_Smoothness", d.Smooth); mat.SetFloat("_Metallic", d.Metal);
        mat.SetFloat("_BlendSharpness", 6); mat.SetFloat("_DetailContrast", 1); mat.SetFloat("_ColorVariation", 1);
        mat.SetFloat("_SpecularHighlights", d.Spec ? 1 : 0); mat.SetFloat("_EnvironmentReflections", d.Env ? 1 : 0);
        SetKeyword(mat, "_SPECULARHIGHLIGHTS_OFF", !d.Spec); SetKeyword(mat, "_ENVIRONMENTREFLECTIONS_OFF", !d.Env);
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>テンプレの子（マテリアルバリアント）。色だけ上書きする。extra でほかの上書き（床の板など）を足せる。</summary>
    internal static Material Variant(Tpl t, string name, Color color, Action<Material> extra = null, string sub = null)
    {
        var parent = AssetDatabase.LoadAssetAtPath<Material>(TemplatePath(t)) ?? Template(t);
        // 名前は元のマテリアルと同じにする（足音 FootstepAudio がマテリアル名の言葉で床を判定しているため）
        string folder = VariantDir + t + (sub != null ? "/" + sub : "");
        Directory.CreateDirectory(folder);
        string path = $"{folder}/{name}.mat";
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
        ["dart_dark"] = Tpl.Plastic, ["tape"] = Tpl.Plastic, ["pot"] = Tpl.Ceramic, ["cream"] = Tpl.Plastic,
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
        // 光る物（glow_water は未コミットの変更があるので入れない）
        ["glow_bar_lime"] = Tpl.Emissive, ["glow_bulb"] = Tpl.Emissive, ["glow_fairy"] = Tpl.Emissive, ["glow_lantern"] = Tpl.Emissive,
        ["glow_led_green"] = Tpl.Emissive, ["glow_neon_pink"] = Tpl.Emissive, ["glow_neon_purple"] = Tpl.Emissive,
        ["glow_screen_green"] = Tpl.Emissive, ["glow_screen_white"] = Tpl.Emissive, ["glow_slot_red"] = Tpl.Emissive,
        ["glow_slot_yellow"] = Tpl.Emissive, ["glow_tank_light"] = Tpl.Emissive, ["glow_tv"] = Tpl.Emissive, ["glow_tv_ground"] = Tpl.Emissive,
        ["glow_tv_sky"] = Tpl.Emissive, ["glow_window"] = Tpl.Emissive, ["lamp_inner"] = Tpl.Emissive,
    };

    // 部品の名前で決めるもの（同じ色のマテリアルでも、コード・持ち手・靴底はゴム、マグ・お椀は陶器）
    static readonly (System.Text.RegularExpressions.Regex pattern, Tpl tpl)[] NameRules =
    {
        (new System.Text.RegularExpressions.Regex(@"(?i)cord|wire|grip|sole|wheel|caster|cable|tire|tyre|hose|strap"), Tpl.Rubber),
        (new System.Text.RegularExpressions.Regex(@"(?i)\bmug\b|bowl|saucer|plate"), Tpl.Ceramic),
    };
    static readonly HashSet<string> Plush = new HashSet<string> { "sofa_yellow", "sofa_yellow_dark", "pillow_teal", "plush_grey", "shark_blue", "shark_belly", "cloth_blue", "cloth_cream" };
    // 名前で決めるときに子を作っておく元の色
    static readonly string[] RuleColors = { "plastic_black", "plastic_white", "metal_dark", "cream", "red", "blue", "teal", "orange", "yellow", "green", "purple", "pink" };

    static Tpl? RuleFor(string rendererName, string oldName)
    {
        if (OldToTemplate.TryGetValue(oldName, out var mapped) && (mapped == Tpl.Emissive || mapped == Tpl.Glass)) return null;
        foreach (var rule in NameRules) if (rule.pattern.IsMatch(rendererName)) return rule.tpl;
        return null;
    }

    /// <summary>元の Restyle マテリアルに対応する子を作る（色は元の色を写す）。対応がなければ null。</summary>
    internal static Material VariantFor(string oldName, Tpl? forced = null)
    {
        Tpl t;
        if (forced.HasValue) t = forced.Value;
        else if (!OldToTemplate.TryGetValue(oldName, out t)) return null;
        var old = AssetDatabase.LoadAssetAtPath<Material>(Restyle + oldName + ".mat");
        if (old == null) return null;
        Color c = old.GetColor("_BaseColor");
        Action<Material> extra = null;
        if (Tiles.TryGetValue(oldName, out var tile))
        {
            if (oldName == "wall_plaster") c *= .9f;   // 壁が明るくなりすぎないよう、少しだけ暗く（前と同じ）
            var target = c;
            return Variant(t, oldName, c, v => ApplyTile(v, tile, target));
        }
        if (t == Tpl.Emissive)
            extra = v => { v.SetColor("_EmissionColor", old.GetColor("_EmissionColor")); v.SetFloat("_Smoothness", old.GetFloat("_Smoothness")); };
        else if (t == Tpl.Fabric && Plush.Contains(oldName))
            extra = v => { v.SetFloat("_DetailContrast", .6f); v.SetFloat("_BumpScale", .4f); v.SetFloat("_TileSize", .14f); };   // 起毛（ソファ・ぬいぐるみ・クッション）
        // 床・壁・ソファ・段ボール・ラグは上の Tiles（描いたテクスチャ）で決める。前の finish_floor などは使わなくなった
        else if (oldName == "ceiling") extra = v => v.SetFloat("_TileSize", 1.2f);
        else if (oldName == "gold") extra = v => { v.SetFloat("_Metallic", .9f); v.SetFloat("_Smoothness", .6f); };
        return Variant(t, oldName, c, extra);
    }

    // ================================================================ 大きい面の描いたテクスチャ（2026/11/23）
    // Textures/Tiles の色（albedo）とノーマル（OpenGL 形式＝Unity と同じ向きなので反転しない）を、元の色の子マテリアルに貼る。
    // 色は「元の色 ÷ テクスチャの平均の色」をかけて、絵の模様を残したまま部屋の配色（元の色）にそろえる。
    // tile は模様 1 枚が何メートルか（床の板の幅 約 0.14m、ソファの織り目 約 7mm、ラグの模様 約 0.2m になる大きさ）
    const string TileDir = "Assets/_Art/Restyle/Textures/Tiles/";
    static readonly Dictionary<string, (string tex, float tile, float bump, float smooth)> Tiles = new Dictionary<string, (string, float, float, float)>
    {
        ["floor_planks"] = ("floor_wood", 1.4f, .8f, .13f),
        ["wall_plaster"] = ("wall_plaster", 2.0f, .5f, -1),
        ["sofa_yellow"] = ("fabric", .25f, .6f, -1), ["sofa_yellow_dark"] = ("fabric", .25f, .6f, -1),
        ["cardboard"] = ("cardboard", .5f, .5f, -1), ["cardboard_dark"] = ("cardboard", .5f, .5f, -1),
        ["rug"] = ("rug", .8f, .5f, -1), ["rug_edge"] = ("rug", .8f, .5f, -1),
    };
    static readonly Dictionary<string, Color> TileMeans = new Dictionary<string, Color>();

    static void ApplyTile(Material v, (string tex, float tile, float bump, float smooth) t, Color target)
    {
        string albedo = TileDir + t.tex + "_albedo.png", normal = TileDir + t.tex + "_normal.png";
        var a = ImportTile(albedo, false); var n = ImportTile(normal, true);
        if (a == null || n == null) { Debug.LogWarning("[Material Templates] tile texture missing: " + t.tex); return; }
        var mean = TileMean(albedo);
        v.SetTexture("_BaseMap", a); v.SetTexture("_BumpMap", n);
        v.SetFloat("_TileSize", t.tile); v.SetFloat("_BumpScale", t.bump);
        v.SetFloat("_DetailContrast", 1); v.SetFloat("_ColorVariation", 1);
        if (t.smooth >= 0) v.SetFloat("_Smoothness", t.smooth);
        v.SetColor("_BaseColor", new Color(target.r / Mathf.Max(mean.r, .05f), target.g / Mathf.Max(mean.g, .05f), target.b / Mathf.Max(mean.b, .05f), 1));
    }

    /// <summary>元の Restyle の素材（URP Lit）に描いたタイルを貼る。originalHex は Palette の元の色（何度実行しても同じ結果になるよう、今の色ではなく元の色から決める）。</summary>
    internal static void TileOnRestyle(string material, string tex, string originalHex, float bump, Vector2 tiling, float smooth)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(Restyle + material + ".mat");
        string albedo = TileDir + tex + "_albedo.png", normal = TileDir + tex + "_normal.png";
        var a = ImportTile(albedo, false); var n = ImportTile(normal, true);
        if (mat == null || a == null || n == null) { Debug.LogWarning("[Material Finish] tile not applied: " + material); return; }
        ColorUtility.TryParseHtmlString("#" + originalHex, out var target);
        var mean = TileMean(albedo);
        mat.SetTexture("_BaseMap", a); mat.SetTextureScale("_BaseMap", tiling);
        mat.SetTexture("_BumpMap", n); mat.SetFloat("_BumpScale", bump); mat.EnableKeyword("_NORMALMAP");
        mat.SetColor("_BaseColor", new Color(target.r / Mathf.Max(mean.r, .05f), target.g / Mathf.Max(mean.g, .05f), target.b / Mathf.Max(mean.b, .05f), 1));
        if (smooth >= 0) mat.SetFloat("_Smoothness", smooth);
        EditorUtility.SetDirty(mat);
    }

    /// <summary>タイルの取り込み設定（1024・ミップマップ・繰り返し・高品質圧縮。ノーマルは Normal map）。</summary>
    static Texture2D ImportTile(string path, bool normal)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return null;
        var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        int aniso = normal ? 4 : 8;
        if (importer.textureType != type || importer.maxTextureSize != 1024 || !importer.mipmapEnabled || importer.wrapMode != TextureWrapMode.Repeat
            || importer.textureCompression != TextureImporterCompression.CompressedHQ || importer.anisoLevel != aniso || importer.flipGreenChannel)
        {
            importer.textureType = type; importer.maxTextureSize = 1024; importer.mipmapEnabled = true; importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.anisoLevel = aniso; importer.flipGreenChannel = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>色の画像の平均の色（sRGB）。</summary>
    static Color TileMean(string path)
    {
        if (TileMeans.TryGetValue(path, out var c)) return c;
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        t.LoadImage(File.ReadAllBytes(path));
        var p = t.GetPixels32(); double r = 0, g = 0, b = 0; int n = 0;
        for (int i = 0; i < p.Length; i += 7) { r += p[i].r; g += p[i].g; b += p[i].b; n++; }
        UnityEngine.Object.DestroyImmediate(t);
        c = new Color((float)(r / n / 255), (float)(g / n / 255), (float)(b / n / 255), 1);
        TileMeans[path] = c; return c;
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
        float t = .86f + .08f * coarse + .05f * fine - .06f * pits - .05f * crack - .06f * stain;   // ひび・くぼみは控えめ（目立つと模様に見える）
        c = new Color(t, t, t, 1);
        h = coarse * .3f + fine * .25f - pits * .2f - crack * .12f;
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

    // ゴム：細かい粒と、ほんの少しのむら
    static void RubberPixel(float u, float v, out Color c, out float h)
    {
        float grain = Mathf.PerlinNoise(u * 600, v * 600), mottle = N(u, v, 6, 31);
        float t = .93f + .03f * mottle + .04f * grain;
        c = new Color(t, t, t, 1);
        h = grain * .5f + mottle * .1f;
    }

    // 陶器：釉薬のゆるいうねり
    static void CeramicPixel(float u, float v, out Color c, out float h)
    {
        float wave = N(u, v, 4, 37), pin = S(.82f, .86f, Mathf.PerlinNoise(u * 300 + 3, v * 300 + 1));
        float t = .96f + .03f * wave - .05f * pin;
        c = new Color(t, t, t, 1);
        h = wave * .3f - pin * .3f;
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
        var cache = new Dictionary<string, Material>();
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
                    if (old == null) continue;
                    // 元の Restyle マテリアルか、すでにテンプレの子になっている物（名前は元と同じ）だけを扱う
                    string oldPath = AssetDatabase.GetAssetPath(old);
                    if (!oldPath.StartsWith(Restyle) && !oldPath.StartsWith(VariantDir)) continue;
                    var rule = RuleFor(r.name, old.name);
                    string key = old.name + "|" + (rule.HasValue ? rule.Value.ToString() : "");
                    if (!cache.TryGetValue(key, out var v)) { v = VariantFor(old.name, rule); cache[key] = v; }
                    if (v != null && v != old) { mats[i] = v; changed = true; }
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

    // ギミック付きプレハブで使っている、Restyle 以外のマテリアル → テンプレ（色は元の色を写す）
    static readonly Dictionary<string, Tpl> ExtraMaterials = new Dictionary<string, Tpl>
    {
        ["Assets/_Materials/Facilities/ClosetBody.mat"] = Tpl.Wood, ["Assets/_Materials/Facilities/ClosetDoor.mat"] = Tpl.Wood,
        ["Assets/_Materials/Facilities/ClosetHandle.mat"] = Tpl.Metal,
        ["Assets/_Materials/Blackjack/BlackjackWood.mat"] = Tpl.Wood, ["Assets/_Materials/Blackjack/BlackjackGold.mat"] = Tpl.Metal,
        ["Assets/_Materials/Blackjack/BlackjackFelt.mat"] = Tpl.Fabric,
    };

    static Material ResolveVariant(Renderer r, Material old, Dictionary<string, Material> cache)
    {
        if (old == null) return null;
        string oldPath = AssetDatabase.GetAssetPath(old);
        if (ExtraMaterials.TryGetValue(oldPath, out var et))
        {
            string ek = oldPath;
            if (!cache.TryGetValue(ek, out var ev))
            {
                Color c = old.HasProperty("_BaseColor") ? old.GetColor("_BaseColor") : old.color;
                ev = Variant(et, old.name, c, et == Tpl.Metal && old.name.Contains("Gold") ? (Action<Material>)(v => { v.SetFloat("_Metallic", .9f); v.SetFloat("_Smoothness", .6f); }) : null);
                cache[ek] = ev;
            }
            return ev;
        }
        if (oldPath.StartsWith(VariantDir) && oldPath.Contains("/Town/")) return null;   // 町の子は町の表で扱う
        if (oldPath.StartsWith(VariantDir))
        {
            // すでに子：Restyle の元があれば作り直し、なければそのまま
            if (AssetDatabase.LoadAssetAtPath<Material>(Restyle + old.name + ".mat") == null) return null;
        }
        else if (!oldPath.StartsWith(Restyle)) return null;
        var rule = RuleFor(r.name, old.name);
        string key = old.name + "|" + (rule.HasValue ? rule.Value.ToString() : "");
        if (!cache.TryGetValue(key, out var v2)) { v2 = VariantFor(old.name, rule); cache[key] = v2; }
        return v2;
    }

    /// <summary>ギミック付きプレハブの Visual の中だけをテンプレの子へ付け替える（ルート・当たり判定・スクリプト・通信は触らない）。</summary>
    [MenuItem("Roomies/Art/Restyle/Material Templates/3 Apply To Gimmick Prefabs")]
    internal static void ApplyTemplatesToPrefabs()
    {
        var cache = new Dictionary<string, Material>();
        foreach (var path in RoomiesBevelBuilder.GimmickPrefabs)
        {
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                int n = 0;
                foreach (var visual in root.GetComponentsInChildren<Transform>(true).Where(t => t.name == "Visual"))
                    foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = r.sharedMaterials; bool changed = false;
                        for (int i = 0; i < mats.Length; i++)
                        {
                            var v = ResolveVariant(r, mats[i], cache);
                            if (v != null && v != mats[i]) { mats[i] = v; changed = true; }
                        }
                        if (changed) { r.sharedMaterials = mats; n++; }
                    }
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log($"[Material Templates] {path}: {n} renderers");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
    }

    // ================================================================ 町（2026/11/13）
    // 町と共通の色の素材（Resources/RoomiesArt/Materials）を、部品の名前で素材の型に振り分ける。鉱山（Roomies_Mining）は触らない。
    // 子は Variants/型/Town/元の名前.mat（部屋の子と色が違うので分ける。名前は元のまま＝足音の判定は変わらない）
    const string TownLibrary = "Assets/Resources/RoomiesArt/Materials/";
    const string NeighborhoodPrefab = "Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab";

    static System.Text.RegularExpressions.Regex Rx(string p) => new System.Text.RegularExpressions.Regex(p, System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    // 部品の名前で決める（上から順）
    static readonly (System.Text.RegularExpressions.Regex name, Tpl tpl)[] TownNameRules =
    {
        (Rx(@"mullion|rail|frame|surround|sill|trim|lintel|jamb|fascia"), Tpl.PaintedMetal),
        (Rx(@"window|glazing|windshield|glass|^water$"), Tpl.Glass),
        (Rx(@"tire|tyre|wheel|cable|hose"), Tpl.Rubber),
        (Rx(@"canopy|leaf|leaves|bush|hedge|shrub|crown|water plant"), Tpl.Foliage),
        (Rx(@"soil|^plot|garden bed|ground|lawn|grass|field|^pplane"), Tpl.Ground),
        (Rx(@"roof|chimney|parapet|eave|gutter"), Tpl.Roof),
        (Rx(@"road|cul-de-sac|lane|crosswalk|painted route|asphalt|parking|bay edge|side road|main road"), Tpl.Asphalt),
        (Rx(@"trunk|sleeper|fence|crate|plank|bench|deck|pallet|\blog\b|signpost|beam|basket"), Tpl.Wood),
        (Rx(@"awning|curtain|rug|cushion|flag|banner|tarp|umbrella"), Tpl.Fabric),
        (Rx(@"poster|label|notice|paper|book|magazine|menu|parcel"), Tpl.Paper),
        (Rx(@"paving|kerb|curb|footing|foundation|column|capital|pier|rock|mountain|stone|tile|tread|stair|step|walk|plinth|pedestal|platform|sidewalk|cliff|boulder|snow|wall|floor|ceiling|ramp|bridge|viaduct|pillar|ledge"), Tpl.Concrete),
        (Rx(@"cage|\bbar\b|bars|antenna|pipe|steel|bogie|pantograph|lamp|pole|ladder|vent|duct|grill|grate|bracket|wire|tower|arm|spoke|axle"), Tpl.Metal),
        (Rx(@"shutter|locker|panel|cabin|siding|rack|sign|plate|button|kiosk|booth|hoop|backboard|door|box|bin|unit|terminal|machine|cab\b|nose|skirt|body"), Tpl.PaintedMetal),
    };
    // 名前で決まらないときの、元の素材ごとの型
    static readonly Dictionary<string, Tpl> TownDefaults = new Dictionary<string, Tpl>
    {
        ["silver"] = Tpl.Metal, ["wood"] = Tpl.Wood, ["green"] = Tpl.Foliage, ["paper"] = Tpl.Paper,
        ["blue"] = Tpl.Plaster, ["coral"] = Tpl.Plaster, ["cream"] = Tpl.Plaster, ["ink"] = Tpl.Concrete,
        ["mustard"] = Tpl.Plaster, ["pink"] = Tpl.Plaster, ["teal"] = Tpl.Plaster,
    };

    static Tpl? TownTemplate(string partName, string oldName)
    {
        if (oldName == "screen" || oldName == "mining_particles") return null;   // 画面・粒子はそのまま
        foreach (var rule in TownNameRules) if (rule.name.IsMatch(partName)) return rule.tpl;
        return TownDefaults.TryGetValue(oldName, out var t) ? t : (Tpl?)null;
    }

    static Material TownVariant(Renderer r, Material old, Dictionary<string, Material> cache)
    {
        if (old == null) return null;
        string path = AssetDatabase.GetAssetPath(old);
        string oldName;
        if (path.StartsWith(TownLibrary)) oldName = old.name;
        else if (path.StartsWith(VariantDir) && path.Contains("/Town/")) oldName = old.name;   // もう町の子：作り直す
        else return null;
        var t = TownTemplate(r.name, oldName);
        if (!t.HasValue) return null;
        string key = t.Value + "|" + oldName;
        if (cache.TryGetValue(key, out var v)) return v;
        var src = AssetDatabase.LoadAssetAtPath<Material>(TownLibrary + oldName + ".mat");
        if (src == null) return null;
        Color c = src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor") : src.color;
        Action<Material> extra = null;
        if (t.Value == Tpl.Metal && oldName == "silver") extra = m => { m.SetFloat("_Metallic", .6f); m.SetFloat("_Smoothness", .4f); };
        else if (t.Value == Tpl.Glass) { c = Color.Lerp(c, new Color(.78f, .9f, 1f), .45f); extra = m => m.SetFloat("_Smoothness", .6f); }   // 窓：空を映した明るいガラスに
        v = Variant(t.Value, oldName, c, extra, "Town");
        cache[key] = v;
        return v;
    }

    internal static int ApplyTownUnder(Transform root, Dictionary<string, Material> cache)
    {
        int n = 0;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r is ParticleSystemRenderer) continue;
            if (InMining(r.transform)) continue;
            var mats = r.sharedMaterials; bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                // 部屋の素材（Restyle）は部屋と同じ子へ、町の色の素材は町の子へ
                var v = ResolveVariant(r, mats[i], cache) ?? TownVariant(r, mats[i], cache);
                if (v != null && v != mats[i]) { mats[i] = v; changed = true; }
            }
            if (changed) { r.sharedMaterials = mats; EditorUtility.SetDirty(r); n++; }
        }
        return n;
    }

    internal static bool InMining(Transform t)
    {
        for (var x = t; x != null; x = x.parent) if (x.name.StartsWith("Roomies_Mining")) return true;
        return false;
    }

    /// <summary>町：RoomiesNeighborhood プレハブ（道・家の外側・店・庭・階段・留置所・配達カウンター）と、シーンの遠景 Roomies_Backdrop。</summary>
    [MenuItem("Roomies/Art/Restyle/Material Templates/4 Apply To Town")]
    internal static void ApplyTemplatesToTown()
    {
        var cache = new Dictionary<string, Material>();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            int n = ApplyTownUnder(root.transform, cache);
            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
            Debug.Log($"[Material Templates] town prefab: {n} renderers");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        foreach (var scene in Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount).Select(UnityEngine.SceneManagement.SceneManager.GetSceneAt).ToArray())
        {
            if (!scene.isLoaded) continue;
            var backdrop = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_Backdrop");
            if (backdrop == null) continue;
            int n = ApplyTownUnder(backdrop.transform, cache);
            // シーン側で素材を上書きしている町の部品（家の床・壁など。Build Room が上書きしている）もそろえる
            var town = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "Roomies_EnvironmentArt");
            if (town != null) n += ApplyTownUnder(town.transform, cache);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[Material Templates] {scene.name} backdrop: {n} renderers");
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

