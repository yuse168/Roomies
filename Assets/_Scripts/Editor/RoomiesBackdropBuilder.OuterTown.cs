#if UNITY_EDITOR
using UnityEngine;

/// <summary>
/// 遠景のビル・倉庫・工場（Build Distant Backdrop → BuildOuterTown から呼ぶ。2026/11/14 作り直し）。
/// ・乱数（random / Range / Pick）の呼ぶ順と回数は前と同じ。足した部品は乱数を使わず、大きさから決める（ほかの遠景の配置が変わらないように）
/// ・ビル：足元の台、窓の帯の下の窓台、窓の帯の前の付け柱（窓が引っ込んで見える）、屋上のパラペット、階段室、高いビルは水槽
/// ・倉庫：波板の素材（縦の筋。RoomiesNature の手続きテクスチャ）、シャッターの箱・枠・継ぎ目、荷積みの台、屋根の明かり取り・とい
/// ・工場：のこぎり屋根（北向きのガラス）、煙突の帯と口、タンクのふた・帯・はしご、高窓、大きな扉
/// 遠いので三角形は少なめ（箱と三角柱だけ）。見た目だけの部品は遠景のまとめ（Combine）で素材ごとにまとまる。
/// </summary>
internal static partial class RoomiesBackdropBuilder
{
    static Material corrugated;
    const float FloorH = 3.4f;

    private static void Block(Transform parent, Vector3 position)
    {
        Transform building = Group(parent, "Block");
        building.position = position;
        building.localRotation = Quaternion.Euler(0f, random.Next(8) * 45f, 0f);

        float width = Range(12f, 20f);
        float depth = Range(10f, 18f);
        float height = Range(12f, 34f);
        string wall = Pick("cream", "paper", "teal", "coral");

        Box(building, "Body", new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), wall);
        Box(building, "Roof", new Vector3(0f, height + 0.15f, 0f), new Vector3(width - .3f, 0.3f, depth - .3f), "ink");
        Box(building, "Plinth", new Vector3(0f, .6f, 0f), new Vector3(width + .3f, 1.2f, depth + .3f), "ink");
        // 屋上のパラペット（4辺）と、その下の出っ張った帯
        Box(building, "Cornice ledge", new Vector3(0f, height - .2f, 0f), new Vector3(width + .5f, .4f, depth + .5f), "paper");
        foreach (float s in new[] { -1f, 1f })
        {
            Box(building, "Cornice ledge", new Vector3(0f, height + .5f, s * (depth / 2f - .05f)), new Vector3(width + .2f, 1.0f, .4f), wall);
            Box(building, "Cornice ledge", new Vector3(s * (width / 2f - .05f), height + .5f, 0f), new Vector3(.4f, 1.0f, depth + .2f), wall);
        }

        // 階数ぶんの窓。全部同じ高さに並べない。窓の帯の下に窓台
        int floors = Mathf.Clamp(Mathf.FloorToInt(height / FloorH), 2, 8);
        for (int f = 1; f <= floors; f++)
        {
            float y = f * FloorH;
            if (y > height - 1.6f) break;
            Box(building, "Window band", new Vector3(0f, y, depth / 2f + 0.02f),
                new Vector3(width * 0.76f, 1.4f, 0.06f), "blue");
            Box(building, "Window sill", new Vector3(0f, y - .78f, depth / 2f + .14f), new Vector3(width * .8f, .14f, .28f), "paper");
            if (random.Next(10) < 6)
            {
                Box(building, "Window band", new Vector3(width / 2f + 0.02f, y, 0f),
                    new Vector3(0.06f, 1.4f, depth * 0.76f), "blue");
                Box(building, "Window sill", new Vector3(width / 2f + .14f, y - .78f, 0f), new Vector3(.28f, .14f, depth * .8f), "paper");
            }
        }
        // 付け柱（正面と +X の面）。窓の帯の前に立てて、窓が引っ込んで見えるようにする
        float top = height - .2f;
        int front = Mathf.Max(2, Mathf.RoundToInt(width * .76f / 3.2f));
        for (int i = 0; i <= front; i++)
        {
            float x = -width * .38f + width * .76f * i / front;
            Box(building, "Wall pilaster", new Vector3(x, (1.2f + top) / 2f, depth / 2f + .12f), new Vector3(.4f, top - 1.2f, .24f), wall);
        }
        int side = Mathf.Max(2, Mathf.RoundToInt(depth * .76f / 3.2f));
        for (int i = 0; i <= side; i++)
        {
            float z = -depth * .38f + depth * .76f * i / side;
            Box(building, "Wall pilaster", new Vector3(width / 2f + .12f, (1.2f + top) / 2f, z), new Vector3(.24f, top - 1.2f, .4f), wall);
        }
        // 入口のひさし
        Box(building, "Entrance hood", new Vector3(0f, 3.0f, depth / 2f + .9f), new Vector3(Mathf.Min(5f, width * .4f), .2f, 1.8f), "ink");

        // 屋上の設備（同じ形が並ばないように）
        if (random.Next(10) < 6)
            Box(building, "Rooftop unit", new Vector3(Range(-3f, 3f), height + 1.2f, Range(-3f, 3f)),
                new Vector3(Range(2f, 4f), 1.8f, Range(2f, 4f)), "silver");
        if (random.Next(10) < 4)
            Box(building, "Antenna", new Vector3(Range(-4f, 4f), height + 3.5f, Range(-4f, 4f)),
                new Vector3(0.2f, 5f, 0.2f), "ink");
        // 階段室と、高いビルの水槽（乱数を使わず、隅に置く）
        Box(building, "Stair hut", new Vector3(-width / 2f + 2.2f, height + 1.3f, -depth / 2f + 2.2f), new Vector3(2.6f, 2.6f, 2.6f), wall);
        Box(building, "Stair hut cap", new Vector3(-width / 2f + 2.2f, height + 2.65f, -depth / 2f + 2.2f), new Vector3(2.9f, .15f, 2.9f), "ink");
        if (height > 20f)
        {
            Cylinder(building, "Water tank", new Vector3(width / 2f - 2.4f, height + 2.4f, -depth / 2f + 2.4f), 2.2f, 2.4f, "silver", upright: true);
            foreach (float d in new[] { -.7f, .7f })
                Box(building, "Steel tank leg", new Vector3(width / 2f - 2.4f + d, height + .6f, -depth / 2f + 2.4f), new Vector3(.15f, 1.2f, .15f), "ink");
        }
    }

    private static void Warehouse(Transform parent, Vector3 position)
    {
        Transform shed = Group(parent, "Warehouse");
        shed.position = position;
        shed.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

        float width = Range(22f, 38f);
        float depth = Range(14f, 22f);
        float height = Range(6f, 9f);

        // 本体は波板（縦の筋の素材）
        BoxM(shed, "Corrugated body", new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), Corrugated());
        Box(shed, "Roof", new Vector3(0f, height + 0.25f, 0f),
            new Vector3(width + 1.2f, 0.5f, depth + 1.2f), "teal");
        Box(shed, "Plinth", new Vector3(0f, .4f, 0f), new Vector3(width + .2f, .8f, depth + .2f), "ink");
        // 屋根の明かり取り・棟・とい
        foreach (float z in new[] { -depth / 4f, depth / 4f })
            Box(shed, "Skylight glass", new Vector3(0f, height + .55f, z), new Vector3(width * .8f, .1f, 1.2f), "blue");
        Box(shed, "Roof ridge vent", new Vector3(0f, height + .75f, 0f), new Vector3(width * .9f, .5f, .9f), "silver");
        foreach (float s in new[] { -1f, 1f })
            Box(shed, "Roof gutter", new Vector3(0f, height - .05f, s * (depth / 2f + .65f)), new Vector3(width + 1.2f, .25f, .25f), "silver");

        // シャッター（規則的に並ぶが、ここは倉庫らしさなのでよい）。箱・枠・継ぎ目と、前の荷積みの台
        int doors = Mathf.Clamp(Mathf.FloorToInt(width / 9f), 2, 4);
        for (int i = 0; i < doors; i++)
        {
            float x = -width / 2f + width / (doors + 1f) * (i + 1);
            Box(shed, "Shutter", new Vector3(x, 2.2f, depth / 2f + 0.08f),
                new Vector3(5f, 4.2f, 0.16f), "mustard");
            Box(shed, "Shutter housing", new Vector3(x, 4.55f, depth / 2f + .25f), new Vector3(5.4f, .5f, .5f), "ink");
            foreach (float s in new[] { -1f, 1f })
                Box(shed, "Shutter guide", new Vector3(x + s * 2.6f, 2.2f, depth / 2f + .14f), new Vector3(.2f, 4.4f, .28f), "ink");
            foreach (float y in new[] { 1.6f, 2.6f, 3.6f })
                Box(shed, "Shutter slat", new Vector3(x, y, depth / 2f + .17f), new Vector3(5f, .06f, .02f), "ink");
        }
        Box(shed, "Loading dock", new Vector3(0f, .5f, depth / 2f + 1.25f), new Vector3(width * .9f, 1.0f, 2.5f), "cream");

        if (random.Next(10) < 5)
            Box(shed, "Yard crate", new Vector3(Range(-width / 2f, width / 2f), 1.2f, depth / 2f + Range(4f, 9f)),
                new Vector3(Range(2f, 4f), 2.4f, Range(2f, 4f)), "wood");
    }

    private static void Factory(Transform parent, Vector3 position)
    {
        Transform factory = Group(parent, "Factory");
        factory.position = position;
        factory.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);

        float width = Range(20f, 32f);
        float depth = Range(16f, 26f);
        float height = Range(8f, 13f);

        Box(factory, "Body", new Vector3(0f, height / 2f, 0f), new Vector3(width, height, depth), "ink");
        Box(factory, "Roof", new Vector3(0f, height + 0.15f, 0f),
            new Vector3(width + 1f, 0.3f, depth + 1f), "silver");
        Box(factory, "Plinth", new Vector3(0f, .5f, 0f), new Vector3(width + .3f, 1.0f, depth + .3f), "paper");
        // のこぎり屋根（立った面にガラス）
        int teeth = Mathf.Max(3, Mathf.FloorToInt(width / 4.5f));
        float tw = width / teeth;
        for (int i = 0; i < teeth; i++)
        {
            float x = -width / 2f + tw * (i + .5f);
            var tooth = RoomiesRestyleKit.Prism(factory, "Sawtooth top", "saw_unit", new Vector2(-.5f, 0f), new Vector2(.5f, 0f), new Vector2(.5f, 1f), 1f,
                new Vector3(x, height + .3f, 0f), LoadMaterial("silver"));
            tooth.transform.localScale = new Vector3(tw, 2.4f, depth * .9f);
            Paint(tooth.GetComponent<MeshRenderer>(), LoadMaterial("silver"));
            Box(factory, "Sawtooth glazing", new Vector3(x + tw / 2f + .03f, height + .3f + 1.25f, 0f), new Vector3(.06f, 1.9f, depth * .86f), "blue");
        }
        // 高窓と大きな扉
        Box(factory, "Factory glazing", new Vector3(0f, height - 2.2f, depth / 2f + .03f), new Vector3(width * .8f, 1.6f, .06f), "blue");
        Box(factory, "Factory glazing", new Vector3(width / 2f + .03f, height - 2.2f, 0f), new Vector3(.06f, 1.6f, depth * .8f), "blue");
        Box(factory, "Factory door", new Vector3(-width / 4f, 2.6f, depth / 2f + .06f), new Vector3(6f, 5.2f, .12f), "mustard");
        Box(factory, "Factory door frame", new Vector3(-width / 4f, 5.35f, depth / 2f + .12f), new Vector3(6.6f, .3f, .24f), "silver");

        // 煙突（高さを変える）。上の方に赤い帯と口
        int stacks = random.Next(2, 4);
        for (int i = 0; i < stacks; i++)
        {
            float stackHeight = Range(14f, 26f);
            float x = Range(-width / 3f, width / 3f);
            float z = Range(-depth / 3f, depth / 3f);
            float diameter = Range(2f, 3.2f);
            Cylinder(factory, "Chimney", new Vector3(x, stackHeight / 2f + height, z), diameter, stackHeight, "cream", upright: true);
            foreach (float dy in new[] { 1.6f, 4.0f })
                Cylinder(factory, "Stack stripe", new Vector3(x, height + stackHeight - dy, z), diameter * 1.04f, 1.0f, "coral", upright: true);
            Cylinder(factory, "Stack cap", new Vector3(x, height + stackHeight + .15f, z), diameter * 1.12f, .3f, "ink", upright: true);
        }

        // タンク（ふた・帯・はしご）
        for (int i = 0; i < random.Next(1, 4); i++)
        {
            float x = width / 2f + Range(5f, 12f);
            float z = Range(-depth / 2f, depth / 2f);
            float diameter = Range(6f, 9f);
            Cylinder(factory, "Tank", new Vector3(x, 4f, z), diameter, 8f, "paper", upright: true);
            Cylinder(factory, "Tank lid", new Vector3(x, 8.25f, z), diameter * .92f, .5f, "silver", upright: true);
            foreach (float y in new[] { 2.6f, 5.4f })
                Cylinder(factory, "Tank band", new Vector3(x, y, z), diameter * 1.02f, .3f, "ink", upright: true);
            Box(factory, "Tank ladder", new Vector3(x + diameter / 2f + .06f, 4f, z), new Vector3(.08f, 8f, .6f), "ink");
        }
    }

    /// <summary>倉庫の波板（縦の筋）。Build のたびに1回だけ作る。</summary>
    static Material Corrugated()
    {
        if (corrugated != null) return corrugated;
        var tex = RoomiesNature.Tex("warehouse_corrugated", 256, 256, (float u, float v, out Color c, out float h) =>
        {
            float rib = .5f + .5f * Mathf.Cos(u * Mathf.PI * 2f * 8f);
            float t = .78f + .14f * rib + .04f * RoomiesNature.Fbm(u, v, 6, 3, 2);
            c = new Color(t * .9f, t * .93f, t * .96f, 1f); h = rib;
        }, 3f, out var normal);
        corrugated = RoomiesNature.Lit("M_Warehouse_Corrugated", tex, normal, Color.white, .35f);
        corrugated.SetTextureScale("_BaseMap", new Vector2(10f, 1f));
        return corrugated;
    }

    private static void BoxM(Transform parent, string name, Vector3 localPosition, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localScale = size;
        Object.DestroyImmediate(box.GetComponent<Collider>());
        Paint(box.GetComponent<MeshRenderer>(), material);
    }
}
#endif
