#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// 町の作り直し 1：窓と外壁。何度実行しても同じ結果。
/// ・店（CORNER STORE / NEIGHBORHOOD WORKS）と家の1階の正面・東面に、厚みのある外壁を重ね、窓・扉の所をくり抜く。
///   くり抜いた所に枠・窓台・奥に引っ込んだガラス（店内・部屋の絵。夜は明かりが点いて見える）を入れる。
/// ・ROOMIES EXPRESS は吹き抜けの荷さばき場なので、宙に浮いていた窓を消し、奥の壁にシャッター・扉・事務所の窓を付ける。
/// ・壁に貼っていた古い窓・枠・ハイライトは消す。外壁の分の当たり判定を足す。当たり判定のなかった2店には本体の当たり判定を足す。
/// 置く物はすべて町のプレハブ（RoomiesNeighborhood）の「Town facades」の下。寸法は下の表。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    const string NeighborhoodPrefab = "Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab";
    const string FacadeGroup = "Town facades";
    const string Workplaces = "Workplaces - facades and loading details";
    const string House = "Shared house - lounge and terrace";

    enum Kind { Shop, Window, Door, Shutter }

    struct Opening
    {
        public float x0, x1, y0, y1; public Kind kind; public string glass;
        public Opening(float x0, float x1, float y0, float y1, Kind kind, string glass = null) { this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1; this.kind = kind; this.glass = glass; }
    }

    class Facade
    {
        public string name, wall, frame; public Vector3 origin; public float yaw, width, height, thick = .24f;
        public bool plinth = true, band, collider = true; public List<Opening> openings = new List<Opening>();
    }

    [MenuItem("Roomies/Art/Town/1 Windows And Walls")]
    internal static void BuildWindowsAndWalls()
    {
        RoomiesMaterialFinishBuilder.ResetTownMaterials();
        var root = PrefabUtility.LoadPrefabContents(NeighborhoodPrefab);
        try
        {
            var old = root.transform.Find(FacadeGroup);
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var group = new GameObject(FacadeGroup).transform;
            group.SetParent(root.transform, false);
            group.gameObject.isStatic = true;

            RemoveOldWindows(root.transform);
            foreach (var f in Facades()) Build(group, f);
            BodyColliders(group);
            NudgeProps(root.transform);

            PrefabUtility.SaveAsPrefabAsset(root, NeighborhoodPrefab);
            Debug.Log($"[Town] windows and walls: {group.GetComponentsInChildren<Renderer>().Length} renderers, {group.GetComponentsInChildren<Collider>().Length} colliders");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        CombineForSpeed();
    }

    // ================================================================ 表（寸法）
    static IEnumerable<Facade> Facades()
    {
        // CORNER STORE：正面（+z 向き）。1階は5区画（真ん中が扉）、2階は部屋の窓
        var corner = new Facade { name = "CORNER STORE front", wall = "wall_corner", frame = "frame_corner", origin = new Vector3(18.6f, 0, -8.7f), yaw = 0, width = 15f, height = 5.38f };
        Storefront(corner, 5, .5f, 2, 3.7f, 4.95f);
        yield return corner;
        var cornerSide = new Facade { name = "CORNER STORE west side", wall = "wall_corner", frame = "frame_corner", origin = new Vector3(18.6f, 0, -18.7f), yaw = -90, width = 10f, height = 5.9f, thick = .2f };
        foreach (float x in new[] { 2.2f, 5f, 7.8f }) cornerSide.openings.Add(new Opening(x - .65f, x + .65f, 3.7f, 4.95f, Kind.Window, x < 4 ? "glass_home_dark" : "glass_home_lit"));
        cornerSide.openings.Add(new Opening(6.9f, 8.7f, .9f, 2.4f, Kind.Window, "glass_shop"));
        yield return cornerSide;

        // NEIGHBORHOOD WORKS：正面。1階は5区画（2番目が扉）、上は小さな明かり取り
        var works = new Facade { name = "NEIGHBORHOOD WORKS front", wall = "wall_works", frame = "frame_works", origin = new Vector3(-14.1f, 0, -9.2f), yaw = 0, width = 12f, height = 4.38f };
        Storefront(works, 5, .45f, 1, 3.55f, 4.1f);
        yield return works;

        // ROOMIES EXPRESS：吹き抜けの奥の壁（+z 向き）。シャッター2枚・扉・2階の事務所の窓
        var express = new Facade { name = "ROOMIES EXPRESS dock wall", wall = "wall_express", frame = "frame_express", origin = new Vector3(-30.45f, 0, -18.24f), yaw = 0, width = 11.95f, height = 8.2f, thick = .22f, band = true };
        express.openings.Add(new Opening(.8f, 3.9f, 0, 3.3f, Kind.Shutter));
        express.openings.Add(new Opening(4.7f, 7.8f, 0, 3.3f, Kind.Shutter));
        express.openings.Add(new Opening(8.9f, 10f, 0, 2.3f, Kind.Door, "glass_shop"));
        float[] office = { 1.0f, 3.7f, 6.4f, 9.1f };
        for (int i = 0; i < office.Length; i++) express.openings.Add(new Opening(office[i], office[i] + 1.9f, 4.6f, 5.9f, Kind.Window, i % 2 == 0 ? "glass_shop" : "glass_home_dark"));
        yield return express;

        // 家の1階の外壁は、作り直し2のメインの家（RoomiesTownRebuild.MainHouse）で作る
    }

    /// <summary>店の正面：bays 区画（door 番目は扉）＋上の窓の列。</summary>
    static void Storefront(Facade f, int bays, float pier, int door, float upperY0, float upperY1)
    {
        float bay = (f.width - (bays + 1) * pier) / bays;
        for (int i = 0; i < bays; i++)
        {
            float x0 = pier + i * (bay + pier), x1 = x0 + bay, mid = (x0 + x1) / 2;
            if (i == door) f.openings.Add(new Opening(mid - .7f, mid + .7f, 0, 2.75f, Kind.Door, "glass_shop"));
            else f.openings.Add(new Opening(x0, x1, .55f, 2.75f, Kind.Shop, "glass_shop"));
            float w = Mathf.Min(1.5f, bay - .5f);
            f.openings.Add(new Opening(mid - w / 2, mid + w / 2, upperY0, upperY1, Kind.Window, i % 3 == 1 ? "glass_home_dark" : "glass_home_lit"));
        }
    }

    // ================================================================ 組み立て
    static GameObject Build(Transform parent, Facade f)
    {
        var go = new GameObject(f.name) { isStatic = true };
        go.transform.SetParent(parent, false);
        go.transform.localPosition = f.origin; go.transform.localRotation = Quaternion.Euler(0, f.yaw, 0);
        var t = go.transform;
        float W = f.width, H = f.height, T = f.thick;
        var wall = Mat(f.wall); var frame = Mat(f.frame); var sill = Mat("sill"); var plinth = Mat("plinth");

        // 壁：縦の帯に分けて、開口のない所を埋める
        var xs = new List<float> { 0, W };
        foreach (var o in f.openings) { xs.Add(Mathf.Clamp(o.x0, 0, W)); xs.Add(Mathf.Clamp(o.x1, 0, W)); }
        xs = xs.Distinct().OrderBy(x => x).ToList();
        for (int i = 0; i < xs.Count - 1; i++)
        {
            float a = xs[i], b = xs[i + 1]; if (b - a < .005f) continue;
            float mid = (a + b) / 2, y = 0;
            foreach (var o in f.openings.Where(o => o.x0 < mid && o.x1 > mid).OrderBy(o => o.y0))
            {
                if (o.y0 > y + .005f) Box(t, "Facade wall", a, b, y, o.y0, 0, T, wall);
                y = Mathf.Max(y, o.y1);
            }
            if (H > y + .005f) Box(t, "Facade wall", a, b, y, H, 0, T, wall);
        }
        // 角の柱（少し出す）と腰の帯・上の帯
        Box(t, "Facade pilaster", -.02f, .32f, 0, H, 0, T + .05f, wall);
        Box(t, "Facade pilaster", W - .32f, W + .02f, 0, H, 0, T + .05f, wall);
        if (f.plinth)
        {
            float x = -.02f;
            foreach (var o in f.openings.Where(o => o.y0 < .05f).OrderBy(o => o.x0)) { if (o.x0 - .02f > x) Box(t, "Plinth", x, o.x0 - .02f, 0, .32f, 0, T + .06f, plinth); x = o.x1 + .02f; }
            if (W + .02f > x) Box(t, "Plinth", x, W + .02f, 0, .32f, 0, T + .06f, plinth);
        }
        if (f.band) Box(t, "Cornice band", -.08f, W + .08f, H - .22f, H, 0, T + .12f, sill);

        foreach (var o in f.openings) BuildOpening(t, o, T, frame, sill);

        // 外壁の分の当たり判定（見た目と同じ板1枚）。境界の外の家には付けない
        if (!f.collider) return go;
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(W / 2, H / 2, T / 2 + .01f); col.size = new Vector3(W, H, T + .02f);
        return go;
    }

    static void BuildOpening(Transform t, Opening o, float T, Material frame, Material sill)
    {
        float fw = o.kind == Kind.Shop ? .08f : .07f;
        float x0 = o.x0, x1 = o.x1, y0 = o.y0, y1 = o.y1;
        bool floor = o.kind == Kind.Door || o.kind == Kind.Shutter;
        // 枠（開口の内側。壁の面より少し奥）
        Box(t, "Window frame", x0, x0 + fw, y0, y1, .02f, .13f, frame);
        Box(t, "Window frame", x1 - fw, x1, y0, y1, .02f, .13f, frame);
        Box(t, "Window frame", x0, x1, y1 - fw, y1, .02f, .13f, frame);
        if (!floor) Box(t, "Window frame", x0, x1, y0, y0 + fw, .02f, .13f, frame);

        switch (o.kind)
        {
            case Kind.Shop:
            case Kind.Window:
                Box(t, "Window glazing", x0 + fw, x1 - fw, y0 + fw, y1 - fw, .03f, .05f, Mat(o.glass));
                if (x1 - x0 > 1.4f) Box(t, "Window mullion", (x0 + x1) / 2 - .03f, (x0 + x1) / 2 + .03f, y0 + fw, y1 - fw, .04f, .1f, frame);
                if (o.kind == Kind.Shop) Box(t, "Window transom", x0 + fw, x1 - fw, y1 - .48f, y1 - .42f, .04f, .1f, frame);
                Box(t, "Window sill", x0 - .06f, x1 + .06f, y0 - .07f, y0 + .01f, 0, T + .07f, sill);
                break;
            case Kind.Door:
                Box(t, "Door", x0 + fw, x1 - fw, y0, y1 - fw, .02f, .07f, frame);
                Box(t, "Window glazing", x0 + fw + .12f, x1 - fw - .12f, y0 + 1.0f, y1 - fw - .14f, .07f, .085f, Mat(o.glass));
                Box(t, "Door handle", x1 - fw - .2f, x1 - fw - .15f, y0 + .95f, y0 + 1.2f, .07f, .14f, Mat("handle"));
                Box(t, "Door step", x0 - .2f, x1 + .2f, 0, .1f, T, T + .45f, sill);
                break;
            case Kind.Shutter:
                var slat = Mat("shutter");
                for (float y = y0; y < y1 - fw - .01f; y += .16f)
                    Box(t, "Shutter slat", x0 + fw, x1 - fw, y, Mathf.Min(y + .14f, y1 - fw), .03f, ((int)(y / .16f)) % 2 == 0 ? .07f : .06f, slat);
                Box(t, "Shutter housing", x0 - .1f, x1 + .1f, y1, y1 + .38f, 0, T + .22f, slat);
                Box(t, "Shutter bollard", x0 - .32f, x0 - .14f, 0, .9f, T, T + .2f, Mat("frame_express"));
                Box(t, "Shutter bollard", x1 + .14f, x1 + .32f, 0, .9f, T, T + .2f, Mat("frame_express"));
                break;
        }
    }

    static Material Mat(string key) => RoomiesMaterialFinishBuilder.TownMat(key);

    /// <summary>ローカル座標の範囲で箱を置く（当たり判定なし。見た目だけ）。</summary>
    static GameObject Box(Transform parent, string name, float x0, float x1, float y0, float y1, float z0, float z1, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = name;
        Object.DestroyImmediate(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3((x0 + x1) / 2, (y0 + y1) / 2, (z0 + z1) / 2);
        go.transform.localScale = new Vector3(Mathf.Abs(x1 - x0), Mathf.Abs(y1 - y0), Mathf.Abs(z1 - z0));
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        go.isStatic = true;
        return go;
    }

    // ================================================================ 片付けと調整
    static void RemoveOldWindows(Transform root)
    {
        var work = root.Find(Workplaces);
        if (work != null)
            foreach (Transform c in work.Cast<Transform>().ToArray())
            {
                bool express = c.position.x < -17.5f;
                if (c.name == "Shop window surround" || c.name == "Shop glazing" || c.name == "Window mullion" || c.name == "Window highlight" || c.name == "Foundation course"
                    || (express && (c.name == "Shop awning" || c.name == "Awning stripe")))
                    Object.DestroyImmediate(c.gameObject);
            }
        var house = root.Find(House);
        if (house != null)
            foreach (Transform c in house.Cast<Transform>().ToArray())
                if (c.name == "Lower window frame" || c.name == "Lower window" || c.name == "Mullion") Object.DestroyImmediate(c.gameObject);
    }

    /// <summary>当たり判定のなかった店の本体に、見た目どおりの当たり判定を足す（Demo の箱の大きさ）。</summary>
    static void BodyColliders(Transform group)
    {
        var host = new GameObject("Shop body colliders").transform; host.SetParent(group, false);
        void Body(string name, Vector3 min, Vector3 max)
        {
            var go = new GameObject(name) { isStatic = true }; go.transform.SetParent(host, false);
            var c = go.AddComponent<BoxCollider>(); c.center = (min + max) / 2; c.size = max - min;
        }
        Body("CORNER STORE body", new Vector3(18.4f, 0, -18.7f), new Vector3(33.6f, 6f, -8.7f));
        Body("NEIGHBORHOOD WORKS body", new Vector3(-14.1f, 0, -18.2f), new Vector3(-2.1f, 5f, -9.2f));
    }

    /// <summary>外壁が厚くなった分、壁に付いている飾り・看板を前へ出す。正面の窓にかぶるエアコンは消す。</summary>
    static void NudgeProps(Transform root)
    {
        var work = root.Find(Workplaces);
        foreach (var (name, front) in new[] { ("CORNER STORE", -8.7f), ("NEIGHBORHOOD WORKS", -9.2f) })
        {
            var fascia = work.Find(name + " fascia"); if (fascia != null) { var p = fascia.localPosition; p.z = front + .24f + .12f; fascia.localPosition = p; }
            var sign = work.Find("Sign - " + name); if (sign != null) { var p = sign.localPosition; p.z = front + .24f + .235f; sign.localPosition = p; }
        }
        foreach (Transform c in work.Cast<Transform>().ToArray())
            if (c.name.StartsWith("air_conditioner") && Mathf.Abs(c.position.x + 12.8f) < .5f) Object.DestroyImmediate(c.gameObject);
    }
}
#endif
