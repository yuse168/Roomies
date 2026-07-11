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

    public static readonly Color Panel      = new Color(0.075f, 0.085f, 0.13f, 0.95f); // カード背景
    public static readonly Color PanelSoft  = new Color(1f, 1f, 1f, 0.06f);            // 行の淡い背景
    public static readonly Color Accent     = new Color(1.00f, 0.62f, 0.12f);          // オレンジ
    public static readonly Color Blue       = new Color(0.16f, 0.66f, 0.94f);
    public static readonly Color Green      = new Color(0.24f, 0.75f, 0.42f);
    public static readonly Color Red        = new Color(0.88f, 0.33f, 0.33f);
    public static readonly Color Gold       = new Color(1.00f, 0.84f, 0.29f);
    public static readonly Color TextMain   = new Color(0.96f, 0.97f, 1.00f);
    public static readonly Color TextSub    = new Color(0.62f, 0.67f, 0.78f);
    public static readonly Color DarkButton = new Color(0.16f, 0.18f, 0.26f);

    // ================================================================
    // スプライト生成（画像アセット不要）
    // ================================================================

    private static Sprite roundedSprite;

    /// <summary>角丸矩形スプライト（9-slice対応）。全ランタイムUIで共有。</summary>
    public static Sprite RoundedSprite
    {
        get
        {
            if (roundedSprite != null) return roundedSprite;

            const int size   = 64;
            const int radius = 20;

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

            roundedSprite = Sprite.Create(
                tex,
                new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(radius + 4, radius + 4, radius + 4, radius + 4)); // 9-slice境界

            return roundedSprite;
        }
    }

    /// <summary>縦グラデーションのスプライト（メニュー背景など）。</summary>
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
        img.sprite = RoundedSprite;
        img.type   = Image.Type.Sliced;
        img.color  = Panel;

        AddShadow(go);
        return img;
    }

    /// <summary>ドロップシャドウを付ける（Imageなどに）。</summary>
    public static void AddShadow(GameObject go)
    {
        if (go.GetComponent<Shadow>() != null) return;
        var shadow = go.AddComponent<Shadow>();
        shadow.effectColor    = new Color(0f, 0f, 0f, 0.45f);
        shadow.effectDistance = new Vector2(0f, -5f);
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
        if (text == null) return;
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
        img.sprite = RoundedSprite;
        img.type   = Image.Type.Sliced;
        img.color  = Color.white; // 実際の色はButtonのColorTintで乗せる

        b.transition    = Selectable.Transition.ColorTint;
        b.targetGraphic = img;

        var cb = b.colors;
        cb.normalColor      = bg;
        cb.highlightedColor = Color.Lerp(bg, Color.white, 0.18f);
        cb.pressedColor     = Color.Lerp(bg, Color.black, 0.22f);
        cb.selectedColor    = bg;
        cb.disabledColor    = new Color(bg.r, bg.g, bg.b, 0.30f);
        cb.fadeDuration     = 0.08f;
        b.colors = cb;

        AddShadow(b.gameObject);

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
        rt.anchoredPosition = new Vector2(0, -150);

        var img = chip.AddComponent<Image>();
        img.sprite = RoundedSprite;
        img.type   = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1.6f;
        img.color  = Panel;
        AddShadow(chip);

        // 文言に合わせてチップが伸び縮みするようにレイアウトで包む
        var hlg = chip.AddComponent<HorizontalLayoutGroup>();
        hlg.padding                = new RectOffset(20, 20, 10, 10);
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
        text.fontStyle = FontStyles.Bold;
        text.color     = TextMain;
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
/// ボタンのホバー/プレスでふわっとスケールする演出。
/// UITheme.StyleButtonが自動で付ける。
/// </summary>
public class UIButtonJuice : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private float targetScale = 1f;

    public void OnPointerEnter(PointerEventData e) { targetScale = 1.05f; }
    public void OnPointerExit(PointerEventData e)  { targetScale = 1f; }
    public void OnPointerDown(PointerEventData e)  { targetScale = 0.96f; }
    public void OnPointerUp(PointerEventData e)    { targetScale = 1.05f; }

    private void OnDisable()
    {
        targetScale = 1f;
        transform.localScale = Vector3.one;
    }

    private void Update()
    {
        transform.localScale = Vector3.Lerp(
            transform.localScale,
            Vector3.one * targetScale,
            Time.unscaledDeltaTime * 12f);
    }
}
