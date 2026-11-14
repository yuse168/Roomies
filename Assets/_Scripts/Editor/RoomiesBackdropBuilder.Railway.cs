#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 遠景の高架と電車（Build Distant Backdrop から呼ぶ）。乱数は使わない（ほかの遠景の乱数の順を変えないため）。
/// 2026/11/14 作り直し：
///  ・橋げたの下に鋼の桁（I形）を3本通し、桁のあいだに斜めの補強を入れる。下面は明るい板（黒い板に見えないように）
///  ・橋脚は丸い柱にし、柱の頭の台と、桁を受ける支え（沓）を置く。橋脚に沿って排水管を下ろす
///  ・高欄の上に防音の壁（ガラスと柱）、架線の柱・腕・線
///  ・電車（近い線路だけ）：角丸の車体、引っ込んだ窓（暗い帯の上に柱を立てる）、丸い先頭、台車と車輪、車両のあいだの幌
/// 細かい物（補強・防音の壁の柱）は町に近い所（|x| ≤ DetailRange）だけに置く（遠くは見えないので）。
/// </summary>
internal static partial class RoomiesBackdropBuilder
{
    const float DeckThick = 1.6f;          // 橋げたの厚さ（上面 = deckHeight + 0.8）
    const float GirderH = 1.6f;            // 桁の高さ（橋げたの下面から）
    static readonly float[] GirderZ = { -2.6f, 0f, 2.6f };
    const float DetailRange = 130f;        // 補強・防音の壁の柱を置く範囲（線路の中心から）
    const float BraceStep = 8f, NoisePostStep = 6f, CatenaryStep = 40f;

    private static void BuildRailway(Transform parent, string name, Vector3 position, float yaw,
                                     float deckHeight, float length, bool pierCollider,
                                     float waitSeconds, float startDelay, float speed,
                                     bool detailed = true)
    {
        Transform group = Group(parent, name);
        group.position = position;
        group.localRotation = Quaternion.Euler(0f, yaw, 0f);

        Transform visual = Group(group, "Visual");
        float deckBottom = deckHeight - DeckThick / 2f, deckTop = deckHeight + DeckThick / 2f;
        float girderBottom = deckBottom - GirderH;

        // 橋げた（親のローカルX方向に伸びる）。下面は明るい板、横は細い暗い見切り
        Box(visual, "Deck", new Vector3(0f, deckHeight, 0f), new Vector3(length, DeckThick, 9f), "cream");
        Box(visual, "Viaduct soffit", new Vector3(0f, deckBottom - .03f, 0f), new Vector3(length, .06f, 8.6f), "paper");
        foreach (float side in new[] { -4.56f, 4.56f })
            Box(visual, "Deck fascia", new Vector3(0f, deckBottom + .15f, side), new Vector3(length, .3f, .12f), "ink");

        // 鋼の桁（I形：腹板と下のつば）
        foreach (float z in GirderZ)
        {
            Box(visual, "Steel girder web", new Vector3(0f, deckBottom - GirderH / 2f, z), new Vector3(length, GirderH, .3f), "teal");
            Box(visual, "Steel girder flange", new Vector3(0f, girderBottom + .12f, z), new Vector3(length, .24f, 1.1f), "teal");
        }

        // 高欄（両側）と、その上の防音の壁（ガラス）
        foreach (float side in new[] { -4.2f, 4.2f })
        {
            Box(visual, "Parapet", new Vector3(0f, deckHeight + 1.5f, side),
                new Vector3(length, 1.4f, 0.4f), "cream");
            Box(visual, "Parapet cap", new Vector3(0f, deckHeight + 2.25f, side),
                new Vector3(length, 0.2f, 0.7f), "silver");
            if (detailed)
            {
                Box(visual, "Noise wall glass", new Vector3(0f, deckHeight + 2.95f, side),
                    new Vector3(length, 1.2f, .08f), "blue");
                Box(visual, "Noise wall rail", new Vector3(0f, deckHeight + 3.6f, side),
                    new Vector3(length, .12f, .2f), "silver");
            }
        }

        // 枕木とレール（遠い高架は枕木を省く。見えない作り込みは入れない）
        foreach (float side in new[] { -1.6f, 1.6f })
            Box(visual, "Rail", new Vector3(0f, deckHeight + 1.0f, side),
                new Vector3(length, 0.22f, 0.3f), "silver");

        if (detailed)
        {
            for (float x = -length / 2f + 4f; x <= length / 2f - 4f; x += 10f)
                Box(visual, "Sleeper", new Vector3(x, deckHeight + 0.88f, 0f),
                    new Vector3(0.6f, 0.2f, 4.4f), "wood");

            // 桁のあいだの斜めの補強（町に近い所だけ。向きを交互に）と、防音の壁の柱
            int k = 0;
            for (float x = -DetailRange; x <= DetailRange; x += BraceStep, k++)
            {
                for (int bay = 0; bay < GirderZ.Length - 1; bay++)
                {
                    float z0 = GirderZ[bay], z1 = GirderZ[bay + 1];
                    float dz = z1 - z0, dy = GirderH - .4f;
                    float ang = Mathf.Atan2(dy, dz) * Mathf.Rad2Deg * (((k + bay) % 2 == 0) ? 1f : -1f);
                    BoxRot(visual, "Steel cross brace", new Vector3(x, deckBottom - GirderH / 2f, (z0 + z1) / 2f),
                        new Vector3(.18f, Mathf.Sqrt(dz * dz + dy * dy), .18f), new Vector3(ang - 90f, 0f, 0f), "teal");
                }
                Box(visual, "Steel bottom strut", new Vector3(x, girderBottom + .3f, 0f),
                    new Vector3(.16f, .16f, GirderZ[GirderZ.Length - 1] - GirderZ[0]), "teal");
            }
            for (float x = -DetailRange; x <= DetailRange; x += NoisePostStep)
                foreach (float side in new[] { -4.2f, 4.2f })
                    Box(visual, "Steel post", new Vector3(x, deckHeight + 2.95f, side), new Vector3(.16f, 1.4f, .16f), "silver");
        }

        // 架線（片側の柱から腕を出し、線路の真上に線を張る）
        {
            float wireY = deckHeight + 7.0f, messengerY = deckHeight + 7.75f;
            for (float x = -length / 2f + 20f; x <= length / 2f - 20f; x += CatenaryStep)
            {
                Box(visual, "Catenary pole", new Vector3(x, (deckTop + messengerY + .4f) / 2f, 3.7f),
                    new Vector3(.35f, messengerY + .4f - deckTop, .35f), "silver");
                Box(visual, "Catenary arm", new Vector3(x, messengerY, 1.7f), new Vector3(.14f, .14f, 4.2f), "silver");
                Box(visual, "Catenary hanger", new Vector3(x, (wireY + messengerY) / 2f, 0f), new Vector3(.06f, messengerY - wireY, .06f), "ink");
            }
            Box(visual, "Contact wire", new Vector3(0f, wireY, 0f), new Vector3(length, .07f, .07f), "ink");
            Box(visual, "Messenger wire", new Vector3(0f, messengerY, 0f), new Vector3(length, .07f, .07f), "ink");
        }

        // 橋脚（近い高架だけ当たり判定を付ける）。丸い柱・柱の頭の台・桁を受ける支え・排水管
        float pierStep = detailed ? 34f : 56f;
        float capTop = girderBottom - .15f;
        for (float x = -length / 2f + 18f; x <= length / 2f - 18f; x += pierStep)
        {
            // 道路や歩道の真上に橋脚が落ちないようにずらす（通行の邪魔になるため）
            if (Mathf.Abs(x) < 9f) continue;

            Transform pier = Group(visual, "Pier");
            pier.localPosition = new Vector3(x, 0f, 0f);

            float columnTop = capTop - 1.0f;
            Column(pier, "Column", new Vector3(0f, columnTop / 2f, 0f), 3.4f, columnTop, "cream", pierCollider);
            Cylinder(pier, "Pier flare", new Vector3(0f, columnTop - .1f, 0f), 4.4f, .6f, "cream", upright: true);
            Box(pier, "Pier cap", new Vector3(0f, capTop - .45f, 0f), new Vector3(3.8f, .9f, 8.2f), "cream");
            Box(pier, "Pier cap edge", new Vector3(0f, capTop - .05f, 0f), new Vector3(4.0f, .1f, 8.4f), "silver");
            foreach (float z in GirderZ)
                Box(pier, "Bearing", new Vector3(0f, capTop + .075f, z), new Vector3(1.0f, .15f, 1.0f), "ink");
            Box(pier, "Footing", new Vector3(0f, 0.4f, 0f),
                new Vector3(5.4f, 0.8f, 5.4f), "ink");
            if (detailed)
            {
                Cylinder(pier, "Drain pipe", new Vector3(1.85f, capTop / 2f, 1.0f), .26f, capTop, "silver", upright: true);
                Box(pier, "Drain pipe bend", new Vector3(1.85f, capTop + .2f, 1.0f), new Vector3(.26f, .4f, .26f), "silver");
                for (float y = 2f; y < capTop - 1f; y += 3f)
                    Box(pier, "Pipe bracket", new Vector3(1.78f, y, 1.0f), new Vector3(.12f, .12f, .4f), "ink");
            }
        }

        BuildTrain(group, deckHeight, waitSeconds, startDelay, speed, length, detailed);
    }

    private static void BuildTrain(Transform parent, float deckHeight,
                                   float waitSeconds, float startDelay, float speed, float length,
                                   bool detailed)
    {
        var train = new GameObject("Train");
        train.transform.SetParent(parent, false);
        train.transform.localPosition = new Vector3(-length / 2f, deckHeight + 1.1f, 0f);

        Transform visual = Group(train.transform, "Visual");

        if (detailed)
        {
            bool wasStatic = RoomiesRestyleKit.MarkStatic;
            RoomiesRestyleKit.MarkStatic = false;   // 走るので static にしない
            RoomiesRestyleKit.Begin();
            try { for (int car = 0; car < 4; car++) DetailedCar(visual, car); }
            finally { RoomiesRestyleKit.MarkStatic = wasStatic; AssetDatabase.SaveAssets(); }
        }
        else
        {
            for (int car = 0; car < 4; car++)
            {
                Transform body = Group(visual, car == 0 ? "Car (lead)" : $"Car {car + 1}");
                body.localPosition = new Vector3(-car * 19f, 0f, 0f);
                string paint = car == 0 ? "coral" : "paper";
                Box(body, "Body", new Vector3(0f, 2.2f, 0f), new Vector3(17f, 3.4f, 3.6f), paint);
                Box(body, "Roof", new Vector3(0f, 4.0f, 0f), new Vector3(16.4f, 0.5f, 3.2f), "silver");
                Box(body, "Skirt", new Vector3(0f, 0.6f, 0f), new Vector3(16.6f, 0.7f, 3.2f), "ink");
                Box(body, "Belt line", new Vector3(0f, 3.1f, 1.81f), new Vector3(16.8f, 0.35f, 0.08f), "coral");
                Box(body, "Belt line", new Vector3(0f, 3.1f, -1.81f), new Vector3(16.8f, 0.35f, 0.08f), "coral");
                // 遠くの電車は、窓や台車まで作り込まない（見えないので）
                if (car == 0)
                    Box(body, "Nose", new Vector3(9.4f, 1.9f, 0f), new Vector3(2.4f, 2.6f, 3.4f), paint);
            }
        }

        // 通過音の口。電車の子なので、音も電車と一緒に動く
        var soundGo = new GameObject("Pass sound");
        soundGo.transform.SetParent(train.transform, false);
        soundGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        AudioSource source = soundGo.AddComponent<AudioSource>();
        source.clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Resources/Audio/train/pass.wav");
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 1f;                     // 位置のある音にする
        source.rolloffMode = AudioRolloffMode.Linear;
        source.minDistance = 25f;
        source.maxDistance = detailed ? 260f : 380f;  // 遠い線路のほうが広く届かせる
        source.dopplerLevel = 0.6f;                   // 通り過ぎるときの音程変化

        var mover = train.AddComponent<BackdropTrain>();
        var serialized = new SerializedObject(mover);
        serialized.FindProperty("startX").floatValue = -length / 2f;
        serialized.FindProperty("endX").floatValue = length / 2f;
        serialized.FindProperty("minSpeed").floatValue = speed * 0.8f;
        serialized.FindProperty("maxSpeed").floatValue = speed * 1.25f;
        // たまに通る程度の間隔。近い線路のほうが短い間隔で来る
        serialized.FindProperty("minWaitSeconds").floatValue = waitSeconds;
        serialized.FindProperty("maxWaitSeconds").floatValue = waitSeconds * 2.6f;
        serialized.FindProperty("startDelay").floatValue = startDelay;
        serialized.FindProperty("passVolume").floatValue = detailed ? 0.7f : 0.35f;
        serialized.FindProperty("passSource").objectReferenceValue = source;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // 車両の横の開口（窓とドア）。x の範囲。ドアは下まで開く
    static readonly (float x0, float x1, bool door)[] CarOpenings =
    {
        (-7.8f, -6.2f, false), (-4.9f, -3.5f, true), (-2.8f, -.2f, false), (.2f, 2.8f, false), (3.5f, 4.9f, true), (6.2f, 7.8f, false),
    };

    /// <summary>近い線路の電車の1両（原点はレールの上面。車輪の下がちょうど 0）。</summary>
    static void DetailedCar(Transform visual, int car)
    {
        Transform body = Group(visual, car == 0 ? "Car (lead)" : $"Car {car + 1}");
        body.localPosition = new Vector3(-car * 19f, 0f, 0f);
        string paint = car == 0 ? "coral" : "paper";
        string belt = car == 0 ? "paper" : "coral";
        const float lowY0 = .9f, lowY1 = 2.6f, bandY1 = 3.7f, topY1 = 4.5f, half = 1.8f;
        float bandY = (lowY1 + bandY1) / 2f;

        // 車体：下の胴（角丸）・窓の帯（暗い色で少し内側）・屋根（角丸）・帯の線
        RBox(body, "Body lower", new Vector3(0f, (lowY0 + lowY1) / 2f, 0f), new Vector3(17f, lowY1 - lowY0, half * 2f), .3f, paint);
        Box(body, "Side panel dark", new Vector3(0f, bandY, 0f), new Vector3(16.7f, bandY1 - lowY1, half * 2f - .16f), "ink");
        RBox(body, "Car top", new Vector3(0f, (bandY1 + topY1) / 2f, 0f), new Vector3(16.8f, topY1 - bandY1, half * 2f), .38f, "silver");
        foreach (float s in new[] { -1f, 1f })
            Box(body, "Belt line", new Vector3(0f, lowY1 - .5f, s * (half + .01f)), new Vector3(16.6f, .16f, .04f), belt);

        // 窓とドア。開口のあいだに車体の色の柱を立てる（窓が引っ込んで見える）
        float prev = -8.35f;
        foreach (var (x0, x1, door) in CarOpenings)
        {
            Posts(body, prev, x0, bandY, bandY1 - lowY1, half, paint);
            prev = x1;
            foreach (float s in new[] { -1f, 1f })
            {
                if (door)
                {
                    Box(body, "Door", new Vector3((x0 + x1) / 2f, (lowY0 + bandY1 - .1f) / 2f, s * (half + .02f)), new Vector3(x1 - x0, bandY1 - .1f - lowY0, .05f), "silver");
                    Box(body, "Door window", new Vector3((x0 + x1) / 2f, bandY + .05f, s * (half + .05f)), new Vector3(x1 - x0 - .3f, .75f, .02f), "blue");
                }
                else
                    Box(body, "Window", new Vector3((x0 + x1) / 2f, bandY + .05f, s * (half - .07f)), new Vector3(x1 - x0 - .08f, .85f, .02f), "blue");
            }
        }
        Posts(body, prev, 8.35f, bandY, bandY1 - lowY1, half, paint);

        // 床下の機器・台車（枠・横の枠・車輪）
        Box(body, "Underfloor box", new Vector3(0f, .7f, 0f), new Vector3(6f, .45f, 2.6f), "ink");
        foreach (float x in new[] { -5.5f, 5.5f })
        {
            Box(body, "Bogie frame", new Vector3(x, .55f, 0f), new Vector3(3.2f, .35f, 2.2f), "ink");
            foreach (float s in new[] { -1f, 1f })
            {
                Box(body, "Bogie side frame", new Vector3(x, .45f, s * 1.3f), new Vector3(3.4f, .4f, .12f), "silver");
                foreach (float dx in new[] { -.9f, .9f })
                    Cylinder(body, "Axle disc", new Vector3(x + dx, .42f, s * 1.18f), .84f, .08f, "ink");
            }
        }
        // 車両のあいだの幌（前の車両とのすき間）
        if (car > 0) Box(body, "Gangway", new Vector3(9.5f, (lowY0 + bandY1) / 2f, 0f), new Vector3(2.0f, bandY1 - lowY0, 2.5f), "ink");

        if (car == 0)
        {
            // 丸い先頭：下の鼻・運転台の窓の帯・屋根の先・前の窓・前照灯・連結器
            RBox(body, "Nose", new Vector3(9.3f, (lowY0 + lowY1) / 2f, 0f), new Vector3(2.0f, lowY1 - lowY0, half * 2f), .6f, paint);
            RBox(body, "Nose cab", new Vector3(9.0f, bandY, 0f), new Vector3(1.6f, bandY1 - lowY1, half * 2f - .16f), .4f, "ink");
            RBox(body, "Nose top", new Vector3(8.9f, (bandY1 + topY1) / 2f, 0f), new Vector3(1.4f, topY1 - bandY1, half * 2f), .38f, "silver");
            Box(body, "Windshield", new Vector3(9.81f, bandY + .05f, 0f), new Vector3(.03f, .85f, 2.7f), "blue");
            foreach (float s in new[] { -1f, 1f })
                Box(body, "Headlight", new Vector3(10.31f, 1.65f, s * 1.0f), new Vector3(.06f, .25f, .45f), "mustard");
            Box(body, "Coupler", new Vector3(10.45f, .75f, 0f), new Vector3(.5f, .25f, .3f), "ink");
        }

        // パンタグラフ（ひし形の腕と、架線に当たる舟）
        if (car == 0 || car == 3)
        {
            float px = car == 0 ? -6f : 6f;
            Box(body, "Pantograph base", new Vector3(px, topY1 + .1f, 0f), new Vector3(1.6f, 0.2f, 1.8f), "ink");
            foreach (float a in new[] { 30f, -30f })
                BoxRot(body, "Pantograph arm", new Vector3(px, topY1 + .75f, 0f), new Vector3(.12f, 1.27f, .12f), new Vector3(0f, 0f, a), "silver");
            Box(body, "Pantograph shoe", new Vector3(px, topY1 + 1.35f, 0f), new Vector3(.3f, .1f, 1.6f), "silver");
        }
    }

    /// <summary>窓の帯の上に、x0〜x1 を埋める車体の色の柱を立てる（両側）。</summary>
    static void Posts(Transform body, float x0, float x1, float y, float h, float half, string paint)
    {
        if (x1 - x0 < .01f) return;
        foreach (float s in new[] { -1f, 1f })
            Box(body, "Body post", new Vector3((x0 + x1) / 2f, y, s * (half - .03f)), new Vector3(x1 - x0, h, .06f), paint);
    }

    // ================================================================ 部品（高架と電車）
    private static void BoxRot(Transform parent, string name, Vector3 localPosition, Vector3 size, Vector3 euler, string material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localRotation = Quaternion.Euler(euler);
        box.transform.localScale = size;
        Object.DestroyImmediate(box.GetComponent<Collider>());
        Paint(box.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }

    /// <summary>角丸の箱（RoomiesRestyleKit のメッシュ）。</summary>
    private static void RBox(Transform parent, string name, Vector3 localPosition, Vector3 size, float radius, string material)
    {
        var go = RoomiesRestyleKit.RBox(parent, name, localPosition, size, radius, LoadMaterial(material));
        Paint(go.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }

    /// <summary>丸い柱。collide のときは柱の形の当たり判定を残す（Wall レイヤー）。</summary>
    private static void Column(Transform parent, string name, Vector3 localPosition, float diameter, float height, string material, bool collide)
    {
        var column = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        column.name = name;
        column.transform.SetParent(parent, false);
        column.transform.localPosition = localPosition;
        column.transform.localScale = new Vector3(diameter, height / 2f, diameter);
        if (!collide) Object.DestroyImmediate(column.GetComponent<Collider>());
        else column.layer = Mathf.Max(0, LayerMask.NameToLayer("Wall"));
        Paint(column.GetComponent<MeshRenderer>(), LoadMaterial(material));
    }
}
#endif
