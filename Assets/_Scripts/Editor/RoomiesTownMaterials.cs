#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 町の作り直し用の素材（2026/11/13）。素材の型の子（Variants/型/Town/town_〇〇.mat）として作る。
/// 窓ガラスは店内・部屋の中を描いた手続きテクスチャを、発光（Emissive）の子に貼る（夜に明かりの点いた窓に見える）。
/// </summary>
internal static partial class RoomiesMaterialFinishBuilder
{
    static readonly Dictionary<string, Material> townMats = new Dictionary<string, Material>();

    internal static void ResetTownMaterials() => townMats.Clear();

    /// <summary>町の作り直しで使う素材（名前で引く）。</summary>
    internal static Material TownMat(string key)
    {
        if (townMats.TryGetValue(key, out var m) && m != null) return m;
        m = BuildTownMat(key);
        townMats[key] = m;
        return m;
    }

    static Material BuildTownMat(string key)
    {
        Color C(float r, float g, float b) => new Color(r, g, b);
        Material V(Tpl t, Color c, Action<Material> extra = null) => Variant(t, "town_" + key, c, extra, "Town");
        switch (key)
        {
            case "wall_corner": return V(Tpl.Brick, C(.68f, .38f, .3f));
            case "wall_works": return V(Tpl.Plaster, C(.34f, .6f, .58f));
            case "wall_express": return V(Tpl.Concrete, C(.74f, .74f, .72f));
            case "wall_house": return V(Tpl.Plaster, C(.9f, .86f, .77f));
            case "plinth": return V(Tpl.Concrete, C(.44f, .44f, .45f));
            case "sill": return V(Tpl.Concrete, C(.87f, .86f, .82f));
            case "frame_corner": return V(Tpl.PaintedMetal, C(.42f, .12f, .1f));
            case "frame_works": return V(Tpl.PaintedMetal, C(.93f, .93f, .9f));
            case "frame_express": return V(Tpl.PaintedMetal, C(.92f, .7f, .18f));
            case "frame_house": return V(Tpl.PaintedMetal, C(.12f, .46f, .47f));
            case "shutter": return V(Tpl.Metal, C(.66f, .69f, .72f), v => { v.SetFloat("_Metallic", .45f); v.SetFloat("_Smoothness", .32f); });
            case "handle": return V(Tpl.Metal, C(.78f, .78f, .76f));
            // 家（段階2）
            case "wall_h1": return V(Tpl.Plaster, C(.92f, .87f, .76f));
            case "wall_h2": return V(Tpl.Plaster, C(.63f, .72f, .6f));
            case "wall_h3": return V(Tpl.Plaster, C(.66f, .77f, .84f));
            case "wall_h4": return V(Tpl.Plaster, C(.93f, .72f, .6f));
            case "wall_h5": return V(Tpl.Plaster, C(.91f, .78f, .46f));
            case "wall_b1": return V(Tpl.Brick, C(.6f, .33f, .26f));
            case "wall_b2": return V(Tpl.Brick, C(.78f, .64f, .5f));
            case "roof_slate": return V(Tpl.Roof, C(.27f, .3f, .35f));
            case "roof_terra": return V(Tpl.Roof, C(.68f, .35f, .25f));
            case "roof_teal": return V(Tpl.Roof, C(.2f, .43f, .45f));
            case "roof_moss": return V(Tpl.Roof, C(.36f, .42f, .32f));
            case "roof_flat": return V(Tpl.Concrete, C(.5f, .5f, .5f));
            case "frame_h_white": return V(Tpl.PaintedMetal, C(.93f, .92f, .88f));
            case "frame_h_navy": return V(Tpl.PaintedMetal, C(.18f, .25f, .4f));
            case "frame_h_green": return V(Tpl.PaintedMetal, C(.2f, .38f, .28f));
            case "metal_dark": return V(Tpl.PaintedMetal, C(.26f, .28f, .3f));
            case "ac_unit": return V(Tpl.PaintedMetal, C(.84f, .84f, .81f));
            case "planter_wood": return V(Tpl.Wood, C(.5f, .34f, .22f));
            case "planter_leaf": return V(Tpl.Foliage, C(.3f, .5f, .28f));
            case "paving": return V(Tpl.Concrete, C(.72f, .7f, .66f));
            case "door_wood": return V(Tpl.Wood, C(.45f, .3f, .2f));
            // 通りと夜（段階3）
            case "hydrant": return V(Tpl.PaintedMetal, C(.74f, .17f, .12f));
            case "bulb": return V(Tpl.Emissive, C(1f, .92f, .78f), v => v.SetColor("_EmissionColor", new Color(1f, .8f, .55f) * 4f));
            case "block_windows_a": return WindowGlass("town_block_windows_a", (float u, float v, out Color c, out float h) => BlockWindowPixel(u, v, out c, out h, 3), C(1f, .85f, .62f), .7f);
            case "block_windows_b": return WindowGlass("town_block_windows_b", (float u, float v, out Color c, out float h) => BlockWindowPixel(u, v, out c, out h, 11), C(1f, .85f, .62f), .7f);
            // 牢屋（作り直し2）
            case "jail_block": return V(Tpl.Brick, C(.62f, .62f, .63f));
            case "jail_paint_low": return V(Tpl.Plaster, C(.3f, .5f, .48f));
            case "jail_paint_high": return V(Tpl.Plaster, C(.86f, .86f, .81f));
            case "jail_floor": return V(Tpl.Concrete, C(.56f, .56f, .55f));
            case "jail_bars": return V(Tpl.Metal, C(.5f, .52f, .55f), v => { v.SetFloat("_Metallic", .7f); v.SetFloat("_Smoothness", .45f); });
            case "jail_mattress": return V(Tpl.Fabric, C(.52f, .58f, .66f));
            case "jail_blanket": return V(Tpl.Fabric, C(.86f, .5f, .28f));
            case "jail_pillow": return V(Tpl.Fabric, C(.9f, .9f, .86f));
            case "jail_mirror": return V(Tpl.Metal, C(.8f, .82f, .85f), v => { v.SetFloat("_Metallic", .9f); v.SetFloat("_Smoothness", .8f); });
            case "jail_frosted": return V(Tpl.Emissive, C(.8f, .88f, .95f), v => v.SetColor("_EmissionColor", new Color(.6f, .7f, .8f) * .6f));
            // メインの家（作り直し2）
            case "house_brick": return V(Tpl.Brick, C(.7f, .45f, .36f));
            case "house_siding": return V(Tpl.Wood, C(.93f, .9f, .82f));
            case "stair_concrete": return V(Tpl.Concrete, C(.74f, .72f, .68f));
            case "stair_tread": return V(Tpl.Concrete, C(.62f, .6f, .57f));
            case "sign_wood": return V(Tpl.Wood, C(.2f, .45f, .46f));
            case "flower_pink": return V(Tpl.Fabric, C(.95f, .55f, .7f));
            case "flower_yellow": return V(Tpl.Fabric, C(.98f, .85f, .35f));
            // カジノ（作り直し2）
            case "casino_gold": return V(Tpl.Metal, C(.9f, .7f, .32f), v => { v.SetFloat("_Metallic", .85f); v.SetFloat("_Smoothness", .55f); });
            case "casino_wood": return V(Tpl.Wood, C(.36f, .2f, .14f));
            case "casino_wall": return V(Tpl.Fabric, C(.45f, .1f, .16f));
            case "casino_black": return V(Tpl.PaintedMetal, C(.08f, .08f, .1f));
            case "casino_ceiling": return V(Tpl.Plaster, C(.18f, .12f, .2f));
            case "casino_red": return V(Tpl.Fabric, C(.66f, .08f, .12f));
            case "casino_felt": return V(Tpl.Fabric, C(.1f, .45f, .28f));
            case "casino_felt_line": return V(Tpl.Fabric, C(.92f, .88f, .7f));
            case "casino_leather": return V(Tpl.Fabric, C(.22f, .1f, .08f));
            case "casino_green_shade": return V(Tpl.PaintedMetal, C(.1f, .4f, .25f));
            case "casino_card": return V(Tpl.Paper, C(.95f, .94f, .9f));
            case "chip_red": return V(Tpl.Plastic, C(.85f, .14f, .14f));
            case "chip_blue": return V(Tpl.Plastic, C(.15f, .3f, .85f));
            case "chip_green": return V(Tpl.Plastic, C(.15f, .65f, .3f));
            case "chip_black": return V(Tpl.Plastic, C(.08f, .08f, .08f));
            case "slot_red": return V(Tpl.PaintedMetal, C(.75f, .12f, .18f));
            case "slot_purple": return V(Tpl.PaintedMetal, C(.42f, .18f, .62f));
            case "slot_teal": return V(Tpl.PaintedMetal, C(.12f, .52f, .55f));
            case "slot_chrome": return V(Tpl.Metal, C(.85f, .86f, .88f), v => { v.SetFloat("_Metallic", .9f); v.SetFloat("_Smoothness", .7f); });
            // カジノの建物（作り直し3）
            case "casino_facade": return V(Tpl.Plaster, C(.48f, .11f, .16f));
            case "casino_cream": return V(Tpl.Plaster, C(.93f, .87f, .74f));
            case "casino_marble": return V(Tpl.Ceramic, C(.92f, .9f, .86f), v => v.SetFloat("_Smoothness", .6f));
            case "crystal": return V(Tpl.Emissive, C(.95f, .97f, 1f), v => v.SetColor("_EmissionColor", new Color(1f, .95f, .85f) * 1.6f));
            case "poster_cards": return V(Tpl.Emissive, C(.95f, .2f, .3f), v => v.SetColor("_EmissionColor", new Color(1f, .25f, .35f) * 1.4f));
            case "poster_dice": return V(Tpl.Emissive, C(.3f, .5f, 1f), v => v.SetColor("_EmissionColor", new Color(.3f, .55f, 1f) * 1.4f));
            case "poster_coins": return V(Tpl.Emissive, C(1f, .8f, .25f), v => v.SetColor("_EmissionColor", new Color(1f, .75f, .2f) * 1.4f));
            case "bottle_amber": return V(Tpl.Emissive, C(.85f, .5f, .15f), v => v.SetColor("_EmissionColor", new Color(.9f, .5f, .15f) * .5f));
            case "bottle_green": return V(Tpl.Emissive, C(.2f, .6f, .3f), v => v.SetColor("_EmissionColor", new Color(.2f, .6f, .3f) * .5f));
            case "bottle_clear": return V(Tpl.Emissive, C(.85f, .9f, .95f), v => v.SetColor("_EmissionColor", new Color(.8f, .9f, 1f) * .5f));
            case "bottle_red": return V(Tpl.Emissive, C(.7f, .1f, .15f), v => v.SetColor("_EmissionColor", new Color(.7f, .1f, .15f) * .5f));
            // カジノ（3 階建て・木とレンガの見た目。2026/11/23）
            case "casino_brick": return V(Tpl.Brick, C(.60f, .30f, .22f));
            case "casino_timber": return V(Tpl.Wood, C(.30f, .20f, .17f));
            case "casino_wainscot": return V(Tpl.Wood, C(.47f, .31f, .23f));
            case "casino_plaster": return V(Tpl.Plaster, C(.72f, .66f, .54f));
            case "casino_ceiling_wood": return V(Tpl.Wood, C(.25f, .17f, .15f));
            case "casino_purple": return V(Tpl.Fabric, C(.33f, .21f, .50f));
            case "casino_window_glow": return V(Tpl.Emissive, C(1f, .74f, .46f), v => v.SetColor("_EmissionColor", new Color(1f, .58f, .28f) * .8f));
            // 描いたタイル（Textures/Tiles）：板の床、レンガの床、トランプの印の敷物（敷物は絵の色のまま）
            case "casino_plank": { var target = C(.47f, .31f, .23f); return V(Tpl.Wood, target, v => ApplyTile(v, ("floor_wood", 1.4f, .8f, .13f), target)); }
            case "casino_brick_floor": { var target = C(.56f, .34f, .25f); return V(Tpl.Brick, target, v => ApplyTile(v, ("floor_brick", 1.8f, .8f, .1f), target)); }
            case "casino_carpet": return V(Tpl.Fabric, Color.white, v => { ApplyTile(v, ("carpet_suits", 1.6f, .5f, -1f), Color.white); v.SetColor("_BaseColor", Color.white); });
            // 留置所（作り直し3）
            case "police_brick": return V(Tpl.Brick, C(.62f, .3f, .24f));
            case "police_stone": return V(Tpl.Concrete, C(.88f, .84f, .74f));
            case "police_lamp": return V(Tpl.Emissive, C(.3f, .5f, 1f), v => v.SetColor("_EmissionColor", new Color(.25f, .45f, 1f) * 2.5f));
            case "flag_blue": return V(Tpl.Fabric, C(.15f, .3f, .7f));
            case "flag_white": return V(Tpl.Fabric, C(.95f, .95f, .92f));
            // メインの家（作り直し3）
            case "house_stucco": return V(Tpl.Plaster, C(.72f, .86f, .8f));
            case "house_shutter": return V(Tpl.PaintedMetal, C(.93f, .55f, .5f));
            case "house_stone": return V(Tpl.Concrete, C(.7f, .66f, .6f));
            case "house_batten": return V(Tpl.Wood, C(.96f, .9f, .78f));
            case "deck_wood": return V(Tpl.Wood, C(.62f, .44f, .3f));
            case "awning_a": return V(Tpl.Fabric, C(.95f, .5f, .45f));
            case "awning_b": return V(Tpl.Fabric, C(.97f, .95f, .9f));
            case "slot_screen": return V(Tpl.Glass, C(.04f, .05f, .08f), v => v.SetFloat("_Smoothness", .85f));
            case "neon_pink": return V(Tpl.Emissive, C(1f, .45f, .75f), v => v.SetColor("_EmissionColor", new Color(1f, .3f, .7f) * 3f));
            case "neon_cyan": return V(Tpl.Emissive, C(.5f, .95f, 1f), v => v.SetColor("_EmissionColor", new Color(.3f, .9f, 1f) * 3f));
            // 道路（作り直し2）
            case "road_gutter": return V(Tpl.Concrete, C(.55f, .55f, .53f));
            case "road_paint_white": return V(Tpl.Plaster, C(.92f, .92f, .88f));
            case "road_paint_yellow": return V(Tpl.Plaster, C(.95f, .74f, .2f));
            case "curb_granite": return V(Tpl.Concrete, C(.66f, .66f, .67f));
            case "tactile_yellow": return V(Tpl.Rubber, C(.95f, .78f, .15f));
            case "glass_shop": return WindowGlass("town_shop_interior", ShopInteriorPixel, C(1f, .86f, .66f), 1.1f);
            case "glass_home_lit": return WindowGlass("town_home_window", HomeWindowPixel, C(1f, .8f, .55f), .8f);
            case "glass_home_dark": return WindowGlass("town_home_window", HomeWindowPixel, C(.55f, .65f, 1f), .05f);
        }
        throw new ArgumentException("Unknown town material: " + key);
    }

    static Material WindowGlass(string tex, Pixel pixel, Color glow, float power)
    {
        var map = Make(tex, 512, pixel, out _, 0);
        return Variant(Tpl.Emissive, tex + (tex.StartsWith("town_block") ? "" : power > .3f ? "_lit" : "_dark"), Color.white, v =>
        {
            v.SetTexture("_BaseMap", map); v.SetTexture("_EmissionMap", map);
            v.SetColor("_EmissionColor", glow * power);
            v.SetFloat("_Smoothness", .82f);
        }, "Town");
    }

    // 店内：奥の壁・棚と商品・天井の明かり・床。ガラスの映り込みの筋
    static void ShopInteriorPixel(float u, float v, out Color c, out float h)
    {
        h = 0;
        var wall = new Color(.62f, .56f, .45f);
        c = wall * (.85f + .15f * N(u, v, 6, 3));
        if (v > .9f) c = Color.Lerp(new Color(.9f, .88f, .8f), new Color(1f, .98f, .9f), S(.9f, .97f, v));   // 天井の明かり
        else if (v < .12f) c = new Color(.32f, .28f, .24f) * (.8f + .2f * N(u, v, 9, 5));                      // 床
        else
        {
            foreach (float shelf in new[] { .2f, .42f, .64f })
            {
                if (v > shelf - .012f && v < shelf + .006f) { c = new Color(.25f, .18f, .13f); break; }       // 棚板
                if (v > shelf && v < shelf + .14f)
                {
                    float col = Mathf.Floor(u * 26), fu = u * 26 - col;
                    float top = .06f + .08f * Hash(col, shelf * 10);
                    if (fu > .12f && fu < .88f && v < shelf + top)
                    {
                        float k = Hash(col, shelf * 7 + 1);
                        Color[] pal = { new Color(.9f, .3f, .25f), new Color(.95f, .75f, .2f), new Color(.3f, .6f, .9f), new Color(.4f, .75f, .4f), new Color(.92f, .9f, .85f), new Color(.85f, .45f, .7f) };
                        c = pal[Mathf.Min(pal.Length - 1, (int)(k * pal.Length))] * (.8f + .2f * (1 - (v - shelf) / top));
                    }
                    break;
                }
            }
            if (Mathf.Abs(u - .34f) < .012f || Mathf.Abs(u - .67f) < .012f) c *= .6f;                            // 棚の端
        }
        float edge = Mathf.Min(Mathf.Min(u, 1 - u), Mathf.Min(v, 1 - v));
        c *= .75f + .25f * S(0, .18f, edge);
        float streak = Mathf.Exp(-Mathf.Pow((u + v * .6f - .55f) / .05f, 2)) * .22f + Mathf.Exp(-Mathf.Pow((u + v * .6f - .72f) / .025f, 2)) * .12f;
        c = new Color(Mathf.Min(1, c.r + streak), Mathf.Min(1, c.g + streak), Mathf.Min(1, c.b + streak * 1.1f), 1);
    }

    // 部屋の窓：左右のカーテンと上のバランス、奥にぼんやりした部屋と明かり、映り込みの筋
    static void HomeWindowPixel(float u, float v, out Color c, out float h)
    {
        h = 0;
        float lamp = Mathf.Exp(-((u - .5f) * (u - .5f) * 9 + (v - .7f) * (v - .7f) * 12));
        c = new Color(.28f, .24f, .21f) * (.7f + .3f * N(u, v, 5, 2)) + new Color(.5f, .42f, .3f) * lamp;
        if (v < .3f) c *= .7f;
        float curtain = Mathf.Max(S(.3f, .22f, u), S(.7f, .78f, u));
        if (curtain > 0)
        {
            float fold = .8f + .2f * Mathf.Sin(u * 90f);
            c = Color.Lerp(c, new Color(.86f, .78f, .64f) * fold, curtain);
        }
        if (v > .88f) c = new Color(.8f, .72f, .58f) * (.85f + .15f * Mathf.Sin(u * 60f));
        float streak = Mathf.Exp(-Mathf.Pow((u + v * .5f - .62f) / .05f, 2)) * .18f;
        c = new Color(Mathf.Min(1, c.r + streak), Mathf.Min(1, c.g + streak), Mathf.Min(1, c.b + streak * 1.15f), 1);
    }

    // ビルの窓の帯：横に10枚。明かりの点いた部屋（暖色・ブラインド）と、空を映す暗いガラスを混ぜる
    static void BlockWindowPixel(float u, float v, out Color c, out float h, int seed)
    {
        h = 0;
        float gu = u * 10; float col = Mathf.Floor(gu); float fu = gu - col;
        bool frame = fu < .08f || fu > .92f || v < .1f || v > .9f;
        if (frame) { c = new Color(.78f, .78f, .76f); return; }
        bool lit = Hash(col, seed) > .55f;
        if (lit)
        {
            float blind = .85f + .15f * Mathf.Sin(v * 120f);
            c = Color.Lerp(new Color(1f, .82f, .55f), new Color(.85f, .6f, .38f), S(.2f, .9f, v)) * blind;
        }
        else c = Color.Lerp(new Color(.1f, .14f, .22f), new Color(.32f, .42f, .55f), S(.1f, .9f, v)) * (.9f + .1f * Hash(col, seed + 5));
        if (Mathf.Abs(fu - .5f) < .025f) c *= .55f;   // 真ん中の桟
    }

    // レンガ：互い違いの積み方、目地は少し明るく凹む
    static void BrickPixel(float u, float v, out Color c, out float h)
    {
        float gv = v * 16; float row = Mathf.Floor(gv); float fv = gv - row;
        float gu = u * 5 + (row % 2) * .5f; float col = Mathf.Floor(gu); float fu = gu - col;
        float mortar = Mathf.Max(1 - S(0, .12f, fv), Mathf.Max(1 - S(0, .035f, fu), 1 - S(0, .035f, 1 - fu)));
        float tone = .74f + .24f * Hash(col + 31, row) + .06f * N(u, v, 20, 7) + .04f * Mathf.PerlinNoise(u * 300, v * 300);
        tone *= 1 - .1f * S(.6f, .85f, N(u, v, 3, 17));
        float t = Mathf.Lerp(tone, 1.05f, mortar);
        c = new Color(t, t, t, 1);
        h = (1 - mortar) * .8f + .05f * Mathf.PerlinNoise(u * 200, v * 200);
    }
}
#endif
