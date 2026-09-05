using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ランタイム生成UIの共通デザインシステム。
/// 角丸スプライトのコード生成・配色パレット・カード/ボタン/ラベルのスタイルヘルパー。
/// 画像アセット不要＆シーン非依存の方針（MenuThemer / HudThemer / NightEventUI が使用）。
/// </summary>
public static class UITheme
{
    // ================================================================
    // パレット
    // ================================================================

    public static readonly Color Panel      = new Color(1.00f, 0.985f, 0.95f, 0.96f);
    public static readonly Color PanelSoft  = new Color(0.91f, 0.90f, 1.00f, 0.97f);
    public static readonly Color Accent     = new Color(0.98f, 0.31f, 0.58f);
    public static readonly Color Purple     = new Color(0.55f, 0.36f, 0.92f);
    public static readonly Color Blue       = new Color(0.18f, 0.68f, 0.95f);
    public static readonly Color Green      = new Color(0.12f, 0.64f, 0.34f);
    public static readonly Color Red        = new Color(0.95f, 0.29f, 0.34f);
    public static readonly Color Gold       = new Color(0.88f, 0.43f, 0.04f);
    public static readonly Color TextMain   = new Color(0.12f, 0.12f, 0.24f);
    public static readonly Color TextSub    = new Color(0.38f, 0.39f, 0.52f);
    public static readonly Color Border     = new Color(0.42f, 0.34f, 0.68f, 0.18f);
    public static readonly Color DarkButton = new Color(0.87f, 0.84f, 1.00f);
    public static readonly Color WarmTop    = new Color(0.23f, 0.74f, 0.98f);
    public static readonly Color WarmBottom = new Color(0.58f, 0.39f, 0.94f);

    // ---- メニュー用パレット ------------------------------------------
    // 3Dの部屋の上に直接乗る前提。白いカードではなく「濃い面＋太い白フチ」。
    public static readonly Color MenuInk     = new Color(0.055f, 0.105f, 0.14f, 0.96f);
    public static readonly Color MenuInkSoft = new Color(0.12f, 0.22f, 0.25f, 0.96f);
    public static readonly Color MenuEdge    = new Color(0.72f, 0.86f, 0.83f, 0.72f);
    public static readonly Color MenuShade   = new Color(0.025f, 0.045f, 0.065f);
    public static readonly Color Pink        = new Color(1.00f, 0.31f, 0.58f);
    public static readonly Color Cyan        = new Color(0.16f, 0.76f, 1.00f);
    public static readonly Color Lime        = new Color(0.44f, 0.87f, 0.30f);
    public static readonly Color Sun         = new Color(1.00f, 0.80f, 0.20f);
    public static readonly Color Grape       = new Color(0.40f, 0.28f, 0.64f);

    /// <summary>色を暗い紫側へ寄せる（ボタンの厚み＝側面の色に使う）。</summary>
    public static Color Shade(Color c, float t) =>
        Color.Lerp(c, new Color(MenuShade.r, MenuShade.g, MenuShade.b, c.a), t);

    /// <summary>色を白側へ寄せる（ハイライト状態に使う）。</summary>
    public static Color Tint(Color c, float t) =>
        Color.Lerp(c, new Color(1f, 1f, 1f, c.a), t);

    // ================================================================
    // スプライト生成（画像アセット不要）
    // ================================================================

    private const int RoundedSize    = 64;
    private const int RoundedRadius  = 13;
    private const int PillSize       = 96;
    private const int PillRadius     = 46;

    private static Sprite roundedSprite;
    private static Sprite pillSprite;
    private static Sprite panelSprite;
    private static Sprite buttonSprite;

    public static Sprite PanelSprite =>
        panelSprite != null ? panelSprite : panelSprite = RoundedSprite;

    public static Sprite ButtonSprite =>
        buttonSprite != null ? buttonSprite : buttonSprite = RoundedSprite;

    /// <summary>角丸矩形スプライト（9-slice対応）。全ランタイムUIで共有。</summary>
    public static Sprite RoundedSprite =>
        roundedSprite != null
            ? roundedSprite
            : roundedSprite = CreateRoundedSprite(RoundedSize, RoundedRadius, RoundedRadius + 4);

    /// <summary>カプセル（ピル）型スプライト。厚みのあるパーティーゲーム風ボタン用。</summary>
    public static Sprite PillSprite =>
        pillSprite != null
            ? pillSprite
            : pillSprite = CreateRoundedSprite(PillSize, PillRadius, PillRadius);

    private static Sprite CreateRoundedSprite(int size, int radius, int slice)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = x + 0.5f;
                float py = y + 0.5f;

                // 角丸矩形のSDF：角の中心からの距離でアンチエイリアス付きαを出す
                float dx = Mathf.Max(radius - px, px - (size - radius), 0f);
                float dy = Mathf.Max(radius - py, py - (size - radius), 0f);
                float d  = Mathf.Sqrt(dx * dx + dy * dy);
                float a  = Mathf.Clamp01(radius - d + 0.5f);

                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        float border = Mathf.Min(slice, (size - 2f) * 0.5f);
        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(border, border, border, border));
    }

    /// <summary>
    /// 9-sliceの見かけの角丸半径をピクセルで指定する。
    /// pixelsPerUnitMultiplierでスプライト境界を拡縮するので、
    /// 小さいボタンでも角が潰れず、大きいパネルでもしっかり丸くなる。
    /// </summary>
    public static void SetCornerRadius(Image image, float radius)
    {
        if (image == null || image.sprite == null) return;
        image.type = Image.Type.Sliced;

        // 0.99倍しておくと、radius = 高さ/2 のカプセル指定でも
        // 上下の9-slice境界の合計が高さを超えず、角が破綻しない。
        float spriteRadius = image.sprite == pillSprite ? PillRadius : RoundedRadius;
        image.pixelsPerUnitMultiplier =
            Mathf.Clamp(spriteRadius / Mathf.Max(2f, radius * 0.99f), 0.08f, 16f);
    }

    /// <summary>縦グラデーションのスプライト（メニュー背景など）。</summary>
    private static Sprite CreateCutCornerSprite(int size, int cut)
    {
        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        var pixels = new Color32[size * size];

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                bool cutBottomLeft = x + y < cut;
                bool cutTopRight = (size - 1 - x) + (size - 1 - y) < cut;
                byte alpha = (byte)(cutBottomLeft || cutTopRight ? 0 : 255);
                pixels[y * size + x] = new Color32(255, 255, 255, alpha);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(
            tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect,
            new Vector4(cut + 2, cut + 2, cut + 2, cut + 2));
    }

    public static Sprite VerticalGradient(Color top, Color bottom)
    {
        const int h = 128;
        var tex = new Texture2D(1, h, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int y = 0; y < h; y++)
        {
            tex.SetPixel(0, y, Color.Lerp(bottom, top, (float)y / (h - 1)));
        }
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, 1, h), new Vector2(0.5f, 0.5f));
    }

    /// <summary>中央から外へ向かうラジアルグラデーション（起動ロゴの背景など）。</summary>
    public static Sprite RadialGradient(Color inner, Color outer, float falloff = 1.35f)
    {
        const int size = 128;
        const float center = (size - 1) * 0.5f;

        var tex = new Texture2D(size, size, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center) / center;
                float dy = (y - center) / center;
                float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
                float t = Mathf.Pow(d, falloff);
                pixels[y * size + x] = Color.Lerp(inner, outer, t);
            }
        }

        tex.SetPixels32(pixels);
        tex.Apply();

        return Sprite.Create(
            tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
            SpriteMeshType.FullRect);
    }

    public static Sprite HorizontalGradient(Color left, Color right)
    {
        const int width = 128;
        var tex = new Texture2D(width, 1, TextureFormat.ARGB32, false);
        tex.wrapMode = TextureWrapMode.Clamp;

        for (int x = 0; x < width; x++)
            tex.SetPixel(x, 0, Color.Lerp(left, right, (float)x / (width - 1)));

        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f));
    }

    // ================================================================
    // 生成ヘルパー
    // ================================================================

    /// <summary>ScreenSpaceOverlayのCanvasを生成する（1920x1080基準）。</summary>
    public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder)
    {
        var go = new GameObject(name);
        if (parent != null) go.transform.SetParent(parent, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    /// <summary>角丸＋影つきのカードを生成する。</summary>
    public static Image Card(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.sprite = PanelSprite;
        img.type   = Image.Type.Sliced;
        img.color  = Panel;

        AddShadow(go, 0.15f, 5f);
        AddBorder(go);
        AddSurfaceDetail(img);
        return img;
    }

    /// <summary>ドロップシャドウを付ける（Imageなどに）。</summary>
    public static void AddShadow(GameObject go, float alpha = 0.20f, float distance = 3f)
    {
        if (go == null) return;
        var shadow = go.GetComponent<Shadow>();
        if (shadow == null) shadow = go.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, alpha);
        shadow.effectDistance = new Vector2(0f, -distance);
        shadow.useGraphicAlpha = true;
    }

    /// <summary>生活ゲーム風の細い明色ボーダーを付ける。</summary>
    public static void AddBorder(GameObject go, Color? color = null, Vector2? distance = null)
    {
        if (go == null) return;
        var outline = go.GetComponent<Outline>();
        if (outline == null) outline = go.AddComponent<Outline>();
        outline.effectColor = color ?? Border;
        outline.effectDistance = distance ?? new Vector2(1f, -1f);
        outline.useGraphicAlpha = true;
    }

    /// <summary>
    /// 旧仕様の飾り（全面の半透明グラデ板 SurfaceTone と、左上の意味のない棒 SurfaceAccent）を撤去する。
    /// 呼び出し側は12ファイルに散っているのでシグネチャは残し、
    /// 中身は「もし残っていたら消す」だけにしてある。
    /// </summary>
    public static void AddSurfaceDetail(Image surface, Color? accent = null)
    {
        if (surface == null) return;
        RemoveChild(surface.transform, "SurfaceTone");
        RemoveChild(surface.transform, "SurfaceAccent");
    }

    private static void RemoveChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child == null) return;
        if (Application.isPlaying) Object.Destroy(child.gameObject);
        else Object.DestroyImmediate(child.gameObject);
    }

    // ================================================================
    // メニュー用サーフェス／ボタン
    // ================================================================

    /// <summary>
    /// 3Dの部屋の上に置く面を作る。白いカードではなく「濃い面＋太い白フチ」。
    /// 対象のImageをフチにして、内側に子のFillを敷く（子は親のImageより手前に描かれるため）。
    /// </summary>
    public static Image MenuSurface(
        GameObject go, float radius = 34f, float edge = 6f, Color? fill = null)
    {
        if (go == null) return null;

        var border = go.GetComponent<Image>();
        if (border == null) border = go.AddComponent<Image>();
        border.enabled = true;
        border.sprite = RoundedSprite;
        border.color  = MenuEdge;
        SetCornerRadius(border, radius);

        RemoveChild(go.transform, "SurfaceTone");
        RemoveChild(go.transform, "SurfaceAccent");

        var outline = go.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        AddShadow(go, 0.28f, 6f);

        var fillTr = go.transform.Find("Fill") as RectTransform;
        Image fillImage;
        if (fillTr == null)
        {
            var fillGo = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGo.transform.SetParent(go.transform, false);
            fillTr = (RectTransform)fillGo.transform;
            fillImage = fillGo.GetComponent<Image>();
        }
        else fillImage = fillTr.GetComponent<Image>();

        fillTr.SetAsFirstSibling();
        Fill(fillTr, edge);
        fillImage.sprite = RoundedSprite;
        fillImage.color  = fill ?? MenuInk;
        fillImage.raycastTarget = false;
        SetCornerRadius(fillImage, Mathf.Max(4f, radius - edge));

        // 親にLayoutGroupが付いていても、内側の面は常に全面へ張る
        IgnoreLayout(fillTr.gameObject);

        return border;
    }

    /// <summary>プレイ中UI・演出用の面を新規生成する（濃色＋太い白フチ＋影）。</summary>
    public static Image Surface(
        Transform parent, string name,
        float radius = 24f, float edge = 4f, Color? fill = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return MenuSurface(go, radius, edge, fill);
    }

    /// <summary>情報チップ（角丸の小さな面）。厚みは持たず、状態や補足を1行で示す用。</summary>
    public static Image Chip(Transform parent, string name, Color fill, float radius = 18f)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.sprite = PillSprite;
        image.color  = fill;
        image.raycastTarget = false;
        SetCornerRadius(image, radius);
        return image;
    }

    /// <summary>
    /// 全画面演出の背景。ベタ塗りの板ではなく
    /// 「濃紫の遮光 ＋ 上下から差す演出色」の2枚重ねにして、後ろの部屋をうっすら残す。
    /// opacity=1で完全遮光（リスポーンを隠すなど、機能的に不透明が必要な場面用）。
    /// </summary>
    public static void StageBackdrop(Transform parent, Color tint, float opacity = 0.9f)
    {
        var scrim = new GameObject("Scrim", typeof(RectTransform), typeof(Image));
        scrim.transform.SetParent(parent, false);
        Fill((RectTransform)scrim.transform);
        var scrimImage = scrim.GetComponent<Image>();
        scrimImage.sprite = VerticalGradient(
            new Color(0.11f, 0.05f, 0.22f, opacity),
            new Color(0.05f, 0.02f, 0.11f, opacity));
        scrimImage.raycastTarget = false;

        var glow = new GameObject("Glow", typeof(RectTransform), typeof(Image));
        glow.transform.SetParent(parent, false);
        Fill((RectTransform)glow.transform);
        var glowImage = glow.GetComponent<Image>();
        glowImage.sprite = VerticalGradient(
            new Color(tint.r, tint.g, tint.b, 0.30f),
            new Color(tint.r, tint.g, tint.b, 0.08f));
        glowImage.raycastTarget = false;
    }

    private static void IgnoreLayout(GameObject go)
    {
        var element = go.GetComponent<LayoutElement>();
        if (element == null) element = go.AddComponent<LayoutElement>();
        element.ignoreLayout = true;
    }

    /// <summary>
    /// 厚みのあるカプセル型ボタンに着せ替える。
    /// ルートのImageが「土台（側面）」、子のCapが「天面」。
    /// 押すとCapが土台へ沈み、離すとバネで跳ね返る（UIButtonJuiceが担当）。
    /// </summary>
    public static void StylePill(
        Button button, Color bg, Color fg, float maxFontSize = 34f, float depth = 9f)
    {
        if (button == null) return;

        var rect = (RectTransform)button.transform;

        var baseImage = button.GetComponent<Image>();
        if (baseImage == null) baseImage = button.gameObject.AddComponent<Image>();
        baseImage.enabled       = true;
        baseImage.sprite        = PillSprite;
        baseImage.color         = Shade(bg, 0.46f);
        baseImage.raycastTarget = true;

        // 旧スタイルの1px枠と影は厚みと喧嘩するので止める
        var outline = button.GetComponent<Outline>();
        if (outline != null) outline.enabled = false;
        var shadow = button.GetComponent<Shadow>();
        if (shadow != null) shadow.enabled = false;
        RemoveChild(button.transform, "SurfaceTone");
        RemoveChild(button.transform, "SurfaceAccent");
        RemoveChild(button.transform, "ActionMarker");

        var capTr = button.transform.Find("Cap") as RectTransform;
        Image cap;
        if (capTr == null)
        {
            var capGo = new GameObject("Cap", typeof(RectTransform), typeof(Image));
            capGo.transform.SetParent(button.transform, false);
            capTr = (RectTransform)capGo.transform;
            cap = capGo.GetComponent<Image>();
        }
        else cap = capTr.GetComponent<Image>();

        capTr.SetAsFirstSibling();
        capTr.anchorMin = Vector2.zero;
        capTr.anchorMax = Vector2.one;
        capTr.offsetMin = new Vector2(0f, depth);
        capTr.offsetMax = Vector2.zero;
        IgnoreLayout(capTr.gameObject);
        cap.sprite        = PillSprite;
        cap.color         = Color.white; // 実際の色はButtonのColorTintで乗せる
        cap.raycastTarget = false;

        float height = Mathf.Max(rect.sizeDelta.y, rect.rect.height);
        float radius = Mathf.Max(12f, (height - depth) * 0.5f);
        SetCornerRadius(baseImage, Mathf.Max(12f, height * 0.5f));
        SetCornerRadius(cap, radius);

        // 既存のラベルを天面の中へ移す（沈み込みに一緒に付いてくるように）
        var labels = button.GetComponentsInChildren<TMP_Text>(true);
        foreach (var label in labels)
        {
            if (label.transform.parent != capTr)
                label.transform.SetParent(capTr, false);

            Fill(label.rectTransform, 14f);
            label.fontStyle        = FontStyles.Bold;
            label.color            = fg;
            label.alignment        = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin      = Mathf.Min(18f, maxFontSize);
            label.fontSizeMax      = maxFontSize;
            label.raycastTarget    = false;
            label.textWrappingMode = TextWrappingModes.NoWrap;
        }

        button.transition    = Selectable.Transition.ColorTint;
        button.targetGraphic = cap;

        var colors = button.colors;
        colors.normalColor      = bg;
        colors.highlightedColor = Tint(bg, 0.16f);
        colors.pressedColor     = Shade(bg, 0.08f);
        colors.selectedColor    = Tint(bg, 0.10f);
        colors.disabledColor    = new Color(bg.r * 0.55f, bg.g * 0.55f, bg.b * 0.60f, 0.75f);
        colors.colorMultiplier  = 1f;
        colors.fadeDuration     = 0.06f;
        button.colors = colors;

        var juice = button.GetComponent<UIButtonJuice>();
        if (juice == null) juice = button.gameObject.AddComponent<UIButtonJuice>();
        juice.SetCap(capTr, depth);
    }

    private static void Fill(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
        rect.localScale = Vector3.one;
    }

    /// <summary>TMPラベルを生成する。</summary>
    public static TextMeshProUGUI Label(
        Transform parent, string name, string text,
        float size, Color color, TextAlignmentOptions align, bool bold = false)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var label = go.AddComponent<TextMeshProUGUI>();
        label.text      = text;
        label.fontSize  = size;
        label.color     = color;
        label.alignment = align;
        if (bold) label.fontStyle = FontStyles.Bold;

        return label;
    }

    /// <summary>文字の視認性を上げるアウトライン（3D越しのHUD向け）。</summary>
    public static void AddTextOutline(TMP_Text text, float width = 0.22f)
    {
        // 非表示パネル内のTMPは初期化前でMaterialがまだ無い場合がある。
        if (text == null || !text.gameObject.activeInHierarchy ||
            text.font == null || text.fontSharedMaterial == null) return;
        text.outlineWidth = width;
        text.outlineColor = new Color32(0, 0, 0, 200);
    }

    // ================================================================
    // 既存シーンUIのレストア（Themer用）
    // ================================================================

    /// <summary>
    /// 既存のButtonをテーマに合わせて着せ替える。
    /// 角丸背景・状態カラー・ラベル調整・ホバー演出を適用。
    /// </summary>
    public static void StyleButton(Button b, Color bg, Color fg, float maxFontSize = 30f)
    {
        if (b == null) return;

        var img = b.GetComponent<Image>();
        if (img == null) img = b.gameObject.AddComponent<Image>();
        img.sprite = ButtonSprite;
        img.type   = Image.Type.Sliced;
        img.color  = Color.white; // 実際の色はButtonのColorTintで乗せる

        b.transition    = Selectable.Transition.ColorTint;
        b.targetGraphic = img;

        var cb = b.colors;
        cb.normalColor      = bg;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
        cb.pressedColor     = Color.Lerp(bg, Color.black, 0.10f);
        cb.selectedColor    = Color.Lerp(bg, Color.white, 0.24f);
        cb.disabledColor    = new Color(bg.r * 0.78f, bg.g * 0.78f, bg.b * 0.78f, 0.48f);
        cb.fadeDuration     = 0.07f;
        b.colors = cb;

        AddShadow(b.gameObject, 0.18f, 4f);
        AddBorder(b.gameObject, new Color(1f, 1f, 1f, 0.64f), new Vector2(2f, -2f));
        EnsureActionMarker(b, bg);

        var label = b.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.fontStyle = FontStyles.Bold;
            label.color     = fg;
            label.alignment = TextAlignmentOptions.Center;
            label.enableAutoSizing = true;
            label.fontSizeMin = 16f;
            label.fontSizeMax = maxFontSize;
        }

        if (b.GetComponent<UIButtonJuice>() == null)
            b.gameObject.AddComponent<UIButtonJuice>();
    }

    private static void EnsureActionMarker(Button button, Color bg)
    {
        var existing = button.transform.Find("ActionMarker");
        if (existing != null) existing.gameObject.SetActive(false);
    }

    private static float ColorDistance(Color a, Color b)
    {
        float dr = a.r - b.r;
        float dg = a.g - b.g;
        float db = a.b - b.b;
        return Mathf.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>
    /// インタラクト表示用のチップを組み立てる。
    /// 既存のTMPテキストを角丸チップ（影付き・文言に合わせて自動伸縮）へ移設し、
    /// 表示切り替え用のチップ本体GameObjectを返す。
    /// </summary>
    public static GameObject BuildInteractChip(TMP_Text text)
    {
        var parent = text.transform.parent;

        var chip = new GameObject("InteractChip", typeof(RectTransform));
        chip.transform.SetParent(parent, false);

        var rt = (RectTransform)chip.transform;
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, -152);

        // 3Dの上で浮くように、濃色の面＋太い白フチにする
        MenuSurface(chip, 26f, 3f);

        // 文言に合わせてチップが伸び縮みするようにレイアウトで包む
        var hlg = chip.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(26, 26, 13, 13);
        hlg.childAlignment         = TextAnchor.MiddleCenter;
        hlg.childControlWidth      = true;
        hlg.childControlHeight     = true;
        hlg.childForceExpandWidth  = false;
        hlg.childForceExpandHeight = false;

        var csf = chip.AddComponent<ContentSizeFitter>();
        csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

        // テキストをチップ内へ移設して整える
        text.transform.SetParent(chip.transform, false);
        text.fontSize  = 26f;
        text.enableAutoSizing = false;
        text.fontStyle = FontStyles.Bold;
        text.color     = Color.white;
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.rectTransform.localScale = Vector3.one;

        return chip;
    }

    /// <summary>
    /// 名前でGameObjectを探す（非アクティブ含む・全ロード済みシーン対象）。
    /// パネルが初期非表示でも見つけられるようにGameObject.Findの代わりに使う。
    /// </summary>
    public static GameObject FindDeep(string name)
    {
        for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                var found = FindDeepIn(root.transform, name);
                if (found != null) return found.gameObject;
            }
        }
        return null;
    }

    private static Transform FindDeepIn(Transform t, string name)
    {
        if (t.name == name) return t;
        for (int i = 0; i < t.childCount; i++)
        {
            var found = FindDeepIn(t.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>名前が一致する全GameObjectを探す（非アクティブ含む）。同名UIが複数あるとき用。</summary>
    public static System.Collections.Generic.List<GameObject> FindAllDeep(string name)
    {
        var results = new System.Collections.Generic.List<GameObject>();
        for (int s = 0; s < UnityEngine.SceneManagement.SceneManager.sceneCount; s++)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;

            foreach (var root in scene.GetRootGameObjects())
                CollectDeep(root.transform, name, results);
        }
        return results;
    }

    private static void CollectDeep(Transform t, string name,
        System.Collections.Generic.List<GameObject> results)
    {
        if (t.name == name) results.Add(t.gameObject);
        for (int i = 0; i < t.childCount; i++)
            CollectDeep(t.GetChild(i), name, results);
    }
}

/// <summary>
/// ボタンの手応えを作る演出。
///  ・ホバー/選択：ひとまわり大きくなり、天面がわずかに浮く
///  ・プレス     ：天面が土台へ沈む（厚みが消える）
///  ・リリース   ：バネで跳ね返る（わずかにオーバーシュート）
/// UITheme.StylePill が Cap を渡すと厚みの沈み込みが有効になる。
/// Capが無いフラットなボタン（UITheme.StyleButton）ではスケールだけが動く。
/// </summary>
public class UIButtonJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler,
    ISelectHandler, IDeselectHandler
{
    private const float Stiffness = 620f;
    private const float Damping   = 26f;

    private RectTransform cap;
    private float depth;

    private float scale = 1f;
    private float scaleVelocity;
    private float sink;
    private float sinkVelocity;

    private bool hovered;
    private bool pressed;
    private bool selected;

    private Outline border;
    private Color defaultBorder;

    /// <summary>厚みのあるボタンの天面と厚み量を登録する。</summary>
    public void SetCap(RectTransform capRect, float capDepth)
    {
        cap = capRect;
        depth = capDepth;
    }

    private void Awake()
    {
        border = GetComponent<Outline>();
        if (border != null) defaultBorder = border.effectColor;
    }

    public void OnPointerEnter(PointerEventData e) => hovered = true;
    public void OnPointerExit(PointerEventData e)  { hovered = false; pressed = false; }
    public void OnPointerDown(PointerEventData e)  => pressed = true;
    public void OnPointerUp(PointerEventData e)    => pressed = false;

    public void OnSelect(BaseEventData eventData)
    {
        selected = true;
        if (border != null && border.enabled)
            border.effectColor = new Color(UITheme.Sun.r, UITheme.Sun.g, UITheme.Sun.b, 0.95f);
    }

    public void OnDeselect(BaseEventData eventData)
    {
        selected = false;
        pressed = false;
        if (border != null) border.effectColor = defaultBorder;
    }

    private void OnDisable()
    {
        hovered = pressed = selected = false;
        scale = 1f;
        scaleVelocity = 0f;
        sink = 0f;
        sinkVelocity = 0f;
        transform.localScale = Vector3.one;
        transform.localRotation = Quaternion.identity;
        ApplySink();
        if (border != null) border.effectColor = defaultBorder;
    }

    private void Update()
    {
        bool lifted = hovered || selected;

        float targetScale = pressed ? 0.975f : lifted ? 1.05f : 1f;
        float targetSink  = pressed ? depth : lifted ? -2f : 0f;

        Spring(ref scale, ref scaleVelocity, targetScale);
        Spring(ref sink, ref sinkVelocity, targetSink);

        transform.localScale = new Vector3(scale, scale, 1f);
        ApplySink();
    }

    private static void Spring(ref float value, ref float velocity, float target)
    {
        float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
        velocity += (target - value) * Stiffness * dt;
        velocity *= Mathf.Exp(-Damping * dt);
        value += velocity * dt;
    }

    private void ApplySink()
    {
        if (cap == null) return;
        cap.offsetMin = new Vector2(cap.offsetMin.x, depth - sink);
        cap.offsetMax = new Vector2(cap.offsetMax.x, -sink);
    }
}
