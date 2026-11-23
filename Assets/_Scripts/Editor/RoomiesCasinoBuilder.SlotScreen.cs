#if UNITY_EDITOR
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Art = RoomiesCasinoArt;

/// <summary>
/// スロットの画面（Anchor_Screen の Canvas の中身）と、筐体のランプを組み立てる（Roomies/Casino/2 Build Gimmick Prefabs）。
///
/// 画面は 520×760 の Canvas。上から：題字 → リールの窓（3 本・見えるのは 3 段。真ん中の段が当たりのライン）→ 当たりの額 → 賭け金 → 結果 → 操作の案内。
/// その上に、演出の部品（暗くする板・走る光・フラッシュ・放射状の光・ロゴ・コイン・きらめき）を重ねてある。ふだんは隠してあり、SlotScreen が出し入れする。
/// 絵は Assets/_Art/Casino/Slot/（作り方は ArtSource/Casino/Screen/make_casino_screen_art.py）。
/// 位置・大きさ・色は、作ったあとプレハブの中で直してよい（配置ツールを実行し直すと、ここの値に戻る）。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    const string SlotArtDir = "Assets/_Art/Casino/Slot";
    static readonly string[] SlotSymbolNames = { "cherry", "bell", "bar", "crown", "seven" };
    const float SlotCanvasW = 520f, SlotCanvasH = 760f;
    const float ReelCenterY = 78f, ReelCell = 110f, ReelColumnW = 146f, ReelGap = 156f;

    // リールに並ぶ絵の順（見た目だけ。当たりの確率は SlotMachine の重みが決める）
    static readonly int[][] SlotStrips =
    {
        new[] { 0, 1, 2, 0, 3, 1, 0, 4, 2, 1, 0, 3, 1, 2, 0, 4 },
        new[] { 1, 0, 3, 2, 0, 1, 4, 0, 2, 1, 3, 0, 1, 0, 2, 4 },
        new[] { 2, 1, 0, 4, 1, 0, 3, 0, 1, 2, 0, 4, 3, 1, 0, 2 },
    };

    static Sprite SlotSprite(string name)
    {
        string path = $"{SlotArtDir}/{name}.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogError("[Casino] slot art missing: " + path + "（python ArtSource/Casino/Screen/make_casino_screen_art.py を実行）"); return null; }
        if (importer.textureType != TextureImporterType.Sprite || importer.spriteImportMode != SpriteImportMode.Single || !importer.mipmapEnabled
            || !importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
        {
            importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = true; importer.alphaIsTransparency = true; importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Image UiImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = go.GetComponent<Image>();
        image.sprite = sprite; image.color = color; image.raycastTarget = false;
        return image;
    }

    static RectTransform UiGroup(Transform parent, string name, Vector2 position, Vector2 size, bool mask = false)
    {
        var go = mask ? new GameObject(name, typeof(RectTransform), typeof(RectMask2D)) : new GameObject(name, typeof(RectTransform));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        return rect;
    }

    static TextMeshProUGUI UiText(Transform parent, string name, string text, Vector2 position, Vector2 size, float fontSize, Color color, TMP_FontAsset font, bool bold = true)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var t = go.GetComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.text = text; t.fontSize = fontSize; t.color = color; t.alignment = TextAlignmentOptions.Center;
        t.enableAutoSizing = true; t.fontSizeMin = fontSize * .6f; t.fontSizeMax = fontSize;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.raycastTarget = false; t.richText = true;
        if (bold) t.fontStyle = FontStyles.Bold;
        return t;
    }

    /// <summary>画面の中身を作り直し、SlotMachine と SlotScreen に参照を割り当てる。</summary>
    static void BuildSlotScreen(GameObject root, SlotMachine machine, Transform canvas)
    {
        // いまの文字のフォント（日本語が出る物）を引き継ぐ
        var oldText = canvas.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault(t => t.font != null);
        TMP_FontAsset font = oldText != null ? oldText.font : null;

        foreach (Transform c in canvas.Cast<Transform>().ToArray()) Object.DestroyImmediate(c.gameObject);
        var canvasRect = (RectTransform)canvas;
        canvasRect.sizeDelta = new Vector2(SlotCanvasW, SlotCanvasH);
        canvas.localScale = Vector3.one * (Art.SlotScreenW / SlotCanvasW);

        Sprite white = SlotSprite("fx_white"), glow = SlotSprite("fx_glow");
        Color gold = new Color(.95f, .74f, .32f), ink = new Color(.07f, .05f, .1f), cream = new Color(.97f, .93f, .84f);

        // 画面の外へはみ出す物（コイン・光）は、ここで切る
        RectTransform screen = UiGroup(canvas, "Screen", Vector2.zero, new Vector2(SlotCanvasW, SlotCanvasH), true);

        // ---- 背景（ふだん／フィーバー）
        UiImage(screen, "Background", SlotSprite("bg_normal"), Vector2.zero, new Vector2(SlotCanvasW, SlotCanvasH), Color.white);
        Image fever = UiImage(screen, "BackgroundFever", SlotSprite("bg_fever"), Vector2.zero, new Vector2(SlotCanvasW, SlotCanvasH), new Color(1, 1, 1, 0));

        // ---- 題字
        var title = UiImage(screen, "Title", SlotSprite("logo_title"), new Vector2(0, 312), new Vector2(400, 125), Color.white);
        title.preserveAspect = true;

        // ---- リールの窓
        Vector2 windowSize = new Vector2(ReelGap * 2 + ReelColumnW + 16, ReelCell * 3);
        UiImage(screen, "ReelFrame", white, new Vector2(0, ReelCenterY), windowSize + new Vector2(16, 16), gold);
        UiImage(screen, "ReelBack", white, new Vector2(0, ReelCenterY), windowSize, ink);
        var cells = new Image[15];
        Sprite[] symbols = SlotSymbolNames.Select(n => SlotSprite("sym_" + n)).ToArray();
        Sprite[] blurs = SlotSymbolNames.Select(n => SlotSprite("sym_" + n + "_blur")).ToArray();
        for (int r = 0; r < 3; r++)
        {
            float x = (r - 1) * ReelGap;
            UiImage(screen, "ReelStrip_" + (r + 1), white, new Vector2(x, ReelCenterY), new Vector2(ReelColumnW, ReelCell * 3), cream);
            // 上下の端を暗くして、丸いドラムに見せる
            RectTransform column = UiGroup(screen, "Reel_" + (r + 1), new Vector2(x, ReelCenterY), new Vector2(ReelColumnW, ReelCell * 3), true);
            for (int k = 0; k < 5; k++)
            {
                var cell = UiImage(column, "Cell_" + k, symbols[SlotStrips[r][(2 - k + 16) % 16]], new Vector2(0, (2 - k) * ReelCell), new Vector2(102, 102), Color.white);
                cell.preserveAspect = true;
                cells[r * 5 + k] = cell;
            }
            UiImage(column, "ShadeTop", glow, new Vector2(0, ReelCell * 1.5f), new Vector2(ReelColumnW * 1.6f, ReelCell * 1.3f), new Color(.1f, .06f, .12f, .75f));
            UiImage(column, "ShadeBottom", glow, new Vector2(0, -ReelCell * 1.5f), new Vector2(ReelColumnW * 1.6f, ReelCell * 1.3f), new Color(.1f, .06f, .12f, .75f));
        }
        // 当たりのライン（真ん中の段）の目印
        foreach (float sx in new[] { -1f, 1f })
        {
            var mark = UiImage(screen, sx < 0 ? "LineMark_L" : "LineMark_R", white, new Vector2(sx * (windowSize.x / 2 + 2), ReelCenterY), new Vector2(18, 18), new Color(1f, .4f, .7f));
            mark.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
        }

        // ---- 当たりの額・賭け金・結果・案内
        UiImage(screen, "WinPanel", white, new Vector2(0, -138), new Vector2(430, 78), new Color(.07f, .05f, .1f, .9f));
        UiText(screen, "WinLabel", "WIN", new Vector2(-150, -138), new Vector2(110, 50), 34, gold, font);
        var counter = UiText(screen, "WinCounter", "0", new Vector2(40, -138), new Vector2(260, 66), 58, new Color(.55f, .5f, .45f), font);
        counter.alignment = TextAlignmentOptions.Right;
        var bet = UiText(screen, "Bet Text", "BET 10 R", new Vector2(0, -206), new Vector2(430, 50), 36, cream, font);
        var result = UiText(screen, "ResultText", "READY TO PLAY", new Vector2(0, -256), new Vector2(450, 48), 30, cream, font);
        var feverText = UiText(screen, "FeverText", "", new Vector2(0, -300), new Vector2(450, 36), 24, new Color(1f, .85f, .3f), font);
        UiText(screen, "ControlHint", "[ E ]  SPIN   /   SCROLL  BET", new Vector2(0, -344), new Vector2(460, 30), 20, new Color(.8f, .76f, .86f), font, false);

        // ---- 演出の部品（ふだんは隠す）
        // リーチ：窓の上と下を暗くする板、右のリールの枠、走る光
        float top = ReelCenterY + windowSize.y / 2 + 8, bottom = ReelCenterY - windowSize.y / 2 - 8;
        Image dimTop = UiImage(screen, "DimTop", white, new Vector2(0, (top + SlotCanvasH / 2) / 2), new Vector2(SlotCanvasW, SlotCanvasH / 2 - top), new Color(0, 0, 0, 0));
        Image dimBottom = UiImage(screen, "DimBottom", white, new Vector2(0, (bottom - SlotCanvasH / 2) / 2), new Vector2(SlotCanvasW, bottom + SlotCanvasH / 2), new Color(0, 0, 0, 0));
        Image reachFrame = UiImage(screen, "ReachFrame", glow, new Vector2(ReelGap, ReelCenterY), new Vector2(ReelColumnW * 1.9f, ReelCell * 4.2f), new Color(1f, .35f, .55f, .8f));
        RectTransform sweepMask = UiGroup(screen, "SweepMask", new Vector2(0, ReelCenterY), windowSize, true);
        Image sweep = UiImage(sweepMask, "Sweep", SlotSprite("fx_sweep"), Vector2.zero, new Vector2(150, 520), new Color(1f, .95f, .8f, .55f));
        sweep.rectTransform.localRotation = Quaternion.Euler(0, 0, -18);
        Image reachLogo = UiImage(screen, "ReachLogo", SlotSprite("logo_reach"), new Vector2(0, 312), new Vector2(380, 160), Color.white);
        reachLogo.preserveAspect = true;

        // 当たり：ラインの光、放射状の光、ロゴ、きらめき、コイン、フラッシュ
        Image lineGlow = UiImage(screen, "WinLineGlow", glow, new Vector2(0, ReelCenterY), new Vector2(windowSize.x * 1.25f, 120), new Color(1f, .85f, .35f, .8f));
        Image line = UiImage(screen, "WinLine", white, new Vector2(0, ReelCenterY), new Vector2(windowSize.x, 8), new Color(1f, .95f, .6f, 1f));
        Image sunburst = UiImage(screen, "Sunburst", SlotSprite("fx_sunburst"), new Vector2(0, ReelCenterY), new Vector2(980, 980), new Color(1f, .86f, .4f, .85f));
        var sparkles = new Image[8];
        var sparklePos = new[] { new Vector2(-200, 250), new Vector2(190, 230), new Vector2(-220, 60), new Vector2(225, 20), new Vector2(-150, -110), new Vector2(160, -90), new Vector2(-40, 270), new Vector2(60, -200) };
        for (int i = 0; i < sparkles.Length; i++)
            sparkles[i] = UiImage(screen, "Sparkle_" + i, SlotSprite("fx_sparkle"), sparklePos[i], new Vector2(84, 84), new Color(1f, 1f, .9f, 1f));
        var coins = new Image[24];
        for (int i = 0; i < coins.Length; i++)
            coins[i] = UiImage(screen, "Coin_" + i, SlotSprite("fx_coin"), new Vector2(0, -330), new Vector2(62, 62), Color.white);
        Image logo = UiImage(screen, "Logo", SlotSprite("logo_bigwin"), new Vector2(0, ReelCenterY), new Vector2(480, 190), Color.white);
        logo.preserveAspect = true;
        Image flash = UiImage(screen, "Flash", white, Vector2.zero, new Vector2(SlotCanvasW, SlotCanvasH), new Color(1, 1, 1, 0));

        foreach (var g in new Graphic[] { dimTop, dimBottom, reachFrame, sweep, reachLogo, lineGlow, line, sunburst, logo, flash }) g.gameObject.SetActive(false);
        foreach (var g in sparkles.Concat(coins)) g.gameObject.SetActive(false);

        // ---- 筐体のランプ（画面の左右の金の縁に 4 つずつ）と、前を照らす明かり
        Transform oldLamps = root.transform.Find("Anchor_Lamps");
        if (oldLamps != null) Object.DestroyImmediate(oldLamps.gameObject);
        Transform lampRoot = Child(root.transform, "Anchor_Lamps");
        Mesh lampMesh = Art.Build("slot_lamp_glow", lp => lp.Lathe(new[] { (.034f, 0f), (.028f, .018f), (0f, .03f) }, 6, "warm", 0, false, false, true));
        var lamps = new GameObject[8];
        float lampX = Art.SlotScreenW / 2 + .055f, y0 = Art.SlotScreenY - Art.SlotScreenH / 2 + .1f, y1 = Art.SlotScreenY + Art.SlotScreenH / 2 - .1f;
        for (int i = 0; i < 8; i++)
        {
            // 左を下から上へ、続けて右を上から下へ（流れる順）
            bool left = i < 4;
            float y = Mathf.Lerp(y0, y1, (left ? i : 7 - i) / 3f);
            Transform lamp = Child(lampRoot, "Lamp_" + (i + 1), new Vector3(left ? lampX : -lampX, y, Art.SlotFrontZ + .05f));
            lamp.localRotation = Quaternion.Euler(90, 0, 0);
            lamps[i] = Art.Place(lamp, "Lit", lampMesh, default, 0, false, false);
        }
        var lightGo = new GameObject("Light");
        lightGo.transform.SetParent(lampRoot, false);
        lightGo.transform.localPosition = new Vector3(0, Art.SlotScreenY + .2f, Art.SlotFrontZ + .55f);
        var light = lightGo.AddComponent<Light>();
        light.type = LightType.Point; light.range = 2.6f; light.intensity = .6f; light.color = new Color(1f, .82f, .5f); light.shadows = LightShadows.None;

        // ---- 参照の割り当て
        var screenComp = root.GetComponent<SlotScreen>();
        if (screenComp == null) screenComp = root.AddComponent<SlotScreen>();
        var ss = new SerializedObject(screenComp);
        SetObjects(ss.FindProperty("symbolSprites"), symbols);
        SetObjects(ss.FindProperty("blurSprites"), blurs);
        SetObjects(ss.FindProperty("reelCells"), cells);
        ss.FindProperty("cellHeight").floatValue = ReelCell;
        SetIntArray(ss.FindProperty("reelStrips"), SlotStrips.SelectMany(a => a).ToArray());
        ss.FindProperty("winLine").objectReferenceValue = line;
        ss.FindProperty("winLineGlow").objectReferenceValue = lineGlow;
        SetObjects(ss.FindProperty("dimPanels"), new[] { dimTop, dimBottom });
        ss.FindProperty("reachFrame").objectReferenceValue = reachFrame;
        ss.FindProperty("sweep").objectReferenceValue = sweep.rectTransform;
        ss.FindProperty("sweepTravel").floatValue = windowSize.x + 160;
        ss.FindProperty("reachLogo").objectReferenceValue = reachLogo;
        ss.FindProperty("flash").objectReferenceValue = flash;
        ss.FindProperty("sunburst").objectReferenceValue = sunburst;
        ss.FindProperty("logo").objectReferenceValue = logo;
        ss.FindProperty("logoWin").objectReferenceValue = SlotSprite("logo_win");
        ss.FindProperty("logoBigWin").objectReferenceValue = SlotSprite("logo_bigwin");
        ss.FindProperty("logoJackpot").objectReferenceValue = SlotSprite("logo_jackpot");
        ss.FindProperty("logoFever").objectReferenceValue = SlotSprite("logo_fever");
        SetObjects(ss.FindProperty("coins"), coins);
        SetObjects(ss.FindProperty("sparkles"), sparkles);
        ss.FindProperty("winCounter").objectReferenceValue = counter;
        ss.FindProperty("feverBackground").objectReferenceValue = fever;
        SetObjects(ss.FindProperty("lampsLit"), lamps);
        ss.FindProperty("lampLight").objectReferenceValue = light;
        ss.ApplyModifiedPropertiesWithoutUndo();

        var so = new SerializedObject(machine);
        so.FindProperty("resultText").objectReferenceValue = result;
        so.FindProperty("feverText").objectReferenceValue = feverText;
        so.FindProperty("betText").objectReferenceValue = bet;
        so.FindProperty("screen").objectReferenceValue = screenComp;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static void SetObjects(SerializedProperty array, Object[] values)
    {
        array.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
#endif
