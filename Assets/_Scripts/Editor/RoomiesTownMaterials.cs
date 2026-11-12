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
            case "glass_shop": return WindowGlass("town_shop_interior", ShopInteriorPixel, C(1f, .86f, .66f), 1.1f);
            case "glass_home_lit": return WindowGlass("town_home_window", HomeWindowPixel, C(1f, .8f, .55f), .8f);
            case "glass_home_dark": return WindowGlass("town_home_window", HomeWindowPixel, C(.55f, .65f, 1f), .05f);
        }
        throw new ArgumentException("Unknown town material: " + key);
    }

    static Material WindowGlass(string tex, Pixel pixel, Color glow, float power)
    {
        var map = Make(tex, 512, pixel, out _, 0);
        return Variant(Tpl.Emissive, tex + (power > .3f ? "_lit" : "_dark"), Color.white, v =>
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
