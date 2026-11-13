#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 作り直し3-2：牢屋を「町の留置所（警察の詰め所）」の建物として0から設計する（2026/11/13 ゆせさんの依頼 20:34）。Rebuild/1 Jail から呼ぶ。
/// 外形：x 0.8〜12.45、z 13.2〜22.65、高さ 4.3m。正面（-z、通りの側）にレンガとクリームの石の外壁、青い警察灯、ひさし、看板。
/// 中は3つ：受付の待合室（左。入口から入る）・面会の廊下（格子の前）・留置室（右。元の場所と当たり判定のまま）。
/// 裏（z 22.65〜24）は取引の NPC（Dealer）への通り道として空けておく。
/// 当たり判定：外壁（入口を空ける）・屋根・待合室と廊下の床・受付のカウンター。留置室を閉じ込める当たり判定は元の「Community Jail」のまま。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const float PX0 = .8f, PX1 = 12.45f, PZ0 = 13.2f, PZ1 = 22.65f, PT = .3f, PTop = 4.3f;
    const float PDoorX0 = 2.2f, PDoorX1 = 3.5f, PDoorH = 2.4f;
    const float PCellWallX = 4.71f, PCorridorZ1 = 15.05f;   // 留置室の左の壁（元の当たり判定）と、格子の前の廊下の奥

    static void StationBuilding(Transform g, Transform oldJail, Transform work)
    {
        MoveSceneBoxOutOfStation();
        var cellLight = oldJail != null ? oldJail.Find("Cell light")?.GetComponent<Light>() : null;
        if (cellLight != null) cellLight.intensity = 2.6f;   // 夜の留置室が暗すぎないように（元 1.5）
        StationShell(g);
        StationFacade(g, oldJail);
        StationLobby(g, work);
        StationCorridor(g);
    }

    // ================================================================ 外壁・屋根・床（当たり判定つき）
    static void StationShell(Transform g)
    {
        float ix0 = PX0 + PT, ix1 = PX1 - PT, iz0 = PZ0 + PT, iz1 = PZ1 - PT;
        // 外壁（町の外壁と同じ作り：厚い板に窓と扉の開口）
        var front = new Facade { name = "Station front", wall = "police_brick", frame = "frame_h_navy", origin = new Vector3(PX1, 0, iz0), yaw = 180, width = PX1 - PX0, height = PTop, thick = PT, band = true, collider = false };
        float L(float worldX) => PX1 - worldX;   // 正面の外壁の左端（東）からの位置
        front.openings.Add(new Opening(L(PDoorX1), L(PDoorX0), 0, PDoorH, Kind.Door, "glass_home_lit"));
        foreach (var (x0, x1) in new[] { (5.6f, 7.0f), (8.0f, 9.4f), (10.4f, 11.8f) }) front.openings.Add(new Opening(L(x1), L(x0), 1.15f, 2.45f, Kind.Window, "glass_home_lit"));
        front.openings.Add(new Opening(L(4.4f), L(3.9f), 1.15f, 2.45f, Kind.Window, "glass_home_lit"));
        Build(g, front);
        var left = new Facade { name = "Station west", wall = "police_brick", frame = "frame_h_navy", origin = new Vector3(ix0, 0, PZ0), yaw = -90, width = PZ1 - PZ0, height = PTop, thick = PT, band = true, collider = false };
        left.openings.Add(new Opening(3.0f, 4.2f, 1.15f, 2.45f, Kind.Window, "glass_home_lit"));
        left.openings.Add(new Opening(6.0f, 7.2f, 1.15f, 2.45f, Kind.Window, "glass_home_dark"));
        Build(g, left);
        var right = new Facade { name = "Station east", wall = "police_brick", frame = "frame_h_navy", origin = new Vector3(ix1, 0, PZ1), yaw = 90, width = PZ1 - PZ0, height = PTop, thick = PT, band = true, collider = false };
        Build(g, right);
        var back = new Facade { name = "Station back", wall = "police_brick", frame = "frame_h_navy", origin = new Vector3(PX0, 0, iz1), yaw = 0, width = PX1 - PX0, height = PTop, thick = PT, band = true, collider = false };
        back.openings.Add(new Opening(1.2f, 2.4f, 1.6f, 2.4f, Kind.Window, "glass_home_dark"));
        Build(g, back);
        // 当たり判定（正面は入口を空ける）
        Solid(g, "Station wall collider", new Vector3(PX0, 0, PZ0), new Vector3(PDoorX0, PTop, iz0));
        Solid(g, "Station wall collider", new Vector3(PDoorX1, 0, PZ0), new Vector3(PX1, PTop, iz0));
        Solid(g, "Station wall collider", new Vector3(PDoorX0, PDoorH, PZ0), new Vector3(PDoorX1, PTop, iz0));
        Solid(g, "Station wall collider", new Vector3(PX0, 0, PZ0), new Vector3(ix0, PTop, PZ1));
        Solid(g, "Station wall collider", new Vector3(ix1, 0, PZ0), new Vector3(PX1, PTop, PZ1));
        Solid(g, "Station wall collider", new Vector3(PX0, 0, iz1), new Vector3(PX1, PTop, PZ1));
        // 屋根：天井の上の厚い板・防水面・パラペット・室外機・アンテナ
        Box(g, "Station roof slab", PX0, PX1, JCeil, 3.8f, PZ0, PZ1, Mat("roof_flat"));
        Solid(g, "Station roof collider", new Vector3(PX0, JCeil, PZ0), new Vector3(PX1, 3.8f, PZ1));
        Box(g, "Station roof deck", PX0 + .3f, PX1 - .3f, 3.8f, 3.84f, PZ0 + .3f, PZ1 - .3f, Mat("roof_flat"));
        Box(g, "Station coping", PX0 - .05f, PX1 + .05f, PTop, PTop + .08f, PZ0 - .05f, PZ1 + .05f, Mat("police_stone"));
        Box(g, "Station AC unit", 8.0f, 9.4f, 3.84f, 4.6f, 19.5f, 20.5f, Mat("ac_unit"));
        Cyl(g, "Station AC fan", new Vector3(8.7f, 4.62f, 20f), new Vector3(.8f, .02f, .8f), Mat("metal_dark"));
        Cyl(g, "Radio antenna", new Vector3(11.6f, 5.6f, 21.8f), new Vector3(.06f, 1.8f, .06f), Mat("metal_dark"));
        // 待合室と廊下の床（当たり判定。留置室の床は元のまま）
        Solid(g, "Station floor collider", new Vector3(ix0, 0, iz0), new Vector3(PCellWallX, JFloor, iz1));
        Solid(g, "Station floor collider", new Vector3(PCellWallX, 0, iz0), new Vector3(ix1, JFloor, PCorridorZ1));
        // 入口の段
        Box(g, "Station step", PDoorX0 - .3f, PDoorX1 + .3f, 0, .08f, PZ0 - .6f, PZ0, Mat("police_stone"));
        Solid(g, "Station step collider", new Vector3(PDoorX0 - .3f, 0, PZ0 - .6f), new Vector3(PDoorX1 + .3f, .08f, PZ0));
    }

    // ================================================================ 外観：石の帯・ひさし・警察灯・看板・旗・格子の窓
    static void StationFacade(Transform g, Transform oldJail)
    {
        var stone = Mat("police_stone"); var navy = Mat("frame_h_navy"); var dark = Mat("metal_dark");
        float face = PZ0;
        // 上の石の帯（看板の台）と、角の石
        Box(g, "Station stone band", PX0 - .06f, PX1 + .06f, 3.05f, 3.9f, face - .08f, face, stone);
        foreach (float x in new[] { PX0, PX1 - .4f })
            for (float y = .3f; y < 3.0f; y += .5f)
                Box(g, "Quoin stone", x - .03f, x + .43f, y, y + .38f, face - .05f, face, stone);
        // 看板の文字（元の COMMUNITY JAIL）を石の帯の上へ。通りの側から読める向きのまま
        var sign = oldJail != null ? oldJail.Find("Sign - COMMUNITY JAIL") : null;
        if (sign != null) { sign.localPosition = new Vector3(7.6f, 3.47f, face - .115f); sign.localRotation = Quaternion.identity; sign.localScale = Vector3.one; }
        Box(g, "Station sign plate", 5.4f, 9.8f, 3.2f, 3.75f, face - .1f, face - .08f, navy);
        // 入口：ひさし・青い警察灯（POLICE の文字）・扉の両わきの灯
        Box(g, "Station canopy", PDoorX0 - .45f, PDoorX1 + .45f, 2.7f, 2.82f, face - 1.0f, face, navy);
        Box(g, "Station canopy edge", PDoorX0 - .45f, PDoorX1 + .45f, 2.62f, 2.7f, face - 1.02f, face - .95f, stone);
        foreach (float x in new[] { PDoorX0 - .35f, PDoorX1 + .35f }) RBox(g, "Canopy bracket", new Vector3(x, 2.5f, face - .4f), new Vector3(.06f, .5f, .06f), new Vector3(50, 0, 0), navy);
        Box(g, "Police lamp box", 2.2f, 3.5f, 2.86f, 3.15f, face - .3f, face - .08f, Mat("police_lamp"));
        Box(g, "Police lamp cap", 2.2f, 3.5f, 3.15f, 3.2f, face - .32f, face - .06f, navy);
        NewSignFrom(g, sign, "Sign - POLICE", "POLICE", new Vector3((PDoorX0 + PDoorX1) / 2, 3.0f, face - .315f), 0, .36f, new Color(.05f, .1f, .35f));
        foreach (float x in new[] { PDoorX0 - .3f, PDoorX1 + .3f })
        {
            Box(g, "Door lamp", x - .1f, x + .1f, 1.9f, 2.2f, face - .14f, face, dark);
            Box(g, "Door lamp lens", x - .07f, x + .07f, 1.93f, 2.17f, face - .15f, face - .14f, Mat("bulb"));
        }
        // 廊下の窓の外の格子
        foreach (var (x0, x1) in new[] { (5.6f, 7.0f), (8.0f, 9.4f), (10.4f, 11.8f) })
            for (float x = x0 + .12f; x < x1 - .05f; x += .17f) Cyl(g, "Window bar", new Vector3(x, 1.8f, face - .06f), new Vector3(.035f, .65f, .035f), dark);
        // 旗竿と旗（左前の角）
        Cyl(g, "Flag pole", new Vector3(.45f, 3.2f, face - .4f), new Vector3(.08f, 3.2f, .08f), Mat("handle"));
        Cyl(g, "Flag pole base", new Vector3(.45f, .1f, face - .4f), new Vector3(.4f, .1f, .4f), stone);
        Box(g, "Flag", .48f, 1.6f, 5.55f, 6.25f, face - .41f, face - .39f, Mat("flag_blue"));
        Box(g, "Flag stripe", .48f, 1.6f, 5.83f, 5.97f, face - .415f, face - .385f, Mat("flag_white"));
        // 植え込みと、通りの側の注意書きの柱
        Box(g, "Station hedge", 4.0f, 12.3f, 0, .7f, face - .65f, face - .1f, Mat("planter_leaf"));
        Box(g, "Hedge planter", 3.95f, 12.35f, 0, .3f, face - .7f, face - .05f, stone);
        // 監視カメラ（右前の角）
        RBox(g, "Security camera", new Vector3(PX1 - .2f, 3.0f, face - .25f), new Vector3(.16f, .14f, .34f), new Vector3(20, 25, 0), Mat("ac_unit"));
        // 夜に正面を照らす光（朝は消える）
        SpotLight(g, "Shop light - JAIL", new Vector3(5.5f, 4.6f, 9.5f), new Vector3(5.5f, 1.2f, face), 4.5f, 10f, 110f, LampColor);
        PointLight(g, "Shop light - JAIL police lamp", new Vector3(2.85f, 3.0f, face - .8f), 1.4f, 4f, new Color(.4f, .6f, 1f));
    }

    // ================================================================ 受付の待合室
    static void StationLobby(Transform g, Transform work)
    {
        float ix0 = PX0 + PT, iz0 = PZ0 + PT, iz1 = PZ1 - PT;
        var wood = Mat("planter_wood"); var dark = Mat("metal_dark"); var low = Mat("jail_paint_low"); var high = Mat("jail_paint_high");
        // 床：白黒の市松のタイル
        var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
        floor.name = "Lobby tile floor"; Object.DestroyImmediate(floor.GetComponent<Collider>());
        floor.transform.SetParent(g, false);
        floor.transform.localPosition = new Vector3((ix0 + PCellWallX) / 2, JFloor + .002f, (iz0 + iz1) / 2);
        floor.transform.localRotation = Quaternion.Euler(90, 0, 0);
        floor.transform.localScale = new Vector3(PCellWallX - ix0, iz1 - iz0, 1);
        floor.GetComponent<MeshRenderer>().sharedMaterial = CheckerTile(PCellWallX - ix0, iz1 - iz0);
        floor.isStatic = true;
        // 壁：腰の塗り分けと見切り（左・奥・留置室側・正面の内側）
        var faces = new (Vector3 n, float plane, float a0, float a1)[]
        {
            (Vector3.right, ix0, iz0, iz1), (Vector3.back, iz1, ix0, PCellWallX), (Vector3.left, PCellWallX, PCorridorZ1, iz1),
            (Vector3.forward, iz0, ix0, PDoorX0), (Vector3.forward, iz0, PDoorX1, PCellWallX),
        };
        foreach (var f in faces)
        {
            FaceBox(g, "Lobby wall paint", f.n, f.plane, f.a0, f.a1, JFloor, 1.2f, 0, .02f, low);
            FaceBox(g, "Lobby wall paint", f.n, f.plane, f.a0, f.a1, 1.2f, JCeil, 0, .015f, high);
            FaceBox(g, "Lobby wall trim", f.n, f.plane, f.a0, f.a1, 1.17f, 1.23f, 0, .04f, dark);
            FaceBox(g, "Lobby skirting", f.n, f.plane, f.a0, f.a1, JFloor, JFloor + .12f, 0, .03f, dark);
        }
        Box(g, "Lobby ceiling", ix0, PCellWallX, JCeil - .02f, JCeil, iz0, iz1, high);
        foreach (float z in new[] { 15.5f, 19.0f })
        {
            Box(g, "Ceiling panel light frame", 2.3f, 3.5f, JCeil - .05f, JCeil - .02f, z - .35f, z + .35f, Mat("ac_unit"));
            Box(g, "Ceiling panel light", 2.38f, 3.42f, JCeil - .055f, JCeil - .05f, z - .28f, z + .28f, Mat("bulb"));
        }
        PointLight(g, "Jail light - lobby", new Vector3(2.9f, 3.0f, 17.3f), 3.0f, 8f, new Color(1f, .93f, .82f));
        // 受付のカウンター（奥の方を横切る。当たり判定）と、その後ろの机・椅子・書類棚・旗
        float cz0 = 19.3f, cz1 = 19.9f, cx1 = 3.7f;
        Box(g, "Reception counter", ix0, cx1, JFloor, 1.05f, cz0, cz1, wood);
        Box(g, "Reception counter top", ix0, cx1 + .05f, 1.05f, 1.12f, cz0 - .08f, cz1, Mat("police_stone"));
        Box(g, "Reception counter panel", ix0 + .1f, cx1 - .1f, .3f, .9f, cz0 - .02f, cz0, Mat("frame_h_navy"));
        Box(g, "Reception badge", 2.2f, 2.6f, .45f, .8f, cz0 - .03f, cz0 - .02f, Mat("frame_express"));
        Solid(g, "Reception collider", new Vector3(ix0, 0, cz0 - .08f), new Vector3(cx1 + .05f, 1.12f, cz1));
        Box(g, "Desk monitor", 1.6f, 2.15f, 1.12f, 1.5f, cz1 - .25f, cz1 - .2f, Mat("casino_black"));
        Box(g, "Desk monitor screen", 1.63f, 2.12f, 1.15f, 1.47f, cz1 - .255f, cz1 - .25f, Mat("neon_cyan"));
        Box(g, "Desk phone", 2.6f, 2.85f, 1.12f, 1.2f, cz1 - .45f, cz1 - .25f, Mat("casino_black"));
        Box(g, "Desk papers", 3.0f, 3.35f, 1.12f, 1.16f, cz1 - .5f, cz1 - .25f, Mat("flag_white"));
        Box(g, "Desk bell", 3.5f, 3.6f, 1.12f, 1.18f, cz0 - .02f, cz0 + .08f, Mat("casino_gold"));
        Box(g, "Officer chair seat", 2.1f, 2.6f, .5f, .58f, 20.4f, 20.9f, Mat("casino_black"));
        Box(g, "Officer chair back", 2.1f, 2.6f, .58f, 1.15f, 20.85f, 20.92f, Mat("casino_black"));
        Cyl(g, "Officer chair post", new Vector3(2.35f, .33f, 20.65f), new Vector3(.06f, .17f, .06f), dark);
        for (int k = 0; k < 2; k++)
        {
            float x0 = 2.9f + k * .6f;
            Box(g, "Filing cabinet", x0, x0 + .55f, JFloor, 1.4f, iz1 - .6f, iz1, Mat("ac_unit"));
            for (int d = 0; d < 3; d++) Box(g, "Filing drawer handle", x0 + .2f, x0 + .35f, .5f + d * .4f, .54f + d * .4f, iz1 - .62f, iz1 - .6f, dark);
        }
        Cyl(g, "Flag stand pole", new Vector3(1.5f, 1.2f, 21.8f), new Vector3(.04f, 1.05f, .04f), Mat("casino_gold"));
        Box(g, "Flag stand flag", 1.52f, 2.2f, 1.6f, 2.1f, 21.79f, 21.81f, Mat("flag_blue"));
        // 待合のベンチ（左の壁）・掲示板（手配書）・給水器・観葉植物
        Box(g, "Waiting bench seat", ix0 + .05f, ix0 + .5f, .45f, .52f, 14.2f, 16.6f, wood);
        Box(g, "Waiting bench back", ix0, ix0 + .08f, .52f, 1.0f, 14.2f, 16.6f, wood);
        foreach (float z in new[] { 14.35f, 16.45f }) Box(g, "Waiting bench leg", ix0 + .1f, ix0 + .45f, JFloor, .45f, z - .05f, z + .05f, dark);
        Box(g, "Notice board", ix0, ix0 + .04f, 1.4f, 2.4f, 16.9f, 18.6f, Mat("planter_wood"));
        int p = 0;
        foreach (var (z, y) in new[] { (17.2f, 2.0f), (17.75f, 2.05f), (18.25f, 1.95f), (17.4f, 1.6f), (18.1f, 1.6f) })
        {
            Box(g, "Wanted poster", ix0 + .04f, ix0 + .05f, y - .2f, y + .2f, z - .15f, z + .15f, Mat("flag_white"));
            Box(g, "Wanted poster face", ix0 + .05f, ix0 + .055f, y - .05f, y + .12f, z - .08f, z + .08f, Mat(p++ % 2 == 0 ? "frame_express" : "jail_paint_low"));
        }
        Box(g, "Water cooler", 4.2f, 4.6f, JFloor, 1.0f, 16.0f, 16.4f, Mat("ac_unit"));
        Cyl(g, "Water bottle", new Vector3(4.4f, 1.25f, 16.2f), new Vector3(.28f, .25f, .28f), Mat("jail_frosted"));
        Cyl(g, "Lobby plant pot", new Vector3(4.25f, JFloor + .25f, 21.8f), new Vector3(.4f, .25f, .4f), Mat("roof_terra"));
        Cyl(g, "Lobby plant", new Vector3(4.25f, JFloor + .8f, 21.8f), new Vector3(.55f, .35f, .55f), Mat("planter_leaf"));
        // 奥の壁の時計と、警察の紋章
        Cyl(g, "Wall clock", new Vector3(1.9f, 2.6f, iz1 - .03f), new Vector3(.4f, .02f, .4f), Mat("flag_white")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Cyl(g, "Wall clock rim", new Vector3(1.9f, 2.6f, iz1 - .02f), new Vector3(.44f, .015f, .44f), Mat("metal_dark")).transform.localRotation = Quaternion.Euler(90, 0, 0);
        Box(g, "Clock hand", 1.89f, 1.91f, 2.6f, 2.75f, iz1 - .055f, iz1 - .05f, Mat("metal_dark"));
        Box(g, "Clock hand", 1.9f, 2.0f, 2.59f, 2.61f, iz1 - .055f, iz1 - .05f, Mat("metal_dark"));
        Box(g, "Police crest", 3.2f, 3.9f, 2.2f, 2.9f, iz1 - .03f, iz1, Mat("frame_h_navy"));
        RBox(g, "Police crest star", new Vector3(3.55f, 2.55f, iz1 - .04f), new Vector3(.32f, .32f, .02f), new Vector3(0, 0, 45), Mat("casino_gold"));
        RBox(g, "Police crest star", new Vector3(3.55f, 2.55f, iz1 - .045f), new Vector3(.32f, .32f, .02f), Vector3.zero, Mat("casino_gold"));
        // 案内の札（廊下の上）
        NewSignFrom(g, null, "Sign - VISITORS", "VISITORS", new Vector3(PCellWallX - .03f, 2.9f, 14.3f), 90, .5f, new Color(.95f, .95f, .9f), work);
        Box(g, "Visitors sign plate", PCellWallX - .01f, PCellWallX + .01f, 2.75f, 3.05f, 13.7f, 14.9f, Mat("frame_h_navy"));
    }

    // ================================================================ 面会の廊下（格子の前）
    static void StationCorridor(Transform g)
    {
        float iz0 = PZ0 + PT, ix1 = PX1 - PT;
        var low = Mat("jail_paint_low"); var high = Mat("jail_paint_high"); var dark = Mat("metal_dark");
        Box(g, "Corridor floor", PCellWallX, ix1, JFloor - .01f, JFloor + .003f, iz0, PCorridorZ1, Mat("jail_floor"));
        Box(g, "Visitor line", PCellWallX + .2f, ix1 - .2f, JFloor + .003f, JFloor + .006f, PCorridorZ1 - .55f, PCorridorZ1 - .48f, Mat("frame_express"));
        // 正面の内側（窓のあいだ）と、右の壁
        foreach (var (a0, a1) in new[] { (PCellWallX, 5.6f), (7.0f, 8.0f), (9.4f, 10.4f), (11.8f, ix1) })
        {
            FaceBox(g, "Corridor wall paint", Vector3.forward, iz0, a0, a1, JFloor, 1.2f, 0, .02f, low);
            FaceBox(g, "Corridor wall paint", Vector3.forward, iz0, a0, a1, 1.2f, JCeil, 0, .015f, high);
        }
        foreach (var (a0, a1) in new[] { (5.6f, 7.0f), (8.0f, 9.4f), (10.4f, 11.8f) })
        {
            FaceBox(g, "Corridor wall paint", Vector3.forward, iz0, a0, a1, JFloor, 1.15f, 0, .02f, low);
            FaceBox(g, "Corridor wall paint", Vector3.forward, iz0, a0, a1, 2.45f, JCeil, 0, .015f, high);
        }
        FaceBox(g, "Corridor wall trim", Vector3.forward, iz0, PCellWallX, ix1, 1.17f, 1.23f, 0, .04f, dark);
        FaceBox(g, "Corridor wall paint", Vector3.left, ix1, iz0, PCorridorZ1, JFloor, 1.2f, 0, .02f, low);
        FaceBox(g, "Corridor wall paint", Vector3.left, ix1, iz0, PCorridorZ1, 1.2f, JCeil, 0, .015f, high);
        Box(g, "Corridor ceiling", PCellWallX, ix1, JCeil - .02f, JCeil, iz0, PCorridorZ1 - .1f, high);
        // 壁付けの灯と、面会の丸椅子
        foreach (float x in new[] { 6.3f, 9.9f })
        {
            Box(g, "Corridor lamp", x - .2f, x + .2f, 2.7f, 2.85f, iz0, iz0 + .2f, dark);
            Box(g, "Corridor lamp lens", x - .17f, x + .17f, 2.69f, 2.7f, iz0 + .02f, iz0 + .18f, Mat("bulb"));
        }
        PointLight(g, "Jail light - corridor", new Vector3(8.4f, 2.9f, 14.2f), 2.6f, 8f, new Color(1f, .93f, .82f));
        foreach (float x in new[] { 6.6f, 10.2f })
        {
            Cyl(g, "Visitor stool", new Vector3(x, JFloor + .42f, 14.0f), new Vector3(.36f, .04f, .36f), Mat("frame_h_navy"));
            Cyl(g, "Visitor stool post", new Vector3(x, JFloor + .2f, 14.0f), new Vector3(.05f, .2f, .05f), dark);
        }
    }

    /// <summary>シーンの持ち運べる配達の箱（SM_Box_Wood_L）のうち、留置所の外壁にめり込む所に置かれた物を、入口の前の外へ少し動かす。</summary>
    static void MoveSceneBoxOutOfStation()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool moved = false;
        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.GetComponent<CarryableObject>() == null) continue;
            var p = root.transform.position;
            if (p.x > PX0 - .6f && p.x < PX1 + .6f && p.z > PZ0 - .8f && p.z < PZ1 + .6f)
            {
                root.transform.position = new Vector3(p.x, p.y, PZ0 - 1.6f);
                UnityEditor.EditorUtility.SetDirty(root.transform);
                moved = true;
            }
        }
        if (moved) { UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene); UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene); }
    }

    /// <summary>白黒の市松のタイル（30cm 角）。</summary>
    static Material CheckerTile(float w, float d)
    {
        var tex = RoomiesNature.Tex("station_checker", 512, 512, (float u, float v, out Color c, out float h) =>
        {
            int iu = Mathf.FloorToInt(u * 4), iv = Mathf.FloorToInt(v * 4);
            float fu = u * 4 - iu, fv = v * 4 - iv;
            float grout = Mathf.Max(RoomiesNature.Smooth(.025f, 0, Mathf.Min(fu, 1 - fu)), RoomiesNature.Smooth(.025f, 0, Mathf.Min(fv, 1 - fv)));
            float t = (iu + iv) % 2 == 0 ? .88f : .2f;
            t *= .95f + .08f * RoomiesNature.Fbm(u, v, 16, 2, 3);
            t = Mathf.Lerp(t, .45f, grout);
            c = new Color(t, t * .99f, t * .96f, 1); h = 1 - grout;
        }, 2f, out var n);
        var m = RoomiesNature.Lit("M_Station_Checker", tex, n, Color.white, .45f, false, .5f);
        m.SetTextureScale("_BaseMap", new Vector2(w / 1.2f, d / 1.2f));
        return m;
    }

    /// <summary>看板の文字を写して作る（src が無ければ町のプレハブの AFTER HOURS を写す）。</summary>
    static void NewSignFrom(Transform g, Transform src, string name, string text, Vector3 pos, float yaw, float scale, Color color, Transform work = null)
    {
        if (src == null && work != null) src = work.Find("Sign - AFTER HOURS");
        if (src == null) src = g.root.GetComponentsInChildren<TMPro.TextMeshPro>(true).Select(t => t.transform).FirstOrDefault(t => t.name.StartsWith("Sign - "));
        if (src == null) return;
        var copy = Object.Instantiate(src.gameObject, g);
        copy.name = name;
        var tmp = copy.GetComponent<TMPro.TextMeshPro>();
        tmp.text = text; tmp.color = color;
        copy.transform.localPosition = pos; copy.transform.localRotation = Quaternion.Euler(0, yaw, 0); copy.transform.localScale = Vector3.one * scale;
        ((RectTransform)copy.transform).sizeDelta = new Vector2(Mathf.Max(2f, text.Length * .55f), .6f);
    }
}
#endif
