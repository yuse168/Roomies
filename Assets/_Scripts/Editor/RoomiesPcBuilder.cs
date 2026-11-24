#if UNITY_EDITOR
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 部屋の PC（2026/11/25）の配置ツール。実行時には動かない。
///   Roomies/PC/1 Build Prefab        … Assets/_Prefabs/Room/RoomComputer.prefab（見て E を押す当たり判定・Anchor_Screen）
///   Roomies/PC/2 Place In Open Scene … GameRoom の机に RoomComputer、シーンの一番上に StockMarket（株の市場。1 つ）
///   Roomies/PC/3 Build Desktop UI    … GameRoomUI に PC の画面（RoomComputerUI）を作り直し、GameUIRefs につなぐ
/// 画面の部品の位置・色・文字は、ここの数字を直して 3 を実行し直す（シーンで直した分は消える）。
/// 角の丸い板は Assets/_Art/UI/Pc/ の絵（9 スライス）。文字は HUD と同じ MPLUSU-Black SDF。
/// </summary>
internal static class RoomiesPcBuilder
{
    const string PrefabPath = "Assets/_Prefabs/Room/RoomComputer.prefab";
    const string ArtDir = "Assets/_Art/UI/Pc";
    const string FontPath = "Assets/TextMesh Pro/Resources/Fonts & Materials/MPLUSU-Black SDF.asset";

    // 机（部屋の背景 RoomiesRoomRestyleBuilder.Desk と同じ位置と向き）
    static readonly Vector3 DeskPosition = new Vector3(22.33f, 3.2725f, 16.9f);
    const float DeskYaw = -90f;

    // art_rules.md の色（UI の黒・紫の差し色・ピンクのネオン・紙・ソファの黄・キッチンの緑）
    static readonly Color Ink = Hex("15151B"), Purple = Hex("8E69BE"), Pink = Hex("F765BA"), Paper = Hex("F4ECDA"), PaperDark = Hex("D8CEB5"),
        Sofa = Hex("D3A843"), Kitchen = Hex("52755C"), Wall = Hex("2D2445"), Cream = Hex("FFF8EC"), InkSoft = Hex("4A4458"), Warm = Hex("FFB35C");

    static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }

    // ================================================================ 1. プレハブ
    [MenuItem("Roomies/PC/1 Build Prefab")]
    internal static void BuildPrefab()
    {
        System.IO.Directory.CreateDirectory("Assets/_Prefabs/Room");
        var root = new GameObject("RoomComputer");
        try
        {
            root.AddComponent<NetworkObject>();
            var pc = root.AddComponent<RoomComputer>();
            // 見て E を押す所：モニターとキーボードのまわり（机のローカル。机の天板より上だけ）
            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(-.1f, .98f, -.04f);
            box.size = new Vector3(.86f, .44f, .5f);
            var anchor = new GameObject("Anchor_Screen").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(-.1f, 1.085f, -.131f);
            new GameObject("Visual").transform.SetParent(root.transform, false);
            var so = new SerializedObject(pc);
            so.FindProperty("anchorScreen").objectReferenceValue = anchor;
            so.FindProperty("interactDistance").floatValue = 2.5f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { Object.DestroyImmediate(root); }
        Debug.Log("[PC] prefab built");
    }

    // ================================================================ 2. シーンに置く
    [MenuItem("Roomies/PC/2 Place In Open Scene")]
    internal static void PlaceInScene()
    {
        Scene scene = SceneManager.GetSceneByName("GameRoom");
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogWarning("[PC] GameRoom を開いて実行する"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null) BuildPrefab();
        RoomiesCasinoBuilder.PlacePrefab(scene, PrefabPath, "RoomComputer", DeskPosition, DeskYaw);

        var market = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "StockMarket");
        if (market == null) { market = new GameObject("StockMarket"); SceneManager.MoveGameObjectToScene(market, scene); }
        if (market.GetComponent<NetworkObject>() == null) market.AddComponent<NetworkObject>();
        if (market.GetComponent<StockMarket>() == null) market.AddComponent<StockMarket>();
        EditorUtility.SetDirty(market);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PC] placed in GameRoom");
    }

    // ================================================================ 3. 画面
    static Sprite roundS, roundL, circle;
    static TMP_FontAsset font;

    static Sprite LoadSprite(string file, int border)
    {
        string path = $"{ArtDir}/{file}";
        var imp = (TextureImporter)AssetImporter.GetAtPath(path);
        if (imp != null && (imp.textureType != TextureImporterType.Sprite || imp.spriteBorder.x != border || imp.mipmapEnabled))
        {
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = new Vector4(border, border, border, border);
            imp.mipmapEnabled = false;
            imp.alphaIsTransparency = true;
            imp.filterMode = FilterMode.Bilinear;
            imp.textureCompression = TextureImporterCompression.Uncompressed;
            imp.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Vector2? pivot = null)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot ?? new Vector2(.5f, .5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    static RectTransform Stretch(Transform parent, string name, float inset = 0)
    {
        var rt = Rect(parent, name, new Vector2(.5f, .5f), Vector2.zero, Vector2.zero);
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(-inset, -inset);
        return rt;
    }

    static Image Panel(RectTransform rt, Color color, Sprite sprite, float ppu = 1f)
    {
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite; img.type = Image.Type.Sliced; img.color = color; img.pixelsPerUnitMultiplier = ppu;
        img.raycastTarget = false;
        return img;
    }

    static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, TextAlignmentOptions align, Vector2 anchor, Vector2 pos, Vector2 box, Vector2? pivot = null)
    {
        var rt = Rect(parent, name, anchor, pos, box, pivot);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = font; t.text = text; t.fontSize = size; t.color = color; t.alignment = align;
        t.textWrappingMode = TextWrappingModes.NoWrap; t.overflowMode = TextOverflowModes.Overflow; t.raycastTarget = false;   // Ellipsis だと、行の高さが枠より少し高いだけで行ごと消える
        return t;
    }

    static (Button button, TextMeshProUGUI label) Pill(Transform parent, string name, string label, Color color, Color textColor, Vector2 anchor, Vector2 pos, Vector2 size, float fontSize = 24)
    {
        var rt = Rect(parent, name, anchor, pos, size);
        var img = Panel(rt, color, circle);
        img.raycastTarget = true;
        var b = rt.gameObject.AddComponent<Button>();
        var cb = b.colors;
        cb.normalColor = Color.white; cb.highlightedColor = new Color(1f, .93f, .85f); cb.pressedColor = new Color(.85f, .8f, .78f);
        cb.disabledColor = new Color(.62f, .6f, .64f, .55f); cb.fadeDuration = .08f;
        b.colors = cb;
        b.targetGraphic = img;
        var t = Text(rt, "Label", label, fontSize, textColor, TextAlignmentOptions.Center, new Vector2(.5f, .5f), Vector2.zero, size);
        return (b, t);
    }

    [MenuItem("Roomies/PC/3 Build Desktop UI")]
    internal static void BuildDesktopUI()
    {
        Scene scene = SceneManager.GetSceneByName("GameRoomUI");
        if (!scene.IsValid() || !scene.isLoaded) { Debug.LogWarning("[PC] GameRoomUI を開いて実行する"); return; }
        roundS = LoadSprite("pc_round_s.png", 20);
        roundL = LoadSprite("pc_round_l.png", 44);
        circle = LoadSprite("pc_circle.png", 31);
        font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

        var old = scene.GetRootGameObjects().FirstOrDefault(g => g.name == "RoomComputerUI");
        if (old != null) Object.DestroyImmediate(old);
        var root = new GameObject("RoomComputerUI", typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(root, scene);
        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9200;   // 夜のお知らせの帯（NightEventUI 9000）より上、日の切り替え（10000）より下
        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        root.AddComponent<GraphicRaycaster>();
        var group = root.AddComponent<CanvasGroup>();
        var ui = root.AddComponent<RoomComputerUI>();
        var so = new SerializedObject(ui);
        so.FindProperty("group").objectReferenceValue = group;

        // 部屋（後ろ）を暗く
        var dim = Stretch(root.transform, "Dim");
        Panel(dim, new Color(Ink.r, Ink.g, Ink.b, .86f), null).raycastTarget = true;

        // モニターの枠と画面
        var monitor = Rect(root.transform, "Monitor", new Vector2(.5f, .5f), Vector2.zero, new Vector2(1840, 1010));
        Panel(monitor, Hex("1E1C26"), roundL);
        so.FindProperty("monitor").objectReferenceValue = monitor;
        var screen = Stretch(monitor, "Screen", 22);
        Panel(screen, Wall, roundL, 1.3f);
        // 壁紙：部屋の暖色の明かりの帯と、ゆるい字
        var glow = Rect(screen, "Wallpaper glow", new Vector2(.5f, 0f), new Vector2(0, 150), new Vector2(1796, 300));
        Panel(glow, new Color(Warm.r, Warm.g, Warm.b, .07f), roundL, 1.3f);
        Text(screen, "Wallpaper logo", "ROOMIES OS", 120, new Color(1f, 1f, 1f, .06f), TextAlignmentOptions.BottomRight, new Vector2(1f, 0f), new Vector2(-50, 110), new Vector2(1000, 160), new Vector2(1f, 0f));

        // デスクトップのアイコン（左上に縦に並べる）
        string[] appNames = { "家具ショップ", "株トレード" };
        string[] appGlyph = { "家", "株" };
        Color[] appColor = { Sofa, Kitchen };
        var icons = new Button[2];
        for (int i = 0; i < 2; i++)
        {
            var icon = Rect(screen, "Icon " + appNames[i], new Vector2(0f, 1f), new Vector2(110, -120 - i * 210), new Vector2(150, 190));
            var hit = icon.gameObject.AddComponent<Image>(); hit.color = new Color(1, 1, 1, 0); hit.raycastTarget = true;
            var tile = Rect(icon, "Tile", new Vector2(.5f, 1f), new Vector2(0, -60), new Vector2(120, 120));
            Panel(tile, appColor[i], roundL, 1.6f);
            Text(tile, "Glyph", appGlyph[i], 70, Cream, TextAlignmentOptions.Center, new Vector2(.5f, .5f), new Vector2(0, 2), new Vector2(120, 120));
            Text(icon, "Name", appNames[i], 22, Cream, TextAlignmentOptions.Center, new Vector2(.5f, 0f), new Vector2(0, 18), new Vector2(170, 34));
            var b = icon.gameObject.AddComponent<Button>();
            b.targetGraphic = tile.GetComponent<Image>();
            icons[i] = b;
        }
        SetArray(so.FindProperty("appIcons"), icons);

        // タスクバー
        var bar = Rect(screen, "Taskbar", new Vector2(.5f, 0f), new Vector2(0, 46), new Vector2(1740, 72));
        Panel(bar, new Color(Ink.r, Ink.g, Ink.b, .92f), circle);
        var os = Rect(bar, "OS", new Vector2(0f, .5f), new Vector2(120, 0), new Vector2(200, 52));
        Panel(os, Purple, circle);
        Text(os, "Label", "ROOMIES OS", 22, Cream, TextAlignmentOptions.Center, new Vector2(.5f, .5f), Vector2.zero, new Vector2(200, 52));
        var marks = new GameObject[2];
        for (int i = 0; i < 2; i++)
        {
            var m = Rect(bar, "Running " + appNames[i], new Vector2(0f, .5f), new Vector2(330 + i * 200, 0), new Vector2(180, 48));
            Panel(m, new Color(appColor[i].r, appColor[i].g, appColor[i].b, .9f), circle);
            Text(m, "Label", appNames[i], 20, Cream, TextAlignmentOptions.Center, new Vector2(.5f, .5f), Vector2.zero, new Vector2(180, 48));
            m.gameObject.SetActive(false);
            marks[i] = m.gameObject;
        }
        SetArray(so.FindProperty("taskbarMarks"), marks);
        so.FindProperty("moneyText").objectReferenceValue = Text(bar, "Money", "共同口座  ¥0", 24, Hex("9BE38B"), TextAlignmentOptions.Right, new Vector2(1f, .5f), new Vector2(-560, 0), new Vector2(380, 50), new Vector2(1f, .5f));
        so.FindProperty("clockText").objectReferenceValue = Text(bar, "Clock", "DAY 1 朝  03:00", 24, Cream, TextAlignmentOptions.Right, new Vector2(1f, .5f), new Vector2(-250, 0), new Vector2(290, 50), new Vector2(1f, .5f));
        var close = Pill(bar, "Close", "とじる  Esc", Pink, Cream, new Vector2(1f, .5f), new Vector2(-118, 0), new Vector2(210, 52), 22);
        so.FindProperty("closeButton").objectReferenceValue = close.button;

        // アプリの窓（真ん中）
        var windows = new GameObject[2];
        var windowClose = new Button[2];
        RectTransform Window(int i, string title, Color color)
        {
            var w = Rect(screen, "Window " + title, new Vector2(.5f, .5f), new Vector2(70, 30), new Vector2(1560, 820));
            var shadow = Stretch(w, "Shadow");
            shadow.offsetMin += new Vector2(0, -10); shadow.offsetMax += new Vector2(0, -10);
            Panel(shadow, new Color(0, 0, 0, .35f), roundL, 1.2f);   // 影（ぼかさず、少し下へずらすだけ）
            var body = Stretch(w, "Body");
            Panel(body, Paper, roundL, 1.2f).raycastTarget = true;
            var head = Rect(body, "Title bar", new Vector2(.5f, 1f), new Vector2(0, -38), new Vector2(1560, 76));
            Panel(head, color, roundL, 1.2f);
            var headFill = Rect(head, "Title bar fill", new Vector2(.5f, 0f), new Vector2(0, 14), new Vector2(1560, 28));   // 下側の角は四角に
            Panel(headFill, color, null);
            Text(head, "Title", title, 32, Cream, TextAlignmentOptions.Left, new Vector2(0f, .5f), new Vector2(36, 0), new Vector2(800, 60), new Vector2(0f, .5f));
            var x = Pill(head, "Close", "×", new Color(1, 1, 1, .25f), Cream, new Vector2(1f, .5f), new Vector2(-46, 0), new Vector2(52, 52), 30);
            windowClose[i] = x.button;
            windows[i] = w.gameObject;
            w.gameObject.SetActive(false);
            return body;
        }

        // ---- 家具ショップ：4 列 × 3 段のカード
        var shop = Window(0, "家具ショップ　─　夜のあいだに注文、翌朝から効く", Sofa);
        var cards = so.FindProperty("furnitureCards");
        int cardCount = Mathf.Max(12, FurnitureCatalog.Count);
        cards.arraySize = cardCount;
        for (int i = 0; i < cardCount; i++)
        {
            int col = i % 4, row = i / 4;
            var card = Rect(shop, "Card " + i, new Vector2(0f, 1f), new Vector2(40 + col * 375, -106 - row * 212), new Vector2(355, 196), new Vector2(0f, 1f));
            Panel(card, Cream, roundL, 2f);
            var sw = Rect(card, "Swatch", new Vector2(0f, .5f), new Vector2(70, 18), new Vector2(104, 104));
            var swImg = Panel(sw, Sofa, roundL, 2.4f);
            var initial = Text(sw, "Initial", "?", 52, new Color(1, 1, 1, .92f), TextAlignmentOptions.Center, new Vector2(.5f, .5f), Vector2.zero, new Vector2(104, 104));
            var title = Text(card, "Name", "家具", 24, Ink, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(136, -40), new Vector2(205, 36), new Vector2(0f, .5f));
            var eff = Text(card, "Effect", "効果", 16, InkSoft, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(136, -78), new Vector2(205, 28), new Vector2(0f, .5f));
            var price = Text(card, "Price", "¥0", 26, Hex("B8552E"), TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(136, -114), new Vector2(205, 36), new Vector2(0f, .5f));
            var buy = Pill(card, "Buy", "注文する", Purple, Cream, new Vector2(1f, 0f), new Vector2(-92, 34), new Vector2(160, 46), 20);
            var e = cards.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("root").objectReferenceValue = card.gameObject;
            e.FindPropertyRelative("swatch").objectReferenceValue = swImg;
            e.FindPropertyRelative("initial").objectReferenceValue = initial;
            e.FindPropertyRelative("title").objectReferenceValue = title;
            e.FindPropertyRelative("price").objectReferenceValue = price;
            e.FindPropertyRelative("effect").objectReferenceValue = eff;
            e.FindPropertyRelative("buy").objectReferenceValue = buy.button;
            e.FindPropertyRelative("buyLabel").objectReferenceValue = buy.label;
        }
        var shopStatus = Rect(shop, "Status", new Vector2(.5f, 0f), new Vector2(0, 42), new Vector2(1480, 54));
        Panel(shopStatus, PaperDark, circle);
        so.FindProperty("furnitureStatus").objectReferenceValue = Text(shopStatus, "Text", "", 22, Ink, TextAlignmentOptions.Center, new Vector2(.5f, .5f), Vector2.zero, new Vector2(1440, 50));

        // ---- 株トレード：左に銘柄の表、右に選んだ銘柄
        var st = Window(1, "株トレード　─　町の会社の株を、共同口座で売り買い", Purple);
        var news = Rect(st, "News", new Vector2(.5f, 1f), new Vector2(0, -112), new Vector2(1480, 50));
        Panel(news, Ink, circle);
        so.FindProperty("newsText").objectReferenceValue = Text(news, "Text", "ニュース：", 21, Warm, TextAlignmentOptions.Left, new Vector2(0f, .5f), new Vector2(28, 0), new Vector2(1420, 46), new Vector2(0f, .5f));
        var rows = so.FindProperty("stockRows");
        rows.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            var r = Rect(st, "Row " + i, new Vector2(0f, 1f), new Vector2(40, -156 - i * 92), new Vector2(560, 82), new Vector2(0f, 1f));
            var bg = Panel(r, new Color(1, 1, 1, .6f), roundL, 2.2f);
            bg.raycastTarget = true;
            var b = r.gameObject.AddComponent<Button>();
            b.targetGraphic = bg;
            var e = rows.GetArrayElementAtIndex(i);
            e.FindPropertyRelative("button").objectReferenceValue = b;
            e.FindPropertyRelative("background").objectReferenceValue = bg;
            e.FindPropertyRelative("code").objectReferenceValue = Text(r, "Code", "AAA", 16, InkSoft, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(22, -22), new Vector2(120, 24), new Vector2(0f, .5f));
            e.FindPropertyRelative("title").objectReferenceValue = Text(r, "Name", "会社", 24, Ink, TextAlignmentOptions.Left, new Vector2(0f, 0f), new Vector2(22, 28), new Vector2(300, 36), new Vector2(0f, .5f));
            e.FindPropertyRelative("holding").objectReferenceValue = Text(r, "Holding", "", 16, Purple, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(110, -22), new Vector2(160, 24), new Vector2(0f, .5f));
            e.FindPropertyRelative("price").objectReferenceValue = Text(r, "Price", "¥0", 28, Ink, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-22, -30), new Vector2(200, 36), new Vector2(1f, .5f));
            e.FindPropertyRelative("change").objectReferenceValue = Text(r, "Change", "+0.0%", 18, InkSoft, TextAlignmentOptions.Right, new Vector2(1f, 0f), new Vector2(-22, 20), new Vector2(160, 28), new Vector2(1f, .5f));
        }
        // 右側
        var detail = Rect(st, "Detail", new Vector2(1f, 1f), new Vector2(-40, -156), new Vector2(880, 556), new Vector2(1f, 1f));
        Panel(detail, Cream, roundL, 1.4f);
        so.FindProperty("detailTitle").objectReferenceValue = Text(detail, "Name", "会社", 32, Ink, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(30, -40), new Vector2(560, 46), new Vector2(0f, .5f));
        so.FindProperty("detailPrice").objectReferenceValue = Text(detail, "Price", "¥0", 52, Ink, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-30, -46), new Vector2(300, 64), new Vector2(1f, .5f));
        so.FindProperty("detailChange").objectReferenceValue = Text(detail, "Change", "+0.0%", 20, InkSoft, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-30, -94), new Vector2(360, 30), new Vector2(1f, .5f));
        var chartBox = Rect(detail, "Chart box", new Vector2(.5f, 1f), new Vector2(0, -250), new Vector2(820, 250));
        Panel(chartBox, Wall, roundL, 1.6f);
        var chartRt = Stretch(chartBox, "Chart", 18);
        var chart = chartRt.gameObject.AddComponent<UILineChart>();
        chart.raycastTarget = false;
        so.FindProperty("chart").objectReferenceValue = chart;
        Text(chartBox, "Caption", "値動き（新しいのが右）", 16, new Color(1, 1, 1, .45f), TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(18, -18), new Vector2(400, 24), new Vector2(0f, .5f));
        so.FindProperty("detailHolding").objectReferenceValue = Text(detail, "Holding", "持ち株 0 株", 21, Ink, TextAlignmentOptions.Left, new Vector2(0f, 1f), new Vector2(30, -406), new Vector2(560, 32), new Vector2(0f, .5f));
        so.FindProperty("detailProfit").objectReferenceValue = Text(detail, "Profit", "", 26, Ink, TextAlignmentOptions.Right, new Vector2(1f, 1f), new Vector2(-30, -406), new Vector2(260, 34), new Vector2(1f, .5f));
        var buyColor = Hex("DC405C"); var sellColor = Hex("2C85DB");
        so.FindProperty("buyOne").objectReferenceValue = Pill(detail, "Buy 1", "買う 1 株", buyColor, Cream, new Vector2(0f, 0f), new Vector2(110, 46), new Vector2(156, 56), 21).button;
        so.FindProperty("buyFive").objectReferenceValue = Pill(detail, "Buy 5", "買う 5 株", buyColor, Cream, new Vector2(0f, 0f), new Vector2(276, 46), new Vector2(156, 56), 21).button;
        so.FindProperty("sellOne").objectReferenceValue = Pill(detail, "Sell 1", "売る 1 株", sellColor, Cream, new Vector2(1f, 0f), new Vector2(-442, 46), new Vector2(156, 56), 21).button;
        so.FindProperty("sellFive").objectReferenceValue = Pill(detail, "Sell 5", "売る 5 株", sellColor, Cream, new Vector2(1f, 0f), new Vector2(-276, 46), new Vector2(156, 56), 21).button;
        so.FindProperty("sellAll").objectReferenceValue = Pill(detail, "Sell all", "全部売る", Hex("1F5E9E"), Cream, new Vector2(1f, 0f), new Vector2(-110, 46), new Vector2(156, 56), 21).button;
        so.FindProperty("stockStatus").objectReferenceValue = Text(detail, "Status", "", 20, Purple, TextAlignmentOptions.Center, new Vector2(.5f, 0f), new Vector2(0, 104), new Vector2(820, 30));
        var total = Rect(st, "Total", new Vector2(.5f, 0f), new Vector2(0, 48), new Vector2(1480, 56));
        Panel(total, PaperDark, circle);
        so.FindProperty("totalText").objectReferenceValue = Text(total, "Text", "", 24, Ink, TextAlignmentOptions.Left, new Vector2(0f, .5f), new Vector2(30, 0), new Vector2(900, 50), new Vector2(0f, .5f));
        so.FindProperty("feeNote").objectReferenceValue = Text(total, "Fee", "", 16, InkSoft, TextAlignmentOptions.Right, new Vector2(1f, .5f), new Vector2(-30, 0), new Vector2(640, 40), new Vector2(1f, .5f));

        SetArray(so.FindProperty("appWindows"), windows);
        SetArray(so.FindProperty("windowCloseButtons"), windowClose);
        so.ApplyModifiedPropertiesWithoutUndo();

        // 閉じているときは見えない（Awake でも閉じる）
        group.alpha = 0f; group.interactable = false; group.blocksRaycasts = false;

        // マップ側（RoomComputer）から使うので、受け渡し口につなぐ
        var refs = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<GameUIRefs>(true)).FirstOrDefault();
        if (refs != null)
        {
            var rso = new SerializedObject(refs);
            rso.FindProperty("computerUI").objectReferenceValue = ui;
            rso.ApplyModifiedPropertiesWithoutUndo();
        }
        else Debug.LogWarning("[PC] GameRoomUI に GameUIRefs が無い");
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[PC] desktop UI built");
    }

    static void SetArray(SerializedProperty p, Object[] values)
    {
        p.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++) p.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }
}
#endif
