#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// カジノの小物の形（吊り照明・スツール・椅子・ソファ・机・ロープの柵・垂れ幕・電球の列・ネオン看板など）。
/// どれも 1 つのメッシュで、原点は足元（吊る物は天井に付く点）、正面は +Z。
/// </summary>
internal static partial class RoomiesCasinoArt
{
    // ================================================================ 吊り照明（原点は天井。drop は傘の下の端までの長さ）
    internal static Mesh PendantLamp(float drop) => Build("pendant_" + Mathf.RoundToInt(drop * 100), lp =>
    {
        lp.Lathe(new[] { (.07f, -.04f), (.07f, 0f) }, 6, "wood_dark", 0, false, true);
        lp.Box(new Vector3(-.012f, -drop + .42f, -.012f), new Vector3(.012f, -.04f, .012f), "ink");
        // 傘（外は橙、内側と電球が光る）
        lp.Lathe(new[] { (.36f, -drop), (.33f, -drop + .12f), (.16f, -drop + .34f), (.09f, -drop + .42f) }, 8, b => b == 0 ? "lamp_shade_dark" : "lamp_shade", Mathf.PI / 8, true);
        // 傘の内側の面は広いので、電球より暗い色で光らせる（"warm" のままだと、下から見上げたとき真っ白に飛ぶ）
        lp.Cap(.345f, -drop + .01f, 8, "lamp_under", false, Mathf.PI / 8, true);
        lp.Lathe(new[] { (0f, -drop - .05f), (.09f, -drop + .02f), (.09f, -drop + .1f) }, 6, "warm", 0, false, false, true);
    });

    // ================================================================ 腰かけ（丸い座面はふっくら、脚は上下で太さを変える）
    internal static Mesh Stool(string seat) => Build("stool_" + seat, lp =>
    {
        lp.Lathe(new[] { (.2f, 0f), (.19f, .025f), (.07f, .05f), (.035f, .12f), (.03f, .5f), (.06f, .57f), (.12f, .6f) }, 8, b => b < 2 ? "gold_dark" : "metal", Mathf.PI / 8, false, true);
        lp.Lathe(new[] { (.17f, .6f), (.215f, .625f), (.22f, .665f), (.19f, .7f), (.1f, .715f), (0f, .72f) }, 8, seat, Mathf.PI / 8, false, true);
        lp.Lathe(new[] { (.15f, .24f), (.162f, .255f), (.15f, .27f) }, 8, "gold_dark", Mathf.PI / 8, false, false);
    });

    /// <summary>
    /// ひじ掛けのない、背もたれつきの椅子（台のまわり）。正面（座る人の向く方）が +Z。
    /// 脚は下へ向けて細く、座面はふっくら、背もたれは上へ向けて少しすぼまり、後ろへ反る。
    /// </summary>
    internal static Mesh Chair(string cloth, string clothDark) => Build("chair_" + cloth, lp =>
    {
        foreach (var (x, z) in new[] { (-.21f, -.2f), (.21f, -.2f), (-.21f, .2f), (.21f, .2f) })
        {
            lp.Push(new Vector3(x, 0, z));
            lp.Lathe(new[] { (.02f, 0f), (.024f, .02f), (.03f, .3f), (.036f, .42f) }, 6, b => b == 0 ? "gold_dark" : "wood_dark", 0, false, true);
            lp.Pop();
        }
        lp.Prism(Chamfer(.54f, .52f, .1f), .4f, .44f, "wood_dark", null, true);                     // 座面の下の枠
        lp.Cushion(Chamfer(.56f, .54f, .14f), .44f, .58f, cloth);                                    // 座面
        lp.Push(new Vector3(0, .5f, -.25f), new Vector3(-10, 0, 0));
        lp.Loft(Chamfer(.5f, .09f, .04f), new[] { (0f, .86f), (.14f, 1f), (.42f, 1.04f), (.56f, .98f), (.64f, .8f) }, clothDark, cloth, true, .03f);   // 背もたれ
        lp.Pop();
    });

    /// <summary>ソファ（幅を決められる）。背が -Z、座る人の向く方が +Z。座面と背はふっくら、ひじ掛けは上が丸く太い。</summary>
    internal static Mesh Sofa(float width, string cloth, string clothDark) => Build($"sofa_{Mathf.RoundToInt(width * 100)}_{cloth}", lp =>
    {
        float hw = width / 2;
        lp.Loft(Chamfer(width, .9f, .12f), new[] { (.08f, .96f), (.14f, 1f), (.34f, 1f) }, clothDark, null, true);
        foreach (float x in new[] { -hw + .12f, hw - .12f })
            foreach (float z in new[] { -.34f, .34f })
            {
                lp.Push(new Vector3(x, 0, z));
                lp.Lathe(new[] { (.03f, 0f), (.045f, .08f) }, 6, "wood_dark", 0, false, true);
                lp.Pop();
            }
        // 背もたれ（上へ向けて丸く）・ひじ掛け（上が太い）・座面のクッション
        lp.Loft(Offset(Chamfer(width, .26f, .1f), new Vector2(0, -.32f)), new[] { (.34f, 1f), (.7f, 1f), (.86f, .97f), (.94f, .86f) }, cloth, null, false, .03f);
        foreach (float s in new[] { -1f, 1f })
            lp.Loft(Offset(Chamfer(.22f, .78f, .09f), new Vector2(s * (hw - .11f), .04f)), new[] { (.34f, .92f), (.5f, .96f), (.6f, 1.08f), (.67f, 1.02f), (.7f, .8f) }, cloth, null, false, .015f);
        int cushions = Mathf.Max(1, Mathf.RoundToInt((width - .44f) / .75f));
        float cw = (width - .44f) / cushions;
        for (int i = 0; i < cushions; i++)
            lp.Cushion(Offset(Chamfer(cw - .03f, .62f, .1f), new Vector2(-hw + .22f + cw * (i + .5f), .1f)), .34f, .5f, cloth);
    });

    internal static Mesh RoundTable(float radius, float height) => Build($"round_table_{Mathf.RoundToInt(radius * 100)}_{Mathf.RoundToInt(height * 100)}", lp =>
    {
        // 脚は、足元で広がり、真ん中で細く、天板の下でまた広がる
        lp.Lathe(new[] { (radius * .58f, 0f), (radius * .55f, .03f), (radius * .2f, .07f), (.06f, .16f), (.045f, height * .5f), (.06f, height - .14f), (radius * .32f, height - .05f) }, 8, "wood_dark", Mathf.PI / 8, false, true);
        lp.Prism(Ellipse(radius, radius, 10, Mathf.PI / 10), height - .05f, height, "wood", "wood_light", true);
    });

    internal static Mesh LowTable(float width, float depth) => Build($"low_table_{Mathf.RoundToInt(width * 100)}_{Mathf.RoundToInt(depth * 100)}", lp =>
    {
        float hw = width / 2 - .09f, hd = depth / 2 - .09f;
        foreach (var (x, z) in new[] { (-hw, -hd), (hw, -hd), (-hw, hd), (hw, hd) })
        {
            lp.Push(new Vector3(x, 0, z));
            lp.Lathe(new[] { (.025f, 0f), (.03f, .03f), (.045f, .36f) }, 6, "wood_dark", 0, false, true);
            lp.Pop();
        }
        lp.Prism(Chamfer(width, depth, .09f), .36f, .43f, "wood", "wood_light", true);
    });

    // ================================================================ ロープの柵（柱 1 本）とロープ
    internal static Mesh Stanchion() => Build("stanchion", lp =>
    {
        lp.Lathe(new[] { (.15f, 0f), (.13f, .03f), (.035f, .06f), (.03f, .9f), (.05f, .92f), (.055f, .97f), (0f, 1.02f) }, 6, "gold", 0, false, true);
    });

    internal static Mesh Rope(float length) => Build("rope_" + Mathf.RoundToInt(length * 100), lp =>
    {
        // 少したるんだロープ（原点は片方の端。+X へ伸びる）
        const int seg = 4;
        for (int i = 0; i < seg; i++)
        {
            float t0 = (float)i / seg, t1 = (float)(i + 1) / seg;
            Vector3 a = new Vector3(t0 * length, -.18f * Mathf.Sin(t0 * Mathf.PI), 0), b = new Vector3(t1 * length, -.18f * Mathf.Sin(t1 * Mathf.PI), 0);
            lp.Bar(a, b, .045f, "maroon");
        }
    });

    // ================================================================ 垂れ幕（紫に菱形。上の棒つき。原点は上の棒の真ん中、幕は下へ）
    internal static Mesh Banner(float width) => Build("banner_" + Mathf.RoundToInt(width * 100), lp =>
    {
        float h = width * 1024f / 388f, hw = width / 2;
        Rect r = ImageRect(Img.Banner);
        lp.Box(new Vector3(-hw - .06f, -.03f, -.03f), new Vector3(hw + .06f, .03f, .03f), "gold_dark");
        foreach (float z in new[] { .012f, -.012f })
        {
            var n = z > 0 ? Vector3.forward : Vector3.back;
            float s = z > 0 ? 1 : -1;   // 裏は左右を返して、絵が裏返らないようにする
            lp.Image(new Vector3(s * hw, -h, z), new Vector3(-s * hw, -h, z), new Vector3(-s * hw, 0, z), new Vector3(s * hw, 0, z), r, n);
        }
    });

    // ================================================================ 電球の列（たるんだコードに、暖色とピンクの電球が交互）。原点は片方の端、+X へ伸びる
    internal static Mesh BulbString(float length, int bulbs) => Build($"bulbs_{Mathf.RoundToInt(length * 100)}_{bulbs}", lp =>
    {
        float sag = Mathf.Min(.35f, length * .07f);
        Vector3 At(float t) => new Vector3(t * length, -sag * Mathf.Sin(t * Mathf.PI), 0);
        const int seg = 6;
        for (int i = 0; i < seg; i++) lp.Bar(At((float)i / seg), At((float)(i + 1) / seg), .018f, "ink");
        for (int i = 0; i < bulbs; i++)
        {
            Vector3 p = At((i + .5f) / bulbs);
            lp.Box(p + new Vector3(-.016f, -.05f, -.016f), p + new Vector3(.016f, 0, .016f), "ink");
            lp.Push(p + new Vector3(0, -.05f, 0));
            lp.Lathe(new[] { (.02f, 0f), (.045f, -.05f), (.03f, -.1f), (0f, -.115f) }, 5, i % 2 == 0 ? "warm" : "pink", 0, false, false, true);
            lp.Pop();
        }
    });

    // ================================================================ 小さな飾り
    /// <summary>チップの山いくつか（台の上の飾り）。</summary>
    internal static Mesh ChipPile() => Build("chip_pile", lp =>
    {
        foreach (var (x, z, n, c) in new[] { (0f, 0f, 6, "chip_red"), (.1f, .03f, 4, "chip_blue"), (.04f, -.09f, 8, "chip_green"), (-.08f, .06f, 3, "chip_black") })
            lp.Cyl(new Vector3(x, 0, z), .04f, n * .012f, 8, c, false, "chip_white");
    });

    /// <summary>グラス（足つき）と、ポップコーンの入れ物。</summary>
    internal static Mesh Glass() => Build("glass", lp =>
        lp.Lathe(new[] { (.035f, 0f), (.03f, .01f), (.008f, .02f), (.008f, .08f), (.04f, .12f), (.045f, .17f) }, 6, b => b >= 3 ? "orange" : "white", 0, true, true));

    internal static Mesh Popcorn() => Build("popcorn", lp =>
    {
        lp.Lathe(new[] { (.07f, 0f), (.1f, .2f) }, 8, "red", Mathf.PI / 8, false, true);
        lp.Lathe(new[] { (.1f, .2f), (.105f, .22f) }, 8, "white", Mathf.PI / 8, false);
        lp.Lathe(new[] { (.1f, .2f), (.09f, .26f), (.04f, .3f), (0f, .31f) }, 6, "popcorn", 0, false);
    });

    /// <summary>壁の小さな額（絵は色の面だけ）。原点は額の真ん中、表が +Z。</summary>
    internal static Mesh Frame(float w, float h, string picture) => Build($"frame_{Mathf.RoundToInt(w * 100)}_{Mathf.RoundToInt(h * 100)}_{picture}", lp =>
    {
        lp.Box(new Vector3(-w / 2, -h / 2, 0), new Vector3(w / 2, h / 2, .03f), "wood_dark");
        lp.Box(new Vector3(-w / 2 + .04f, -h / 2 + .04f, .03f), new Vector3(w / 2 - .04f, h / 2 - .04f, .036f), picture);
    });

    /// <summary>壁の小さな灯（箱形のランタン）。原点は壁に付く面の真ん中、表が +Z。</summary>
    internal static Mesh Lantern() => Build("lantern", lp =>
    {
        lp.Box(new Vector3(-.03f, -.02f, 0), new Vector3(.03f, .02f, .14f), "ink");
        lp.Box(new Vector3(-.09f, -.2f, .08f), new Vector3(.09f, -.16f, .26f), "ink");
        lp.Box(new Vector3(-.09f, .12f, .08f), new Vector3(.09f, .16f, .26f), "ink");
        lp.Box(new Vector3(-.07f, -.16f, .1f), new Vector3(.07f, .12f, .24f), "orange", true);
    });

    /// <summary>植木鉢（鉢だけ。葉は部屋の小物の観葉植物を載せる）と、葉の代わりの茂み。</summary>
    internal static Mesh Planter() => Build("planter", lp =>
    {
        lp.Lathe(new[] { (.2f, 0f), (.27f, .34f), (.29f, .34f), (.29f, .4f) }, 8, b => b == 2 ? "terracotta" : "terracotta", Mathf.PI / 8, false, true);
        lp.Cap(.26f, .37f, 8, "soil", true, Mathf.PI / 8);
        // 葉：大きな葉を 7 枚、外へ広がる向きに
        for (int i = 0; i < 7; i++)
        {
            float yaw = i * 360f / 7f + (i % 2) * 14f, tilt = 22f + (i % 3) * 14f, len = .62f + (i % 3) * .14f;
            lp.Push(new Vector3(0, .37f, 0), new Vector3(0, yaw, 0));
            lp.Push(Vector3.zero, new Vector3(tilt, 0, 0));
            string c = i % 2 == 0 ? "leaf" : "leaf_dark";
            Vector3 b0 = new Vector3(0, 0, 0), mid = new Vector3(0, len * .55f, 0), tip = new Vector3(0, len, .12f);
            Vector3 l = new Vector3(-.14f, len * .5f, .05f), r = new Vector3(.14f, len * .5f, .05f);
            Vector2 u = Sw(c);
            // 真ん中で折れた葉（表と裏）
            foreach (var n in new[] { Vector3.forward, Vector3.back })
            {
                lp.Tri(b0, l, mid, u, u, u, n); lp.Tri(l, tip, mid, u, u, u, n);
                lp.Tri(b0, mid, r, u, u, u, n); lp.Tri(mid, tip, r, u, u, u, n);
            }
            lp.Pop(); lp.Pop();
        }
    });

    /// <summary>酒瓶（棚に並べる）。色は胴の色。</summary>
    internal static Mesh Bottle(string glass) => Build("bottle_" + glass, lp =>
    {
        lp.Lathe(new[] { (.034f, 0f), (.038f, .01f), (.038f, .16f), (.016f, .22f), (.014f, .28f), (.018f, .285f), (.018f, .3f) }, 6, b => b >= 4 ? "gold" : glass, 0, true, true);
        lp.Lathe(new[] { (.0385f, .05f), (.0385f, .12f) }, 6, "paper", 0, false);
    });

    // ================================================================ ネオン看板（線の絵を切り抜いて光らせる板）
    internal static GameObject NeonSign(Transform parent, string name, Vector3 localPosition, float yaw, float size)
    {
        Begin();
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0, yaw + 180, 0);   // 板の表（-Z）を、置く向きの +Z へ
        go.transform.localScale = new Vector3(size, size, 1);
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = neonMat;
        r.shadowCastingMode = ShadowCastingMode.Off;
        go.isStatic = true;
        return go;
    }

    // ================================================================ 光る文字（ネオンの文字）
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset";

    /// <summary>光る文字。親の +Z 側から読める向きで置く。height は文字の高さ（m）のめやす。</summary>
    internal static TextMeshPro NeonText(Transform parent, string name, string text, Vector3 localPosition, float yaw, float height, string hex = "FF4FB4", float power = 3.2f)
    {
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
        string path = $"{MatDir}/casino_text_{hex}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(font.material); AssetDatabase.CreateAsset(mat, path); }
        mat.shader = font.material.shader;
        mat.CopyPropertiesFromMaterial(font.material);
        ColorUtility.TryParseHtmlString("#" + hex, out var c);
        mat.SetColor("_FaceColor", c.linear * power);
        EditorUtility.SetDirty(mat);

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localRotation = Quaternion.Euler(0, yaw + 180, 0);
        var tmp = go.AddComponent<TextMeshPro>();
        tmp.font = font; tmp.fontSharedMaterial = mat;
        tmp.text = text; tmp.color = Color.white;
        tmp.fontSize = height * 10f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        ((RectTransform)go.transform).sizeDelta = new Vector2(Mathf.Max(1f, text.Length * height * .9f), height * 1.4f);
        go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        go.isStatic = true;
        return tmp;
    }
}
#endif
