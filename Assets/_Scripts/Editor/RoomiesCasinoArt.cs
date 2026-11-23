#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// カジノの見た目づくりの道具（エディタ専用。2026/11/23）。部屋の小物の作り直しと同じやり方：
///  ・物 1 つを 1 つのメッシュで作る（面ごとに頂点を分けて、少し角の立った陰影にする）。メッシュは Assets/_Art/Casino/CasinoMeshes.asset
///  ・色と絵は 1 枚のアトラス（Assets/_Art/Casino/casino_atlas.png）から取る。単色は色見本のマス、絵（台の枠線・盤・スロットの飾りなど）は切り出して並べる
///  ・マテリアルは 3 つだけ：casino_atlas（ふつうの面）、casino_atlas_glow（光る面。同じアトラスを発光に使う）、casino_neon（ネオン看板の線）
///
/// 元の絵（ArtSource/Casino/。Unity に取り込まない）を直したら、Roomies/Casino/2 Build Gimmick Prefabs か 1 Build Building を実行し直す。
/// 色見本の色を足す・直すときは Palette の表を直す。
/// </summary>
internal static partial class RoomiesCasinoArt
{
    internal const string Dir = "Assets/_Art/Casino";
    const string MeshAssetPath = Dir + "/CasinoMeshes.asset";
    const string AtlasPath = Dir + "/casino_atlas.png";
    const string NeonPath = Dir + "/casino_neon.png";
    const string MatDir = "Assets/_Materials/Casino";
    const string SourceDir = "ArtSource/Casino";
    const int AtlasSize = 2048;

    // ================================================================ 色見本（名前 → 色）。アトラスの右下に 32px のマスで並ぶ
    static readonly (string name, string hex)[] Palette =
    {
        ("wood_dark", "4D342C"), ("wood", "79503A"), ("wood_light", "A9754F"), ("wood_pale", "C99A6B"),
        ("plaster", "B9AA8B"), ("cream", "E9DDBF"), ("paper", "D8CEB5"), ("white", "F4EFE2"),
        ("black", "15151B"), ("ink", "2A2230"), ("grey", "5C5A66"), ("metal", "8D8F98"), ("metal_dark", "3C3D46"),
        ("gold", "D9A441"), ("gold_dark", "9A6B22"), ("brass", "C28B3A"),
        ("red", "B8323C"), ("red_dark", "7C1F2B"), ("maroon", "8A2B3C"), ("maroon_dark", "5E1C2A"),
        ("pink", "F765BA"), ("pink_dark", "B23C85"), ("pink_pale", "F7A8D4"),
        ("purple", "8E69BE"), ("purple_dark", "5A3A86"), ("purple_deep", "3A2458"),
        ("felt", "2F6B45"), ("felt_dark", "1F4A30"), ("green", "52755C"), ("leaf", "4E8A45"), ("leaf_dark", "35663A"),
        ("blue", "294C75"), ("night", "18263C"), ("sky", "6FA8DC"),
        ("orange", "FFB35C"), ("warm", "FFE0A1"), ("yellow", "D3A843"),
        ("lamp_shade", "E8893A"), ("lamp_shade_dark", "B5651F"),
        ("teal", "3F8F8A"), ("teal_dark", "2B6663"), ("teal_pale", "8FD0C6"),
        ("chip_red", "D9363E"), ("chip_blue", "3D6BD9"), ("chip_green", "3FA35A"), ("chip_black", "22222A"), ("chip_white", "F2EEE0"),
        ("glass_dark", "0E1420"), ("cyan", "7FF2FF"), ("card", "F7F3E8"), ("popcorn", "F6E3A0"), ("soil", "3B2A22"),
        ("terracotta", "B8623F"), ("brick", "9A4A32"),
        // 建物の箱に使う色（前は町の素材だった物をアトラスへ寄せた）：足元の石、3 階の壁の布、外から見た窓の明かり
        ("stone", "707073"), ("wall_purple", "543680"), ("window_glow", "977654"),
        // 吊り照明の傘の内側（光る面。2.6 倍で光らせても白く飛ばない暗さの橙）
        ("lamp_under", "AD854F"),
    };
    const int SwatchCell = 32, SwatchCols = 16;
    static readonly RectInt SwatchBlock = new RectInt(1036, 1500, 512, 256);   // アトラスの中の場所（左上が原点の px）

    // ================================================================ 絵（元の絵の切り出し → アトラスの中の場所。どちらも左上が原点の px）
    internal enum Img { Betting, Wheel, Felt, SlotTop, SlotBottom, Banner }
    static readonly Dictionary<Img, (string file, RectInt src, RectInt dst)> Images = new Dictionary<Img, (string, RectInt, RectInt)>
    {
        // ルーレットの賭ける場所（まわりの飾りの線は外す）
        [Img.Betting] = ("roulette_betting_albedo.png", new RectInt(20, 100, 980, 740), new RectInt(4, 4, 980, 740)),
        // ルーレットの盤（真上から。0 が上、時計回り）
        [Img.Wheel] = ("roulette_wheel_albedo.png", new RectInt(0, 0, 1024, 1024), new RectInt(1036, 4, 1000, 1000)),
        // ブラックジャックのフェルト（上が親の側）
        [Img.Felt] = ("blackjack_felt_albedo.png", new RectInt(0, 60, 1024, 800), new RectInt(4, 760, 1016, 794)),
        // スロットの顔：上の王冠の板と、下のボタンの帯
        [Img.SlotTop] = ("slot_face_albedo.png", new RectInt(0, 0, 1024, 270), new RectInt(1036, 1016, 768, 202)),
        [Img.SlotBottom] = ("slot_face_albedo.png", new RectInt(0, 836, 1024, 188), new RectInt(1036, 1230, 768, 141)),
        // 垂れ幕（真ん中の細長い所）
        [Img.Banner] = ("banner_diamond_albedo.png", new RectInt(318, 0, 388, 1024), new RectInt(1816, 1016, 194, 512)),
    };

    static Material atlasMat, glowMat, neonMat;
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static readonly HashSet<string> filled = new HashSet<string>();
    static readonly Dictionary<string, int> paletteIndex = new Dictionary<string, int>();
    static bool begun;

    internal static Material AtlasMaterial { get { Begin(); return atlasMat; } }
    internal static Material GlowMaterial { get { Begin(); return glowMat; } }
    internal static Material NeonMaterial { get { Begin(); return neonMat; } }

    // ================================================================ 準備（アトラス・マテリアル・メッシュの入れ物）
    internal static void Begin()
    {
        if (begun && atlasMat != null) return;
        Directory.CreateDirectory(Dir);
        Directory.CreateDirectory(MatDir);
        paletteIndex.Clear();
        for (int i = 0; i < Palette.Length; i++) paletteIndex[Palette[i].name] = i;

        BuildAtlas();
        var atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
        atlasMat = LitMaterial("casino_atlas", atlas, false);
        glowMat = LitMaterial("casino_atlas_glow", atlas, true);
        neonMat = BuildNeonMaterial();

        meshes.Clear(); filled.Clear();
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(MeshAssetPath).OfType<Mesh>()) meshes[m.name] = m;
        begun = true;
    }

    internal static void End()
    {
        AssetDatabase.SaveAssets();
        begun = false;
    }

    static Material LitMaterial(string name, Texture2D atlas, bool glow)
    {
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", atlas);
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0);
        mat.SetFloat("_EnvironmentReflections", 0); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        mat.enableInstancing = true;
        if (glow)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetTexture("_EmissionMap", atlas);
            mat.SetColor("_EmissionColor", Color.white * 2.6f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>ネオン看板：線の絵（アルファで切り抜き）を、同じ形の白いマスクで光らせる。両面。</summary>
    static Material BuildNeonMaterial()
    {
        string maskPath = Dir + "/casino_neon_mask.png";
        CopyIfChanged($"{SourceDir}/casino_neon_pink.png", NeonPath);
        CopyIfChanged($"{SourceDir}/casino_neon_mask.png", maskPath);
        foreach (string p in new[] { NeonPath, maskPath })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(p);
            if (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency || importer.maxTextureSize != 1024)
            {
                importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true; importer.maxTextureSize = 1024;
                importer.mipmapEnabled = true; importer.SaveAndReimport();
            }
        }
        string path = $"{MatDir}/casino_neon.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(NeonPath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .4f); mat.EnableKeyword("_ALPHATEST_ON");
        mat.SetOverrideTag("RenderType", "TransparentCutout"); mat.renderQueue = (int)RenderQueue.AlphaTest;
        mat.SetFloat("_Cull", 0);
        mat.SetFloat("_Smoothness", .1f);
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath));
        ColorUtility.TryParseHtmlString("#FF4FB4", out var pink);
        mat.SetColor("_EmissionColor", pink.linear * 5f);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>窓のガラス（透ける）。名前に glass が入っているので、町をまとめる道具は影を落とさない物として扱う。</summary>
    internal static Material GlassMaterial()
    {
        Directory.CreateDirectory(MatDir);
        string path = $"{MatDir}/casino_window_glass.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", new Color(.42f, .56f, .78f, .16f));
        mat.SetFloat("_Smoothness", .9f); mat.SetFloat("_Metallic", 0);
        mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        mat.SetFloat("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    static void CopyIfChanged(string from, string to)
    {
        byte[] src = File.ReadAllBytes(from);
        if (File.Exists(to) && File.ReadAllBytes(to).SequenceEqual(src)) return;
        File.WriteAllBytes(to, src);
        AssetDatabase.ImportAsset(to, ImportAssetOptions.ForceUpdate);
    }

    // ================================================================ アトラスを組む
    static void BuildAtlas()
    {
        var px = new Color32[AtlasSize * AtlasSize];
        var fill = new Color32(42, 34, 48, 255);
        for (int i = 0; i < px.Length; i++) px[i] = fill;

        var cache = new Dictionary<string, Texture2D>();
        foreach (var kv in Images)
        {
            if (!cache.TryGetValue(kv.Value.file, out var src))
            {
                src = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                src.LoadImage(File.ReadAllBytes($"{SourceDir}/{kv.Value.file}"));
                cache[kv.Value.file] = src;
            }
            Blit(px, src, kv.Value.src, kv.Value.dst);
        }
        foreach (var t in cache.Values) Object.DestroyImmediate(t);

        for (int i = 0; i < Palette.Length; i++)
        {
            ColorUtility.TryParseHtmlString("#" + Palette[i].hex, out var c);
            Color32 c32 = c;
            int x0 = SwatchBlock.x + i % SwatchCols * SwatchCell, y0 = SwatchBlock.y + i / SwatchCols * SwatchCell;
            for (int y = 0; y < SwatchCell; y++) for (int x = 0; x < SwatchCell; x++) Set(px, x0 + x, y0 + y, c32);
        }

        var tex = new Texture2D(AtlasSize, AtlasSize, TextureFormat.RGBA32, false);
        tex.SetPixels32(px); tex.Apply();
        byte[] png = tex.EncodeToPNG();
        Object.DestroyImmediate(tex);
        if (!File.Exists(AtlasPath) || !File.ReadAllBytes(AtlasPath).SequenceEqual(png))
        {
            File.WriteAllBytes(AtlasPath, png);
            AssetDatabase.ImportAsset(AtlasPath, ImportAssetOptions.ForceUpdate);
        }
        var importer = (TextureImporter)AssetImporter.GetAtPath(AtlasPath);
        if (importer.maxTextureSize != AtlasSize || importer.wrapMode != TextureWrapMode.Clamp || importer.anisoLevel != 4
            || importer.textureCompression != TextureImporterCompression.CompressedHQ || !importer.mipmapEnabled)
        {
            importer.maxTextureSize = AtlasSize; importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }

    /// <summary>アトラスの配列は下が原点。場所の表は左上が原点なので、ここで上下を返す。</summary>
    static void Set(Color32[] px, int x, int yTop, Color32 c)
    {
        if (x < 0 || x >= AtlasSize || yTop < 0 || yTop >= AtlasSize) return;
        px[(AtlasSize - 1 - yTop) * AtlasSize + x] = c;
    }

    static void Blit(Color32[] px, Texture2D src, RectInt s, RectInt d)
    {
        for (int y = 0; y < d.height; y++)
            for (int x = 0; x < d.width; x++)
            {
                float u = (s.x + (x + .5f) * s.width / d.width) / src.width;
                float v = 1f - (s.y + (y + .5f) * s.height / d.height) / src.height;
                Set(px, d.x + x, d.y + y, src.GetPixelBilinear(u, v));
            }
    }

    // ================================================================ UV
    /// <summary>色見本のマスの真ん中の UV。</summary>
    internal static Vector2 Sw(string color)
    {
        if (!paletteIndex.TryGetValue(color, out int i)) throw new ArgumentException("Unknown casino color: " + color);
        float x = SwatchBlock.x + (i % SwatchCols + .5f) * SwatchCell, y = SwatchBlock.y + (i / SwatchCols + .5f) * SwatchCell;
        return new Vector2(x / AtlasSize, 1f - y / AtlasSize);
    }

    /// <summary>絵の UV の範囲（2px 内側）。x・y は左下。</summary>
    internal static Rect ImageRect(Img image)
    {
        RectInt d = Images[image].dst;
        const float inset = 2f;
        return new Rect((d.x + inset) / AtlasSize, 1f - (d.y + d.height - inset) / AtlasSize, (d.width - inset * 2) / AtlasSize, (d.height - inset * 2) / AtlasSize);
    }

    /// <summary>絵の中の点（元の絵の px。左上が原点）を、切り出した範囲の 0〜1（左下が原点）にする。</summary>
    internal static Vector2 ImagePoint01(Img image, float srcX, float srcY)
    {
        RectInt s = Images[image].src;
        return new Vector2((srcX - s.x) / s.width, 1f - (srcY - s.y) / s.height);
    }

    internal static Vector2Int ImageSourceSize(Img image) => new Vector2Int(Images[image].src.width, Images[image].src.height);

    // ================================================================ メッシュの入れ物
    /// <summary>名前のついた形を作る（もうあれば作り直す）。光る面があれば 2 つ目のサブメッシュになる。</summary>
    internal static Mesh Build(string key, Action<Lp> fill)
    {
        Begin();
        if (!meshes.TryGetValue(key, out var mesh) || mesh == null)
        {
            mesh = new Mesh { name = key };
            if (AssetDatabase.LoadMainAssetAtPath(MeshAssetPath) == null) AssetDatabase.CreateAsset(mesh, MeshAssetPath);
            else AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
            meshes[key] = mesh;
        }
        if (filled.Add(key))
        {
            var lp = new Lp();
            fill(lp);
            lp.Fill(mesh);
            EditorUtility.SetDirty(mesh);
        }
        return mesh;
    }

    /// <summary>形を置く（MeshFilter と MeshRenderer だけの物）。マテリアルはサブメッシュの数に合わせる。</summary>
    internal static GameObject Place(Transform parent, string name, Mesh mesh, Vector3 localPosition = default, float yaw = 0, bool isStatic = false, bool shadow = true)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0, yaw, 0);
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterials = MaterialsFor(mesh);
        r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        r.lightProbeUsage = LightProbeUsage.Off; r.reflectionProbeUsage = ReflectionProbeUsage.Off;
        go.isStatic = isStatic;
        return go;
    }

    internal static Material[] MaterialsFor(Mesh mesh)
    {
        Begin();
        // 光る面だけの形（電球など）は、サブメッシュ 1 つで光るマテリアル
        if (mesh.subMeshCount == 1) return new[] { mesh.name.EndsWith("_glow") ? glowMat : atlasMat };
        return new[] { atlasMat, glowMat };
    }

    /// <summary>形の三角形の数（確認用）。</summary>
    internal static int Triangles(Mesh mesh) => mesh.triangles.Length / 3;

    // ================================================================ 多角形（XZ 平面。上から見て反時計回り）
    internal static Vector2[] Ellipse(float rx, float rz, int sides, float phase = 0)
    {
        var p = new Vector2[sides];
        for (int i = 0; i < sides; i++) { float a = phase + i * Mathf.PI * 2 / sides; p[i] = new Vector2(Mathf.Cos(a) * rx, Mathf.Sin(a) * rz); }
        return p;
    }

    /// <summary>角を落とした四角（8 角形）。中心が原点。</summary>
    internal static Vector2[] Chamfer(float width, float depth, float cut)
    {
        float x = width / 2, z = depth / 2; cut = Mathf.Min(cut, Mathf.Min(x, z) * .95f);
        return new[]
        {
            new Vector2(x, -z + cut), new Vector2(x, z - cut), new Vector2(x - cut, z), new Vector2(-x + cut, z),
            new Vector2(-x, z - cut), new Vector2(-x, -z + cut), new Vector2(-x + cut, -z), new Vector2(x - cut, -z),
        };
    }

    internal static Vector2[] Offset(Vector2[] poly, Vector2 by) => poly.Select(p => p + by).ToArray();

    // ================================================================ メッシュを組む道具
    internal sealed class Lp
    {
        readonly List<Vector3> v = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<int>[] tris = { new List<int>(), new List<int>() };   // 0 = ふつうの面、1 = 光る面
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        Matrix4x4 m = Matrix4x4.identity;

        /// <summary>このあと置く形を、移して回す（Pop で戻す）。入れ子にできる。</summary>
        public void Push(Vector3 position, Vector3 euler = default, Vector3? scale = null)
        {
            stack.Push(m);
            m = m * Matrix4x4.TRS(position, Quaternion.Euler(euler), scale ?? Vector3.one);
        }
        public void Pop() => m = stack.Pop();

        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ua, Vector2 ub, Vector2 uc, Vector3 outward, bool glow = false)
        {
            a = m.MultiplyPoint3x4(a); b = m.MultiplyPoint3x4(b); c = m.MultiplyPoint3x4(c);
            outward = m.MultiplyVector(outward);
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), outward) < 0) { (b, c) = (c, b); (ub, uc) = (uc, ub); }
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); uv.Add(ua); uv.Add(ub); uv.Add(uc);
            var t = tris[glow ? 1 : 0]; t.Add(i); t.Add(i + 1); t.Add(i + 2);
        }

        /// <summary>四角形（a→b→c→d が周の順）。</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 outward, bool glow = false)
        { Tri(a, b, c, ua, ub, uc, outward, glow); Tri(a, c, d, ua, uc, ud, outward, glow); }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, string color, Vector3 outward, bool glow = false)
        { Vector2 u = Sw(color); Quad(a, b, c, d, u, u, u, u, outward, glow); }

        /// <summary>絵を貼った四角形。a = 絵の左下、b = 右下、c = 右上、d = 左上。</summary>
        public void Image(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Rect r, Vector3 outward)
            => Quad(a, b, c, d, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax), outward);

        /// <summary>箱（最小の角と最大の角）。</summary>
        public void Box(Vector3 min, Vector3 max, string color, bool glow = false)
            => Box((min + max) / 2, max - min, Quaternion.identity, color, glow);

        /// <summary>向きのある箱（中心・大きさ・回転）。</summary>
        public void Box(Vector3 center, Vector3 size, Quaternion rot, string color, bool glow = false)
        {
            Vector2 u = Sw(color);
            Vector3 h = size * .5f;
            Vector3 P(Vector3 k) => center + rot * Vector3.Scale(k, h);
            foreach (var f in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 a = Mathf.Abs(f.x) > 0 ? Vector3.up : Vector3.right, b = Vector3.Cross(f, a);
                Quad(P(f + a + b), P(f + a - b), P(f - a - b), P(f - a + b), u, u, u, u, rot * f, glow);
            }
        }

        /// <summary>多角形の柱（凸の多角形を y0〜y1 に立てる）。topColor を変えると天面だけ別の色。上の輪を縮めたり広げたりもできる（topScale）。</summary>
        public void Prism(Vector2[] poly, float y0, float y1, string color, string topColor = null, bool bottom = false, float topScale = 1f, bool glow = false, Vector2 topShift = default)
        {
            Vector2 u = Sw(color), ut = Sw(topColor ?? color);
            Vector2 c2 = Vector2.zero; foreach (var p in poly) c2 += p; c2 /= poly.Length;
            Vector3 Lo(Vector2 p) => new Vector3(p.x, y0, p.y);
            Vector3 Hi(Vector2 p) { Vector2 q = c2 + (p - c2) * topScale + topShift; return new Vector3(q.x, y1, q.y); }
            int n = poly.Length;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % n];
                Vector2 mid = (p + q) / 2 - c2;
                Quad(Lo(p), Lo(q), Hi(q), Hi(p), u, u, u, u, new Vector3(mid.x, 0, mid.y), glow);
                Tri(Hi(c2), Hi(p), Hi(q), ut, ut, ut, Vector3.up, glow);
                if (bottom) Tri(Lo(c2), Lo(p), Lo(q), u, u, u, Vector3.down, glow);
            }
        }

        /// <summary>輪（外の多角形と内の多角形のあいだ）。2 つの多角形は同じ数の点で、同じ順に並んでいること。ふち・枠用。</summary>
        public void Ring(Vector2[] outer, Vector2[] inner, float y0, float y1, string color, string topColor = null, bool glow = false)
        {
            Vector2 u = Sw(color), ut = Sw(topColor ?? color);
            Vector2 c2 = Vector2.zero; foreach (var p in outer) c2 += p; c2 /= outer.Length;
            int n = outer.Length;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                Vector2 mid = (outer[i] + outer[j]) / 2 - c2;
                var o = new Vector3(mid.x, 0, mid.y);
                Vector3 P(Vector2 p, float y) => new Vector3(p.x, y, p.y);
                Quad(P(outer[i], y0), P(outer[j], y0), P(outer[j], y1), P(outer[i], y1), u, u, u, u, o, glow);        // 外の側面
                Quad(P(inner[i], y0), P(inner[j], y0), P(inner[j], y1), P(inner[i], y1), u, u, u, u, -o, glow);       // 内の側面
                Quad(P(outer[i], y1), P(outer[j], y1), P(inner[j], y1), P(inner[i], y1), ut, ut, ut, ut, Vector3.up, glow);   // 上の面
            }
        }

        /// <summary>多角形の天面に絵を貼る（真上から見た絵。bounds の範囲が絵の全体に当たる。絵の上が -Z、絵の左が +X の向き）。</summary>
        public void TopImage(Vector2[] poly, float y, Rect r, Rect bounds)
        {
            Vector2 c2 = Vector2.zero; foreach (var p in poly) c2 += p; c2 /= poly.Length;
            Vector2 U(Vector2 p) => new Vector2(r.xMin + (bounds.xMax - p.x) / bounds.width * r.width, r.yMin + (bounds.yMax - p.y) / bounds.height * r.height);
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 p = poly[i], q = poly[(i + 1) % poly.Length];
                Tri(new Vector3(c2.x, y, c2.y), new Vector3(p.x, y, p.y), new Vector3(q.x, y, q.y), U(c2), U(p), U(q), Vector3.up);
            }
        }

        /// <summary>回転体（y 軸のまわり）。rings は下から (半径, 高さ)。色は帯ごとに決められる。</summary>
        public void Lathe((float r, float y)[] rings, int sides, Func<int, string> colorOfBand, float phase = 0, bool capTop = true, bool capBottom = false, bool glow = false)
        {
            Vector3 R(float r, float y, float a) => new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
            for (int b = 0; b < rings.Length - 1; b++)
            {
                var r0 = rings[b]; var r1 = rings[b + 1];
                float dr = r1.r - r0.r, dy = r1.y - r0.y;
                Vector2 u = Sw(colorOfBand(b));
                for (int k = 0; k < sides; k++)
                {
                    float a0 = phase + k * Mathf.PI * 2 / sides, a1 = phase + (k + 1) * Mathf.PI * 2 / sides;
                    Vector3 outward = R(1, 0, (a0 + a1) / 2) * dy - Vector3.up * dr;
                    if (outward.sqrMagnitude < 1e-12f) continue;
                    Quad(R(r0.r, r0.y, a0), R(r0.r, r0.y, a1), R(r1.r, r1.y, a1), R(r1.r, r1.y, a0), u, u, u, u, outward, glow);
                }
            }
            if (capTop) Cap(rings[rings.Length - 1].r, rings[rings.Length - 1].y, sides, colorOfBand(rings.Length - 2), true, phase, glow);
            if (capBottom) Cap(rings[0].r, rings[0].y, sides, colorOfBand(0), false, phase, glow);
        }

        public void Lathe((float r, float y)[] rings, int sides, string color, float phase = 0, bool capTop = true, bool capBottom = false, bool glow = false)
            => Lathe(rings, sides, _ => color, phase, capTop, capBottom, glow);

        /// <summary>円柱（底の中心・半径・高さ）。</summary>
        public void Cyl(Vector3 baseCenter, float radius, float height, int sides, string color, bool glow = false, string topColor = null)
        {
            Push(baseCenter);
            Lathe(new[] { (radius, 0f), (radius, height) }, sides, color, Mathf.PI / sides, false, true, glow);
            Cap(radius, height, sides, topColor ?? color, true, Mathf.PI / sides, glow);
            Pop();
        }

        public void Cap(float r, float y, int sides, string color, bool up, float phase = 0, bool glow = false)
        {
            if (r <= 1e-5f) return;
            Vector2 u = Sw(color);
            var c = new Vector3(0, y, 0);
            for (int k = 0; k < sides; k++)
            {
                float a0 = phase + k * Mathf.PI * 2 / sides, a1 = phase + (k + 1) * Mathf.PI * 2 / sides;
                Tri(c, new Vector3(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), new Vector3(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), u, u, u, up ? Vector3.up : Vector3.down, glow);
            }
        }

        /// <summary>2 点をつなぐ棒（四角い断面）。</summary>
        public void Bar(Vector3 from, Vector3 to, float thickness, string color, bool glow = false)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-10f) return;
            Box((from + to) / 2, new Vector3(thickness, thickness, d.magnitude), Quaternion.LookRotation(d.normalized, Mathf.Abs(d.normalized.y) > .95f ? Vector3.forward : Vector3.up), color, glow);
        }

        public void Fill(Mesh mesh)
        {
            mesh.Clear();
            mesh.indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(v); mesh.SetUVs(0, uv);
            bool hasBase = tris[0].Count > 0, hasGlow = tris[1].Count > 0;
            if (hasBase && hasGlow) { mesh.subMeshCount = 2; mesh.SetTriangles(tris[0], 0); mesh.SetTriangles(tris[1], 1); }
            else { mesh.subMeshCount = 1; mesh.SetTriangles(hasGlow ? tris[1] : tris[0], 0); }
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds();
        }
    }
}
#endif
