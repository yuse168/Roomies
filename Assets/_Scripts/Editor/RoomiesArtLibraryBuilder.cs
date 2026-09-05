#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Editable source for the baked Roomies prop library. No runtime mesh generation.</summary>
internal static partial class RoomiesArtLibraryBuilder
{
    internal const string Root = "Assets/Resources/RoomiesArt";
    static readonly Dictionary<string, Material> Palette = new Dictionary<string, Material>();
    static readonly List<Mesh> TemporaryMeshes = new List<Mesh>();
    static Transform model;

    [MenuItem("Roomies/Art/Build Prop Library")]
    internal static void Build()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        Directory.CreateDirectory(Root + "/Furniture");
        Directory.CreateDirectory(Root + "/Props");
        Directory.CreateDirectory(Root + "/Meshes");
        Directory.CreateDirectory(Root + "/Materials");
        AssetDatabase.Refresh();
        ColorMat("cream", "F5E6C8"); ColorMat("teal", "438E92"); ColorMat("coral", "D86C60");
        ColorMat("mustard", "EAB859"); ColorMat("ink", "303A4B"); ColorMat("wood", "A16D4E");
        ColorMat("pink", "E7A0A7"); ColorMat("green", "628A63"); ColorMat("silver", "B7C6CC", .65f);
        ColorMat("blue", "7AA6C6"); ColorMat("paper", "FAF2DD"); ColorMat("screen", "66C6BD", .1f, true);
        var items = FurnitureCatalog.Items;
        foreach (var item in items) Bake(item.id, "Furniture", () => Furniture(item.id), item.placeholderSize, true);
        Bake("planter", "Props", Planter);
        Bake("drink_can", "Props", Can);
        Bake("tissue_box", "Props", Tissue);
        Bake("magazine_stack", "Props", Magazines);
        Bake("slippers", "Props", Slippers);
        Bake("laundry_basket", "Props", Laundry);
        Bake("trash_bag", "Props", TrashBag);
        Bake("street_bin", "Props", StreetBin);
        Bake("air_conditioner", "Props", AirConditioner);
        Bake("bench", "Props", Bench);
        Bake("street_lamp", "Props", StreetLamp);
        Bake("parcel_stack", "Props", Parcels);
        Bake("cafe_sign", "Props", CafeSign);
        Bake("sofa", "Props", Sofa);
        Bake("coffee_table", "Props", CoffeeTable);
        Bake("mug", "Props", Mug);
        Bake("slot_cabinet", "Props", SlotCabinet, V(1,2,.6f), true);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Roomies Art] Baked 12 furniture and 17 prop prefabs with shared URP materials.");
    }

    static void ColorMat(string name, string hex, float metallic = 0, bool glow = false)
    {
        string path = $"{Root}/Materials/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        ColorUtility.TryParseHtmlString("#" + hex, out var color);
        mat.SetColor("_BaseColor", color); mat.SetFloat("_Metallic", metallic);
        mat.SetFloat("_Smoothness", metallic > 0 ? .55f : .27f);
        if (glow) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", color * .5f); }
        EditorUtility.SetDirty(mat); Palette[name] = mat;
    }

    static void Bake(string id, string folder, Action build, Vector3 size = default, bool centered = false)
    {
        var go = new GameObject(id); model = go.transform;
        try
        {
            build();
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            Vector3 scale = size == Vector3.zero ? Vector3.one : new Vector3(size.x / bounds.size.x, size.y / bounds.size.y, size.z / bounds.size.z);
            Vector3 origin = centered ? bounds.center : new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Matrix4x4 normalize = Matrix4x4.Scale(scale) * Matrix4x4.Translate(-origin);
            var materials = renderers.Select(r => r.sharedMaterial).Distinct().ToArray();
            var submeshes = new List<Mesh>();
            foreach (var material in materials)
            {
                var mesh = new Mesh();
                mesh.CombineMeshes(renderers.Where(r => r.sharedMaterial == material).Select(r => new CombineInstance
                { mesh = r.GetComponent<MeshFilter>().sharedMesh, transform = normalize * r.localToWorldMatrix }).ToArray(), true, true);
                submeshes.Add(mesh);
            }
            var combined = new Mesh { name = id, indexFormat = IndexFormat.UInt32 };
            combined.CombineMeshes(submeshes.Select(m => new CombineInstance { mesh = m, transform = Matrix4x4.identity }).ToArray(), false, false);
            combined.RecalculateBounds();
            foreach (var mesh in submeshes) Object.DestroyImmediate(mesh);
            string meshPath = $"{Root}/Meshes/{id}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) AssetDatabase.CreateAsset(combined, meshPath);
            else { EditorUtility.CopySerialized(combined, existing); Object.DestroyImmediate(combined); combined = existing; }
            foreach (Transform child in go.transform.Cast<Transform>().ToArray()) Object.DestroyImmediate(child.gameObject);
            go.AddComponent<MeshFilter>().sharedMesh = combined;
            go.AddComponent<MeshRenderer>().sharedMaterials = materials;
            var collider = go.AddComponent<BoxCollider>(); collider.center = combined.bounds.center; collider.size = combined.bounds.size;
            PrefabUtility.SaveAsPrefabAsset(go, $"{Root}/{folder}/{id}.prefab");
        }
        finally
        {
            Object.DestroyImmediate(go);
            foreach (var mesh in TemporaryMeshes) Object.DestroyImmediate(mesh);
            TemporaryMeshes.Clear();
        }
    }

    // Rounded boxes use a bevelled grid, preserving broad planar surfaces instead of scaled spheres.
    static void Box(string name, Vector3 p, Vector3 s, string color, float bevel = .025f, Vector3 rotation = default)
    {
        float radius = Mathf.Min(bevel, Mathf.Min(s.x, Mathf.Min(s.y, s.z)) * .45f);
        var verts = new List<Vector3>(); var normals = new List<Vector3>(); var tris = new List<int>();
        Vector3 half = s * .5f, core = half - Vector3.one * radius;
        Vector3[] faceNormals = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
        foreach (Vector3 n in faceNormals)
        {
            Vector3 u = Mathf.Abs(n.y) > .5f ? Vector3.right : Vector3.up;
            Vector3 v = Vector3.Cross(n, u);
            float hn = Vector3.Dot(half, Abs(n)), hu = Vector3.Dot(half, Abs(u)), hv = Vector3.Dot(half, Abs(v));
            float[] xs = { -hu, -hu + radius, hu - radius, hu };
            float[] ys = { -hv, -hv + radius, hv - radius, hv };
            int start = verts.Count;
            for (int y = 0; y < 4; y++) for (int x = 0; x < 4; x++)
            {
                Vector3 q = n * hn + u * xs[x] + v * ys[y];
                Vector3 c = new Vector3(Mathf.Clamp(q.x, -core.x, core.x), Mathf.Clamp(q.y, -core.y, core.y), Mathf.Clamp(q.z, -core.z, core.z));
                Vector3 normal = (q - c).normalized; verts.Add(c + normal * radius); normals.Add(normal);
            }
            for (int y = 0; y < 3; y++) for (int x = 0; x < 3; x++)
            { int a = start + y * 4 + x; tris.AddRange(new[] { a, a + 1, a + 5, a, a + 5, a + 4 }); }
        }
        var mesh = new Mesh(); mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetTriangles(tris, 0); mesh.RecalculateBounds();
        Part(name, mesh, p, rotation, color);
    }
    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);
    static void Part(string name, Mesh mesh, Vector3 position, Vector3 rotation, string color)
    {
        TemporaryMeshes.Add(mesh);
        var part = new GameObject(name); part.transform.SetParent(model, false);
        part.transform.localPosition = position; part.transform.localEulerAngles = rotation;
        part.AddComponent<MeshFilter>().sharedMesh = mesh;
        part.AddComponent<MeshRenderer>().sharedMaterial = Palette[color];
    }
    static void Ellipsoid(string name, Vector3 p, Vector3 size, string color)
    {
        const int rings = 10, segments = 16;
        var verts = new List<Vector3>(); var tris = new List<int>();
        for (int j = 0; j <= rings; j++) for (int i = 0; i <= segments; i++)
        {
            float t = Mathf.PI * j / rings, a = 2 * Mathf.PI * i / segments;
            verts.Add(Vector3.Scale(V(Mathf.Sin(t) * Mathf.Cos(a), Mathf.Cos(t), Mathf.Sin(t) * Mathf.Sin(a)), size * .5f));
        }
        for (int j = 0; j < rings; j++) for (int i = 0; i < segments; i++)
        { int a = j * (segments + 1) + i; tris.AddRange(new[] { a, a + 1, a + segments + 2, a, a + segments + 2, a + segments + 1 }); }
        var mesh = new Mesh(); mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        Part(name, mesh, p, Vector3.zero, color);
    }
    static void Cylinder(string name, Vector3 p, float radius, float height, string color, Vector3 rotation = default)
    {
        const int segments = 24;
        var verts = new List<Vector3>(); var tris = new List<int>();
        // Bevelled capped profile; separate rings keep a clean lip silhouette.
        float b = Mathf.Min(.012f, Mathf.Min(radius * .15f, height * .15f));
        Vector2[] profile = { new Vector2(0, -height / 2), new Vector2(radius - b, -height / 2), new Vector2(radius, -height / 2 + b), new Vector2(radius, height / 2 - b), new Vector2(radius - b, height / 2), new Vector2(0, height / 2) };
        foreach (var ring in profile) for (int i = 0; i <= segments; i++) { float a = i * 2 * Mathf.PI / segments; verts.Add(V(ring.x * Mathf.Cos(a), ring.y, ring.x * Mathf.Sin(a))); }
        for (int j = 0; j < profile.Length - 1; j++) for (int i = 0; i < segments; i++)
        { int a = j * (segments + 1) + i; tris.AddRange(new[] { a, a + segments + 1, a + 1, a + 1, a + segments + 1, a + segments + 2 }); }
        var mesh = new Mesh(); mesh.SetVertices(verts); mesh.SetTriangles(tris, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        Part(name, mesh, p, rotation, color);
    }
    static void Line(string name, Vector3 a, Vector3 b, float width, string color)
    {
        Box(name, (a + b) * .5f, V(width, (a - b).magnitude, width), color, width * .3f, Quaternion.FromToRotation(Vector3.up, b - a).eulerAngles);
    }
    static void Feet(float x, float z, float height, string color)
    { foreach (int a in new[] { -1, 1 }) foreach (int b in new[] { -1, 1 }) Box("Foot", V(a * x, height / 2, b * z), V(.07f, height, .07f), color); }
    static void Furniture(string id)
    {
        switch (id)
        {
            case "coffee_maker":
                Box("Base", V(0,.035f,0), V(.4f,.07f,.4f), "ink");
                Box("Reservoir", V(0,.29f,-.12f), V(.35f,.5f,.15f), "teal");
                Box("Brewer", V(0,.51f,0), V(.36f,.14f,.36f), "teal");
                Cylinder("Filter", V(0,.42f,.04f), .11f,.09f,"ink");
                Cylinder("Carafe", V(0,.18f,.045f), .11f,.21f,"cream");
                Cylinder("Lid", V(0,.295f,.045f), .12f,.035f,"ink");
                Box("Handle",V(.14f,.19f,.045f),V(.06f,.16f,.07f),"ink");
                Box("Switch",V(.11f,.52f,.185f),V(.04f,.035f,.013f),"mustard"); break;
            case "energy_drink_fridge":
                Box("Cabinet",V(0,.8f,0),V(.7f,1.6f,.7f),"teal",.055f);
                Box("Door recess",V(0,.84f,.354f),V(.58f,1.37f,.035f),"ink");
                Box("Display",V(0,.9f,.377f),V(.45f,1.12f,.025f),"blue");
                for(int y=0;y<3;y++) { Box("Shelf",V(0,.43f+y*.32f,.41f),V(.46f,.025f,.07f),"cream"); for(int x=0;x<3;x++) Cylinder("Can",V(-.15f+x*.15f,.55f+y*.32f,.425f),.045f,.18f,x%2==0?"mustard":"coral"); }
                Box("Pull",V(.25f,.87f,.42f),V(.035f,.32f,.055f),"silver"); break;
            case "comfy_bed":
                Feet(.43f,.82f,.12f,"wood"); Box("Frame",V(0,.16f,0),V(1.1f,.15f,2),"wood");
                Box("Mattress",V(0,.29f,0),V(1.06f,.18f,1.96f),"cream",.065f);
                Box("Duvet",V(0,.385f,-.25f),V(1.07f,.09f,1.43f),"coral",.035f);
                Box("Fold",V(0,.425f,.38f),V(1.06f,.045f,.19f),"pink");
                Box("Pillow",V(0,.41f,.72f),V(.74f,.08f,.36f),"paper",.03f);
                for(int i=0;i<4;i++) Box("Quilt seam",V(-.4f+i*.27f,.434f,-.29f),V(.014f,.004f,1.22f),"pink",.001f); break;
            case "welcome_mat":
                Box("Rubber",V(0,.01f,0),V(1,.02f,.6f),"ink"); Box("Woven mat",V(0,.035f,0),V(.95f,.03f,.55f),"mustard");
                for(int i=0;i<9;i++) Box("Weave",V(-.4f+i*.1f,.052f,0),V(.012f,.004f,.48f),"wood",.001f);
                Box("House",V(0,.058f,0),V(.17f,.008f,.15f),"cream"); Line("Roof",V(-.12f,.06f,.09f),V(0,.06f,.2f),.026f,"cream"); Line("Roof",V(0,.06f,.2f),V(.12f,.06f,.09f),.026f,"cream"); break;
            case "piggy_bank":
                Ellipsoid("Body",V(0,.23f,0),V(.36f,.31f,.43f),"pink");
                Cylinder("Snout",V(0,.25f,.23f),.085f,.05f,"coral",V(90,0,0));
                foreach(int s in new[]{-1,1}) { Ellipsoid("Ear",V(s*.11f,.38f,.1f),V(.07f,.1f,.065f),"pink"); Ellipsoid("Eye",V(s*.105f,.31f,.171f),V(.026f,.031f,.024f),"ink"); Ellipsoid("Nostril",V(s*.035f,.255f,.258f),V(.016f,.025f,.013f),"ink"); }
                Feet(.1f,.12f,.11f,"coral"); Box("Coin slot",V(0,.387f,-.01f),V(.13f,.009f,.018f),"ink",.004f); break;
            case "wall_clock":
                Cylinder("Rim",V(0,.25f,0),.25f,.1f,"teal",V(90,0,0)); Cylinder("Dial",V(0,.25f,.052f),.219f,.009f,"paper",V(90,0,0));
                for(int i=0;i<12;i++){ float a=i*Mathf.PI/6; Box("Hour",V(Mathf.Sin(a)*.183f,.25f+Mathf.Cos(a)*.183f,.061f),V(.012f,.026f,.008f),"ink",.002f,V(0,0,-i*30)); }
                Line("Minute",V(0,.25f,.07f),V(.1f,.35f,.07f),.013f,"ink"); Line("Hour",V(0,.25f,.076f),V(-.065f,.29f,.076f),.019f,"coral"); break;
            case "delivery_terminal":
                Box("Foot",V(0,.045f,0),V(.6f,.09f,.5f),"ink"); Box("Pedestal",V(0,.45f,-.07f),V(.22f,.82f,.2f),"teal");
                Box("Terminal",V(0,.98f,0),V(.59f,.44f,.22f),"cream",.035f);
                Box("Screen",V(0,1.02f,.116f),V(.46f,.26f,.015f),"screen");
                for(int i=0;i<3;i++) Box("Order line",V(-.045f,1.095f-i*.06f,.127f),V(.26f,.012f,.008f),"cream",.002f);
                Box("Receipt slot",V(0,.83f,.12f),V(.28f,.021f,.015f),"ink"); break;
            case "lucky_cat":
                Ellipsoid("Body",V(0,.23f,0),V(.34f,.4f,.29f),"cream"); Ellipsoid("Head",V(0,.45f,.015f),V(.32f,.27f,.29f),"cream");
                foreach(int s in new[]{-1,1}){ Ellipsoid("Ear",V(s*.12f,.58f,.015f),V(.08f,.13f,.08f),"cream"); Line("Closed eye",V(s*.095f-.025f,.47f,.153f),V(s*.095f+.025f,.48f,.153f),.018f,"ink"); }
                Box("Collar",V(0,.335f,.115f),V(.28f,.044f,.06f),"coral"); Ellipsoid("Bell",V(0,.3f,.16f),V(.055f,.065f,.035f),"mustard");
                Ellipsoid("Waving paw",V(.19f,.42f,.06f),V(.1f,.25f,.11f),"cream"); Ellipsoid("Coin",V(-.075f,.18f,.16f),V(.14f,.2f,.045f),"mustard"); break;
            case "vending_machine": Vending(); break;
            case "disco_ball":
                Ellipsoid("Core",V(0,.3f,0),V(.56f,.56f,.56f),"ink");
                for(int j=1;j<8;j++) for(int i=0;i<16;i++){ float t=Mathf.PI*j/8, a=2*Mathf.PI*i/16; Vector3 n=V(Mathf.Sin(t)*Mathf.Cos(a),Mathf.Cos(t),Mathf.Sin(t)*Mathf.Sin(a)); Box("Mirror tile",V(0,.3f,0)+n*.284f,V(.075f,.075f,.012f),(i+j)%4==0?"blue":"silver",.003f,Quaternion.LookRotation(n).eulerAngles); }
                Cylinder("Hanger",V(0,.597f,0),.023f,.035f,"silver"); break;
            case "trampoline":
                for(int i=0;i<8;i++){ float a=i*Mathf.PI/4; Box("Leg",V(Mathf.Cos(a)*.62f,.11f,Mathf.Sin(a)*.62f),V(.06f,.22f,.06f),"silver"); }
                Cylinder("Padded frame",V(0,.25f,0),.75f,.1f,"teal"); Cylinder("Mat",V(0,.304f,0),.64f,.009f,"ink");
                for(int i=0;i<24;i++){float a=i*Mathf.PI/12; Line("Spring",V(Mathf.Cos(a)*.59f,.312f,Mathf.Sin(a)*.59f),V(Mathf.Cos(a)*.69f,.312f,Mathf.Sin(a)*.69f),.012f,"silver");} break;
            case "arcade_machine":
                Box("Cabinet",V(0,.85f,0),V(.8f,1.7f,.7f),"teal",.04f);
                Box("Marquee",V(0,1.7f,.04f),V(.78f,.2f,.74f),"coral");
                Box("Screen bezel",V(0,1.2f,.36f),V(.65f,.63f,.05f),"ink"); Box("Screen",V(0,1.2f,.39f),V(.5f,.46f,.016f),"screen");
                Box("Controls",V(0,.81f,.31f),V(.76f,.14f,.38f),"cream"); Cylinder("Stick",V(-.2f,.96f,.39f),.02f,.13f,"ink"); Ellipsoid("Ball",V(-.2f,1.04f,.39f),V(.085f,.085f,.085f),"coral");
                for(int i=0;i<3;i++) Cylinder("Button",V(.03f+i*.09f,.9f,.4f),.033f,.018f,i==0?"mustard":"coral");
                Box("Coin door",V(0,.4f,.36f),V(.24f,.27f,.024f),"ink");
                for(int i=0;i<5;i++) Box("Pixel skyline",V(-.18f+i*.085f,1.13f,.405f),V(.052f,.08f+(i%3)*.05f,.008f),"cream",.002f); break;
        }
    }
    static void Vending()
    {
        Box("Cabinet",V(0,1,0),V(1,2,.7f),"coral",.05f); Box("Header",V(0,1.81f,.355f),V(.86f,.22f,.025f),"cream");
        Box("Window",V(-.1f,1.16f,.36f),V(.68f,.99f,.025f),"ink");
        for(int y=0;y<3;y++) { Box("Shelf",V(-.1f,.77f+y*.28f,.39f),V(.63f,.025f,.08f),"silver"); for(int x=0;x<4;x++) Cylinder("Drink",V(-.34f+x*.16f,.88f+y*.28f,.41f),.043f,.17f,(x+y)%2==0?"mustard":"teal"); }
        Box("Payment",V(.35f,1.18f,.37f),V(.14f,.3f,.04f),"ink"); Box("Reader",V(.35f,1.26f,.398f),V(.09f,.07f,.015f),"screen");
        Box("Pickup hatch",V(-.04f,.33f,.36f),V(.65f,.21f,.035f),"ink"); Box("Hatch rim",V(-.04f,.21f,.395f),V(.7f,.045f,.08f),"silver");
        for(int i=0;i<4;i++) Box("Vent",V(.32f,.24f+i*.04f,.36f),V(.14f,.012f,.012f),"ink",.002f);
    }
    static void Planter()
    {
        Cylinder("Pot",V(0,.2f,0),.22f,.4f,"coral"); Cylinder("Lip",V(0,.39f,0),.245f,.065f,"coral"); Cylinder("Soil",V(0,.425f,0),.207f,.018f,"wood");
        for(int i=0;i<7;i++){float a=i*2.4f; Vector3 p=V(Mathf.Cos(a)*.19f,.68f+(i%3)*.12f,Mathf.Sin(a)*.19f); Line("Stem",V(0,.4f,0),p,.018f,"green"); Ellipsoid("Leaf",p,V(.23f,.33f,.12f),i%2==0?"green":"teal");}
    }
    static void Can(){ Cylinder("Can",V(0,.065f,0),.034f,.13f,"mustard"); Cylinder("Top",V(0,.132f,0),.033f,.007f,"silver"); Box("Pull tab",V(0,.138f,0),V(.012f,.004f,.022f),"ink",.002f); Box("Label",V(0,.07f,.034f),V(.035f,.047f,.004f),"cream"); }
    static void Tissue(){Box("Box",V(0,.065f,0),V(.24f,.13f,.13f),"teal"); Box("Opening",V(0,.132f,0),V(.13f,.009f,.025f),"ink"); Box("Tissue",V(0,.18f,0),V(.095f,.11f,.006f),"paper",.002f,V(0,0,12));}
    static void Magazines(){for(int i=0;i<3;i++){Box("Pages",V(i*.012f,.015f+i*.026f,0),V(.21f,.02f,.29f),"paper",.003f,V(0,i*9,0)); Box("Cover",V(i*.012f,.027f+i*.026f,0),V(.22f,.004f,.3f),i==0?"coral":i==1?"mustard":"teal",.001f,V(0,i*9,0));} Box("Cover picture",V(.025f,.085f,0),V(.12f,.005f,.15f),"cream");}
    static void Slippers(){foreach(int s in new[]{-1,1}){Box("Sole",V(s*.085f,.018f,0),V(.13f,.035f,.29f),"cream",.016f); Ellipsoid("Upper",V(s*.085f,.064f,.063f),V(.135f,.095f,.16f),"coral");}}
    static void Laundry(){Box("Basket",V(0,.2f,0),V(.52f,.4f,.38f),"cream",.05f); for(int i=0;i<8;i++) Box("Weave opening",V(-.21f+i*.06f,.21f,.194f),V(.025f,.24f,.006f),"wood",.008f); Ellipsoid("Towel",V(-.08f,.42f,0),V(.32f,.13f,.34f),"teal"); Ellipsoid("Laundry",V(.14f,.45f,.03f),V(.2f,.18f,.25f),"pink");}
    static void TrashBag(){Ellipsoid("Bag",V(0,.24f,0),V(.43f,.48f,.39f),"ink"); Box("Knot",V(0,.49f,0),V(.095f,.08f,.085f),"ink"); Line("Tie",V(-.075f,.49f,0),V(.075f,.51f,0),.018f,"mustard");}
    static void StreetBin(){Box("Bin",V(0,.45f,0),V(.53f,.86f,.48f),"teal",.055f); Box("Lid",V(0,.9f,0),V(.59f,.09f,.54f),"ink"); Box("Label",V(0,.57f,.247f),V(.25f,.2f,.012f),"cream"); for(int i=0;i<5;i++) Box("Rib",V(-.2f+i*.1f,.28f,.248f),V(.018f,.35f,.02f),"teal");}
    static void AirConditioner(){Box("Housing",V(0,.3f,0),V(.86f,.6f,.35f),"cream"); Cylinder("Fan grille",V(-.16f,.31f,.181f),.22f,.026f,"ink",V(90,0,0)); for(int i=0;i<7;i++) Box("Grille",V(-.16f,.145f+i*.055f,.199f),V(.39f,.012f,.012f),"silver",.002f); for(int i=0;i<6;i++) Box("Vent",V(.3f,.15f+i*.056f,.181f),V(.13f,.018f,.013f),"ink",.002f);}
    static void Bench(){Feet(.72f,.19f,.43f,"ink"); for(int i=0;i<4;i++) Box("Seat slat",V(0,.46f,-.21f+i*.14f),V(1.8f,.065f,.11f),"wood"); for(int i=0;i<3;i++) Box("Back slat",V(0,.65f+i*.14f,-.26f),V(1.8f,.11f,.065f),"wood"); Line("Back support",V(-.73f,.3f,-.28f),V(-.73f,1,-.28f),.05f,"ink"); Line("Back support",V(.73f,.3f,-.28f),V(.73f,1,-.28f),.05f,"ink");}
    static void StreetLamp(){Cylinder("Base",V(0,.09f,0),.21f,.18f,"ink"); Cylinder("Pole",V(0,1.9f,0),.065f,3.7f,"teal"); Line("Arm",V(0,3.7f,0),V(.55f,3.7f,0),.07f,"teal"); Box("Shade",V(.5f,3.65f,0),V(.55f,.13f,.33f),"ink"); Box("Light diffuser",V(.5f,3.575f,0),V(.46f,.025f,.26f),"paper");}
    static void Parcels(){for(int i=0;i<3;i++){Vector3 p=V((i%2)*.36f,i==2?.53f:.19f,0); Box("Carton",p,V(.46f,.36f,.4f),"wood"); Box("Tape",p+V(0,.183f,0),V(.065f,.006f,.4f),"mustard",.001f); Box("Shipping label",p+V(.08f,.07f,.203f),V(.16f,.095f,.008f),"paper");}}
    static void CafeSign(){foreach(int s in new[]{-1,1}){Line("Front leg",V(s*.3f,0,.24f),V(s*.3f,1.1f,0),.055f,"wood"); Line("Back leg",V(s*.3f,0,-.24f),V(s*.3f,1.1f,0),.055f,"wood");} Box("Board",V(0,.66f,.1f),V(.64f,.73f,.06f),"wood"); Box("Chalkboard",V(0,.66f,.134f),V(.55f,.64f,.008f),"ink"); for(int i=0;i<3;i++) Box("Chalk line",V(0,.66f-i*.09f,.144f),V(.32f-i*.06f,.014f,.005f),"cream",.002f); Cylinder("Sun emblem",V(0,.84f,.144f),.075f,.008f,"mustard",V(90,0,0));}
    static void Sofa(){Feet(.8f,.32f,.16f,"wood"); Box("Base",V(0,.3f,0),V(2,.32f,.86f),"teal",.08f); Box("Back",V(0,.76f,-.33f),V(2,.67f,.22f),"teal",.08f); foreach(int s in new[]{-1,1}){Box("Arm",V(s*.92f,.55f,0),V(.2f,.5f,.9f),"teal",.075f); Box("Seat cushion",V(s*.43f,.49f,.055f),V(.8f,.18f,.62f),"blue",.065f);} Box("Throw pillow",V(-.61f,.73f,-.06f),V(.4f,.4f,.16f),"mustard",.06f,V(0,0,12));}
    static void CoffeeTable(){Feet(.5f,.27f,.4f,"wood"); Box("Top",V(0,.44f,0),V(1.24f,.09f,.76f),"wood",.04f); Box("Shelf",V(0,.18f,0),V(1.08f,.04f,.59f),"wood");}
    static void Mug(){Cylinder("Cup",V(0,.055f,0),.045f,.11f,"cream"); Cylinder("Coffee",V(0,.111f,0),.037f,.004f,"wood"); Box("Handle",V(.054f,.06f,0),V(.037f,.057f,.023f),"cream",.011f);}
    static void SlotCabinet()
    {
        Box("Cabinet",V(0,1,0),V(1,2,.51f),"coral",.05f);
        Box("Face",V(0,1.06f,.265f),V(.86f,1.51f,.02f),"ink");
        Box("Marquee",V(0,1.82f,.28f),V(.84f,.21f,.06f),"mustard");
        foreach(int s in new[]{-1,1}) Box("Chrome trim",V(s*.46f,1,.27f),V(.038f,1.83f,.036f),"silver");
        Box("Control deck",V(0,.57f,.26f),V(.88f,.14f,.17f),"cream");
        for(int i=0;i<3;i++) Cylinder("Button",V(-.22f+i*.22f,.653f,.29f),.045f,.021f,i==0?"mustard":"teal");
        Box("Payout hatch",V(0,.23f,.28f),V(.58f,.16f,.027f),"ink");
        Box("Coin slot",V(.32f,.82f,.29f),V(.09f,.13f,.026f),"silver");
        Box("Coin opening",V(.32f,.82f,.307f),V(.014f,.08f,.01f),"ink",.002f);
    }
}
#endif
