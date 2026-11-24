#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// カジノのギミックの形（スロットの筐体・ブラックジャック台・ルーレット・エレベーター・店の人）。
/// どれも「足元が原点、正面が +Z」。寸法は、ギミックの側（当たり判定・画面・賭ける場所）と合わせてある。
/// 形を替えるときはここを直して、Roomies/Casino/2 Build Gimmick Prefabs を実行し直す。
/// </summary>
internal static partial class RoomiesCasinoArt
{
    // ================================================================ スロットの筐体
    // 画面（Canvas）は幅 0.806m・高さ 1.178m。中心の高さと、筐体の前の面の位置
    internal const float SlotScreenW = .806f, SlotScreenH = 1.178f, SlotScreenY = 1.26f, SlotFrontZ = .36f;
    internal const float SlotWidth = .98f, SlotDepth = .74f, SlotHeight = 2.32f;

    internal static Mesh SlotCabinet(int variant) => Build("slot_cabinet_" + variant, lp =>
    {
        string body = variant == 0 ? "purple" : "pink_dark", dark = variant == 0 ? "purple_deep" : "purple_deep";
        float hw = SlotWidth / 2, sx = SlotScreenW / 2 + .03f, sy0 = SlotScreenY - SlotScreenH / 2 - .03f, sy1 = SlotScreenY + SlotScreenH / 2 + .03f;
        // 台座・本体・頭
        lp.Prism(Chamfer(SlotWidth + .06f, SlotDepth + .06f, .08f), 0, .14f, dark);
        lp.Prism(Chamfer(SlotWidth, SlotDepth, .07f), .14f, 1.98f, body, dark);
        lp.Prism(Offset(Chamfer(SlotWidth + .04f, SlotDepth * .8f, .07f), new Vector2(0, .03f)), 1.98f, SlotHeight, dark, null, false, .86f);
        // 画面のくぼみ（黒いガラス）と金の縁
        lp.Quad(new Vector3(sx, sy0, SlotFrontZ + .003f), new Vector3(-sx, sy0, SlotFrontZ + .003f), new Vector3(-sx, sy1, SlotFrontZ + .003f), new Vector3(sx, sy1, SlotFrontZ + .003f), "glass_dark", Vector3.forward);
        foreach (float x in new[] { -sx - .025f, sx + .025f })
            lp.Box(new Vector3(x - .025f, sy0 - .05f, SlotFrontZ), new Vector3(x + .025f, sy1 + .05f, SlotFrontZ + .05f), "gold");
        lp.Box(new Vector3(-sx - .05f, sy1, SlotFrontZ), new Vector3(sx + .05f, sy1 + .05f, SlotFrontZ + .05f), "gold");
        lp.Box(new Vector3(-sx - .05f, sy0 - .05f, SlotFrontZ), new Vector3(sx + .05f, sy0, SlotFrontZ + .05f), "gold");
        // ボタンの台（前へ張り出す）とボタン 3 つ
        lp.Push(new Vector3(0, sy0 - .16f, SlotFrontZ + .13f), new Vector3(-14, 0, 0));
        lp.Prism(Chamfer(SlotWidth - .06f, .3f, .05f), -.05f, .05f, dark, "ink");
        string[] buttons = { "gold", "pink", "gold" };
        for (int i = 0; i < 3; i++) lp.Prism(Offset(Chamfer(.14f, .1f, .025f), new Vector2((i - 1) * .22f, .02f)), .05f, .085f, buttons[i]);
        lp.Pop();
        // 下の板（スロットの絵の下の帯）と、頭の板（王冠の絵）
        Rect bottom = ImageRect(Img.SlotBottom), top = ImageRect(Img.SlotTop);
        float bw = SlotWidth - .12f, bh = bw * 188f / 1024f, by0 = .2f;
        lp.Image(new Vector3(bw / 2, by0, SlotFrontZ + .012f), new Vector3(-bw / 2, by0, SlotFrontZ + .012f), new Vector3(-bw / 2, by0 + bh, SlotFrontZ + .012f), new Vector3(bw / 2, by0 + bh, SlotFrontZ + .012f), bottom, Vector3.forward);
        float tw = SlotWidth - .04f, th = tw * 270f / 1024f, ty0 = sy1 + .07f;
        lp.Box(new Vector3(-tw / 2 - .02f, ty0 - .02f, SlotFrontZ - .1f), new Vector3(tw / 2 + .02f, ty0 + th + .02f, SlotFrontZ + .03f), "gold_dark");
        lp.Image(new Vector3(tw / 2, ty0, SlotFrontZ + .033f), new Vector3(-tw / 2, ty0, SlotFrontZ + .033f), new Vector3(-tw / 2, ty0 + th, SlotFrontZ + .033f), new Vector3(tw / 2, ty0 + th, SlotFrontZ + .033f), top, Vector3.forward);
        // ピンクのネオンの縁（前の左右の角と、足元）
        foreach (float x in new[] { -hw + .01f, hw - .01f })
            lp.Box(new Vector3(x - .018f, .18f, SlotFrontZ - .01f), new Vector3(x + .018f, 1.96f, SlotFrontZ + .026f), "pink", true);
        lp.Box(new Vector3(-hw, .02f, SlotFrontZ + .03f), new Vector3(hw, .06f, SlotFrontZ + .06f), "pink", true);
        // レバー（遊ぶ人から見て右＝ -X）
        lp.Box(new Vector3(-hw - .07f, 1.0f, -.06f), new Vector3(-hw, 1.26f, .1f), "gold_dark");
        lp.Bar(new Vector3(-hw - .09f, 1.2f, .02f), new Vector3(-hw - .12f, 1.66f, .1f), .05f, "metal");
        lp.Push(new Vector3(-hw - .12f, 1.66f, .1f));
        lp.Lathe(new[] { (.0f, -.075f), (.065f, -.04f), (.075f, 0f), (.065f, .04f), (0f, .075f) }, 6, "red", 0, false);
        lp.Pop();
        // てっぺんの灯
        lp.Cyl(new Vector3(0, SlotHeight, 0), .07f, .08f, 6, "warm", true);
    });

    // ================================================================ ブラックジャック台（遊ぶ人の側が +Z の丸い側。親は -Z の真っすぐな側）
    internal const float BjWidth = 2.6f, BjBackZ = -.95f, BjFrontZ = 1.05f, BjTop = .95f;

    static Vector2[] BjOutline(float inset)
    {
        var p = new List<Vector2>();
        float hw = BjWidth / 2 - inset, back = BjBackZ + inset, arcZ = -.3f, rz = BjFrontZ - inset - arcZ;
        p.Add(new Vector2(hw, back));
        const int n = 24;
        for (int i = 0; i <= n; i++) { float a = Mathf.PI * i / n; p.Add(new Vector2(Mathf.Cos(a) * hw, arcZ + Mathf.Sin(a) * rz)); }
        p.Add(new Vector2(-hw, back));
        return p.ToArray();
    }

    internal static Mesh BlackjackTable() => Build("blackjack_table", lp =>
    {
        // 脚と台座
        foreach (float x in new[] { -.7f, .7f }) lp.Prism(Offset(Chamfer(.5f, .6f, .1f), new Vector2(x, -.05f)), .06f, .64f, "wood_dark");
        lp.Prism(Offset(Chamfer(2.0f, .9f, .12f), new Vector2(0, -.05f)), 0, .06f, "ink");
        // 幕板・革のふち・フェルト（絵）
        lp.Prism(BjOutline(.1f), .64f, .86f, "wood", null, true);
        lp.Prism(BjOutline(0), .86f, BjTop, "maroon", "maroon_dark", true);
        var felt = BjOutline(.11f);
        lp.Prism(felt, BjTop, BjTop + .008f, "felt_dark");
        lp.TopImage(felt, BjTop + .0085f, ImageRect(Img.Felt), new Rect(-BjWidth / 2, BjBackZ, BjWidth, BjFrontZ - BjBackZ));
        // 親の側：チップ入れ・カードの箱・チップの山
        lp.Box(new Vector3(-.5f, BjTop + .008f, -.84f), new Vector3(.5f, BjTop + .06f, -.62f), "ink");
        string[] chips = { "chip_red", "chip_blue", "chip_green", "chip_black", "chip_white", "chip_red", "chip_blue", "chip_green" };
        for (int i = 0; i < 8; i++)
        {
            lp.Push(new Vector3(-.43f + i * .123f, BjTop + .075f, -.73f), new Vector3(90, 0, 0));
            lp.Cyl(new Vector3(0, -.09f, 0), .048f, .18f, 8, chips[i]);
            lp.Pop();
        }
        lp.Box(new Vector3(.72f, BjTop + .008f, -.82f), new Vector3(.98f, BjTop + .14f, -.5f), "ink");
        lp.Box(new Vector3(.74f, BjTop + .01f, -.5f), new Vector3(.96f, BjTop + .1f, -.48f), "card");
        lp.Box(new Vector3(-.98f, BjTop + .008f, -.8f), new Vector3(-.76f, BjTop + .1f, -.56f), "red_dark");
        foreach (var (x, z, n, c) in new[] { (-.55f, .42f, 5, "chip_red"), (.3f, .56f, 7, "chip_blue"), (.78f, .3f, 4, "chip_green") })
            lp.ChipStack(new Vector3(x, BjTop + .009f, z), .045f, n, c);
    });

    // ================================================================ ルーレットの台（遊ぶ人の側が +Z。遊ぶ人から見て左が +X）
    internal const float RlLength = 3.3f, RlDepth = 1.5f, RlTop = .92f;
    // 賭ける場所の絵を貼る範囲（+X の端・-Z の端・幅・奥行き）と、盤の中心・半径
    internal const float RlLayoutX0 = .5f, RlLayoutZ0 = -.53f, RlLayoutW = 1.83f, RlLayoutD = 1.10f;
    internal static readonly Vector3 RlWheelCenter = new Vector3(1.08f, RlTop + .03f, 0f);
    internal const float RlWheelRadius = .42f;

    /// <summary>賭ける場所の絵の中の点（元の絵の px）を、台の上の位置（x, z）にする。</summary>
    internal static Vector2 RouletteLayoutPoint(float srcX, float srcY)
    {
        Vector2 p = ImagePoint01(Img.Betting, srcX, srcY);   // 左下が原点の 0〜1
        return new Vector2(RlLayoutX0 - p.x * RlLayoutW, RlLayoutZ0 + (1f - p.y) * RlLayoutD);
    }

    internal static Mesh RouletteTable() => Build("roulette_table", lp =>
    {
        foreach (float x in new[] { -1.0f, 1.0f }) lp.Prism(Offset(Chamfer(.55f, .7f, .1f), new Vector2(x, 0)), .06f, .58f, "wood_dark");
        lp.Prism(Chamfer(2.9f, 1.0f, .12f), 0, .06f, "ink");
        lp.Prism(Chamfer(RlLength - .14f, RlDepth - .14f, .12f), .58f, .84f, "wood", null, true);
        // 革のふち（輪）と、その内側のフェルト
        lp.Ring(Chamfer(RlLength, RlDepth, .14f), Chamfer(RlLength - .16f, RlDepth - .16f, .1f), .84f, RlTop + .035f, "maroon", "maroon_dark");
        lp.Prism(Chamfer(RlLength - .16f, RlDepth - .16f, .1f), .84f, RlTop, "felt_dark", "felt");
        // 賭ける場所の絵
        Rect r = ImageRect(Img.Betting);
        float y = RlTop + .002f;
        lp.Image(new Vector3(RlLayoutX0, y, RlLayoutZ0 + RlLayoutD), new Vector3(RlLayoutX0 - RlLayoutW, y, RlLayoutZ0 + RlLayoutD),
                 new Vector3(RlLayoutX0 - RlLayoutW, y, RlLayoutZ0), new Vector3(RlLayoutX0, y, RlLayoutZ0), r, Vector3.up);
        // 盤の受け皿（回らない外側）
        lp.Push(new Vector3(RlWheelCenter.x, RlTop, RlWheelCenter.z));
        lp.Lathe(new[] { (.5f, 0f), (.5f, .07f), (.45f, .07f), (.43f, .02f) }, 40, b => b == 2 ? "wood_dark" : "wood", 0, false);
        lp.Pop();
        // 立て札（盤の奥）
        lp.Box(new Vector3(RlWheelCenter.x - .04f, RlTop, -.72f), new Vector3(RlWheelCenter.x + .04f, 1.42f, -.64f), "gold_dark");
        lp.Prism(Offset(Chamfer(1.16f, .1f, .03f), new Vector2(RlWheelCenter.x, -.68f)), 1.36f, 2.06f, "ink", "gold");
    });

    /// <summary>回る盤（真上から見た絵を貼った円盤と、真ん中のつまみ）。原点は盤の中心。</summary>
    internal static Mesh RouletteWheel() => Build("roulette_wheel", lp =>
    {
        var disc = Ellipse(RlWheelRadius, RlWheelRadius, 48);     // 大きい円は、辺の数を多く（角が見えないように）
        lp.Prism(disc, -.02f, 0, "wood_dark");
        // 絵は「上が +Z（0 のマス）、右が +X」。TopImage は「絵の上が -Z、左が +X」なので、半回転して貼る
        lp.Push(Vector3.zero, new Vector3(0, 180, 0));
        lp.TopImage(disc, .001f, ImageRect(Img.Wheel), new Rect(-RlWheelRadius, -RlWheelRadius, RlWheelRadius * 2, RlWheelRadius * 2));
        lp.Pop();
        lp.Lathe(new[] { (.09f, 0f), (.07f, .05f), (.025f, .07f), (.025f, .13f), (.045f, .15f), (0f, .17f) }, 16, "gold", 0, false);
        for (int i = 0; i < 4; i++)
        {
            lp.Push(Vector3.zero, new Vector3(0, i * 90, 0));
            lp.Box(new Vector3(-.012f, .09f, .02f), new Vector3(.012f, .11f, .15f), "gold");
            lp.Pop();
        }
    });

    internal static Mesh RouletteBall() => Build("roulette_ball", lp =>
        lp.Lathe(new[] { (0f, -.022f), (.019f, -.011f), (.022f, 0f), (.019f, .011f), (0f, .022f) }, 6, "white", 0, false));

    /// <summary>チップ 1 枚ぶん（高さ 1.2cm）。山の高さは、置く側が y の伸ばしで決める。</summary>
    internal static Mesh Chip(string color) => Build("chip_" + color, lp =>
        lp.Cyl(Vector3.zero, .0375f, .012f, 8, color, false, "chip_white"));

    // ================================================================ エレベーター
    // 箱の内のりの半分・壁の厚さ・扉の開口の半分・扉の高さ・箱の天井（RoomiesCasinoBuilder の当たり判定と同じ値）
    internal const float EvHalfX = 1.2f, EvHalfZ = 1.025f, EvWall = .2f, EvDoorHalf = .7f, EvDoorH = 2.3f, EvCeil = 2.6f, EvBack = .1f;

    /// <summary>その階の、箱を囲む壁（外から見える面）と、箱の内装。height はその階の天井までの高さ。</summary>
    internal static Mesh ElevatorShaft(float height, int floor) => Build("elevator_shaft_" + (floor + 1), lp =>
    {
        float ox = EvHalfX + EvWall, fz0 = EvHalfZ, fz1 = EvHalfZ + EvWall;
        // 外の壁（板張り）と、足元・上の帯
        lp.Box(new Vector3(-ox, 0, -EvHalfZ), new Vector3(-EvHalfX, height, fz1), "wood");
        lp.Box(new Vector3(EvHalfX, 0, -EvHalfZ), new Vector3(ox, height, fz1), "wood");
        lp.Box(new Vector3(-EvHalfX, 0, fz0), new Vector3(-EvDoorHalf, height, fz1), "wood");
        lp.Box(new Vector3(EvDoorHalf, 0, fz0), new Vector3(EvHalfX, height, fz1), "wood");
        lp.Box(new Vector3(-EvDoorHalf, EvDoorH, fz0), new Vector3(EvDoorHalf, height, fz1), "wood");
        lp.Box(new Vector3(-ox - .02f, 0, fz1), new Vector3(ox + .02f, .16f, fz1 + .03f), "wood_dark");
        foreach (float x in new[] { -ox, ox })
            lp.Box(new Vector3(x - .03f, 0, fz1 - .04f), new Vector3(x + .03f, height, fz1 + .04f), "wood_dark");
        // 扉の枠（金）と、上の階数の札
        lp.Box(new Vector3(-EvDoorHalf - .1f, 0, fz1), new Vector3(-EvDoorHalf, EvDoorH + .1f, fz1 + .05f), "gold");
        lp.Box(new Vector3(EvDoorHalf, 0, fz1), new Vector3(EvDoorHalf + .1f, EvDoorH + .1f, fz1 + .05f), "gold");
        lp.Box(new Vector3(-EvDoorHalf - .1f, EvDoorH, fz1), new Vector3(EvDoorHalf + .1f, EvDoorH + .1f, fz1 + .05f), "gold");
        lp.Prism(Offset(Chamfer(.9f, .06f, .02f), new Vector2(0, fz1 + .03f)), EvDoorH + .18f, EvDoorH + .42f, "ink", "gold_dark");
        // 箱の内装：敷物・腰の板・上の壁・天井・手すり・灯
        lp.Box(new Vector3(-EvHalfX, 0, -EvHalfZ), new Vector3(EvHalfX, .012f, fz1), "purple_dark");
        foreach (var (x0, x1) in new[] { (-EvHalfX, -EvHalfX + .02f), (EvHalfX - .02f, EvHalfX) })
        {
            lp.Box(new Vector3(x0, 0, -EvHalfZ), new Vector3(x1, 1.0f, fz0), "wood_light");
            lp.Box(new Vector3(x0, 1.0f, -EvHalfZ), new Vector3(x1, EvCeil, fz0), "cream");
        }
        lp.Box(new Vector3(-EvHalfX, 0, -EvHalfZ + EvBack), new Vector3(EvHalfX, 1.0f, -EvHalfZ + EvBack + .02f), "wood_light");
        lp.Box(new Vector3(-EvHalfX, 1.0f, -EvHalfZ + EvBack), new Vector3(EvHalfX, EvCeil, -EvHalfZ + EvBack + .02f), "cream");
        lp.Box(new Vector3(-EvHalfX, EvCeil, -EvHalfZ), new Vector3(EvHalfX, EvCeil + .1f, fz0), "wood_dark");
        foreach (float x in new[] { -EvHalfX + .05f, EvHalfX - .05f })
            lp.Box(new Vector3(x - .025f, .98f, -EvHalfZ + .3f), new Vector3(x + .025f, 1.03f, fz0 - .2f), "gold");
        lp.Prism(Chamfer(.6f, .6f, .12f), EvCeil - .05f, EvCeil, "warm", null, true, 1f, true);
        // 盤の板（奥の壁。ボタンと階数のランプが載る）
        lp.Prism(Offset(Chamfer(.62f, .05f, .015f), new Vector2(0, -EvHalfZ + EvBack + .045f)), .74f, 2.0f, "ink", "gold_dark");
        lp.Prism(Offset(Chamfer(.9f, .05f, .015f), new Vector2(0, -EvHalfZ + EvBack + .045f)), 2.08f, 2.32f, "ink", "gold_dark");
    });

    /// <summary>引き戸 1 枚（幅は開口の半分）。原点は扉の足元の真ん中。</summary>
    internal static Mesh ElevatorDoor() => Build("elevator_door", lp =>
    {
        float hw = EvDoorHalf / 2;
        lp.Box(new Vector3(-hw, 0, -.04f), new Vector3(hw, EvDoorH, .04f), "brass");
        foreach (float z in new[] { .04f, -.045f })
        {
            lp.Box(new Vector3(-hw + .07f, .25f, z), new Vector3(hw - .07f, EvDoorH - .25f, z + .005f), "gold_dark");
            // 菱形の飾り
            lp.Push(new Vector3(0, EvDoorH * .55f, z + (z > 0 ? .006f : -.001f)), new Vector3(0, 0, 45));
            lp.Box(new Vector3(-.09f, -.09f, 0), new Vector3(.09f, .09f, .004f), "pink_dark");
            lp.Pop();
        }
    });

    /// <summary>ボタンの土台とふた（大きさは当たり判定に合わせる）。</summary>
    internal static Mesh ElevatorButton(Vector3 size, bool keySlot) => Build($"elevator_button_{Mathf.RoundToInt(size.x * 100)}_{Mathf.RoundToInt(size.y * 100)}{(keySlot ? "_key" : "")}", lp =>
    {
        var h = size / 2f;
        lp.Box(new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, h.y, 0), "gold");
        lp.Box(new Vector3(-h.x + .03f, -h.y + .03f, 0), new Vector3(h.x - .03f, h.y - .03f, h.z), keySlot ? "metal" : "cream");
        if (keySlot)
        {
            lp.Box(new Vector3(-.012f, -.045f, h.z), new Vector3(.012f, .02f, h.z + .004f), "ink");
            lp.Push(new Vector3(0, .03f, h.z + .002f), new Vector3(90, 0, 0));
            lp.Cyl(new Vector3(0, -.002f, 0), .03f, .004f, 8, "ink");
            lp.Pop();
        }
    });

    /// <summary>点いた見た目（ボタンのふたの上・階数のランプ）。光る面だけ。</summary>
    internal static Mesh GlowPlate(float width, float height, string color) => Build($"plate_{color}_{Mathf.RoundToInt(width * 100)}_{Mathf.RoundToInt(height * 100)}_glow", lp =>
        lp.Box(new Vector3(-width / 2, -height / 2, 0), new Vector3(width / 2, height / 2, .008f), color, true));

    /// <summary>消えている階数のランプ（3 つ並び）。</summary>
    internal static Mesh LampRowOff() => Build("elevator_lamps_off", lp =>
    {
        for (int k = 0; k < 3; k++)
            lp.Box(new Vector3((1 - k) * .26f - .09f, -.06f, 0), new Vector3((1 - k) * .26f + .09f, .06f, .006f), "maroon_dark");
    });

    // ================================================================ 店の人（Bob とは別のキャラ。丸い体・短い手・単純な目）
    internal static Mesh Clerk() => Clerk("clerk", "purple_dark", "felt", "felt_dark");

    /// <summary>店の人（制服の色と、ひさしの色を変えられる。key は形の名前）。カジノは Clerk()、コンビニは制服をさんご色にした物。</summary>
    internal static Mesh Clerk(string key, string vest, string visor, string visorDark) => Build(key, lp =>
    {
        // 体：下からズボン・ベスト・顔
        lp.Lathe(new[] { (.22f, 0f), (.36f, .08f), (.44f, .38f), (.45f, .72f), (.42f, 1.0f), (.34f, 1.24f), (.2f, 1.4f), (0f, 1.46f) }, 10,
            b => b == 0 ? "ink" : b <= 2 ? vest : "teal_pale", Mathf.PI / 10, false, true);
        // シャツの前とボタン、蝶ネクタイ
        lp.Box(new Vector3(-.1f, .42f, .4f), new Vector3(.1f, .78f, .455f), "white");
        foreach (float y in new[] { .5f, .62f }) lp.Box(new Vector3(-.02f, y, .455f), new Vector3(.02f, y + .04f, .47f), "gold");
        foreach (float s in new[] { -1f, 1f })
        {
            // 羽：結び目から外へ広がる台形（正面から見た形）
            Vector3 a = new Vector3(s * .02f, .8f, .46f), b = new Vector3(s * .02f, .84f, .46f), c = new Vector3(s * .13f, .88f, .45f), d = new Vector3(s * .13f, .76f, .45f);
            lp.Quad(a, b, c, d, "pink", Vector3.forward);
        }
        lp.Box(new Vector3(-.03f, .785f, .44f), new Vector3(.03f, .855f, .475f), "pink_dark");
        // 目（縦長の黒）とほっぺ
        foreach (float x in new[] { -.14f, .14f })
        {
            lp.Box(new Vector3(x - .03f, 1.08f, .4f), new Vector3(x + .03f, 1.2f, .43f), "black");
            lp.Box(new Vector3(x * 1.75f - .035f, 1.0f, .37f), new Vector3(x * 1.75f + .035f, 1.04f, .4f), "pink_pale");
        }
        // 緑のひさし（サンバイザー）
        lp.Lathe(new[] { (.37f, 1.3f), (.37f, 1.36f), (.3f, 1.4f) }, 10, visor, Mathf.PI / 10, true);
        lp.Push(new Vector3(0, 1.3f, .2f), new Vector3(12, 0, 0));
        lp.Prism(Offset(Ellipse(.36f, .26f, 8), new Vector2(0, .16f)), 0, .03f, visorDark, visor, true);
        lp.Pop();
        // 短い手
        foreach (float s in new[] { -1f, 1f })
        {
            lp.Push(new Vector3(s * .44f, .72f, .1f), new Vector3(-30, 0, s * -28));
            lp.Lathe(new[] { (.075f, 0f), (.09f, .1f), (.08f, .22f), (0f, .27f) }, 6, b => b == 2 ? "teal_pale" : "white", 0, false, true);
            lp.Pop();
        }
    });
}
#endif
