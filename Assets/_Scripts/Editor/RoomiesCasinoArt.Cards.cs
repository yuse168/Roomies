#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// トランプ（表と裏の板）・チップ・点数の札の形と、トランプのマテリアル。
/// トランプの絵は Assets/_Art/Casino/casino_cards.png（4096×2048 に 11 列 × 5 段。左上から ♠A〜K、♥、♦、♣ の順で、53 番目が裏面）。
/// 札の角は、絵ではなく板の形で丸く切る（CardCorner）。
/// 絵の作り方は ArtSource/Casino/Screen/make_casino_screen_art.py。
/// </summary>
internal static partial class RoomiesCasinoArt
{
    internal const string CardsTexturePath = Dir + "/casino_cards.png";
    internal const int CardColumns = 11, CardBackIndex = 52;
    internal const float CardCellU = 372f / 4096f, CardCellV = 409f / 2048f;
    /// <summary>角の丸みの半径（横幅に対する割合）。絵を作るスクリプトの CARD_RADIUS と同じ値。</summary>
    internal const float CardCorner = .07f;
    /// <summary>札の厚み。</summary>
    internal const float CardThickness = .0012f;
    // 本物（63×88mm）の約 2.2 倍。遊ぶ人の目の高さから数字が読める大きさ
    internal const float CardW = .14f, CardH = .196f;

    /// <summary>UV を自分で決める形（トランプ用）。名前のついた形を作る（もうあれば作り直す）。</summary>
    static Mesh Raw(string key, Vector3[] vertices, Vector2[] uv, int[] triangles)
    {
        Begin();
        if (!meshes.TryGetValue(key, out var mesh) || mesh == null)
        {
            mesh = new Mesh { name = key };
            if (AssetDatabase.LoadMainAssetAtPath(MeshAssetPath) == null) AssetDatabase.CreateAsset(mesh, MeshAssetPath);
            else AssetDatabase.AddObjectToAsset(mesh, MeshAssetPath);
            meshes[key] = mesh;
        }
        mesh.Clear();
        mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);
        return mesh;
    }

    /// <summary>角を丸くした札の輪郭（上から見て時計回り。x は横、y は縦）。</summary>
    static Vector2[] CardOutline()
    {
        float w = CardW / 2, h = CardH / 2, r = CardW * CardCorner;
        const int steps = 5;
        var points = new System.Collections.Generic.List<Vector2>();
        // 右上 → 右下 → 左下 → 左上 の順に、角を 1 つずつ回る
        var corners = new[] { (new Vector2(w - r, h - r), 90f), (new Vector2(w - r, -h + r), 0f), (new Vector2(-w + r, -h + r), -90f), (new Vector2(-w + r, h - r), 180f) };
        foreach (var (center, start) in corners)
            for (int i = 0; i <= steps; i++)
            {
                float a = (start - 90f * i / steps) * Mathf.Deg2Rad;
                points.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
            }
        return points.ToArray();
    }

    /// <summary>
    /// トランプの表の板（上向き。+Z が札の上の辺）。四隅は丸い。UV は 0〜1 で、どの札を出すかは BlackjackTableView・HiLoTableView が絵の中の場所で切り替える。
    /// 札の厚みの面（ふち）も、この形に入っている（紙の色の所を写す）。
    /// </summary>
    internal static Mesh CardFace()
    {
        var outline = CardOutline();
        int n = outline.Length;
        var v = new System.Collections.Generic.List<Vector3>(); var uv = new System.Collections.Generic.List<Vector2>(); var tri = new System.Collections.Generic.List<int>();
        // 表の面：真ん中から扇の形に張る
        v.Add(Vector3.zero); uv.Add(new Vector2(.5f, .5f));
        foreach (var p in outline) { v.Add(new Vector3(p.x, 0, p.y)); uv.Add(new Vector2(p.x / CardW + .5f, p.y / CardH + .5f)); }
        for (int i = 0; i < n; i++) { tri.Add(0); tri.Add(1 + i); tri.Add(1 + (i + 1) % n); }
        // ふち（厚みの面）：紙の色の所（上の辺の少し内側）を写す
        var paper = new Vector2(.5f, .965f);
        for (int i = 0; i < n; i++)
        {
            Vector2 a = outline[i], b = outline[(i + 1) % n];
            int k = v.Count;
            v.Add(new Vector3(a.x, 0, a.y)); v.Add(new Vector3(b.x, 0, b.y)); v.Add(new Vector3(b.x, -CardThickness, b.y)); v.Add(new Vector3(a.x, -CardThickness, a.y));
            for (int j = 0; j < 4; j++) uv.Add(paper);
            tri.Add(k); tri.Add(k + 2); tri.Add(k + 1); tri.Add(k); tri.Add(k + 3); tri.Add(k + 2);
        }
        return Raw("bj_card_face", v.ToArray(), uv.ToArray(), tri.ToArray());
    }

    /// <summary>トランプの裏の板（下向き）。四隅は丸い。UV は裏面のマスを直接指す。</summary>
    internal static Mesh CardBack() => CardBackMesh("bj_card_back", false);

    /// <summary>裏面を上に向けた板（山のいちばん上に置く用）。</summary>
    internal static Mesh CardBackTop() => CardBackMesh("bj_card_back_top", true);

    static Mesh CardBackMesh(string key, bool faceUp)
    {
        var outline = CardOutline();
        int n = outline.Length;
        int col = CardBackIndex % CardColumns, row = CardBackIndex / CardColumns;
        float u0 = col * CardCellU, v0 = 1f - (row + 1) * CardCellV;
        float y = faceUp ? 0 : -CardThickness;
        var v = new Vector3[n + 1]; var uv = new Vector2[n + 1]; var tri = new int[n * 3];
        Vector2 Map(Vector2 p)
        {
            float s = p.x / CardW + .5f, t = p.y / CardH + .5f;
            if (!faceUp) s = 1f - s;        // 下から見たときに、絵が左右反対にならないように
            return new Vector2(u0 + s * CardCellU, v0 + t * CardCellV);
        }
        v[0] = new Vector3(0, y, 0); uv[0] = Map(Vector2.zero);
        for (int i = 0; i < n; i++) { v[i + 1] = new Vector3(outline[i].x, y, outline[i].y); uv[i + 1] = Map(outline[i]); }
        for (int i = 0; i < n; i++)
        {
            tri[i * 3] = 0;
            tri[i * 3 + 1] = faceUp ? 1 + i : 1 + (i + 1) % n;
            tri[i * 3 + 2] = faceUp ? 1 + (i + 1) % n : 1 + i;
        }
        return Raw(key, v, uv, tri);
    }

    /// <summary>トランプのマテリアル（表も裏もこれ 1 つ）。</summary>
    internal static Material CardsMaterial()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(CardsTexturePath);
        if (importer == null) { Debug.LogError("[Casino] " + CardsTexturePath + " がありません（python ArtSource/Casino/Screen/make_casino_screen_art.py を実行）"); return null; }
        // くっきり見せる設定：絵の大きさのまま（4096）、斜めから見てもぼけない（異方性 16）、縮めた絵（ミップ）へ早めに切り替えない（-0.5）、
        // ミップの境目をなめらかに（トライリニア）、圧縮は高品質（にじみの少ない BC7）
        if (importer.maxTextureSize != 4096 || importer.wrapMode != TextureWrapMode.Clamp || importer.anisoLevel != 16 || !importer.mipmapEnabled
            || importer.filterMode != FilterMode.Trilinear || !Mathf.Approximately(importer.mipMapBias, -.5f) || importer.textureCompression != TextureImporterCompression.CompressedHQ
            || importer.npotScale != TextureImporterNPOTScale.None || !importer.streamingMipmaps == false)
        {
            importer.maxTextureSize = 4096; importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 16; importer.mipmapEnabled = true;
            importer.filterMode = FilterMode.Trilinear; importer.mipMapBias = -.5f;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.npotScale = TextureImporterNPOTScale.None; importer.streamingMipmaps = false;
            var pc = importer.GetPlatformTextureSettings("Standalone");
            pc.overridden = true; pc.maxTextureSize = 4096; pc.format = TextureImporterFormat.BC7; pc.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(pc);
            importer.SaveAndReimport();
        }
        Directory.CreateDirectory(MatDir);
        string path = $"{MatDir}/casino_cards.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(CardsTexturePath));
        mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", .25f); mat.SetFloat("_Metallic", 0);
        mat.SetFloat("_EnvironmentReflections", 0); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>チップ 1 枚（足元が原点）。</summary>
    internal const float ChipRadius = .042f, ChipThickness = .013f;
    internal static Mesh BjChip(string color) => Build("bj_chip_" + color, lp =>
    {
        lp.Cyl(Vector3.zero, ChipRadius, ChipThickness, 10, color, false, "chip_white");
        lp.Cyl(new Vector3(0, ChipThickness, 0), ChipRadius * .62f, .0008f, 10, color);
    });

    /// <summary>点数の札の台（斜めに立てた小さな黒い板と金の縁）。原点は板の真ん中。文字の面は +Z。</summary>
    internal static Mesh ScorePlate() => Build("bj_score_plate", lp =>
    {
        lp.Box(new Vector3(-.085f, -.045f, -.012f), new Vector3(.085f, .045f, -.004f), "gold_dark");
        lp.Box(new Vector3(-.075f, -.036f, -.004f), new Vector3(.075f, .036f, 0), "ink");
    });
}
#endif
