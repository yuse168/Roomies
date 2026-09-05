#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One explicit authoring command; baked scene objects, no runtime construction.</summary>
internal static class RoomiesEnvironmentBuilder
{
    const string ScenePath = "Assets/_Scenes/GameRoom.unity";
    const string RootName = "Roomies_EnvironmentArt";
    static Transform root;
    static Scene scene;
    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>($"{RoomiesArtLibraryBuilder.Root}/Materials/{name}.mat");
    static Vector3 V(float x, float y, float z) => new Vector3(x, y, z);

    [MenuItem("Roomies/Art/Install Environment")]
    internal static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        scene = SceneManager.GetSceneByPath(ScenePath);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
        try
        {
            if (scene.isDirty) throw new InvalidOperationException("GameRoom has unsaved edits. Save them before installing art.");
            if (scene.GetRootGameObjects().Any(go => go.name == RootName))
            { Debug.Log("[Roomies Art] Environment already installed; existing authored changes preserved."); return; }
            if (Mat("cream") == null) throw new InvalidOperationException("Build Prop Library first.");
            var go = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(go, scene); root = go.transform;
            Undo.RegisterCreatedObjectUndo(go, "Install Roomies environment art");
            Street(); House(); Workplaces(); Garden();
            Vector3[] spawnPositions = { V(17.2f,4.34f,16.35f), V(18.6f,4.34f,16.35f), V(17.8f,4.34f,18.1f), V(19.3f,4.34f,18.1f), V(20.7f,4.34f,17.3f) };
            for (int i=0;i<spawnPositions.Length;i++)
            {
                string name = "PlayerSpawn"+(i+1);
                var point = scene.GetRootGameObjects().FirstOrDefault(g=>g.name==name);
                if (point == null) { point=new GameObject(name); SceneManager.MoveGameObjectToScene(point,scene); point.AddComponent<SpawnPoint>(); Undo.RegisterCreatedObjectUndo(point,"Add house spawn"); }
                Undo.RecordObject(point.transform,"Fit spawn below house ceiling"); point.transform.position=spawnPositions[i];
            }
            // Retain the original FBX collision and traversal layout. Only its surface materials change.
            var demo = scene.GetRootGameObjects().First(g => g.name == "Demo");
            foreach (var r in demo.GetComponentsInChildren<Renderer>())
            {
                string color = r.name == "pPlane1" ? "green" : r.name == "pCube8" ? "ink" : r.name == "pCube2" ? "wood" : r.name == "pCube4" ? "teal" : "cream";
                Undo.RecordObject(r, "Color existing environment"); r.sharedMaterial = Mat(color);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            RoomiesArtAudit.Render(scene, V(40,31,43), V(0,0,0), "environment-after");
            RoomiesArtAudit.Render(scene, V(21.4f,5.1f,14.8f), V(16.5f,4.3f,20.5f), "living-room");
            RoomiesArtAudit.Render(scene, V(11,8,5), V(18,4.3f,18), "house-after");
            RoomiesArtAudit.Render(scene, V(23,2.1f,3), V(2,2.3f,-11), "street-after");
            Debug.Log($"[Roomies Art] Installed {go.GetComponentsInChildren<Renderer>().Length} renderers. GameRoom saved.");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    static Transform Group(string name)
    {
        var go = new GameObject(name); go.transform.SetParent(root, false); return go.transform;
    }
    static GameObject Block(Transform parent, string name, Vector3 position, Vector3 size, string material, bool solid = false, Vector3 rotation = default)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size; go.transform.localEulerAngles = rotation;
        go.GetComponent<Renderer>().sharedMaterial = Mat(material);
        if (!solid) Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic = true; return go;
    }
    static GameObject Prop(Transform parent, string id, Vector3 position, float yaw = 0, bool solid = false, bool furniture = false)
    {
        string folder = furniture ? "Furniture" : "Props";
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomiesArtLibraryBuilder.Root}/{folder}/{id}.prefab");
        if (!asset) throw new InvalidOperationException($"Missing prop {id}");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(asset, scene);
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localRotation = Quaternion.Euler(0,yaw,0);
        if (!solid) foreach (var collider in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(collider);
        go.isStatic = true; return go;
    }
    static void Label(Transform parent, string text, Vector3 position, float width, float height, float yaw, string color = "cream")
    {
        var go = new GameObject("Sign - " + text); go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localEulerAngles = V(0,yaw,0);
        var label = go.AddComponent<TextMeshPro>(); label.font = TMP_Settings.defaultFontAsset;
        label.text = text; label.alignment = TextAlignmentOptions.Center; label.fontSize = 5;
        label.enableAutoSizing = true; label.fontSizeMin = .1f; label.fontSizeMax = 20;
        label.rectTransform.sizeDelta = new Vector2(width,height); label.color = Mat(color).GetColor("_BaseColor");
        label.fontStyle = FontStyles.Bold; label.textWrappingMode = TextWrappingModes.NoWrap;
    }
    static void Street()
    {
        var group = Group("Street - sidewalks and wayfinding");
        foreach (int side in new[]{-1,1})
        {
            Block(group,"Sidewalk",V(0,.09f,side*5.2f),V(80,.12f,2.4f),"cream");
            Block(group,"Kerb",V(0,.135f,side*4.03f),V(80,.17f,.13f),"silver");
            for(int x=-38;x<40;x+=2) Block(group,"Paving joint",V(x,.155f,side*5.2f),V(.022f,.006f,2.2f),"silver");
            for(int x=-34;x<=34;x+=17) Prop(group,"street_lamp",V(x,.15f,side*5.8f),side==1?180:0);
        }
        for(int x=-38;x<40;x+=4) Block(group,"Lane dash",V(x,.108f,0),V(1.8f,.008f,.1f),"mustard");
        foreach(int crossing in new[]{-18,18}) for(int z=-3;z<=3;z++) Block(group,"Crosswalk stripe",V(crossing,.11f,z),V(2.3f,.01f,.48f),"paper");
        foreach(int x in new[]{-13,0,30}) { Prop(group,"bench",V(x,.15f,5.65f),180); Prop(group,"street_bin",V(x+1.65f,.15f,5.8f)); }
        Prop(group,"cafe_sign",V(13,.15f,-5.4f),180);
        // Low perimeter rails visually bound the compact play area without blocking job routes.
        foreach(int side in new[]{-1,1})
        {
            Block(group,"Boundary rail",V(side*39.3f,.65f,0),V(.09f,.12f,48),"wood");
            for(int z=-23;z<=23;z+=3) Block(group,"Fence post",V(side*39.3f,.4f,z),V(.14f,.8f,.14f),"wood");
        }
    }
    static void House()
    {
        var group = Group("Shared house - lounge and terrace");
        const float floor = 3.255f;
        // This is the existing raised house platform. Keep the staircase mouth (x19.5..21.6) open.
        Block(group,"Oak floor",V(17.93f,floor,18.68f),V(9.82f,.035f,9.82f),"wood");
        for(int i=0;i<20;i++) Block(group,"Floorboard seam",V(13.2f+i*.49f,floor+.021f,18.68f),V(.013f,.006f,9.7f),"cream");
        // Back and side walls have real open windows; the frames do not add hidden collision planes.
        Wall(group,V(17.93f,floor,23.53f),9.8f,0);
        Wall(group,V(13.1f,floor,18.68f),9.7f,90);
        Wall(group,V(22.78f,floor,18.68f),9.7f,90);
        Block(group,"Front left wall",V(16.25f,4.55f,13.83f),V(6.3f,2.6f,.16f),"cream",true);
        Block(group,"Front right pier",V(22.18f,4.55f,13.83f),V(1.2f,2.6f,.16f),"cream",true);
        Block(group,"Door lintel",V(20.55f,5.7f,13.83f),V(2.35f,.3f,.2f),"teal",true);
        foreach(float x in new[]{19.43f,21.67f}) Block(group,"Entry trim",V(x,4.5f,13.72f),V(.12f,2.5f,.14f),"teal");
        Block(group,"House roof",V(17.93f,6.02f,18.68f),V(10.3f,.2f,10.3f),"teal",true);
        Block(group,"Roof cap",V(17.93f,6.17f,18.68f),V(10.05f,.1f,10.05f),"teal");
        foreach(float x in new[]{13.25f,17.9f,22.6f}) Block(group,"Ceiling beam",V(x,5.82f,18.68f),V(.13f,.19f,9.6f),"wood");
        Block(group,"Home sign",V(16.2f,5.08f,13.71f),V(3.65f,.67f,.07f),"teal");
        Label(group,"ROOMIES",V(16.2f,5.08f,13.66f),3.3f,.5f,0);
        Block(group,"Address plaque",V(19.02f,4.77f,13.7f),V(.37f,.43f,.05f),"mustard"); Label(group,"05",V(19.02f,4.77f,13.665f),.28f,.32f,0,"ink");
        // Ground-level facade turns the original solid foundation into a small neighborhood building.
        foreach(float x in new[]{14.8f,17.9f}) { Block(group,"Lower window frame",V(x,1.85f,13.61f),V(1.75f,1.55f,.12f),"teal"); Block(group,"Lower window",V(x,1.85f,13.53f),V(1.53f,1.32f,.04f),"blue"); Block(group,"Mullion",V(x,1.85f,13.5f),V(.07f,1.34f,.05f),"cream"); }
        // Interior: a shared lounge, quiet corner, and useful clutter grouped against walls.
        Prop(group,"sofa",V(16.5f,floor+.04f,21.3f),180,true);
        Prop(group,"coffee_table",V(16.5f,floor+.04f,19.85f),0,true);
        Prop(group,"magazine_stack",V(16.15f,floor+.53f,19.85f),12);
        Prop(group,"tissue_box",V(16.72f,floor+.53f,19.9f),-10);
        Prop(group,"mug",V(16.75f,floor+.53f,19.63f));
        Prop(group,"drink_can",V(16.2f,floor+.53f,19.6f));
        Block(group,"Woven rug",V(16.5f,floor+.027f,19.8f),V(3.2f,.014f,2.75f),"cream");
        foreach(float z in new[]{18.65f,20.95f}) Block(group,"Rug border",V(16.5f,floor+.039f,z),V(3.05f,.008f,.09f),"coral");
        Prop(group,"planter",V(13.65f,floor+.03f,22.6f));
        Prop(group,"planter",V(22.15f,floor+.03f,14.7f));
        Prop(group,"laundry_basket",V(14.5f,floor+.03f,22.75f),15);
        Prop(group,"slippers",V(20.05f,floor+.035f,14.8f),-15);
        Prop(group,"welcome_mat",V(20.55f,floor+.055f,14.15f),0,false,true);
        // Decorative copies have no PlacedFurniture or network behaviour, so they grant no purchased effects.
        Prop(group,"comfy_bed",V(14.0f,floor+.26f,16.4f),0,true,true);
        Prop(group,"wall_clock",V(18.35f,5.12f,23.34f),180,false,true);
        Prop(group,"air_conditioner",V(22.94f,1.4f,20.8f),90);
        Prop(group,"street_bin",V(13.8f,.1f,12.8f));
        Prop(group,"trash_bag",V(14.6f,.1f,12.7f));
        // Baked interior fixtures: two unshadowed lamps keep the room readable at night.
        foreach(float z in new[]{16.5f,21f})
        {
            Block(group,"Ceiling diffuser",V(18,5.86f,z),V(.8f,.045f,.48f),"paper");
            var light = new GameObject("Warm ceiling light"); light.transform.SetParent(group,false); light.transform.localPosition=V(18,5.55f,z);
            var component=light.AddComponent<Light>(); component.type=LightType.Point; component.color=new Color(1,.85f,.68f); component.intensity=1.7f; component.range=7; component.shadows=LightShadows.None;
        }
    }
    static void Wall(Transform parent, Vector3 position, float width, float yaw)
    {
        var go=new GameObject("Window wall"); go.transform.SetParent(parent,false); go.transform.localPosition=position; go.transform.localEulerAngles=V(0,yaw,0);
        var t=go.transform;
        Block(t,"Sill wall",V(0,.5f,0),V(width,1,.16f),"cream",true);
        Block(t,"Top wall",V(0,2.4f,0),V(width,.4f,.16f),"cream",true);
        foreach(float x in new[]{-width*.5f+.16f,0,width*.5f-.16f}) Block(t,"Wall pier",V(x,1.6f,0),V(.32f,1.2f,.18f),"cream",true);
        foreach(float y in new[]{1.02f,2.18f}) Block(t,"Window rail",V(0,y,0),V(width,.08f,.21f),"teal");
        foreach(float x in new[]{-width*.25f,width*.25f}) { Block(t,"Window mullion",V(x,1.6f,0),V(.075f,1.15f,.19f),"teal"); Block(t,"Curtain",V(x-.75f,1.65f,-.14f),V(.45f,1.14f,.035f),"coral"); }
        Block(t,"Skirting",V(0,.07f,-.12f),V(width,.14f,.08f),"wood");
    }
    static void Workplaces()
    {
        var group=Group("Workplaces - facades and loading details");
        Facade(group,26.15f,-8.68f,15,6,"coral","CORNER STORE");
        Facade(group,-8.14f,-9.18f,12,5,"teal","NEIGHBORHOOD WORKS");
        Facade(group,-25.43f,-9.16f,13.95f,7,"mustard","ROOMIES EXPRESS");
        // Existing arcade machines and blackjack table remain accessible on their original floor.
        Block(group,"Arcade rear wall",V(7.8f,1.65f,-19.55f),V(18,3.2f,.16f),"teal",true);
        Block(group,"Arcade sign backing",V(7.8f,2.5f,-19.43f),V(7,.7f,.08f),"coral");
        Label(group,"AFTER HOURS",V(7.8f,2.5f,-19.37f),6.5f,.55f,180);
        foreach(float x in new[]{-.95f,16.6f}) Block(group,"Arcade canopy post",V(x,1.7f,-9f),V(.16f,3.4f,.16f),"teal",true);
        Block(group,"Arcade canopy",V(7.8f,3.55f,-13.8f),V(18.2f,.18f,12),"cream",true);
        Block(group,"Arcade canopy fascia",V(7.8f,3.5f,-7.76f),V(18.2f,.36f,.14f),"coral");
        Label(group,"PLAY / UNWIND / REPEAT",V(7.8f,3.51f,-7.67f),8,.23f,180);
        Prop(group,"vending_machine",V(15.9f,1.14f,-9.2f),0,true,true);
        Prop(group,"bench",V(-.3f,.14f,-17.1f),90,true);
        Prop(group,"planter",V(16.3f,.15f,-7));
        Prop(group,"parcel_stack",V(-19.4f,.05f,-8.5f),15);
        Prop(group,"parcel_stack",V(-18.7f,.05f,-10),-10);
        Prop(group,"street_bin",V(-32.3f,.05f,-8.7f));
        Prop(group,"air_conditioner",V(-12.8f,1.7f,-9.04f));
        Prop(group,"cafe_sign",V(21.6f,.03f,-6.8f));
        Prop(group,"planter",V(33.2f,.03f,-7.5f));
        // Frame the original delivery trigger without adding collision or moving its button.
        foreach(float x in new[]{4.36f,12.46f}) Block(group,"Delivery bay edge",V(x,.205f,18.7f),V(.1f,.014f,8.1f),"mustard");
        foreach(float z in new[]{14.66f,22.76f}) Block(group,"Delivery bay edge",V(8.41f,.205f,z),V(8.1f,.014f,.1f),"mustard");
        Block(group,"Bay sign",V(8.4f,2.4f,23.05f),V(4,.8f,.12f),"teal");
        Label(group,"DELIVERY BAY",V(8.4f,2.4f,22.975f),3.6f,.55f,0);
        foreach(float x in new[]{6.6f,10.2f}) Block(group,"Bay signpost",V(x,1.1f,23.05f),V(.1f,2.2f,.1f),"wood");
    }
    static void Facade(Transform parent,float x,float front,float width,float height,string color,string name)
    {
        Block(parent,name+" fascia",V(x,height-.3f,front+.11f),V(width+.2f,.65f,.2f),color);
        Block(parent,name+" roof trim",V(x,height+.05f,front-width*.05f-3.5f),V(width+.35f,.15f,8.1f),color);
        Label(parent,name,V(x,height-.3f,front+.23f),width*.68f,.4f,180);
        for(int i=0;i<5;i++)
        {
            float px=x-width*.4f+i*width*.2f;
            Block(parent,"Shop window surround",V(px,1.8f,front+.13f),V(width*.15f,2.05f,.14f),color);
            Block(parent,"Shop glazing",V(px,1.8f,front+.21f),V(width*.15f-.15f,1.87f,.03f),"blue");
            Block(parent,"Window mullion",V(px,1.8f,front+.24f),V(.06f,1.9f,.035f),"cream");
            Block(parent,"Window highlight",V(px-.24f,2.25f,front+.245f),V(.055f,.56f,.01f),"paper",false,V(0,0,25));
        }
        Block(parent,"Shop awning",V(x,3.1f,front+.65f),V(width,.13f,1.4f),color,false,V(-8,0,0));
        for(int i=0;i<12;i++) Block(parent,"Awning stripe",V(x-width*.46f+i*width/12,3.17f,front+.65f),V(width/24,.02f,1.4f),"cream",false,V(-8,0,0));
        Block(parent,"Foundation course",V(x,.22f,front+.1f),V(width,.34f,.2f),"wood");
    }
    static void Garden()
    {
        var group=Group("Pocket gardens and distant silhouettes");
        foreach(Vector3 p in new[]{V(-33,0,15),V(-26,0,20),V(-15,0,16),V(-5,0,21),V(31,0,19),V(35,0,11),V(-36,0,-20),V(36,0,-21)})
        {
            Block(group,"Garden bed",p+V(0,.12f,0),V(3.3f,.24f,2.5f),"wood");
            Block(group,"Soil",p+V(0,.25f,0),V(3.1f,.025f,2.3f),"green");
            var trunk=GameObject.CreatePrimitive(PrimitiveType.Cylinder); trunk.name="Tree trunk"; trunk.transform.SetParent(group,false); trunk.transform.localPosition=p+V(0,1,0); trunk.transform.localScale=V(.27f,1,.27f); trunk.GetComponent<Renderer>().sharedMaterial=Mat("wood"); Object.DestroyImmediate(trunk.GetComponent<Collider>());
            for(int i=0;i<3;i++)
            {
                var crown=GameObject.CreatePrimitive(PrimitiveType.Sphere); crown.name="Rounded tree canopy"; crown.transform.SetParent(group,false); crown.transform.localPosition=p+V((i-1)*.6f,2.5f+(i%2)*.5f,0); crown.transform.localScale=V(1.8f,2.2f,1.7f); crown.GetComponent<Renderer>().sharedMaterial=Mat(i==1?"teal":"green"); Object.DestroyImmediate(crown.GetComponent<Collider>()); crown.isStatic=true;
            }
            Prop(group,"planter",p+V(1.15f,.27f,.6f));
        }
        // Rear facades sit outside the playable ground, establishing a town without extending its bounds.
        for(int i=0;i<11;i++)
        {
            float x=-36+i*7, height=5+(i%3)*1.4f;
            Block(group,"Distant home",V(x,height/2,-29.5f),V(5.8f,height,5),i%3==0?"coral":i%3==1?"cream":"teal");
            Block(group,"Distant roof",V(x,height+.15f,-29.5f),V(6.2f,.3f,5.4f),"ink");
            for(int row=0;row<2;row++) for(int col=0;col<3;col++) Block(group,"Distant window",V(x-1.7f+col*1.7f,1.6f+row*2,-26.97f),V(.8f,1.1f,.03f),"blue");
        }
    }

    [MenuItem("Roomies/Art/Render Prop Gallery")]
    internal static void Gallery()
    {
        var preview=EditorSceneManager.NewPreviewScene();
        var oldScene=scene; scene=preview;
        var host=new GameObject("Prop Gallery"); SceneManager.MoveGameObjectToScene(host,preview);
        try
        {
            root=host.transform;
            var all=Directory.GetFiles(RoomiesArtLibraryBuilder.Root+"/Furniture","*.prefab").OrderBy(p=>p).ToArray();
            for(int i=0;i<all.Length;i++)
            {
                string id=Path.GetFileNameWithoutExtension(all[i]);
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>(all[i]);
                var bounds=asset.GetComponent<MeshFilter>().sharedMesh.bounds;
                Prop(root,id,V((i%4)*2.2f,-bounds.min.y,(i/4)*2.5f),180,false,true);
            }
            Block(root,"Gallery floor",V(3.3f,-.08f,2.5f),V(11,.1f,10),"cream");
            var light=new GameObject("Gallery key"); SceneManager.MoveGameObjectToScene(light,preview); light.transform.rotation=Quaternion.Euler(45,-30,0); var l=light.AddComponent<Light>(); l.type=LightType.Directional;l.intensity=1;
            RoomiesArtAudit.Render(preview,V(9,7,-9),V(3.3f,.5f,2.7f),"furniture-gallery");
        }
        finally { EditorSceneManager.ClosePreviewScene(preview); scene=oldScene; }
    }

    [MenuItem("Roomies/Art/Finalize and Validate Environment")]
    internal static void FinalizeEnvironment()
    {
        scene=SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened) scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        var previousActive=SceneManager.GetActiveScene();
        try
        {
            if(scene.isDirty) throw new InvalidOperationException("Save pending GameRoom edits first.");
            SceneManager.SetActiveScene(scene);
            var host=scene.GetRootGameObjects().First(g=>g.name==RootName); root=host.transform;
            if(root.Find("Entry stairs")==null)
            {
                var stairs=Group("Entry stairs");
                for(int i=0;i<12;i++)
                {
                    float top=.1f+3.17f*(i+1)/12;
                    Block(stairs,"Tread "+(i+1),V(20.55f,top-.075f,10.01f+(i+.5f)/3),V(1.9f,.15f,1f/3+.008f),"cream",true);
                    Block(stairs,"Tread edge",V(20.55f,top+.004f,10.01f+i/3f+.022f),V(1.9f,.008f,.045f),"teal");
                }
            }
            var sun=scene.GetRootGameObjects().First(g=>g.name=="Directional Light").GetComponent<Light>();
            sun.intensity=1.2f; sun.color=new Color(1,.94f,.82f); sun.transform.rotation=Quaternion.Euler(48,-32,0);
            var day=scene.GetRootGameObjects().Select(g=>g.GetComponent<DayManager>()).First(g=>g!=null);
            var settings=new SerializedObject(day); settings.FindProperty("environmentSun").objectReferenceValue=sun; settings.ApplyModifiedPropertiesWithoutUndo();
            RenderSettings.sun=sun;
            RenderSettings.skybox=AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Epic_BlueSunset/Epic_BlueSunset.mat");
            // Lamps augment the sky at night without adding dozens of shadow maps.
            var streets=root.Find("Street - sidewalks and wayfinding");
            if(streets.Find("Street accent 0")==null)
            for(int i=0;i<5;i++)
            {
                var go=new GameObject("Street accent "+i); go.transform.SetParent(streets,false); go.transform.localPosition=V(-34+i*17,3.55f,5.8f);
                var light=go.AddComponent<Light>(); light.type=LightType.Point; light.range=6; light.intensity=1; light.color=new Color(1,.83f,.57f); light.shadows=LightShadows.None;
            }
            // Persist the scenery as a reusable prefab, retaining nested authored prop instances.
            Directory.CreateDirectory("Assets/_Prefabs/Environment");
            PrefabUtility.SaveAsPrefabAssetAndConnect(host,"Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
            Physics.SyncTransforms();
            var report=new System.Text.StringBuilder(); int failures=0;
            foreach(var item in FurnitureCatalog.Items)
            {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomiesArtLibraryBuilder.Root}/Furniture/{item.id}.prefab");
                bool valid=prefab!=null&&prefab.GetComponent<MeshFilter>()?.sharedMesh!=null&&prefab.GetComponent<Collider>()!=null&&prefab.GetComponentInChildren<Unity.Netcode.NetworkObject>(true)==null;
                if(valid) { var size=prefab.GetComponent<MeshFilter>().sharedMesh.bounds.size; valid=(size-item.placeholderSize).sqrMagnitude<.00001f && prefab.GetComponent<MeshRenderer>().sharedMaterials.All(m=>m!=null); }
                report.AppendLine($"Furniture {item.id}: {(valid?"PASS":"FAIL")}"); if(!valid)failures++;
            }
            foreach(var point in scene.GetRootGameObjects().Where(g=>g.GetComponent<SpawnPoint>()!=null))
            {
                Vector3 center=point.transform.position;
                var controller=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab").GetComponent<CharacterController>();
                center+=controller.center;
                float segment=Mathf.Max(0,controller.height*.5f-controller.radius);
                var hits=Physics.OverlapCapsule(center+Vector3.up*segment,center-Vector3.up*segment,controller.radius,~0,QueryTriggerInteraction.Ignore);
                var blockers=hits.Where(c=>c.gameObject.scene==scene).ToArray();
                report.AppendLine($"Spawn {point.name}: {(blockers.Length==0?"PASS":"FAIL")} {string.Join(",",blockers.Select(c=>c.name))}"); if(blockers.Length>0)failures++;
            }
            foreach(string role in new[]{"Giver","Dealer","Police"})
            {
                var npc=AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Prefabs/Smuggling/{role}_Placeholder.prefab");
                var placeholder=npc.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Placeholder");
                bool valid=npc.GetComponent<SmugglingAppearance>().HasReplacementModel&&!placeholder.gameObject.activeSelf&&npc.GetComponent<Collider>()!=null;
                report.AppendLine($"NPC {role}: {(valid?"PASS":"FAIL")}"); if(!valid)failures++;
            }
            int missing=host.GetComponentsInChildren<Transform>(true).Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            report.AppendLine($"Missing scripts: {missing}"); failures+=missing;
            report.AppendLine($"Scenery renderers: {host.GetComponentsInChildren<Renderer>().Length}; failures: {failures}");
            Directory.CreateDirectory("Temp/RoomiesArt"); File.WriteAllText("Temp/RoomiesArt/validation.txt",report.ToString());
            RoomiesArtAudit.Render(scene,V(40,31,43),V(0,0,0),"environment-after");
            RoomiesArtAudit.Render(scene,V(21.4f,5.1f,14.8f),V(16.5f,4.3f,20.5f),"living-room");
            RoomiesArtAudit.Render(scene,V(11,8,5),V(18,4.3f,18),"house-after");
            RoomiesArtAudit.Render(scene,V(23,2.1f,3),V(2,2.3f,-11),"street-after");
            Debug.Log($"[Roomies Art] Validation completed: {failures} failures. See Temp/RoomiesArt/validation.txt");
        }
        finally { SceneManager.SetActiveScene(previousActive); if(opened)EditorSceneManager.CloseScene(scene,true); }
    }
}
#endif
