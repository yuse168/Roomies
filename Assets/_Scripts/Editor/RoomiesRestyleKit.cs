#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// 見た目づくりの道具（エディタ専用）。配置ツールから呼び、作った物はシーンとアセットに保存する。
/// 実行時には動かない。メッシュ・マテリアル・テクスチャは名前で作り直すので、何度実行しても同じ結果になる。
/// </summary>
internal static class RoomiesRestyleKit
{
    internal const string ArtDir = "Assets/_Art/Restyle";
    internal const string MatDir = "Assets/_Materials/Restyle";
    const string MeshAsset = ArtDir + "/RestyleMeshes.asset";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset";

    static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
    static readonly HashSet<string> Filled = new HashSet<string>();
    static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
    static Mesh cube, cylinder, sphere, quad;
    static TMP_FontAsset font;
    /// <summary>動く部品（扉など）やプレハブの中身を作るあいだは false にする。</summary>
    internal static bool MarkStatic = true;

    internal static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    // ================================================================ 準備
    internal static void Begin()
    {
        Directory.CreateDirectory(ArtDir + "/Textures");
        Directory.CreateDirectory(MatDir);
        AssetDatabase.Refresh();
        Meshes.Clear(); Filled.Clear(); Mats.Clear();
        foreach (var m in AssetDatabase.LoadAllAssetsAtPath(MeshAsset).OfType<Mesh>()) Meshes[m.name] = m;
        cube = Builtin(PrimitiveType.Cube); cylinder = Builtin(PrimitiveType.Cylinder);
        sphere = Builtin(PrimitiveType.Sphere); quad = Builtin(PrimitiveType.Quad);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        if (font == null) throw new InvalidOperationException("Font not found: " + FontPath);
    }

    internal static void End()
    {
        AssetDatabase.SaveAssets();
        // 素材を作り直したあと、つやと質感の仕上げ（Material Finish）をかけ直す。作り直しで仕上げが消えないように
        RoomiesMaterialFinishBuilder.ApplyMatte();
        RoomiesMaterialFinishBuilder.ApplySurfaceTextures();
        // 部屋とタイトルの部品をテンプレの子マテリアルへ付け替え直す
        RoomiesMaterialFinishBuilder.ApplyTemplatesToRoom();
        // 作り直した明かりに、影（接地感）の設定をかけ直す
        RoomiesMaterialFinishBuilder.ApplyContactShadows();
        RoomiesMaterialFinishBuilder.ApplyLightBalance();
    }

    static Mesh Builtin(PrimitiveType type)
    {
        var go = GameObject.CreatePrimitive(type);
        var mesh = go.GetComponent<MeshFilter>().sharedMesh;
        Object.DestroyImmediate(go);
        return mesh;
    }

    // ================================================================ マテリアル
    internal static Color Hex(string hex, float a = 1)
    {
        ColorUtility.TryParseHtmlString("#" + hex, out var c); c.a = a; return c;
    }

    /// <summary>URP Lit のマテリアル。同じ名前なら同じアセットを更新する。</summary>
    internal static Material Mat(string name, string hex, float smooth = .18f, float metallic = 0,
        string emission = null, float emissionPower = 1, Texture2D map = null, Vector2? tiling = null,
        bool doubleSided = false, float alpha = 1)
    {
        if (Mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetColor("_BaseColor", Hex(hex, alpha));
        mat.SetFloat("_Smoothness", smooth); mat.SetFloat("_Metallic", metallic);
        mat.SetTexture("_BaseMap", map);
        mat.SetTextureScale("_BaseMap", tiling ?? Vector2.one);
        if (emission != null)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Hex(emission).linear * emissionPower);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        else { mat.DisableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", Color.black); }
        mat.SetFloat("_Cull", doubleSided ? 0 : 2);
        if (alpha < 1)
        {
            mat.SetFloat("_Surface", 1); mat.SetFloat("_Blend", 0);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
        EditorUtility.SetDirty(mat);
        Mats[name] = mat; return mat;
    }

    /// <summary>光る文字用。フォントのマテリアルを写して、面の色を HDR にする。</summary>
    internal static Material TextGlow(string name, string hex, float power)
    {
        if (Mats.TryGetValue(name, out var cached)) return cached;
        string path = $"{MatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(font.material); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = font.material.shader;
        mat.CopyPropertiesFromMaterial(font.material);
        mat.SetColor("_FaceColor", Hex(hex).linear * power);
        EditorUtility.SetDirty(mat);
        Mats[name] = mat; return mat;
    }

    // ================================================================ テクスチャ
    internal static Texture2D Texture(string name, int size, Func<float, float, Color> pixel)
    {
        string path = $"{ArtDir}/Textures/{name}.png";
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++) px[y * size + x] = pixel((x + .5f) / size, (y + .5f) / size);
        tex.SetPixels(px); tex.Apply();
        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer.wrapMode != TextureWrapMode.Repeat || !importer.mipmapEnabled || importer.anisoLevel != 4)
        {
            importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.anisoLevel = 4;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    /// <summary>くり返しても継ぎ目が出ないノイズ（0〜1）。</summary>
    internal static float Noise(float u, float v, float scale, float seed = 0)
    {
        float a = Mathf.PerlinNoise(u * scale + seed, v * scale + seed);
        float b = Mathf.PerlinNoise((u - 1) * scale + seed, v * scale + seed);
        float c = Mathf.PerlinNoise(u * scale + seed, (v - 1) * scale + seed);
        float d = Mathf.PerlinNoise((u - 1) * scale + seed, (v - 1) * scale + seed);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    // ================================================================ メッシュ
    static Mesh GetMesh(string key, Action<Mesh> fill)
    {
        if (!Meshes.TryGetValue(key, out var mesh) || mesh == null)
        {
            mesh = new Mesh { name = key };
            if (AssetDatabase.LoadMainAssetAtPath(MeshAsset) == null) AssetDatabase.CreateAsset(mesh, MeshAsset);
            else AssetDatabase.AddObjectToAsset(mesh, MeshAsset);
            Meshes[key] = mesh;
        }
        if (Filled.Add(key))
        {
            mesh.Clear(); fill(mesh); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        }
        return mesh;
    }

    static string K(float f) => Mathf.RoundToInt(f * 1000).ToString();

    internal static Mesh RoundedBoxMesh(Vector3 size, float radius)
    {
        return GetMesh($"rbox_{K(size.x)}_{K(size.y)}_{K(size.z)}_{K(radius)}", m =>
        {
            Vector3 h = size * .5f;
            float r = Mathf.Min(radius, Mathf.Min(h.x, Mathf.Min(h.y, h.z)));
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            for (int face = 0; face < 6; face++)
            {
                int a = face / 2, u = (a + 1) % 3, w = (a + 2) % 3; float s = face % 2 == 0 ? 1 : -1;
                float[] tu = Steps(h[u], r), tw = Steps(h[w], r);
                int start = verts.Count;
                for (int j = 0; j < 6; j++) for (int i = 0; i < 6; i++)
                {
                    var p = Vector3.zero; p[a] = s * h[a]; p[u] = tu[i]; p[w] = tw[j];
                    var inner = V(Mathf.Clamp(p.x, -(h.x - r), h.x - r), Mathf.Clamp(p.y, -(h.y - r), h.y - r), Mathf.Clamp(p.z, -(h.z - r), h.z - r));
                    var n = (p - inner).normalized;
                    verts.Add(inner + n * r); norms.Add(n);
                    uvs.Add(new Vector2(p[u] / size[u] + .5f, p[w] / size[w] + .5f));
                }
                for (int j = 0; j < 5; j++) for (int i = 0; i < 5; i++)
                {
                    int v0 = start + j * 6 + i, v1 = v0 + 1, v2 = v0 + 6, v3 = v2 + 1;
                    if (s > 0) { tris.Add(v0); tris.Add(v1); tris.Add(v3); tris.Add(v0); tris.Add(v3); tris.Add(v2); }
                    else { tris.Add(v0); tris.Add(v3); tris.Add(v1); tris.Add(v0); tris.Add(v2); tris.Add(v3); }
                }
            }
            m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        });
    }

    static float[] Steps(float h, float r) => new[] { -h, -h + r * .35f, -h + r, h - r, h - r * .35f, h };

    /// <summary>回転体。profile は下から上へ (半径, 高さ)。</summary>
    internal static Mesh LatheMesh(string key, Vector2[] profile, int segments = 14)
    {
        return GetMesh("lathe_" + key, m =>
        {
            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int rings = profile.Length;
            for (int r = 0; r < rings; r++) for (int s = 0; s <= segments; s++)
            {
                float ang = s * Mathf.PI * 2 / segments;
                verts.Add(V(Mathf.Cos(ang) * profile[r].x, profile[r].y, Mathf.Sin(ang) * profile[r].x));
                uvs.Add(new Vector2((float)s / segments, (float)r / (rings - 1)));
            }
            for (int r = 0; r < rings - 1; r++) for (int s = 0; s < segments; s++)
            {
                int v0 = r * (segments + 1) + s, v1 = v0 + 1, v2 = v0 + segments + 1, v3 = v2 + 1;
                tris.Add(v0); tris.Add(v2); tris.Add(v1); tris.Add(v1); tris.Add(v2); tris.Add(v3);
            }
            m.SetVertices(verts); m.SetUVs(0, uvs); m.SetTriangles(tris, 0); m.RecalculateNormals();
        });
    }

    /// <summary>線に沿った管（ネオン・コード・輪）。</summary>
    internal static Mesh TubeMesh(string key, Vector3[] path, float radius, bool closed = false, int sides = 6)
    {
        return GetMesh("tube_" + key, m =>
        {
            var verts = new List<Vector3>(); var tris = new List<int>();
            int count = path.Length;
            for (int i = 0; i < count; i++)
            {
                Vector3 prev = path[closed ? (i - 1 + count) % count : Mathf.Max(i - 1, 0)];
                Vector3 next = path[closed ? (i + 1) % count : Mathf.Min(i + 1, count - 1)];
                Vector3 tangent = (next - prev).normalized;
                Vector3 side = Vector3.Cross(tangent, Mathf.Abs(tangent.z) > .9f ? Vector3.up : Vector3.forward).normalized;
                Vector3 up = Vector3.Cross(side, tangent);
                for (int s = 0; s < sides; s++)
                {
                    float ang = s * Mathf.PI * 2 / sides;
                    verts.Add(path[i] + (side * Mathf.Cos(ang) + up * Mathf.Sin(ang)) * radius);
                }
            }
            int ringCount = closed ? count : count - 1;
            for (int i = 0; i < ringCount; i++) for (int s = 0; s < sides; s++)
            {
                int a = i * sides + s, b = i * sides + (s + 1) % sides;
                int c = ((i + 1) % count) * sides + s, d = ((i + 1) % count) * sides + (s + 1) % sides;
                tris.Add(a); tris.Add(c); tris.Add(b); tris.Add(b); tris.Add(c); tris.Add(d);
            }
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals();
        });
    }

    /// <summary>三角柱（XY の三角を Z に押し出す）。ひれ・くさび用。</summary>
    internal static Mesh PrismMesh(string key, Vector2 a, Vector2 b, Vector2 c, float depth)
    {
        return GetMesh("prism_" + key, m =>
        {
            float z = depth * .5f;
            Vector3 a0 = V(a.x, a.y, -z), b0 = V(b.x, b.y, -z), c0 = V(c.x, c.y, -z), a1 = V(a.x, a.y, z), b1 = V(b.x, b.y, z), c1 = V(c.x, c.y, z);
            var verts = new List<Vector3>(); var tris = new List<int>();
            void Tri(Vector3 p, Vector3 q, Vector3 r) { int i = verts.Count; verts.Add(p); verts.Add(q); verts.Add(r); tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); }
            void Quad(Vector3 p, Vector3 q, Vector3 r, Vector3 s) { Tri(p, q, r); Tri(p, r, s); }
            bool ccw = (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x) > 0;
            if (ccw) { Tri(a0, c0, b0); Tri(a1, b1, c1); Quad(a0, b0, b1, a1); Quad(b0, c0, c1, b1); Quad(c0, a0, a1, c1); }
            else { Tri(a0, b0, c0); Tri(a1, c1, b1); Quad(a0, a1, b1, b0); Quad(b0, b1, c1, c0); Quad(c0, c1, a1, a0); }
            m.SetVertices(verts); m.SetTriangles(tris, 0); m.RecalculateNormals();
        });
    }

    // ================================================================ 部品を置く
    internal static Transform Group(Transform parent, string name, Vector3 localPosition = default, float yaw = 0)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition; go.transform.localEulerAngles = V(0, yaw, 0);
        go.isStatic = MarkStatic;
        return go.transform;
    }

    static GameObject Part(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 scale, Vector3 rot, bool shadow = true)
    {
        var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos; go.transform.localEulerAngles = rot; go.transform.localScale = scale;
        go.GetComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat;
        r.shadowCastingMode = shadow ? ShadowCastingMode.On : ShadowCastingMode.Off;
        go.isStatic = MarkStatic;
        return go;
    }

    internal static GameObject Box(Transform p, string name, Vector3 pos, Vector3 size, Material mat, Vector3 rot = default, bool shadow = true)
        => Part(p, name, cube, mat, pos, size, rot, shadow);

    internal static GameObject RBox(Transform p, string name, Vector3 pos, Vector3 size, float radius, Material mat, Vector3 rot = default)
        => Part(p, name, RoundedBoxMesh(size, radius), mat, pos, Vector3.one, rot);

    /// <summary>円柱。pos は中心。</summary>
    internal static GameObject Cyl(Transform p, string name, Vector3 pos, float diameter, float height, Material mat, Vector3 rot = default)
        => Part(p, name, cylinder, mat, pos, V(diameter, height * .5f, diameter), rot);

    internal static GameObject Ball(Transform p, string name, Vector3 pos, Vector3 size, Material mat, Vector3 rot = default)
        => Part(p, name, sphere, mat, pos, size, rot);

    internal static GameObject Lathe(Transform p, string name, string key, Vector2[] profile, Vector3 pos, Material mat, Vector3 rot = default, int segments = 14, float scale = 1)
        => Part(p, name, LatheMesh(key, profile, segments), mat, pos, Vector3.one * scale, rot);

    internal static GameObject Tube(Transform p, string name, string key, Vector3[] path, float radius, Material mat, bool closed = false, Vector3 pos = default, Vector3 rot = default, bool shadow = true)
        => Part(p, name, TubeMesh(key, path, radius, closed), mat, pos, Vector3.one, rot, shadow);

    internal static GameObject Prism(Transform p, string name, string key, Vector2 a, Vector2 b, Vector2 c, float depth, Vector3 pos, Material mat, Vector3 rot = default)
        => Part(p, name, PrismMesh(key, a, b, c, depth), mat, pos, Vector3.one, rot);

    /// <summary>平らな板（+Z 側から見える面）。画面・紙・ラベル用。</summary>
    internal static GameObject Plane(Transform p, string name, Vector3 pos, Vector2 size, Material mat, Vector3 rot = default)
        => Part(p, name, quad, mat, pos, V(size.x, size.y, 1), rot + V(0, 180, 0), false);

    /// <summary>3D の文字。親の +Z 側（手前）から読める向きで置く。</summary>
    internal static TextMeshPro Text(Transform p, string text, Vector3 pos, Vector2 size, Color color, float maxSize = 4,
        Vector3 rot = default, Material glow = null, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var go = new GameObject("Text - " + text.Replace("\n", " "));
        go.transform.SetParent(p, false); go.transform.localPosition = pos; go.transform.localEulerAngles = rot + V(0, 180, 0);
        var label = go.AddComponent<TextMeshPro>();
        label.font = font; if (glow != null) label.fontSharedMaterial = glow;
        label.text = text; label.alignment = align; label.color = color;
        label.enableAutoSizing = true; label.fontSizeMin = .05f; label.fontSizeMax = maxSize;
        label.rectTransform.sizeDelta = size;
        label.textWrappingMode = text.Contains("\n") ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        go.isStatic = MarkStatic;
        return label;
    }

    /// <summary>歩くための当たり判定（見た目の親に付ける）。</summary>
    internal static void Solid(Transform prop, Vector3 center, Vector3 size)
    {
        var box = prop.gameObject.AddComponent<BoxCollider>(); box.center = center; box.size = size;
    }

    internal static Light Lamp(Transform p, string name, Vector3 pos, Color color, float intensity, float range, bool shadows = false)
    {
        var go = new GameObject(name); go.transform.SetParent(p, false); go.transform.localPosition = pos;
        var light = go.AddComponent<Light>();
        light.type = LightType.Point; light.color = color; light.intensity = intensity; light.range = range;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        light.shadowBias = .03f; light.shadowNormalBias = .2f; light.shadowNearPlane = .08f;
        return light;
    }
}
#endif
