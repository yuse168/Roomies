#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 町の自然物（木・山・草の地面）の作り直し（2026/11/13）。配置ツール。鉱山（Roomies_Mining）は触らない。
/// 素材はすべて自作：テクスチャはここで1画素ずつ描いた手続きテクスチャ、メッシュはここで組み立てたもの。
/// シェーダーは URP 標準の Lit（葉と草はアルファの切り抜き・両面、地面と山はディテールマップ）。
/// 共通の道具：テクスチャの保存、素材、メッシュの組み立て、ノイズ。
/// </summary>
internal static partial class RoomiesNature
{
    const string ArtDir = "Assets/_Art/Restyle/Nature/";
    const string TexDir = ArtDir + "Textures/";
    const string MatDir = "Assets/_Materials/Nature/";
    const string PrefabDir = "Assets/_Prefabs/Environment/Nature/";
    const string NeighborhoodPrefab = "Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab";

    internal delegate void Px(float u, float v, out Color c, out float h);

    // ================================================================ テクスチャ
    /// <summary>色（と高さから作るノーマルマップ）を png に保存する。alpha=true なら切り抜き用（縁の色を広げ、遠くでも葉が痩せない設定）。</summary>
    internal static Texture2D Tex(string name, int w, int h, Px px, float bump, out Texture2D normal, bool alpha = false, bool clamp = false)
    {
        var colors = new Color[w * h]; var heights = new float[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                px((x + .5f) / w, (y + .5f) / h, out var c, out var hh);
                colors[y * w + x] = c; heights[y * w + x] = hh;
            }
        });
        return Save(name, w, h, colors, heights, bump, out normal, alpha, clamp);
    }

    internal static Texture2D Save(string name, int w, int h, Color[] colors, float[] heights, float bump, out Texture2D normal, bool alpha, bool clamp)
    {
        if (alpha) Bleed(colors, w, h);
        var tex = WritePng(name, w, h, colors, false, alpha, clamp);
        normal = null;
        if (bump <= 0 || heights == null) return tex;
        var normals = new Color[w * h];
        Parallel.For(0, h, y =>
        {
            for (int x = 0; x < w; x++)
            {
                float H(int dx, int dy) => heights[((y + dy + h) % h) * w + (x + dx + w) % w];
                var n = new Vector3((H(-1, 0) - H(1, 0)) * bump, (H(0, -1) - H(0, 1)) * bump, 1).normalized;
                normals[y * w + x] = new Color(n.x * .5f + .5f, n.y * .5f + .5f, n.z * .5f + .5f, 1);
            }
        });
        normal = WritePng(name + "_n", w, h, normals, true, false, clamp);
        return tex;
    }

    /// <summary>透明な所の色を、近くの葉の色で埋める（縮小したときに縁が黒くならないように）。</summary>
    static void Bleed(Color[] c, int w, int h)
    {
        Color sum = Color.black; int n = 0;
        foreach (var p in c) if (p.a > .5f) { sum += p; n++; }
        var mean = n > 0 ? sum / n : Color.gray;
        for (int pass = 0; pass < 6; pass++)
        {
            var src = (Color[])c.Clone();
            Parallel.For(0, h, y =>
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x; if (src[i].a > .02f) continue;
                    Color acc = Color.black; int k = 0;
                    for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                    {
                        int xx = x + dx, yy = y + dy; if (xx < 0 || yy < 0 || xx >= w || yy >= h) continue;
                        var s = src[yy * w + xx]; if (s.a > .02f) { acc += s; k++; }
                    }
                    if (k > 0) { var m = acc / k; c[i] = new Color(m.r, m.g, m.b, 0.01f); }
                }
            });
        }
        // 広げた色（a=0.01）は色を残して透明に、届かなかった所は平均の色で透明に
        for (int i = 0; i < c.Length; i++)
        {
            if (c[i].a > .02f) continue;
            c[i] = c[i].a > 0 ? new Color(c[i].r, c[i].g, c[i].b, 0) : new Color(mean.r, mean.g, mean.b, 0);
        }
    }

    static Texture2D WritePng(string name, int w, int h, Color[] px, bool normal, bool alpha, bool clamp)
    {
        Directory.CreateDirectory(TexDir);
        string path = TexDir + name + ".png";
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false, normal);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        imp.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        imp.mipmapEnabled = true; imp.anisoLevel = 4;
        imp.wrapMode = clamp ? TextureWrapMode.Clamp : TextureWrapMode.Repeat;
        imp.alphaIsTransparency = alpha;
        imp.mipMapsPreserveCoverage = alpha; imp.alphaTestReferenceValue = .5f;
        imp.maxTextureSize = Mathf.Max(w, h);
        imp.textureCompression = TextureImporterCompression.CompressedHQ;
        imp.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ================================================================ 素材（URP Lit）
    internal static Material Lit(string name, Texture2D albedo, Texture2D normal, Color tint, float smooth, bool cutout = false, float normalScale = 1)
    {
        Directory.CreateDirectory(MatDir);
        string path = MatDir + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader;
        m.SetTexture("_BaseMap", albedo); m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", 0);
        m.SetFloat("_SpecularHighlights", cutout ? 0 : 1);
        if (cutout) m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); else m.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if (normal != null) { m.SetTexture("_BumpMap", normal); m.SetFloat("_BumpScale", normalScale); m.EnableKeyword("_NORMALMAP"); }
        else { m.SetTexture("_BumpMap", null); m.DisableKeyword("_NORMALMAP"); }
        m.SetFloat("_AlphaClip", cutout ? 1 : 0); m.SetFloat("_Cutoff", .5f);
        m.SetFloat("_Cull", cutout ? (float)CullMode.Off : (float)CullMode.Back);
        m.SetFloat("_Surface", 0);
        if (cutout) { m.EnableKeyword("_ALPHATEST_ON"); m.SetOverrideTag("RenderType", "TransparentCutout"); m.renderQueue = (int)RenderQueue.AlphaTest; }
        else { m.DisableKeyword("_ALPHATEST_ON"); m.SetOverrideTag("RenderType", "Opaque"); m.renderQueue = -1; }
        m.enableInstancing = true;
        EditorUtility.SetDirty(m);
        return m;
    }

    /// <summary>細かい模様（ディテール）を重ねる：近くで見たときの粒立ち。URP Lit の Detail（色は2倍の掛け算）。</summary>
    internal static void Detail(Material m, Texture2D albedo, Texture2D normal, float tile, float normalScale)
    {
        m.SetTexture("_DetailAlbedoMap", albedo); m.SetTexture("_DetailNormalMap", normal);
        m.SetTextureScale("_DetailAlbedoMap", new Vector2(tile, tile));
        m.SetFloat("_DetailAlbedoMapScale", 1); m.SetFloat("_DetailNormalMapScale", normalScale);
        m.SetTexture("_DetailMask", null);
        m.EnableKeyword("_DETAIL_MULX2");
        EditorUtility.SetDirty(m);
    }

    // ================================================================ メッシュ
    internal class MB
    {
        public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
        public readonly List<Vector2> uv = new List<Vector2>();
        public readonly List<int>[] t;
        public MB(int subs) { t = new List<int>[subs]; for (int i = 0; i < subs; i++) t[i] = new List<int>(); }

        /// <summary>折れ線に沿った管（幹・枝）。半径は点ごと。u は周り（uRepeat 回）、v は長さ（vScale m で1回）。</summary>
        public void Tube(IList<Vector3> pts, IList<float> rad, int seg, float uRepeat, float vScale, int sub = 0)
        {
            Vector3 prevN = Vector3.Cross(pts[1] - pts[0], Vector3.right);
            if (prevN.sqrMagnitude < 1e-6f) prevN = Vector3.Cross(pts[1] - pts[0], Vector3.forward);
            prevN.Normalize();
            float len = 0; int start = v.Count;
            for (int i = 0; i < pts.Count; i++)
            {
                Vector3 tan = (i == pts.Count - 1 ? pts[i] - pts[i - 1] : i == 0 ? pts[1] - pts[0] : (pts[i + 1] - pts[i - 1])).normalized;
                Vector3 nn = Vector3.ProjectOnPlane(prevN, tan).normalized; prevN = nn;
                Vector3 bn = Vector3.Cross(tan, nn);
                if (i > 0) len += Vector3.Distance(pts[i], pts[i - 1]);
                for (int j = 0; j <= seg; j++)
                {
                    float a = j / (float)seg * Mathf.PI * 2;
                    Vector3 dir = nn * Mathf.Cos(a) + bn * Mathf.Sin(a);
                    v.Add(pts[i] + dir * rad[i]); n.Add(dir); uv.Add(new Vector2(j / (float)seg * uRepeat, len / vScale));
                }
            }
            int ring = seg + 1;
            for (int i = 0; i < pts.Count - 1; i++)
                for (int j = 0; j < seg; j++)
                {
                    int a = start + i * ring + j, b = a + 1, c = a + ring, d = c + 1;
                    t[sub].Add(a); t[sub].Add(c); t[sub].Add(b);
                    t[sub].Add(b); t[sub].Add(c); t[sub].Add(d);
                }
        }

        /// <summary>四角い板（葉・草）。corners は左下・右下・右上・左上。法線は板ごとに渡す（ふんわり見せるため外向きにそろえる）。</summary>
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, int sub, Rect uvRect)
        {
            int i = v.Count;
            v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            n.Add(na); n.Add(nb); n.Add(nc); n.Add(nd);
            uv.Add(new Vector2(uvRect.xMin, uvRect.yMin)); uv.Add(new Vector2(uvRect.xMax, uvRect.yMin));
            uv.Add(new Vector2(uvRect.xMax, uvRect.yMax)); uv.Add(new Vector2(uvRect.xMin, uvRect.yMax));
            t[sub].Add(i); t[sub].Add(i + 2); t[sub].Add(i + 1);
            t[sub].Add(i); t[sub].Add(i + 3); t[sub].Add(i + 2);
        }

        public Mesh ToMesh(string name)
        {
            var m = new Mesh { name = name, indexFormat = v.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv);
            m.subMeshCount = t.Length;
            for (int i = 0; i < t.Length; i++) m.SetTriangles(t[i], i);
            m.RecalculateBounds(); m.RecalculateTangents();
            return m;
        }
    }

    /// <summary>メッシュをアセットに入れる（同じ名前があれば中身を入れ替える＝参照は切れない）。</summary>
    internal static Mesh StoreMesh(string assetPath, Mesh mesh)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
        if (AssetDatabase.LoadMainAssetAtPath(assetPath) == null)
            AssetDatabase.CreateAsset(new Mesh { name = Path.GetFileNameWithoutExtension(assetPath) }, assetPath);
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            if (o is Mesh old && old.name == mesh.name && !AssetDatabase.IsMainAsset(old))
            {
                EditorUtility.CopySerialized(mesh, old);
                Object.DestroyImmediate(mesh);
                EditorUtility.SetDirty(old);
                return old;
            }
        AssetDatabase.AddObjectToAsset(mesh, assetPath);
        return mesh;
    }

    // ================================================================ ノイズ（スレッドから呼べる自前の値ノイズ。period で繰り返す）
    static float Hash(int x, int y, int seed)
    {
        unchecked
        {
            int h = x * 374761393 + y * 668265263 + seed * 1442695041;
            h = (h ^ (h >> 13)) * 1274126177;
            return ((h ^ (h >> 16)) & 0xffffff) / 16777215f;
        }
    }

    internal static float VNoise(float x, float y, int period, int seed)
    {
        int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
        float fx = x - x0, fy = y - y0;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        int P(int a) => period > 0 ? ((a % period) + period) % period : a;
        float a00 = Hash(P(x0), P(y0), seed), a10 = Hash(P(x0 + 1), P(y0), seed), a01 = Hash(P(x0), P(y0 + 1), seed), a11 = Hash(P(x0 + 1), P(y0 + 1), seed);
        return Mathf.Lerp(Mathf.Lerp(a00, a10, fx), Mathf.Lerp(a01, a11, fx), fy);
    }

    /// <summary>重ねたノイズ（0〜1）。u, v は 0〜1、freq はその中の繰り返し回数（タイルになる）。</summary>
    internal static float Fbm(float u, float v, int freq, int octaves, int seed)
    {
        float sum = 0, amp = .5f, norm = 0;
        for (int o = 0; o < octaves; o++)
        {
            int f = freq << o;
            sum += VNoise(u * f, v * f, f, seed + o * 31) * amp; norm += amp; amp *= .5f;
        }
        return sum / norm;
    }

    internal static float Smooth(float a, float b, float x) => Mathf.SmoothStep(0, 1, Mathf.InverseLerp(a, b, x));
    internal static float Rand01(int a, int b) => Hash(a, b, 977);
}
#endif
