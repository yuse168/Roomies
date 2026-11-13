#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 自然物 4：風で揺れる葉と草（2026/11/13）。何度実行しても同じ結果。
/// 葉・針葉・草の束の素材を、頂点を揺らすシェーダー「Roomies/Foliage Lit」に切り替える（見た目の色・切り抜きは URP Lit のときと同じ）。
/// 揺れの強さ・速さ・止まる距離は素材のインスペクターで直せる（下の表は初期値）。木と草のメッシュは作り直さない。
/// </summary>
internal static partial class RoomiesNature
{
    const string FoliageShader = "Roomies/Foliage Lit";

    // 素材, 揺れ方（0 木・1 草）, 強さ (m), 速さ, 木の高さ (m), 止まる距離 (m)
    static readonly (string mat, int mode, float strength, float speed, float height, float fade)[] WindTable =
    {
        ("M_Leaves_Green", 0, .14f, 1.2f, 7f, 70f),
        ("M_Leaves_Teal", 0, .12f, 1.25f, 5f, 70f),
        ("M_Leaves_Deep", 0, .2f, 1.0f, 10f, 70f),
        ("M_Needles", 0, .16f, .9f, 11f, 70f),
        ("M_Grass_Tuft", 1, .07f, 1.6f, 1f, 35f),
    };

    [MenuItem("Roomies/Art/Nature/4 Wind On Leaves And Grass")]
    internal static void ApplyWind()
    {
        var shader = Shader.Find(FoliageShader);
        if (shader == null) { Debug.LogError("[Nature] shader not found: " + FoliageShader); return; }
        int n = 0;
        foreach (var (name, mode, strength, speed, height, fade) in WindTable)
        {
            var m = AssetDatabase.LoadAssetAtPath<Material>(MatDir + name + ".mat");
            if (m == null) continue;
            var albedo = m.GetTexture("_BaseMap"); var normal = m.GetTexture("_BumpMap");
            var color = m.GetColor("_BaseColor"); float smooth = m.GetFloat("_Smoothness"); float bump = m.HasProperty("_BumpScale") ? m.GetFloat("_BumpScale") : 1;
            m.shader = shader;
            m.shaderKeywords = new string[0];
            m.SetTexture("_BaseMap", albedo); m.SetTexture("_BumpMap", normal); m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth); m.SetFloat("_BumpScale", bump); m.SetFloat("_Cutoff", .5f);
            m.SetFloat("_Translucency", mode == 1 ? .2f : .35f);
            m.SetFloat("_WindMode", mode); m.SetFloat("_WindStrength", strength); m.SetFloat("_WindSpeed", speed);
            m.SetFloat("_WindScale", 1); m.SetFloat("_WindHeight", height); m.SetFloat("_WindFadeDistance", fade);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            m.enableInstancing = true;
            EditorUtility.SetDirty(m); n++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Nature] wind on {n} materials");
    }
}
#endif
