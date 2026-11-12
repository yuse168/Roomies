#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 「プラスチック感」を減らすための素材の仕上げ（2026/11/13）。何度実行しても同じ結果。
/// 1 つや：素材ごとに Smoothness / Metallic とハイライト・映り込みを決める。
/// 値はこの表だけで決まる。表にない素材は触らない。
/// </summary>
internal static class RoomiesMaterialFinishBuilder
{
    const string Restyle = "Assets/_Materials/Restyle/";
    const string Library = "Assets/Resources/RoomiesArt/Materials/";

    // smoothness, metallic, 光のハイライト, 映り込み
    readonly struct Finish
    {
        public readonly float Smooth, Metal; public readonly bool Highlights, Reflections;
        public Finish(float smooth, float metal = 0, bool highlights = true, bool reflections = true)
        { Smooth = smooth; Metal = metal; Highlights = highlights; Reflections = reflections; }
    }

    // 布・紙・段ボール・壁：ほぼつやなし。ハイライトも映り込みも切る
    static readonly Finish Cloth = new Finish(.02f, 0, false, false);
    // 木：わずかに油っぽいつや
    static readonly Finish Wood = new Finish(.14f);
    // 塗装した物・小物
    static readonly Finish Painted = new Finish(.16f);
    // プラスチック・家電
    static readonly Finish Plastic = new Finish(.26f);

    static readonly Dictionary<string, Finish> Table = new Dictionary<string, Finish>
    {
        // ---- 布・紙・段ボール・壁
        [Restyle + "bed_duvet"] = Cloth, [Restyle + "bed_duvet_light"] = Cloth, [Restyle + "bed_sheet"] = Cloth,
        [Restyle + "cloth_blue"] = Cloth, [Restyle + "cloth_cream"] = Cloth, [Restyle + "curtain"] = Cloth,
        [Restyle + "fabric_grey"] = Cloth, [Restyle + "pillow_teal"] = Cloth, [Restyle + "plush_grey"] = Cloth,
        [Restyle + "rug"] = Cloth, [Restyle + "rug_edge"] = Cloth,
        [Restyle + "sofa_yellow"] = new Finish(.05f, 0, true, false), [Restyle + "sofa_yellow_dark"] = new Finish(.05f, 0, true, false),
        [Restyle + "cardboard"] = Cloth, [Restyle + "cardboard_dark"] = Cloth, [Restyle + "tape_brown"] = new Finish(.2f),
        [Restyle + "paper"] = Cloth, [Restyle + "page_grey"] = Cloth, [Restyle + "page_green"] = new Finish(.08f, 0, true, false),
        [Restyle + "page_ink"] = new Finish(.08f, 0, true, false), [Restyle + "page_red"] = new Finish(.08f, 0, true, false),
        [Restyle + "page_yellow"] = new Finish(.08f, 0, true, false), [Restyle + "poster_red"] = Cloth, [Restyle + "poster_teal"] = Cloth,
        [Restyle + "ink_brown"] = Cloth,
        [Restyle + "wall_plaster"] = Cloth, [Restyle + "ceiling"] = Cloth, [Restyle + "soil"] = Cloth, [Restyle + "gravel"] = Cloth,
        [Restyle + "shark_blue"] = Cloth, [Restyle + "shark_belly"] = Cloth, [Restyle + "shark_mouth"] = Cloth,
        [Restyle + "bag_blue"] = new Finish(.22f), [Restyle + "bin_blue"] = new Finish(.18f),
        [Restyle + "crust"] = Cloth, [Restyle + "cheese"] = new Finish(.12f), [Restyle + "cream"] = new Finish(.08f),
        [Restyle + "leaf"] = new Finish(.2f), [Restyle + "leaf_dark"] = new Finish(.2f), [Restyle + "pot"] = new Finish(.1f),

        // ---- 木
        [Restyle + "floor_planks"] = new Finish(.18f), [Restyle + "brown"] = Wood,
        [Restyle + "wood_back"] = Wood, [Restyle + "wood_dark"] = Wood, [Restyle + "wood_light"] = Wood, [Restyle + "wood_mid"] = Wood,

        // ---- 塗装・小物
        [Restyle + "blue"] = Painted, [Restyle + "green"] = Painted, [Restyle + "orange"] = Painted, [Restyle + "pink"] = Painted,
        [Restyle + "purple"] = Painted, [Restyle + "purple_dark"] = Painted, [Restyle + "red"] = Painted, [Restyle + "teal"] = Painted,
        [Restyle + "yellow"] = Painted, [Restyle + "dart_dark"] = Painted, [Restyle + "tape"] = Painted,
        [Restyle + "cabinet_green"] = Painted, [Restyle + "cabinet_green_light"] = Painted,

        // ---- プラスチック・家電・金属
        [Restyle + "plastic_black"] = Plastic, [Restyle + "plastic_white"] = Plastic, [Restyle + "fan_blade"] = Plastic,
        [Restyle + "fridge_green"] = new Finish(.3f), [Restyle + "fridge_green_dark"] = new Finish(.3f),
        [Restyle + "worktop"] = new Finish(.28f), [Restyle + "mw_panel"] = Plastic, [Restyle + "slot_panel"] = Plastic,
        [Restyle + "slot_body"] = new Finish(.35f, .2f), [Restyle + "grease"] = new Finish(.3f),
        [Restyle + "lamp_shade"] = new Finish(.32f, .35f), [Restyle + "lamp_shade_green"] = new Finish(.3f, .2f),
        [Restyle + "metal_dark"] = new Finish(.4f, .6f), [Restyle + "metal_grey"] = new Finish(.45f, .7f),

        // ---- 部屋の古い家具と町で共通の素材（Resources/RoomiesArt）
        [Library + "blue"] = Painted, [Library + "coral"] = Painted, [Library + "cream"] = Painted, [Library + "green"] = Painted,
        [Library + "ink"] = Painted, [Library + "mustard"] = Painted, [Library + "paper"] = new Finish(.06f, 0, true, false),
        [Library + "pink"] = Painted, [Library + "teal"] = Painted, [Library + "wood"] = Wood,
        [Library + "silver"] = new Finish(.45f, .6f),

        // ---- キャラ：少しマットに（ふわっとした粘土・ソフビの間くらい）
        ["Assets/_Materials/BobBody"] = new Finish(.24f),
        // 光る物・ガラス・画面・金・glow_water（未コミットの変更があるので触らない）は表に入れない
    };

    [MenuItem("Roomies/Art/Restyle/Material Finish/1 Matte Surfaces")]
    internal static void ApplyMatte()
    {
        int changed = 0;
        foreach (var pair in Table)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(pair.Key + ".mat");
            if (mat == null) { Debug.LogWarning("[Material Finish] not found: " + pair.Key); continue; }
            var f = pair.Value;
            mat.SetFloat("_Smoothness", f.Smooth);
            mat.SetFloat("_Metallic", f.Metal);
            mat.SetFloat("_SpecularHighlights", f.Highlights ? 1 : 0);
            mat.SetFloat("_EnvironmentReflections", f.Reflections ? 1 : 0);
            SetKeyword(mat, "_SPECULARHIGHLIGHTS_OFF", !f.Highlights);
            SetKeyword(mat, "_ENVIRONMENTREFLECTIONS_OFF", !f.Reflections);
            EditorUtility.SetDirty(mat);
            changed++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"[Material Finish] matte: {changed} materials");
    }

    static void SetKeyword(Material mat, string keyword, bool on)
    {
        if (on) mat.EnableKeyword(keyword); else mat.DisableKeyword(keyword);
    }
}
#endif
