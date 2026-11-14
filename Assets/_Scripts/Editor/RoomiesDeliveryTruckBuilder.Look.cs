#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.Rendering;
using Kit = RoomiesRestyleKit;

/// <summary>
/// 配達トラックの見た目（2026/11/14 作り直し）。2 Build Truck Prefab から呼ぶ。
/// ・見た目は Pivot_Drive/Visual/Truck の下。ぶつかるだけの当たり判定は Visual/Truck/Collision に、前の仮の見た目と同じ形で置く
///   （荷台の床の上 y 0.95・横の板 ±1.05・前の板 z 0.15・あおり z -2.86・運転席・車台）。荷台の床と板には TruckBedSurface を付ける。
/// ・運転席は角丸の箱（RoomiesRestyleKit の RBox）。窓は枠の中に引っ込め、奥に暗い車内を置く。
/// ・そのほか：前の格子・ライト・バンパー・ミラー、ホイール（タイヤ・ホイール・ナット）と泥よけ、荷台の縦の板、横のロゴの板、後ろのランプ。
/// ・最後に町の角の丸み（RoomiesBevelBuilder.ApplyTownUnder）をかける。寸法は下の定数と各部品の値。
/// </summary>
internal static partial class RoomiesDeliveryTruckBuilder
{
    // 運転席（前の仮の当たり判定：x ±1.1・y 0.65〜2.55・z 0.35〜2.15）
    const float CabZ0 = .4f, CabZ1 = 2.15f, CabHalf = 1.1f;
    const float CabWaist = 1.65f, CabRoofY = 2.34f;
    // ホイール（中心の高さと半径）
    const float WheelY = .42f, WheelR = .42f;
    static readonly (string name, float x, float z)[] Wheels =
        { ("Wheel_FL", -1.0f, 1.3f), ("Wheel_FR", 1.0f, 1.3f), ("Wheel_RL", -1.0f, -1.9f), ("Wheel_RR", 1.0f, -1.9f) };
    // 荷台（床の上・板の上・前後）
    const float BedTop = .95f, BedRail = 1.65f, BedZ0 = -2.9f, BedZ1 = .2f;
    const float PlankW = .18f, PlankPitch = .2f;

    static void BuildTruckLook(Transform visual, PhysicsMaterial bedSurface)
    {
        bool wasStatic = Kit.MarkStatic;
        Kit.MarkStatic = false;   // 走り去るので static にしない
        Kit.Begin();
        try
        {
            var look = new GameObject("Truck").transform;
            look.SetParent(visual, false);

            Material body = Mat("PaintedMetal/Town/mustard.mat");
            Material cream = Mat("PaintedMetal/Town/cream.mat");
            Material ink = Mat("PaintedMetal/Town/ink.mat");
            Material frame = Mat("PaintedMetal/Town/town_metal_dark.mat");
            Material rubber = Mat("Rubber/plastic_black.mat");
            Material chrome = Mat("Metal/Town/town_slot_chrome.mat");
            Material rim = Mat("PaintedMetal/Town/silver.mat");
            Material glass = Mat("Glass/Town/blue.mat");
            Material plank = Mat("Wood/Town/town_deck_wood.mat");
            Material deck = Mat("Wood/Town/town_deck_wood.mat");
            Material headlight = Mat("Emissive/glow_bulb.mat");
            Material tail = Mat("Emissive/glow_slot_red.mat");
            Material amber = Mat("Emissive/glow_slot_yellow.mat");

            Collision(look, bedSurface);
            Cab(look, body, cream, ink, frame, rubber, chrome, glass, headlight, amber);
            Chassis(look, frame, rubber, chrome);
            Bed(look, body, cream, frame, rubber, chrome, plank, deck, headlight, tail);
            foreach (var (n, x, z) in Wheels) Wheel(look, n, new Vector3(x, WheelY, z), rubber, rim, chrome);

            RoomiesBevelBuilder.ApplyTownUnder(look);
        }
        finally
        {
            Kit.MarkStatic = wasStatic;
            UnityEditor.AssetDatabase.SaveAssets();
        }
    }

    // ================================================================ ぶつかるだけの当たり判定（前の仮の見た目と同じ形）
    static void Collision(Transform look, PhysicsMaterial bedSurface)
    {
        var g = new GameObject("Collision").transform;
        g.SetParent(look, false);
        Hit(g, "Chassis", new Vector3(0f, .6f, -.3f), new Vector3(2.2f, .3f, 5.2f), null);
        Hit(g, "BedFloor", new Vector3(0f, .85f, -1.35f), new Vector3(2.2f, .2f, 3.1f), bedSurface);
        Hit(g, "BedSide_L", new Vector3(-1.05f, 1.3f, -1.35f), new Vector3(.1f, .7f, 3.1f), bedSurface);
        Hit(g, "BedSide_R", new Vector3(1.05f, 1.3f, -1.35f), new Vector3(.1f, .7f, 3.1f), bedSurface);
        Hit(g, "BedFront", new Vector3(0f, 1.3f, .15f), new Vector3(2.2f, .7f, .1f), bedSurface);
        // 後ろのあおり：床から 0.3m（持った高さから投げても越えられる低さ）
        Hit(g, "Tailgate", new Vector3(0f, 1.1f, -2.86f), new Vector3(2.2f, .3f, .12f), bedSurface);
        Hit(g, "Cab", new Vector3(0f, 1.6f, 1.25f), new Vector3(2.2f, 1.9f, 1.8f), null);
    }

    // ================================================================ 運転席
    static void Cab(Transform look, Material body, Material cream, Material ink, Material frame, Material rubber,
        Material chrome, Material glass, Material headlight, Material amber)
    {
        var g = new GameObject("Cab").transform;
        g.SetParent(look, false);
        float zc = (CabZ0 + CabZ1) / 2, len = CabZ1 - CabZ0, upH = CabRoofY - CabWaist, upY = (CabWaist + CabRoofY) / 2;

        // 下の胴（角丸）・前後のすそ（あいだがタイヤの入る所）・屋根（角丸）
        Kit.RBox(g, "Cab body", new Vector3(0f, 1.3f, zc), new Vector3(2.2f, .7f, len), .14f, body);
        Kit.RBox(g, "Cab skirt front", new Vector3(0f, .8f, 2.0f), new Vector3(2.2f, .4f, .3f), .06f, body);
        Kit.RBox(g, "Cab skirt rear", new Vector3(0f, .8f, .55f), new Vector3(2.2f, .4f, .3f), .06f, body);
        P(g, "Wheel well", new Vector3(0f, .8f, 1.3f), new Vector3(1.6f, .4f, 1.2f), rubber);
        Kit.RBox(g, "Cab roof", new Vector3(0f, CabRoofY + .09f, zc), new Vector3(2.2f, .18f, len), .08f, body);

        // 窓のまわり：後ろの壁・後ろの横の板・前の柱（窓はこの内側に引っ込める）
        P(g, "Cab back wall", new Vector3(0f, upY, CabZ0 + .1f), new Vector3(2.2f, upH, .2f), body);
        foreach (float s in new[] { -1f, 1f })
        {
            P(g, "Cab rear quarter", new Vector3(s * 1.03f, upY, .675f), new Vector3(.14f, upH, .55f), body);
            P(g, "Cab pillar", new Vector3(s * 1.0f, upY, CabZ1 - .09f), new Vector3(.2f, upH, .18f), body);
        }
        // 車内（暗い箱。ガラス越しに奥が見えるように）とガラス
        P(g, "Cab interior", new Vector3(0f, upY, 1.28f), new Vector3(1.9f, upH, 1.4f), rubber);
        P(g, "Windshield glass", new Vector3(0f, upY, CabZ1 - .06f), new Vector3(1.8f, upH, .03f), glass);
        foreach (float s in new[] { -1f, 1f })
            P(g, "Side window glass", new Vector3(s * 1.06f, upY, 1.46f), new Vector3(.03f, upH, 1.02f), glass);
        // ゴムの枠（窓の上下と、横の窓の前後）
        foreach (float y in new[] { CabWaist + .01f, CabRoofY - .01f })
        {
            P(g, "Window gasket", new Vector3(0f, y, CabZ1 - .05f), new Vector3(1.84f, .04f, .05f), rubber);
            foreach (float s in new[] { -1f, 1f })
                P(g, "Window gasket", new Vector3(s * 1.07f, y, 1.46f), new Vector3(.05f, .04f, 1.06f), rubber);
        }
        foreach (float s in new[] { -1f, 1f })
            foreach (float z in new[] { .95f, 1.97f })
                P(g, "Window gasket", new Vector3(s * 1.07f, upY, z), new Vector3(.05f, upH, .04f), rubber);
        // ワイパー
        foreach (float x in new[] { -.45f, .35f })
            P(g, "Wiper", new Vector3(x, CabWaist + .1f, CabZ1 - .03f), new Vector3(.6f, .025f, .02f), rubber, new Vector3(0f, 0f, 12f));
        // 屋根の前の車幅灯
        foreach (float x in new[] { -.5f, 0f, .5f })
            P(g, "Roof marker", new Vector3(x, CabRoofY + .19f, CabZ1 - .1f), new Vector3(.14f, .05f, .07f), amber);

        // 前：格子・ライト・方向指示器・バンパー・番号板
        P(g, "Grille", new Vector3(0f, 1.25f, CabZ1 + .01f), new Vector3(1.1f, .4f, .04f), rubber);
        for (int i = 0; i < 4; i++)
            P(g, "Grille slat", new Vector3(0f, 1.1f + i * .1f, CabZ1 + .035f), new Vector3(1.0f, .03f, .02f), chrome);
        foreach (float s in new[] { -1f, 1f })
        {
            Cy(g, "Headlight bezel", new Vector3(s * .78f, 1.25f, CabZ1 + .02f), .3f, .05f, chrome, new Vector3(90f, 0f, 0f));
            Cy(g, "Headlight lens", new Vector3(s * .78f, 1.25f, CabZ1 + .04f), .24f, .03f, headlight, new Vector3(90f, 0f, 0f));
            P(g, "Indicator lamp", new Vector3(s * .78f, 1.04f, CabZ1 + .02f), new Vector3(.2f, .08f, .04f), amber);
        }
        Kit.RBox(g, "Front bumper", new Vector3(0f, .72f, CabZ1 + .09f), new Vector3(2.3f, .22f, .2f), .07f, chrome);
        P(g, "Front number plate", new Vector3(0f, .72f, CabZ1 + .195f), new Vector3(.5f, .14f, .01f), cream);

        // 横：ミラー・ドアの継ぎ目と取っ手・ステップ・ロゴの板・泥よけのふち
        foreach (float s in new[] { -1f, 1f })
        {
            P(g, "Mirror arm", new Vector3(s * 1.21f, 2.1f, CabZ1 - .1f), new Vector3(.24f, .035f, .035f), chrome);
            P(g, "Mirror arm", new Vector3(s * 1.21f, 1.75f, CabZ1 - .1f), new Vector3(.24f, .035f, .035f), chrome);
            Kit.RBox(g, "Mirror", new Vector3(s * 1.36f, 1.92f, CabZ1 - .1f), new Vector3(.07f, .42f, .2f), .03f, rubber);
            foreach (float z in new[] { .95f, 1.95f })
                P(g, "Door seam", new Vector3(s * 1.102f, 1.3f, z), new Vector3(.01f, .66f, .015f), rubber);
            P(g, "Door handle", new Vector3(s * 1.11f, 1.55f, 1.06f), new Vector3(.03f, .04f, .16f), chrome);
            P(g, "Cab step", new Vector3(s * 1.08f, .58f, .55f), new Vector3(.12f, .05f, .28f), frame);
            Kit.RBox(g, "Logo plate", new Vector3(s * 1.11f, 1.3f, 1.45f), new Vector3(.02f, .42f, .8f), .01f, cream);
            var logo = Kit.Text(g, "ROOMIES\nEXPRESS", new Vector3(s * 1.123f, 1.3f, 1.45f), new Vector2(.74f, .36f), new Color(.16f, .17f, .2f), 1.2f, new Vector3(0f, s * 90f, 0f));
            // 大きさは決め打ちにし、保存の前に文字を組んでおく（シーンに置いたときに上書きの差分が出ないように）
            logo.enableAutoSizing = false;
            logo.fontSize = 1.2f;
            logo.ForceMeshUpdate();
            Arch(g, s < 0 ? "Fender_FL" : "Fender_FR", new Vector3(s * 1.12f, WheelY, 1.3f), rubber);
        }

        // 屋根の看板の台（看板 SignBoard の位置と文字 Anchor_Sign は前と同じ）
        foreach (float x in new[] { -.7f, .7f })
            P(g, "Sign post", new Vector3(x, 2.6f, .95f), new Vector3(.06f, .2f, .06f), frame);
        P(g, "SignBoard", new Vector3(0f, 3.05f, .95f), new Vector3(2.0f, .75f, .08f), ink);
        Kit.RBox(g, "Sign frame", new Vector3(0f, 3.05f, .965f), new Vector3(2.1f, .85f, .06f), .03f, body);
    }

    // ================================================================ 車台（荷台の下）
    static void Chassis(Transform look, Material frame, Material rubber, Material chrome)
    {
        var g = new GameObject("Chassis").transform;
        g.SetParent(look, false);
        foreach (float x in new[] { -.55f, .55f })
            P(g, "Frame rail", new Vector3(x, .55f, -.3f), new Vector3(.14f, .2f, 5.0f), frame);
        foreach (float z in new[] { -2.5f, -1.0f, .2f, 1.8f })
            P(g, "Cross member", new Vector3(0f, .55f, z), new Vector3(1.1f, .12f, .12f), frame);
        foreach (var (_, _, z) in new[] { Wheels[0], Wheels[2] })
            Cy(g, "Axle", new Vector3(0f, WheelY, z), .1f, 1.8f, frame, new Vector3(0f, 0f, 90f));
        // 燃料タンク（左）と電池の箱（右）・排気管
        Cy(g, "Fuel tank", new Vector3(-.82f, .55f, -.55f), .34f, .7f, chrome, new Vector3(90f, 0f, 0f));
        foreach (float z in new[] { -.8f, -.3f })
            Cy(g, "Tank strap", new Vector3(-.82f, .55f, z), .36f, .04f, rubber, new Vector3(90f, 0f, 0f));
        P(g, "Battery box", new Vector3(.82f, .55f, -.55f), new Vector3(.3f, .3f, .5f), frame);
        Cy(g, "Exhaust pipe", new Vector3(.75f, .45f, -.05f), .08f, .45f, chrome, new Vector3(90f, 0f, 0f));
    }

    // ================================================================ 荷台
    static void Bed(Transform look, Material body, Material cream, Material frame, Material rubber, Material chrome,
        Material plank, Material deck, Material headlight, Material tail)
    {
        var g = new GameObject("Bed").transform;
        g.SetParent(look, false);
        float zc = (BedZ0 + BedZ1) / 2, len = BedZ1 - BedZ0;

        // 床：下の枠と木の床板（板の継ぎ目）
        P(g, "Bed base", new Vector3(0f, .82f, zc), new Vector3(1.6f, .14f, len), frame);
        P(g, "Bed deck", new Vector3(0f, .92f, zc), new Vector3(2.0f, .06f, len - .1f), deck);
        for (int i = 1; i < 8; i++)
            P(g, "Deck seam", new Vector3(-1f + i * .25f, BedTop + .001f, zc), new Vector3(.015f, .004f, len - .1f), frame);
        // 床の外側のすそ（後ろのタイヤの前後）と、タイヤの上の泥よけ
        foreach (float s in new[] { -1f, 1f })
        {
            P(g, "Bed skirt", new Vector3(s * .95f, .825f, -.6f), new Vector3(.3f, .25f, 1.6f), body);
            P(g, "Bed skirt", new Vector3(s * .95f, .825f, -2.65f), new Vector3(.3f, .25f, .5f), body);
            P(g, "Fender top", new Vector3(s * .98f, .91f, -1.9f), new Vector3(.32f, .06f, 1.0f), rubber);
            Arch(g, s < 0 ? "Fender_RL" : "Fender_RR", new Vector3(s * 1.12f, WheelY, -1.9f), rubber);
            P(g, "Mud flap", new Vector3(s * 1.0f, .55f, -2.45f), new Vector3(.3f, .4f, .02f), rubber);
        }

        // 横の板：縦の板を並べ、上下の枠と柱で押さえる
        float py = (BedTop + .02f + BedRail - .05f) / 2, ph = BedRail - .05f - BedTop - .02f;
        foreach (float s in new[] { -1f, 1f })
        {
            for (float z = BedZ0 + .04f + PlankW / 2; z < BedZ1 - .04f; z += PlankPitch)
                P(g, "Bed plank", new Vector3(s * 1.05f, py, z), new Vector3(.08f, ph, PlankW), plank);
            P(g, "Bed rail top", new Vector3(s * 1.05f, BedRail - .02f, zc), new Vector3(.14f, .08f, len + .02f), body);
            P(g, "Bed rail bottom", new Vector3(s * 1.05f, BedTop + .04f, zc), new Vector3(.14f, .08f, len + .02f), body);
            foreach (float z in new[] { BedZ0 + .05f, zc, BedZ1 - .05f })
                P(g, "Bed stake", new Vector3(s * 1.06f, (BedTop + BedRail) / 2 + .01f, z), new Vector3(.13f, BedRail - BedTop + .04f, .08f), body);
        }
        // 前の板（運転席の後ろ）
        for (float x = -1.0f + PlankW / 2; x < 1.0f; x += PlankPitch)
            P(g, "Bed plank", new Vector3(x, py, .15f), new Vector3(PlankW, ph, .08f), plank);
        P(g, "Bed rail top", new Vector3(0f, BedRail - .02f, .15f), new Vector3(2.2f, .08f, .12f), body);
        P(g, "Bed rail bottom", new Vector3(0f, BedTop + .04f, .15f), new Vector3(2.2f, .08f, .12f), body);

        // 後ろのあおり（床から 0.3m。当たり判定は Collision/Tailgate）
        for (float x = -1.1f + .02f + PlankW / 2; x < 1.08f; x += PlankPitch)
            P(g, "Tailgate plank", new Vector3(x, 1.09f, -2.86f), new Vector3(PlankW, .24f, .1f), plank);
        P(g, "Tailgate rail", new Vector3(0f, 1.22f, -2.86f), new Vector3(2.22f, .07f, .13f), body);
        P(g, "Tailgate rail", new Vector3(0f, .98f, -2.86f), new Vector3(2.22f, .06f, .13f), body);
        foreach (float x in new[] { -.7f, .7f })
            Cy(g, "Tailgate hinge", new Vector3(x, .96f, -2.93f), .05f, .18f, chrome, new Vector3(0f, 0f, 90f));
        foreach (float x in new[] { -1.04f, 1.04f })
            P(g, "Tailgate latch", new Vector3(x, 1.13f, -2.93f), new Vector3(.06f, .1f, .03f), chrome);

        // 後ろの下：前掛け・テールランプ・後退灯・番号板・バンパー
        P(g, "Rear apron", new Vector3(0f, .82f, -2.88f), new Vector3(2.2f, .16f, .08f), body);
        foreach (float s in new[] { -1f, 1f })
        {
            P(g, "Tail lamp", new Vector3(s * .9f, .82f, -2.925f), new Vector3(.26f, .12f, .03f), tail);
            P(g, "Reverse lamp", new Vector3(s * .62f, .82f, -2.925f), new Vector3(.1f, .1f, .03f), headlight);
        }
        P(g, "Rear number plate", new Vector3(0f, .82f, -2.925f), new Vector3(.45f, .13f, .01f), cream);
        P(g, "Rear bumper", new Vector3(0f, .62f, -2.95f), new Vector3(2.0f, .12f, .14f), frame);
    }

    // ================================================================ ホイール（タイヤ・ホイール・ハブ・ナット）
    static void Wheel(Transform look, string name, Vector3 center, Material rubber, Material rim, Material chrome)
    {
        var g = new GameObject(name).transform;
        g.SetParent(look, false);
        g.localPosition = center;
        // タイヤ：ふちの丸い輪（回転体）。軸を X に向ける
        var tire = new[]
        {
            new Vector2(.27f, -.15f), new Vector2(.36f, -.15f), new Vector2(.405f, -.125f), new Vector2(WheelR, -.06f),
            new Vector2(WheelR, .06f), new Vector2(.405f, .125f), new Vector2(.36f, .15f), new Vector2(.27f, .15f),
        };
        Kit.Lathe(g, "Tire", "truck_tire", tire, Vector3.zero, rubber, new Vector3(0f, 0f, 90f), 22);
        Cy(g, "Rim", Vector3.zero, .56f, .26f, rim, new Vector3(0f, 0f, 90f));
        Cy(g, "Hub cap", Vector3.zero, .2f, .3f, chrome, new Vector3(0f, 0f, 90f));
        for (int i = 0; i < 5; i++)
        {
            float a = i * Mathf.PI * 2 / 5;
            Cy(g, "Lug nut", new Vector3(0f, Mathf.Sin(a) * .14f, Mathf.Cos(a) * .14f), .04f, .29f, chrome, new Vector3(0f, 0f, 90f));
        }
    }

    /// <summary>タイヤの上の泥よけのふち（タイヤの軸のまわりの半円の管）。</summary>
    static void Arch(Transform parent, string key, Vector3 center, Material mat)
    {
        var path = new Vector3[13];
        for (int i = 0; i < path.Length; i++)
        {
            float a = Mathf.PI * i / (path.Length - 1);
            path[i] = new Vector3(0f, Mathf.Sin(a) * .5f, Mathf.Cos(a) * .5f);
        }
        Kit.Tube(parent, "Fender arch", "truck_" + key, path, .045f, mat, false, center);
    }

    // ================================================================ 部品
    /// <summary>見た目だけの箱（当たり判定なし）。</summary>
    static GameObject P(Transform parent, string name, Vector3 center, Vector3 size, Material mat, Vector3 rot = default)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = center;
        g.transform.localEulerAngles = rot;
        g.transform.localScale = size;
        var r = g.GetComponent<MeshRenderer>();
        if (mat != null) r.sharedMaterial = mat;
        if (Mathf.Max(size.x, Mathf.Max(size.y, size.z)) < .3f) r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    /// <summary>見た目だけの円柱。length は長さ（Unity の円柱は高さ2）。</summary>
    static GameObject Cy(Transform parent, string name, Vector3 center, float diameter, float length, Material mat, Vector3 rot)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = name;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent, false);
        g.transform.localPosition = center;
        g.transform.localEulerAngles = rot;
        g.transform.localScale = new Vector3(diameter, length / 2, diameter);
        if (mat != null) g.GetComponent<MeshRenderer>().sharedMaterial = mat;
        return g;
    }

    /// <summary>ぶつかるだけの当たり判定（見た目なし）。</summary>
    static void Hit(Transform parent, string name, Vector3 center, Vector3 size, PhysicsMaterial surface)
    {
        var g = new GameObject(name);
        g.transform.SetParent(parent, false);
        g.transform.localPosition = center;
        var box = g.AddComponent<BoxCollider>();
        box.size = size;
        if (surface != null) box.sharedMaterial = surface;
    }
}
#endif
