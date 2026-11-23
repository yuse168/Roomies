#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// トランプ（表と裏の板）・チップ・点数の札の形と、トランプのマテリアル。
/// トランプの絵は Assets/_Art/Casino/casino_cards.png（14 列 × 4 段。1〜13 列が A〜K、段は ♠♥♦♣。14 列目のいちばん上が裏面）。
/// 絵の作り方は ArtSource/Casino/Screen/make_casino_screen_art.py。
/// </summary>
internal static partial class RoomiesCasinoArt
{
    internal const string CardsTexturePath = Dir + "/casino_cards.png";
    internal const int CardColumns = 14, CardRows = 4;
    internal const float CardCellU = 146f / 2048f;
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

    /// <summary>
    /// トランプの表の板（上向き。+Z が札の上の辺）。UV は 0〜1 で、どの札を出すかは BlackjackTableView が絵の中の場所で切り替える。
    /// </summary>
    internal static Mesh CardFace()
    {
        float w = CardW / 2, h = CardH / 2;
        return Raw("bj_card_face",
            new[] { new Vector3(-w, 0, -h), new Vector3(w, 0, -h), new Vector3(w, 0, h), new Vector3(-w, 0, h) },
            new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) },
            new[] { 0, 3, 2, 0, 2, 1 });
    }

    /// <summary>トランプの裏の板（下向き）。UV は裏面のマスを直接指す。</summary>
    internal static Mesh CardBack()
    {
        float w = CardW / 2, h = CardH / 2, u0 = (CardColumns - 1) * CardCellU, u1 = CardColumns * CardCellU, v0 = 1f - 1f / CardRows;
        const float y = -.0012f;
        return Raw("bj_card_back",
            new[] { new Vector3(-w, y, -h), new Vector3(w, y, -h), new Vector3(w, y, h), new Vector3(-w, y, h) },
            new[] { new Vector2(u1, v0), new Vector2(u0, v0), new Vector2(u0, 1), new Vector2(u1, 1) },
            new[] { 0, 2, 3, 0, 1, 2 });
    }

    /// <summary>トランプのマテリアル（表も裏もこれ 1 つ）。</summary>
    internal static Material CardsMaterial()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(CardsTexturePath);
        if (importer == null) { Debug.LogError("[Casino] " + CardsTexturePath + " がありません（python ArtSource/Casino/Screen/make_casino_screen_art.py を実行）"); return null; }
        if (importer.maxTextureSize != 2048 || importer.wrapMode != TextureWrapMode.Clamp || importer.anisoLevel != 8 || !importer.mipmapEnabled)
        {
            importer.maxTextureSize = 2048; importer.wrapMode = TextureWrapMode.Clamp; importer.anisoLevel = 8; importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
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
