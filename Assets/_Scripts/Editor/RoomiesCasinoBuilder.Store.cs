#if UNITY_EDITOR
using System.Linq;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Art = RoomiesCasinoArt;
using Town = RoomiesTownRebuild;

/// <summary>
/// コンビニのギミックのプレハブを作って置く（2026/11/24）。何度実行しても同じ結果。
///  Roomies/Store/2 Build Gimmick Prefabs … Assets/_Prefabs/Store/ の StoreShop（売り場とレジ）・StoreDoor（自動ドア）・StoreClerk（店の人）
///  Roomies/Store/3 Place In Open Scene  … GameRoom の一番上に StoreShop・StoreDoor・StoreClerk を置いて、たがいの参照をつなぐ（2 を実行したら、続けて 3 も実行する）
///
/// 売り場のプレハブの原点は、店の真ん中の床（RoomiesTownRebuild.StoreFloorCenter）。+Z が通り。中の広さは x ±7.25・z ±4.75。
/// 置き場所の数字（下の表）を直したら、2 と 3 を実行し直す（プレハブの中で直した位置は、作り直すと消える）。
/// </summary>
internal static partial class RoomiesCasinoBuilder
{
    internal const string StoreFolder = "Assets/_Prefabs/Store";
    internal const string StoreShopPath = StoreFolder + "/StoreShop.prefab";
    internal const string StoreDoorPath = StoreFolder + "/StoreDoor.prefab";
    internal const string StoreClerkPath = StoreFolder + "/StoreClerk.prefab";

    // 売り場の物の置き場所（売り場のプレハブの中の位置）
    const float StoreCounterH = .95f;
    static readonly Vector3 StoreClerkLocal = new Vector3(4.3f, .3f, .4f);   // レジの左（お客さん向けの画面で顔が隠れないように）

    [MenuItem("Roomies/Store/2 Build Gimmick Prefabs")]
    internal static void BuildStorePrefabs()
    {
        System.IO.Directory.CreateDirectory(StoreFolder);
        Art.Begin();
        BuildStoreShop();
        BuildStoreDoor();
        BuildStoreClerk();
        Art.End();
        AssetDatabase.SaveAssets();
        Debug.Log("[Store] gimmick prefabs built");
    }

    // ================================================================ 売り場とレジ
    static void BuildStoreShop()
    {
        var root = new GameObject("StoreShop");
        try
        {
            root.AddComponent<NetworkObject>();
            var shop = root.AddComponent<StoreShop>();
            var visual = Child(root.transform, "Visual");
            var collision = Child(visual, "Collision");
            Material glass = Art.GlassMaterial();

            // 店の中の物は、太陽の影を落とさない（屋根と壁があるので、太陽の光は中まで届かない。影を描く回数を増やさないため）
            GameObject Put(string name, Mesh mesh, Vector3 at, float yaw = 0, Material mat = null)
            {
                var go = Art.Place(visual, name, mesh, at, yaw, false, false);
                if (mat != null) go.GetComponent<MeshRenderer>().sharedMaterial = mat;
                return go;
            }

            // ---- 売り場の什器
            Put("Drink cooler", Art.StoreDrinkCooler(4, 2.2f, 2.3f, .8f), new Vector3(-2.4f, 0, -4.35f));
            Put("Drink cooler glass", Art.StoreCoolerGlass(4, 2.2f, 2.3f, .8f), new Vector3(-2.4f, 0, -4.35f), 0, glass).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Put("Open chiller", Art.StoreOpenChiller(3.8f, 1.95f, .9f), new Vector3(-6.8f, 0, -1.3f), 90);
            Put("Bread shelf", Art.StoreBreadShelf(2.5f, 1.6f, .5f), new Vector3(-7.0f, 0, 2.15f), 90);
            Put("Gondola snacks", Art.StoreGondola(4.2f, 1.45f, .9f, 3), new Vector3(-4.6f, 0, -.5f), 90);
            Put("Gondola cups", Art.StoreGondola(4.2f, 1.45f, .9f, 4), new Vector3(-2.2f, 0, -.5f), 90);
            Put("Ice freezer", Art.StoreFreezer(1.5f, 1.0f, .85f), new Vector3(.15f, 0, -1.9f));
            Put("Ice freezer glass", Art.StoreFreezerGlass(1.5f, 1.0f, .85f), new Vector3(.15f, 0, -1.9f), 0, glass).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Put("Counter", Art.StoreCounter(4.75f, .8f, StoreCounterH), new Vector3(4.875f, 0, 1.3f));
            Put("Side counter", Art.StoreSideCounter(1.3f, .4f, StoreCounterH), new Vector3(2.7f, 0, .25f), 90);
            Put("Back shelf", Art.StoreBackShelf(4.4f, 1.8f, .4f), new Vector3(4.95f, 0, -.4f));
            Put("Staff platform", Art.Build("store_platform", lp => { lp.Sharp = true; lp.Box(new Vector3(-2.3f, 0, -.55f), new Vector3(2.3f, .3f, .55f), "steel_dark"); }), new Vector3(4.95f, 0, .35f));
            Put("Partition", Art.Build("store_partition", lp => lp.Box(new Vector3(-.075f, 0, -2.175f), new Vector3(.075f, 2.6f, 2.175f), "store_cream")), new Vector3(2.575f, 0, -2.575f));
            Put("Staff door", Art.StoreStaffDoor(), new Vector3(2.49f, 0, -2.2f), -90);
            Put("Register", Art.StoreRegister(), new Vector3(5.2f, StoreCounterH, 1.25f));
            Put("Scanner", Art.StoreScanner(), new Vector3(6.05f, StoreCounterH, 1.45f));
            Put("Hot case", Art.StoreHotCase(), new Vector3(3.3f, StoreCounterH, 1.25f));
            Put("Hot case glass", Art.StoreHotCaseGlass(), new Vector3(3.3f, StoreCounterH, 1.25f), 0, glass).GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Put("Coffee station", Art.StoreCoffee(1.4f, .65f, .92f), new Vector3(5.6f, 0, 4.35f), 180);
            Put("Magazine rack", Art.StoreMagazineRack(4.9f, 1.0f, .35f), new Vector3(-4.45f, 0, 4.52f), 180);
            Put("Basket stack", Art.StoreBasketStack(), new Vector3(-1.5f, 0, 4.1f), 20);

            // ---- 売り場の上の札（つり下げ）。札は Visual、文字は Signs
            var signs = Child(root.transform, "Signs");
            Mesh board = Art.Build("store_hang_board", lp =>
            {
                Art.StandingSlab(lp, Art.Offset(Art.Chamfer(1.3f, .34f, .1f), new Vector2(0, 0)), .05f, "coral");
                foreach (float x in new[] { -.45f, .45f }) lp.Bar(new Vector3(x, .17f, 0), new Vector3(x, .9f, 0), .012f, "steel_dark");
            });
            void Hang(string text, Vector3 at, float yaw)
            {
                Put("Sign board " + text, board, at, yaw);
                foreach (float side in new[] { 0f, 180f })
                {
                    var t = Label(signs, "Sign " + text, text, at + Quaternion.Euler(0, yaw + side, 0) * new Vector3(0, 0, .03f), 1.6f, new Color(1f, .96f, .88f));
                    t.transform.localRotation = Quaternion.Euler(0, 180 + yaw + side, 0);
                    ((RectTransform)t.transform).sizeDelta = new Vector2(1.2f, .3f);
                    t.fontStyle = FontStyles.Bold;
                }
            }
            Hang("おにぎり・お弁当", new Vector3(-6.1f, 2.4f, -1.3f), 90);
            Hang("パン", new Vector3(-6.4f, 2.2f, 2.15f), 90);
            Hang("お菓子", new Vector3(-4.6f, 2.4f, -.5f), 0);
            Hang("カップめん", new Vector3(-2.2f, 2.4f, -.5f), 0);
            Hang("アイス", new Vector3(.15f, 2.4f, -1.9f), 0);
            Hang("お会計", new Vector3(4.9f, 2.55f, 1.3f), 0);
            Hang("コーヒー", new Vector3(5.6f, 2.3f, 4.1f), 0);
            foreach (var (text, x) in new[] { ("のみもの", -3.5f), ("牛乳", .9f) })
            {
                var t = Label(signs, "Cooler label " + text, text, new Vector3(x, 2.115f, -3.93f), 1.5f, new Color(1f, .96f, .88f));
                ((RectTransform)t.transform).sizeDelta = new Vector2(2f, .2f);
                t.fontStyle = FontStyles.Bold;
            }

            // ---- 売り場（見て E を押す所。棚の当たり判定も兼ねる）
            var sections = new System.Collections.Generic.List<(BoxCollider c, int kind, Transform anchor)>();
            void Section(string name, int kind, Vector3 min, Vector3 max, Vector3 anchorAt)
            {
                var t = Child(root.transform, "Pick_" + name);
                var c = t.gameObject.AddComponent<BoxCollider>();
                c.center = (min + max) / 2f; c.size = max - min;
                sections.Add((c, kind, Child(root.transform, "Anchor_Shelf_" + name, anchorAt)));
            }
            Section("Onigiri", 0, new Vector3(-7.25f, 1.1f, -3.2f), new Vector3(-6.35f, 1.95f, .6f), new Vector3(-6.45f, 1.45f, -1.3f));
            Section("Bento", 1, new Vector3(-7.25f, 0, -3.2f), new Vector3(-6.35f, 1.1f, .6f), new Vector3(-6.4f, .8f, -1.3f));
            Section("Bread", 2, new Vector3(-7.25f, 0, .9f), new Vector3(-6.75f, 1.6f, 3.4f), new Vector3(-6.8f, 1.1f, 2.15f));
            Section("Snacks", 3, new Vector3(-5.05f, 0, -2.9f), new Vector3(-4.15f, 1.45f, 1.9f), new Vector3(-4.6f, 1.2f, -.3f));
            Section("Cups", 4, new Vector3(-2.65f, 0, -2.9f), new Vector3(-1.75f, 1.45f, 1.9f), new Vector3(-2.2f, 1.2f, -.3f));
            Section("Drinks", 5, new Vector3(-6.8f, 0, -4.75f), new Vector3(-.2f, 2.3f, -3.95f), new Vector3(-3.5f, 1.2f, -3.85f));
            Section("Milk", 6, new Vector3(-.2f, 0, -4.75f), new Vector3(2.0f, 2.3f, -3.95f), new Vector3(.9f, 1.2f, -3.85f));
            Section("Ice", 7, new Vector3(-.6f, 0, -2.4f), new Vector3(.9f, .85f, -1.4f), new Vector3(.15f, .95f, -1.9f));

            // ---- レジ（カウンターを見て E）
            var reg = Child(root.transform, "Pick_Register");
            var regCollider = reg.gameObject.AddComponent<BoxCollider>();
            regCollider.center = new Vector3(4.875f, .7f, 1.3f); regCollider.size = new Vector3(4.75f, 1.4f, .8f);

            // ---- ぶつかるだけの当たり判定（見た目と一緒に替わる物は Visual/Collision）
            Wall(collision, "Side counter", new Vector3(2.5f, 0, -.4f), new Vector3(2.9f, StoreCounterH, .9f));
            Wall(collision, "Partition", new Vector3(2.5f, 0, -4.75f), new Vector3(2.65f, 2.6f, -.4f));
            Wall(collision, "Back shelf", new Vector3(2.75f, 0, -.6f), new Vector3(7.15f, 1.8f, -.2f));
            Wall(collision, "Coffee station", new Vector3(4.9f, 0, 4.02f), new Vector3(6.3f, .92f, 4.68f));
            Wall(collision, "Magazine rack", new Vector3(-6.9f, 0, 4.34f), new Vector3(-2.0f, 1.0f, 4.7f));
            Wall(collision, "Baskets", new Vector3(-1.75f, 0, 3.9f), new Vector3(-1.25f, .4f, 4.3f));
            Wall(collision, "Cooler side", new Vector3(2.0f, 0, -4.75f), new Vector3(2.5f, 2.3f, -3.95f));

            // ---- レジのまわりの位置と、動かす物
            float top = StoreCounterH;
            var lane = new[] { Child(root.transform, "Anchor_Lane_1", new Vector3(3.95f, top, 1.55f)), Child(root.transform, "Anchor_Lane_2", new Vector3(4.25f, top, 1.55f)), Child(root.transform, "Anchor_Lane_3", new Vector3(4.55f, top, 1.55f)) };
            var scanner = Child(root.transform, "Anchor_Scanner", new Vector3(6.05f, top + .036f, 1.45f));
            var itemsFrom = Child(root.transform, "Anchor_ItemsFrom", new Vector3(4.25f, top + .25f, 2.05f));
            var bagGive = Child(root.transform, "Anchor_BagGive", new Vector3(6.55f, top, 1.6f));
            StoreShop.ItemSlot ItemSlot(string name, Vector3 at)
            {
                var pivot = Child(root.transform, name, at);
                var v = Child(pivot, "Visual");
                var kinds = new GameObject[Art.StoreKinds.Length];
                for (int k = 0; k < kinds.Length; k++) kinds[k] = Art.Place(v, "Item " + Art.StoreKinds[k], Art.StoreItemSolo(k), default, 0, false, false);
                pivot.gameObject.SetActive(false);
                return new StoreShop.ItemSlot { pivot = pivot, kinds = kinds };
            }
            var fly = Enumerable.Range(1, 3).Select(i => ItemSlot("Pivot_Fly_" + i, new Vector3(0, 1.2f, 0))).ToArray();
            var items = Enumerable.Range(1, 3).Select(i => ItemSlot("Pivot_Item_" + i, lane[i - 1].localPosition)).ToArray();
            var bag = Child(root.transform, "Pivot_Bag", new Vector3(6.75f, top, 1.2f));
            Art.Place(Child(bag, "Visual"), "Bag", Art.StoreBag(), default, 0, false, false);
            bag.gameObject.SetActive(false);
            var drawer = Child(root.transform, "Pivot_Drawer", new Vector3(5.2f, top + .012f, 1.2f));
            Art.Place(Child(drawer, "Visual"), "Drawer", Art.StoreDrawer(), default, 0, false, false);
            var receipt = Child(root.transform, "Pivot_Receipt", new Vector3(5.47f, top + .1f, 1.36f));
            Art.Place(Child(receipt, "Visual"), "Receipt", Art.StoreReceipt(), default, 0, false, false);
            receipt.gameObject.SetActive(false);
            var glow = Child(root.transform, "Pivot_ScanGlow", new Vector3(6.05f, top, 1.45f));
            Art.Place(Child(glow, "Visual"), "Glow", Art.StoreScanGlow(), default, 0, false, false);
            glow.gameObject.SetActive(false);

            // ---- 文字（レジの画面・「+1」）
            var displayAnchor = Child(root.transform, "Anchor_Display", new Vector3(5.2f, top + .38f, 1.3f));
            var display = Label(displayAnchor, "Text_Display", "いらっしゃいませ", default, .55f, new Color(.55f, .95f, .9f));
            ((RectTransform)display.transform).sizeDelta = new Vector2(.28f, .15f);
            display.textWrappingMode = TextWrappingModes.NoWrap;   // 折り返さずに、枠に合わせて縮める（2 行にするときは改行の文字で分ける）
            display.enableAutoSizing = true; display.fontSizeMin = .12f; display.fontSizeMax = .55f;
            var popAnchor = Child(root.transform, "Anchor_Pop", new Vector3(0, 1.6f, 0));
            var pop = Label(popAnchor, "Text_Pop", "+1", default, 2.4f, new Color(.98f, .47f, .36f));
            ((RectTransform)pop.transform).sizeDelta = new Vector2(2.2f, .4f);
            pop.fontStyle = FontStyles.Bold;
            popAnchor.gameObject.SetActive(true);
            pop.gameObject.SetActive(false);

            // ---- 参照の割り当て
            var so = new SerializedObject(shop);
            so.FindProperty("bodyCollider").objectReferenceValue = regCollider;
            var arr = so.FindProperty("sections");
            arr.arraySize = sections.Count;
            for (int i = 0; i < sections.Count; i++)
            {
                var e = arr.GetArrayElementAtIndex(i);
                e.FindPropertyRelative("collider").objectReferenceValue = sections[i].c;
                e.FindPropertyRelative("kind").intValue = sections[i].kind;
                e.FindPropertyRelative("anchor").objectReferenceValue = sections[i].anchor;
            }
            so.FindProperty("registerCollider").objectReferenceValue = regCollider;
            SetObjects(so.FindProperty("laneSpots"), lane);
            so.FindProperty("anchorScanner").objectReferenceValue = scanner;
            so.FindProperty("anchorBag").objectReferenceValue = bag;
            so.FindProperty("anchorBagGive").objectReferenceValue = bagGive;
            so.FindProperty("anchorItemsFrom").objectReferenceValue = itemsFrom;
            void Slots(string prop, StoreShop.ItemSlot[] slots)
            {
                var p = so.FindProperty(prop);
                p.arraySize = slots.Length;
                for (int i = 0; i < slots.Length; i++)
                {
                    var e = p.GetArrayElementAtIndex(i);
                    e.FindPropertyRelative("pivot").objectReferenceValue = slots[i].pivot;
                    SetObjects(e.FindPropertyRelative("kinds"), slots[i].kinds);
                }
            }
            Slots("flySlots", fly);
            Slots("counterItems", items);
            so.FindProperty("bagPivot").objectReferenceValue = bag;
            so.FindProperty("drawerPivot").objectReferenceValue = drawer;
            so.FindProperty("receiptPivot").objectReferenceValue = receipt;
            so.FindProperty("scanGlow").objectReferenceValue = glow.gameObject;
            so.FindProperty("displayText").objectReferenceValue = display;
            so.FindProperty("popText").objectReferenceValue = pop;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, StoreShopPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ================================================================ 自動ドア
    static void BuildStoreDoor()
    {
        var root = new GameObject("StoreDoor");
        try
        {
            root.AddComponent<NetworkObject>();
            var door = root.AddComponent<StoreAutoDoor>();
            Material glass = Art.GlassMaterial();
            float w = .8f, h = Town.SDoorH - Town.SFloor - .02f;
            Mesh frame = Art.Build("store_door_frame", lp =>
            {
                var bar = Art.Chamfer(.06f, .05f, .02f);
                foreach (float x in new[] { -w / 2 + .03f, w / 2 - .03f }) lp.Prism(Art.Offset(bar, new Vector2(x, 0)), 0, h, "steel");
                lp.Box(new Vector3(-w / 2, 0, -.025f), new Vector3(w / 2, .1f, .025f), "steel");
                lp.Box(new Vector3(-w / 2, h - .06f, -.025f), new Vector3(w / 2, h, .025f), "steel");
                // 目の高さの、さんご色の帯（ガラスにぶつからないための印）
                lp.Sharp = true;
                lp.Box(new Vector3(-w / 2 + .06f, 1.32f, -.006f), new Vector3(w / 2 - .06f, 1.38f, .006f), "coral");
                lp.Sharp = false;
            });
            Mesh pane = Art.Build("store_door_glass", lp => { lp.Sharp = true; lp.Box(new Vector3(-w / 2 + .05f, .1f, -.004f), new Vector3(w / 2 - .05f, h - .06f, .004f), "glass_dark"); });
            Transform Leaf(string name, float x)
            {
                var pivot = Child(root.transform, name, new Vector3(x, 0, 0));
                var v = Child(pivot, "Visual");
                Art.Place(v, "Frame", frame, default, 0, false, false);
                var g = Art.Place(v, "Glass", pane, default, 0, false, false);
                g.GetComponent<MeshRenderer>().sharedMaterial = glass;
                g.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                return pivot;
            }
            var left = Leaf("Pivot_DoorL", -w / 2);
            var right = Leaf("Pivot_DoorR", w / 2);
            // とびらの上の感知器（見た目）と、その位置
            var visual = Child(root.transform, "Visual");
            Art.Place(visual, "Sensor", Art.Build("store_door_sensor", lp =>
            {
                lp.Box(new Vector3(-.2f, -.06f, 0), new Vector3(.2f, .02f, .09f), "ink");
                lp.Box(new Vector3(-.02f, -.05f, .09f), new Vector3(.02f, -.03f, .095f), "label_green", true);
            }), new Vector3(0, h + .1f, .02f), 0, false, false);
            var sensor = Child(root.transform, "Anchor_Sensor", new Vector3(0, h + .05f, 0));

            var so = new SerializedObject(door);
            so.FindProperty("leftDoor").objectReferenceValue = left;
            so.FindProperty("rightDoor").objectReferenceValue = right;
            so.FindProperty("sensor").objectReferenceValue = sensor;
            so.FindProperty("slideDistance").floatValue = w - .06f;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, StoreDoorPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ================================================================ 店の人（カジノの店の人と同じ仕組み。鍵は売らずに話すだけ。制服はさんご色）
    static readonly string[] StoreSmallTalk =
    {
        "夜はおにぎりがよく売れるんですよ。",
        "冷蔵庫の食材、切らしてませんか？",
        "カゴに 3 つで、食材 1 回分になります。",
        "アイスは冬でも売れるんです。ふしぎですよね。",
        "向かいのカジノ帰りの人、だいたい肉まんを買っていきます。",
        "この前、警察の人がコーヒーを 3 杯買っていきました。",
        "雨の日は、カップめんがよく出ます。",
        "家賃の日の前は、みなさん節約モードですね。",
        "お弁当、温めますか？……あ、いまは温める機械が休憩中でした。",
    };

    static void BuildStoreClerk()
    {
        var root = new GameObject("StoreClerk");
        try
        {
            root.AddComponent<NetworkObject>();
            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, .8f, 0); capsule.radius = .45f; capsule.height = 1.6f;
            var clerk = root.AddComponent<CasinoClerk>();
            var pivot = Child(root.transform, "Pivot_Body");
            Art.Place(Child(pivot, "Visual"), "Body", Art.Clerk("clerk_store", "coral", "coral", "coral_dark"));

            var so = new SerializedObject(clerk);
            so.FindProperty("bodyPivot").objectReferenceValue = pivot;
            so.FindProperty("interactDistance").floatValue = 3f;
            so.FindProperty("sellsFloor").intValue = 0;
            so.FindProperty("clerkName").stringValue = "コンビニの店員";
            var talk = so.FindProperty("smallTalk");
            talk.arraySize = StoreSmallTalk.Length;
            for (int i = 0; i < StoreSmallTalk.Length; i++) talk.GetArrayElementAtIndex(i).stringValue = StoreSmallTalk[i];
            // 吹き出しは、カウンターの上（店の人の左手の上。帽子に重ならないよう体から離す）
            var speech = BuildSpeechBubble(root, 1.95f);
            var anchor = speech.transform.Find("Anchor_Bubble");
            anchor.localPosition = new Vector3(-1.2f, 1.62f, .35f);
            anchor.localScale = Vector3.one * .85f;
            so.FindProperty("bubble").objectReferenceValue = speech;
            so.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(root, StoreClerkPath);
        }
        finally { Object.DestroyImmediate(root); }
    }

    // ================================================================ シーンに置く
    [MenuItem("Roomies/Store/3 Place In Open Scene")]
    internal static void PlaceStoreInOpenScene()
    {
        Scene scene = GameRoomScene();
        if (!scene.IsValid()) { Debug.LogWarning("[Store] GameRoom を開いて実行する"); return; }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(StoreShopPath) == null) BuildStorePrefabs();
        // カジノと同じく、シーンの一番上に置く（実行し直したときに、同じ名前の物を見つけて置き直すため）
        Vector3 c = Town.StoreFloorCenter;
        var shop = PlacePrefab(scene, StoreShopPath, "StoreShop", c, 0);
        var door = PlacePrefab(scene, StoreDoorPath, "StoreDoor", new Vector3((Town.SDoorX0 + Town.SDoorX1) / 2f, Town.SFloor, Town.SZ1 - .05f), 0);
        var clerk = PlacePrefab(scene, StoreClerkPath, "StoreClerk", c + StoreClerkLocal, 0);

        var so = new SerializedObject(shop.GetComponent<StoreShop>());
        so.FindProperty("clerkBubble").objectReferenceValue = clerk.GetComponent<NpcSpeechBubble>();
        so.ApplyModifiedPropertiesWithoutUndo();
        var ds = new SerializedObject(door.GetComponent<StoreAutoDoor>());
        ds.FindProperty("shop").objectReferenceValue = shop.GetComponent<StoreShop>();
        ds.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[Store] placed in " + scene.name);
    }
}
#endif
