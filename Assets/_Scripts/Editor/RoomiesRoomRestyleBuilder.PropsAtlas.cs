#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using static RoomiesRestyleKit;
using Object = UnityEngine.Object;

/// <summary>
/// 部屋の小物のアトラス（props_atlas.png、2048×2048）とマテリアル（2026/11/23）。
/// 元の絵（Codex が作ったアトラス）はプロジェクト直下の ArtSource/Props/ に置き、Unity には取り込まない。
/// Build Room を実行すると、ここに書いた区画の表どおりに切り出し・回転・にじみ止めをして 1 枚に組む（元の絵がなければ仮の柄）。
///
/// 区画（左下が原点の px）：
///   缶のラベル 6 種 …… 1024×372（2 列×3 段、y 932〜2048）。横幅が缶の周り 1 周
///   瓶のラベル 3 種 …… 1024×186（x 0〜1024、y 374〜932）。横幅が瓶の周り 1 周
///   雑誌の表紙 4 種 …… 256×316（x 1024〜2048、y 616〜932）
///   冷蔵庫のメモ 6 種 … 170×190（x 1024〜2044、y 426〜616。アルファあり）
///   ピザの上面 ………… 374×374（x 0、y 0。円の直径がマス幅。チーズ・ペパロニ・ピーマンの絵を重ねる）
///   ピザの箱のふた …… 256×256（x 374）
///   葉 3 種 …………… 256×320（x 630〜1398。付け根が下、先が上にそろえる。アルファあり）
///   紙くず 4 種 ……… 196×150（x 1398〜1790、2 段。アルファあり）
///   単色の色見本 …… 256×256（x 1792。32px × 8×8）
/// ネコのネオンだけは別のテクスチャ（Textures/Neon の色とマスク）と別のマテリアル（光る）。
/// </summary>
internal static partial class RoomiesRoomRestyleBuilder
{
    const string PropsDir = ArtDir + "/Props";
    internal const string PropsSourceDir = "ArtSource/Props";
    const string PropsAtlasPath = PropsDir + "/props_atlas.png";
    const string PropsMatDir = "Assets/_Materials/Props";
    const string NeonDir = ArtDir + "/Textures/Neon";
    const int Atlas = 2048;
    /// <summary>観葉植物の葉：false = 折れ目のある葉のメッシュに葉の絵を貼る（既定）、true = 葉の板＋アルファ（props_atlas_cutout）。</summary>
    internal static bool PlantCardLeaves = false;
    /// <summary>ピザのペパロニを薄い円盤の形でも載せる（上面の絵と同じ位置）。</summary>
    const bool PizzaToppingDiscs = true;

    static Material propsMat, propsCutout, neonCatMat;

    // ---------------------------------------------------------------- 色見本（単色の区画の番号）
    const int SwMetal = 8, SwMetalDark = 9, SwGlassBrown = 10, SwCream = 11, SwGold = 12, SwCrust = 13, SwCheese = 14, SwPepperoni = 15,
        SwPepper = 16, SwLeaf = 17, SwLeafDark = 18, SwLeafLight = 19, SwStem = 20, SwPot = 21, SwPotDark = 22, SwSoil = 23,
        SwBoxOuter = 24, SwBoxInner = 25, SwBoxEdge = 26, SwGrease = 27, SwCrustDark = 28, SwSocket = 29, SwWhite = 30, SwGlassGreen = 31, SwGlassClear = 32;
    // 0〜5 は缶の胴の色（ラベル 0〜5 に合わせる：桃・ベリー・はちみつ・島・ココア・りんご）
    static readonly string[] SwatchHex =
    {
        "E77A5E", "7B3F93", "E9B53A", "3F9BA3", "9B3A33", "E0782F", "E6E1D6", "3C8A86",
        "9AA0A8", "5E636B", "5A3420", "E3D3B0", "D9A62E", "C98A4A", "E8B04A", "A8322C",
        "4F8A3A", "4E8A3A", "2F5E2C", "79AE48", "3F6630", "B2623A", "8A4628", "3A2A20",
        "D2BC8E", "E4D4AE", "A88E62", "C9B07A", "A86A34", "26302A", "EEE9DE", "2F5A34",
        "9DB0A8",
    };
    const int CanKinds = 6, BottleKinds = 4;
    // 瓶の種類 → ラベルとガラスの色
    static readonly int[] BottleLabel = { 0, 1, 2, 0 };
    static readonly int[] BottleGlass = { SwGlassBrown, SwGlassClear, SwGlassGreen, SwGlassGreen };

    static RectInt CanLabelRect(int i) => new RectInt((i % 2) * 1024, Atlas - 372 * (i / 2 + 1), 1024, 372);
    static RectInt BottleLabelRect(int j) => new RectInt(0, 932 - 186 * (j + 1), 1024, 186);
    static RectInt CoverRect(int k) => new RectInt(1024 + k * 256, 616, 256, 316);
    static RectInt NoteRect(int n) => new RectInt(1024 + n * 170, 426, 170, 190);
    static RectInt LeafRect(int l) => new RectInt(630 + l * 256, 0, 256, 320);
    static RectInt ScrapRect(int s) => new RectInt(1398 + (s % 2) * 196, (s / 2) * 150, 196, 150);
    static readonly RectInt PizzaTopRect = new RectInt(0, 0, 374, 374), BoxPrintRect = new RectInt(374, 0, 256, 256), SwatchRect = new RectInt(1792, 0, 256, 256);

    static Vector2 SwUV(int idx) => new Vector2((SwatchRect.x + (idx % 8) * 32 + 16f) / Atlas, (SwatchRect.y + (idx / 8) * 32 + 16f) / Atlas);
    /// <summary>区画の中の (0〜1, 0〜1) をアトラスの UV に（端は 2px 内側に寄せてにじみを防ぐ）。</summary>
    static Vector2 InRect(RectInt r, float u, float v) => new Vector2((r.x + 2 + Mathf.Clamp01(u) * (r.width - 4)) / Atlas, (r.y + 2 + Mathf.Clamp01(v) * (r.height - 4)) / Atlas);

    // ---------------------------------------------------------------- 元の絵の切り出し位置（左上が原点の px。README_codex.md の並び）
    static RectInt R(int x0, int y0, int x1, int y1) => new RectInt(x0, y0, x1 - x0, y1 - y0);
    static readonly RectInt[] SrcCan = { R(20, 60, 504, 236), R(524, 60, 1008, 236), R(20, 260, 504, 432), R(524, 260, 1008, 432), R(20, 456, 504, 636), R(524, 456, 1008, 636) };
    static readonly RectInt[] SrcBottle = { R(48, 12, 980, 180), R(48, 200, 980, 352), R(48, 368, 980, 500) };
    static readonly RectInt[] SrcCover = { R(56, 20, 452, 508), R(484, 20, 884, 508), R(52, 520, 452, 1012), R(484, 520, 884, 1016) };
    static readonly RectInt[] SrcNote = { R(36, 28, 332, 356), R(360, 8, 668, 368), R(692, 12, 992, 364), R(36, 372, 328, 668), R(356, 380, 664, 668), R(700, 376, 992, 664) };
    static readonly RectInt[] SrcScrap = { R(56, 60, 508, 388), R(572, 36, 956, 368), R(64, 448, 548, 652), R(600, 380, 956, 672) };
    static readonly RectInt[] SrcPepperoni = { R(48, 24, 256, 228), R(296, 20, 500, 224), R(540, 24, 752, 228), R(788, 28, 992, 224) };
    static readonly RectInt[] SrcPepper = { R(36, 248, 296, 392), R(340, 276, 512, 388), R(548, 240, 736, 396), R(780, 272, 980, 372) };
    static readonly RectInt[] SrcCheese = { R(20, 408, 308, 664), R(332, 428, 568, 636), R(576, 452, 768, 592), R(804, 412, 1012, 648) };
    // 葉：付け根・先（左上が原点の px）と、長さに対する幅。0 = ポトス（ハート形）、1 = マドカズラ（穴あき）、2 = 細長い葉
    static readonly (Vector2 b, Vector2 t, float w)[] SrcLeaf =
    {
        (new Vector2(522, 92), new Vector2(446, 322), 1.05f),
        (new Vector2(634, 366), new Vector2(424, 664), .9f),
        (new Vector2(74, 656), new Vector2(314, 344), .62f),
    };
    // ネコのネオンの絵の範囲（cat_neon_pink.png、1024×683）
    static readonly RectInt SrcNeonCat = R(160, 52, 844, 668);

    // ================================================================ アトラスとマテリアル
    static void PropsPalette()
    {
        Directory.CreateDirectory(PropsDir);
        Directory.CreateDirectory(PropsMatDir);
        var atlas = BuildPropsAtlas();
        propsMat = PropsMaterial("props_atlas", atlas, false);
        propsCutout = PropsMaterial("props_atlas_cutout", atlas, true);
        neonCatMat = NeonCatMaterial();
    }

    static Material LitMaterial(string name)
    {
        string path = $"{PropsMatDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        return mat;
    }

    static void SetCutout(Material mat, bool cutout)
    {
        if (cutout)
        {
            mat.SetFloat("_AlphaClip", 1); mat.SetFloat("_Cutoff", .5f); mat.EnableKeyword("_ALPHATEST_ON");
            mat.SetFloat("_Cull", 0); mat.SetOverrideTag("RenderType", "TransparentCutout");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
        }
        else
        {
            mat.SetFloat("_AlphaClip", 0); mat.DisableKeyword("_ALPHATEST_ON"); mat.SetFloat("_Cull", 2);
            mat.SetOverrideTag("RenderType", "Opaque"); mat.renderQueue = -1;
        }
    }

    static Material PropsMaterial(string name, Texture2D atlas, bool cutout)
    {
        var mat = LitMaterial(name);
        mat.SetTexture("_BaseMap", atlas); mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", .12f); mat.SetFloat("_Metallic", 0);   // 光沢のないマット寄り
        mat.SetFloat("_EnvironmentReflections", 0); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        mat.enableInstancing = true;
        SetCutout(mat, cutout);
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>ネコのネオン：色の絵の線だけを残し（アルファで切り抜き）、マスクの白線を光らせる。</summary>
    static Material NeonCatMaterial()
    {
        var pink = ImportTexture($"{NeonDir}/cat_neon_pink.png", 1024, true);
        var mask = ImportTexture($"{NeonDir}/cat_neon_mask.png", 1024, false);
        var mat = LitMaterial("props_neon_cat");
        mat.SetTexture("_BaseMap", pink); mat.SetColor("_BaseColor", Color.white);
        mat.SetFloat("_Smoothness", .3f); mat.SetFloat("_Metallic", 0);
        SetCutout(mat, true);
        mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_EmissionMap", mask);
        mat.SetColor("_EmissionColor", Hex("FF4FB4").linear * 6);
        mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        mat.enableInstancing = true;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>取り込み設定をそろえる（ミップマップ・最大サイズ・高品質圧縮）。</summary>
    static Texture2D ImportTexture(string path, int maxSize, bool alpha)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Texture not found: " + path);
        if (importer.maxTextureSize != maxSize || !importer.mipmapEnabled || importer.alphaIsTransparency != alpha || importer.textureCompression != TextureImporterCompression.CompressedHQ || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.textureType = TextureImporterType.Default; importer.maxTextureSize = maxSize; importer.mipmapEnabled = true;
            importer.alphaIsTransparency = alpha; importer.textureCompression = TextureImporterCompression.CompressedHQ; importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // ================================================================ アトラスを組む
    static Color[] px;

    static Texture2D LoadSource(string name)
    {
        string file = $"{PropsSourceDir}/{name}.png";
        if (!File.Exists(file)) { Debug.LogWarning("[Roomies Props] source not found, using placeholder: " + file); return null; }
        var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!t.LoadImage(File.ReadAllBytes(file))) { Object.DestroyImmediate(t); return null; }
        return t;
    }

    /// <summary>左上が原点の px で元の絵を読む。</summary>
    static Color Sample(Texture2D s, float x, float y) => s.GetPixelBilinear(x / s.width, 1 - y / s.height);

    static void Fill(RectInt r, Func<float, float, Color> fn)
    {
        for (int y = 0; y < r.height; y++) for (int x = 0; x < r.width; x++)
            px[(r.y + y) * Atlas + r.x + x] = fn((x + .5f) / r.width, (y + .5f) / r.height);
    }

    static RectInt Inset(RectInt r, int k) => new RectInt(r.x + k, r.y + k, r.width - 2 * k, r.height - 2 * k);

    /// <summary>四角く切り出して区画に合わせる（区画の下 = 元の絵の下）。</summary>
    static void Crop(RectInt dst, Texture2D s, RectInt src, bool opaque)
    {
        Fill(dst, (u, v) =>
        {
            var c = Sample(s, src.x + u * src.width, src.y + (1 - v) * src.height);
            c.a = opaque ? 1 : Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, .75f, c.a));   // 縁のうすい色ゴミを落とす
            return c;
        });
    }

    /// <summary>付け根から先へ向きをそろえて切り出す（葉）。</summary>
    static void CropAlong(RectInt dst, Texture2D s, Vector2 b, Vector2 t, float width)
    {
        Vector2 dir = t - b; float len = dir.magnitude; dir /= len;
        var perp = new Vector2(-dir.y, dir.x);
        Fill(dst, (u, v) =>
        {
            var p = b + dir * len * (v * 1.04f - .02f) + perp * (u - .5f) * len * width;
            var c = Sample(s, p.x, p.y);
            c.a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.3f, .75f, c.a));
            return c;
        });
    }

    /// <summary>区画の中で、いちばん大きいかたまりだけを残す（紙くずの周りの点々を消す）。</summary>
    static void KeepLargest(RectInt r)
    {
        int w = r.width, h = r.height; var label = new int[w * h]; int best = 0, bestCount = 0, n = 0;
        var queue = new Queue<int>();
        bool On(int i) => px[(r.y + i / w) * Atlas + r.x + i % w].a >= .5f;
        for (int i = 0; i < w * h; i++)
        {
            if (label[i] != 0 || !On(i)) continue;
            n++; int count = 0; label[i] = n; queue.Enqueue(i);
            while (queue.Count > 0)
            {
                int c = queue.Dequeue(); count++; int cx = c % w, cy = c / w;
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int nx = cx + dx, ny = cy + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
                    int ni = ny * w + nx; if (label[ni] != 0 || !On(ni)) continue;
                    label[ni] = n; queue.Enqueue(ni);
                }
            }
            if (count > bestCount) { bestCount = count; best = n; }
        }
        for (int i = 0; i < w * h; i++) if (label[i] != best) { int k = (r.y + i / w) * Atlas + r.x + i % w; px[k].a = 0; }
    }

    /// <summary>透明な所の色を、となりの不透明な色で埋める（ミップマップや板の縁で黒くにじまないように）。</summary>
    static void Bleed(RectInt r, int passes = 10)
    {
        int w = r.width, h = r.height; var done = new bool[w * h];
        for (int i = 0; i < w * h; i++) done[i] = px[(r.y + i / w) * Atlas + r.x + i % w].a >= .5f;
        for (int pass = 0; pass < passes; pass++)
        {
            var next = (bool[])done.Clone();
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                if (done[y * w + x]) continue;
                Color sum = Color.clear; int cnt = 0;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy; if (nx < 0 || ny < 0 || nx >= w || ny >= h || !done[ny * w + nx]) continue;
                    sum += px[(r.y + ny) * Atlas + r.x + nx]; cnt++;
                }
                if (cnt == 0) continue;
                int k = (r.y + y) * Atlas + r.x + x; float a = px[k].a;
                px[k] = sum / cnt; px[k].a = a; next[y * w + x] = true;
            }
            done = next;
        }
    }

    /// <summary>ピザの上面に具の絵を 1 つ重ねる。center と size はピザの上のメートル、angle は度。</summary>
    static void DrawTopping(Texture2D s, RectInt src, Vector2 center, float size, float angle, float strength = 1)
    {
        var r = PizzaTopRect; float m = Mathf.Max(src.width, src.height);
        float cos = Mathf.Cos(-angle * Mathf.Deg2Rad), sin = Mathf.Sin(-angle * Mathf.Deg2Rad);
        for (int y = 0; y < r.height; y++) for (int x = 0; x < r.width; x++)
        {
            float wx = ((x + .5f) / r.width - .5f) * 2 * PizzaCheeseR - center.x, wz = ((y + .5f) / r.height - .5f) * 2 * PizzaCheeseR - center.y;
            if (Mathf.Abs(wx) > size || Mathf.Abs(wz) > size) continue;
            float lx = (wx * cos - wz * sin) / size, lz = (wx * sin + wz * cos) / size;   // -0.5〜0.5 が絵の範囲
            float sx = src.x + src.width * .5f + lx * m, sy = src.y + src.height * .5f - lz * m;
            if (sx < src.x || sx > src.xMax || sy < src.y || sy > src.yMax) continue;
            var c = Sample(s, sx, sy);
            float a = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.4f, .85f, c.a)) * strength;
            int k = (r.y + y) * Atlas + r.x + x;
            if (new Vector2(wx + center.x, wz + center.y).magnitude > PizzaCheeseR * .97f) continue;   // 耳にはのせない
            px[k] = Color.Lerp(px[k], new Color(c.r, c.g, c.b, 1), a);
        }
    }

    static Texture2D BuildPropsAtlas()
    {
        px = new Color[Atlas * Atlas];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(.5f, .5f, .5f, 1);

        var cans = LoadSource("can_labels_atlas");
        for (int i = 0; i < CanKinds; i++)
        { int k = i; if (cans != null) Crop(CanLabelRect(i), cans, Inset(SrcCan[i], 6), true); else Fill(CanLabelRect(i), (u, v) => CanLabelPixel(k, u, v)); }
        var bottles = LoadSource("bottle_labels_atlas");
        for (int j = 0; j < 3; j++)
        { int k = j; if (bottles != null) Crop(BottleLabelRect(j), bottles, Inset(SrcBottle[j], 6), true); else Fill(BottleLabelRect(j), (u, v) => BottleLabelPixel(k, u, v)); }
        var covers = LoadSource("magazine_covers_atlas");
        for (int k = 0; k < 4; k++)
        { if (covers != null) Crop(CoverRect(k), covers, Inset(SrcCover[k], 6), true); else Fill(CoverRect(k), (u, v) => Grain(Hex(SwatchHex[k]), u, v, .03f)); }
        var notes = LoadSource("fridge_notes_atlas");
        for (int n = 0; n < 6; n++)
        { if (notes != null) { Crop(NoteRect(n), notes, SrcNote[n], false); Bleed(NoteRect(n)); } else Fill(NoteRect(n), (u, v) => Hex("F2D048")); }
        var scraps = LoadSource("paper_scraps_atlas");
        for (int s = 0; s < 4; s++)
        { if (scraps != null) { Crop(ScrapRect(s), scraps, SrcScrap[s], false); KeepLargest(ScrapRect(s)); Bleed(ScrapRect(s)); } else Fill(ScrapRect(s), (u, v) => Hex("EEE7D6")); }
        var leaves = LoadSource("plant_leaves_atlas");
        for (int l = 0; l < SrcLeaf.Length; l++)
        {
            if (leaves != null) { CropAlong(LeafRect(l), leaves, SrcLeaf[l].b, SrcLeaf[l].t, SrcLeaf[l].w); KeepLargest(LeafRect(l)); Bleed(LeafRect(l), 24); }
            else Fill(LeafRect(l), LeafCardPixel);
        }

        // ピザの上面：チーズの地に、チーズ・ペパロニ・ピーマンの絵を重ねる（ペパロニの位置は形の円盤と同じ）
        var toppings = LoadSource("pizza_toppings_atlas");
        Fill(PizzaTopRect, (u, v) => PizzaTopPixel(u, v, toppings == null));
        if (toppings != null)
        {
            (float x, float z, float s, float a)[] blobs = { (0, 0, .12f, 0), (.07f, .05f, .1f, 40), (-.07f, .06f, .1f, 110), (-.06f, -.07f, .11f, 200), (.07f, -.06f, .1f, 300), (0, .105f, .08f, 70), (0, -.105f, .08f, 250), (.105f, 0, .08f, 160), (-.105f, 0, .08f, 20) };
            for (int i = 0; i < blobs.Length; i++) DrawTopping(toppings, SrcCheese[i % 4], new Vector2(blobs[i].x, blobs[i].z), blobs[i].s, blobs[i].a, .75f);
            int k = 0; foreach (var p in PepperoniSpots()) DrawTopping(toppings, SrcPepperoni[k % 4], p, .046f, k++ * 47);
            k = 0; foreach (var p in PepperSpots()) DrawTopping(toppings, SrcPepper[k % 4], p, .036f, Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg + 90 + k++ * 20);
        }
        Fill(BoxPrintRect, BoxPrintPixel);
        Fill(SwatchRect, (u, v) =>
        {
            int idx = Mathf.FloorToInt(v * 8) * 8 + Mathf.FloorToInt(u * 8);
            return Grain(idx < SwatchHex.Length ? Hex(SwatchHex[idx]) : new Color(.5f, .5f, .5f), u, v, .025f);
        });
        foreach (var t in new[] { cans, bottles, covers, notes, scraps, leaves, toppings }) if (t != null) Object.DestroyImmediate(t);

        var tex = new Texture2D(Atlas, Atlas, TextureFormat.RGBA32, false);
        tex.SetPixels(px); tex.Apply();
        var bytes = tex.EncodeToPNG();
        Object.DestroyImmediate(tex); px = null;
        bool changed = !File.Exists(PropsAtlasPath) || !BytesEqual(File.ReadAllBytes(PropsAtlasPath), bytes);
        if (changed) { File.WriteAllBytes(PropsAtlasPath, bytes); AssetDatabase.ImportAsset(PropsAtlasPath, ImportAssetOptions.ForceUpdate); }
        var importer = (TextureImporter)AssetImporter.GetAtPath(PropsAtlasPath);
        if (importer.wrapMode != TextureWrapMode.Clamp || !importer.alphaIsTransparency || importer.maxTextureSize != Atlas || importer.anisoLevel != 4 || importer.textureCompression != TextureImporterCompression.CompressedHQ)
        {
            importer.wrapMode = TextureWrapMode.Clamp; importer.alphaIsTransparency = true; importer.mipmapEnabled = true;
            importer.maxTextureSize = Atlas; importer.anisoLevel = 4; importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(PropsAtlasPath);
    }

    static bool BytesEqual(byte[] a, byte[] b)
    {
        if (a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
        return true;
    }

    static Color Grain(Color c, float u, float v, float amount)
    {
        float n = (Mathf.PerlinNoise(u * 61.3f + 3.1f, v * 61.3f + 7.7f) - .5f) * 2 * amount;
        return new Color(Mathf.Clamp01(c.r + n), Mathf.Clamp01(c.g + n), Mathf.Clamp01(c.b + n), c.a);
    }

    // ---------------------------------------------------------------- 仮の柄（元の絵がないとき。実在のブランド・文字は使わない）
    static Color CanLabelPixel(int i, float u, float v)
    {
        Color bg = Color.Lerp(Hex(SwatchHex[i]), Color.white, .35f), fg = Hex(SwatchHex[i]), accent = Hex("EEE9DE");
        float x = u * 2.75f, y = v, pxx = Mathf.Repeat(x, 1.375f);
        Color c = bg;
        if (y < .07f || y > .93f) c = fg;
        else if (Mathf.Abs(pxx - .69f) + Mathf.Abs(y - .5f) * 1.2f < .3f) c = accent;
        return Grain(c, u, v, .02f);
    }

    static Color BottleLabelPixel(int j, float u, float v)
    {
        string[] bands = { "C4453A", "D9A62E", "4F8A3A" };
        Color c = (v < .12f || v > .88f) ? Hex(bands[j % 3]) : Hex("E8DCC0");
        return Grain(c, u, v, .02f);
    }

    const float PizzaR = .18f, PizzaCheeseR = .15f;

    static Color PizzaTopPixel(float u, float v, bool paintToppings)
    {
        float x = (u - .5f) * 2 * PizzaCheeseR, z = (v - .5f) * 2 * PizzaCheeseR, r = Mathf.Sqrt(x * x + z * z);
        if (r > PizzaCheeseR * .985f) return Grain(Hex(SwatchHex[SwCrust]), u, v, .04f);
        float m = Mathf.PerlinNoise(u * 9 + 1.3f, v * 9 + 4.1f);
        Color c = Color.Lerp(Hex("D9982F"), Hex("F2C766"), m);
        if (Mathf.PerlinNoise(u * 23 + 8, v * 23 + 2) > .72f) c = Color.Lerp(c, Hex("B9762A"), .55f);   // 焼き色
        if (paintToppings)
        {
            foreach (var p in PepperoniSpots())
            {
                float d = new Vector2(x - p.x, z - p.y).magnitude;
                if (d < .021f) c = d > .017f ? Hex("7E2420") : Hex("A8322C");
            }
            foreach (var p in PepperSpots())
                if (new Vector2(x - p.x, z - p.y).magnitude < .01f) c = Hex(SwatchHex[SwPepper]);
        }
        return Grain(c, u, v, .02f);
    }

    static Color BoxPrintPixel(float u, float v)
    {
        Color bg = Hex(SwatchHex[SwBoxOuter]), red = Hex("C4453A"), cream = Hex("EEE2C4"), cheese = Hex("E8B04A");
        float x = u - .5f, y = v - .5f, r = Mathf.Sqrt(x * x + y * y);
        Color c = bg;
        float edge = Mathf.Max(Mathf.Abs(x), Mathf.Abs(y));
        if (edge > .40f && edge < .425f) c = red;
        if (r < .3f) c = red;
        if (r < .25f) c = cream;
        float ty = y + .14f, half = .13f * (1 - ty / .3f);
        if (ty > 0 && ty < .3f && Mathf.Abs(x) < half) c = ty > .25f ? Hex("C98A4A") : cheese;
        foreach (var d in new[] { new Vector2(-.04f, -.03f), new Vector2(.035f, .02f), new Vector2(0, .07f) })
            if (new Vector2(x - d.x, y - d.y).magnitude < .022f && ty > 0 && ty < .25f) c = red;
        foreach (var k in new[] { new Vector2(-.36f, -.36f), new Vector2(.36f, -.36f), new Vector2(-.36f, .36f), new Vector2(.36f, .36f) })
            if (Mathf.Abs(x - k.x) + Mathf.Abs(y - k.y) < .03f) c = red;
        return Grain(c, u, v, .02f);
    }

    static Color LeafCardPixel(float u, float v)
    {
        float x = u - .5f, y = v;
        float half = .44f * Mathf.Sin(Mathf.PI * Mathf.Pow(Mathf.Clamp01(y), .75f)) * (1 - .25f * y);
        float a = Mathf.Clamp01((half - Mathf.Abs(x)) * 120);
        Color c = Color.Lerp(Hex(SwatchHex[SwLeafDark]), Hex(SwatchHex[SwLeafLight]), Mathf.Clamp01(y * .8f + (x > 0 ? .15f : 0)));
        if (Mathf.Abs(x) < .008f) c = Color.Lerp(c, Hex("8CBF6A"), .6f);
        c = Grain(c, u, v, .03f); c.a = a;
        return c;
    }
}
#endif
