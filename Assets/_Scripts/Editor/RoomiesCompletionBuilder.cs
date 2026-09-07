#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

internal static class RoomiesCompletionBuilder
{
    const string GamePath="Assets/_Scenes/GameRoom.unity";
    const string Art="Assets/Resources/RoomiesArt";
    static Vector3 V(float x,float y,float z)=>new(x,y,z);
    static Material Mat(string key)=>AssetDatabase.LoadAssetAtPath<Material>($"{Art}/Materials/{key}.mat");
    static Transform Group(Transform parent,string name,Vector3 pos)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;return go.transform;}
    static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size,string material,bool solid=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=Mat(material);
        if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());
        else go.layer=LayerMask.NameToLayer("Wall");
        return go;
    }
    static TMP_Text Label(Transform parent,string text,Vector3 pos,Vector2 size,float yaw=0,float fontSize=3)
    {
        var go=Group(parent,"Sign "+text.Replace('\n',' '),pos);go.localEulerAngles=V(0,yaw,0);
        var label=go.gameObject.AddComponent<TextMeshPro>();label.text=text;label.font=TMP_Settings.defaultFontAsset;
        label.rectTransform.sizeDelta=size;label.alignment=TextAlignmentOptions.Center;label.fontSize=fontSize;label.color=Color.white;
        return label;
    }
    static GameObject Prop(Transform parent,string id,Vector3 pos,float yaw=0)
    {
        var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/Props/{id}.prefab"),parent);
        go.transform.localPosition=pos;go.transform.localEulerAngles=V(0,yaw,0);
        foreach(var collider in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
        return go;
    }
    [MenuItem("Roomies/Completion/Build Store and Town Depth")]
    static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode first.");
        var scene=SceneManager.GetSceneByPath(GamePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty)throw new InvalidOperationException("Save GameRoom edits first.");
        if(opened)scene=EditorSceneManager.OpenScene(GamePath,OpenSceneMode.Additive);
        var previous=SceneManager.GetActiveScene();
        try
        {
            SceneManager.SetActiveScene(scene);
            if(scene.GetRootGameObjects().Any(g=>g.name=="Roomies_Completion"))throw new InvalidOperationException("Already installed; preserve authored changes. Use Validate and Preview.");
            // Replace only the old solid shop and its cosmetic facade within the verified footprint.
            var demo=scene.GetRootGameObjects().First(g=>g.name=="Demo");
            var shop=demo.GetComponentsInChildren<Renderer>().Where(r=>r.bounds.center.x>20&&r.bounds.center.x<32&&r.bounds.center.z< -9&&r.bounds.size.x>10&&r.bounds.size.y>3).ToArray();
            if(shop.Length!=1)throw new InvalidOperationException($"Expected one old shop volume; found {shop.Length}. Inspect before replacing.");
            Undo.RecordObject(shop[0].gameObject,"Replace solid convenience store shell");shop[0].gameObject.SetActive(false);
            var environment=scene.GetRootGameObjects().First(g=>g.name=="Roomies_EnvironmentArt");
            foreach(var t in environment.GetComponentsInChildren<Transform>(true))
            {
                var p=t.position;
                if(t.parent!=null&&t.parent.name.StartsWith("Workplaces")&&p.x>18&&p.x<34&&p.z< -7&&p.z> -18)
                {Undo.RecordObject(t.gameObject,"Replace shop facade");t.gameObject.SetActive(false);}
            }
            var root=new GameObject("Roomies_Completion");Undo.RegisterCreatedObjectUndo(root,"Build shop and town depth");
            BuildStore(Group(root.transform,"Corner convenience store",V(26.15f,0,-13)));
            BuildDepth(Group(root.transform,"Layered neighborhood",Vector3.zero));
            // The original platform supplies Room detection; make its top surface explicit as well.
            var floor=environment.GetComponentsInChildren<Transform>().First(t=>t.name=="Oak floor");
            if(floor.GetComponent<Collider>()==null)floor.gameObject.AddComponent<BoxCollider>();floor.gameObject.tag="Room";
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        }
        finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
        SetFivePlayers();
        ValidateAndPreview();
    }
    static void BuildStore(Transform root)
    {
        Box(root,"Tiled floor",V(0,.07f,0),V(15,.14f,9),"cream",true);
        Box(root,"Rear wall",V(0,1.95f,-4.45f),V(15,3.7f,.18f),"teal",true);
        foreach(float x in new[]{-7.45f,7.45f})Box(root,"Side wall",V(x,1.95f,0),V(.18f,3.7f,9),"cream",true);
        foreach(float x in new[]{-4.6f,4.6f})
        {
            Box(root,"Front window base",V(x,.45f,4.45f),V(5.7f,.62f,.18f),"teal",true);
            Box(root,"Window transom",V(x,3.25f,4.45f),V(5.7f,.18f,.18f),"teal",true);
            // Open glazing bays: view into the shop; low sill has collision.
            foreach(float dx in new[]{-2.8f,0,2.8f})Box(root,"Window frame",V(x+dx,1.9f,4.45f),V(.09f,2.7f,.12f),"teal",true);
        }
        Box(root,"Roof",V(0,3.85f,0),V(15.5f,.24f,9.5f),"cream",true);
        Box(root,"Store sign",V(0,3.5f,4.65f),V(15.5f,.75f,.2f),"coral");
        Label(root,"CORNER / まちのコンビニ",V(0,3.5f,4.77f),new Vector2(13,.6f),180,5);
        Box(root,"Entry mat",V(0,.15f,3.95f),V(3.2f,.02f,1.2f),"mustard");
        Label(root,"レジ打ちスタッフ募集中\n昼営業 / 共同口座にお給料",V(4.7f,2,4.56f),new Vector2(4.8f,1.3f),180,3.2f);
        for(int row=0;row<3;row++)
        {
            float x=-5+row*5;
            Box(root,"Stock shelf back",V(x,1.25f,-3.8f),V(3.8f,2.2f,.12f),"wood",true);
            for(int tier=0;tier<3;tier++)
            {
                float y=.45f+tier*.64f;Box(root,"Stock shelf",V(x,y,-3.4f),V(3.8f,.08f,.85f),"cream",true);
                for(int j=0;j<7;j++)
                {
                    var p=V(x-1.5f+j*.5f,y+.06f,-3.4f);
                    if(row==0)Prop(root,"drink_can",p);
                    else {Box(root,"Packaged groceries",p+V(0,.16f,0),V(.3f,.32f,.27f),row==1?"mustard":"coral");Box(root,"Product label",p+V(0,.17f,.142f),V(.22f,.15f,.015f),"paper");}
                }
            }
            Label(root,new[]{"DRINKS","LUNCH","SNACKS"}[row],V(x,2.65f,-3.69f),new Vector2(3,.35f),180,2.5f);
        }
        for(int lane=0;lane<2;lane++)BuildRegister(Group(root,"Checkout "+(lane+1),V(lane==0?-3.4f:3.4f,0,1)));
        foreach(float x in new[]{-4f,4f})
        {
            Box(root,"Ceiling light",V(x,3.65f,0),V(2.2f,.08f,.45f),"paper");
            var light=Group(root,"Shop lighting",V(x,3.2f,0)).gameObject.AddComponent<Light>();light.type=LightType.Point;light.range=9;light.intensity=2;light.shadows=LightShadows.None;
        }
        Prop(root,"planter",V(-7,.14f,4.8f));Prop(root,"street_bin",V(7,.14f,4.8f));
    }
    static void BuildRegister(Transform root)
    {
        root.gameObject.AddComponent<NetworkObject>();var register=root.gameObject.AddComponent<CashRegister>();
        register.clerkPoint=Group(root,"Clerk standing area",V(0,1.2f,-1.6f));
        Box(root,"Checkout counter",V(0,.61f,0),V(2.7f,1.1f,1.25f),"teal",true);
        Box(root,"Worktop",V(0,1.18f,0),V(2.85f,.09f,1.35f),"cream",true);
        Box(root,"Register screen back",V(0,1.95f,.22f),V(2.65f,1.38f,.12f),"ink");
        register.display=Label(root,"",V(0,1.95f,.148f),new Vector2(2.48f,1.23f),0,1.55f);
        Target(register,Box(root,"Scanner - E",V(0,1.29f,-.15f),V(1.25f,.12f,.44f),"coral",true),-1);
        register.choiceLabels=new TMP_Text[3];
        for(int i=0;i<3;i++)
        {
            var button=Box(root,"Change choice "+i,V((i-1)*.76f,1.28f,-.55f),V(.66f,.1f,.3f),"mustard",true);
            Target(register,button,i);
            var label=Label(root,"—",V((i-1)*.76f,1.34f,-.55f),new Vector2(.62f,.25f),0,1.7f);label.transform.localEulerAngles=V(90,0,0);register.choiceLabels[i]=label;
        }
        register.goods=new Transform[3];
        for(int i=0;i<3;i++)register.goods[i]=Box(root,"Customer product "+i,V(-.85f+i*.85f,1.38f,.54f),V(.28f,.32f,.25f),new[]{"mustard","paper","coral"}[i]).transform;
        var arrival=Group(root,"Customer arrival",V(0,.15f,1.7f));register.customer=Group(arrival,"Customer",Vector3.zero);
        var model=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Models/Bob/Bobs.fbx");
        if(model!=null)
        {
            var npc=Object.Instantiate(model,register.customer);npc.name="Customer visual";
            var renderers=npc.GetComponentsInChildren<Renderer>();if(renderers.Length>0)
            {
                var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                npc.transform.localScale*=1.72f/Mathf.Max(.1f,bounds.size.y);
                bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                npc.transform.position+=register.customer.position-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
            }
            npc.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var collider in npc.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
        }
        register.RefreshVisual();register.customer.gameObject.SetActive(true);
        Label(root,"STAFF / スキャナーを E",V(0,.75f,-.637f),new Vector2(2.5f,.4f),0,2);
    }
    static void Target(CashRegister register,GameObject go,int action)
    {go.layer=0;var target=go.AddComponent<CashRegisterTarget>();target.register=register;target.action=action;}

    [MenuItem("Roomies/Completion/Polish Checkout and Backdrop")]
    static void Polish()
    {
        var scene=SceneManager.GetSceneByPath(GamePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty)throw new InvalidOperationException("Save GameRoom first.");
        if(opened)scene=EditorSceneManager.OpenScene(GamePath,OpenSceneMode.Additive);
        try
        {
            var root=scene.GetRootGameObjects().First(g=>g.name=="Roomies_Completion");
            foreach(var register in root.GetComponentsInChildren<CashRegister>())
            {
                register.sound=register.GetComponent<AudioSource>();
                if(register.sound==null)register.sound=register.gameObject.AddComponent<AudioSource>();
                register.sound.playOnAwake=false;register.sound.spatialBlend=1;register.sound.minDistance=1;register.sound.maxDistance=10;register.sound.rolloffMode=AudioRolloffMode.Linear;
                register.scanSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Audio/Mining/mining_hit.wav");
                register.paymentSound=AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Audio/Mining/mining_sale.wav");
            }
            if(root.transform.Find("Distant land continuation")==null)
            {
                var land=Group(root.transform,"Distant land continuation",Vector3.zero);
                // Continuous land under the background prevents the horizon looking like a floating model.
                Box(land,"Eastern embankment",V(70,-1.5f,0),V(62,3,130),"green",true);
                Box(land,"Western embankment",V(-70,-1.5f,0),V(62,3,130),"green",true);
                Box(land,"Northern ground",V(0,-1.5f,50),V(78,3,50),"green",true);
                Box(land,"Southern ground",V(0,-1.5f,-50),V(78,3,50),"green",true);
                Box(land,"Road continuing east",V(63,.035f,0),V(46,.05f,8),"ink",true);
                for(int i=0;i<5;i++)
                {
                    float x=85+i*4,z=i*i*.3f;
                    var bend=Box(land,"Road bend into town",V(x,.06f+i*.08f,z),V(4.8f,.12f,8),"ink",true);bend.transform.localEulerAngles=V(0,-i*8,0);
                    Box(land,"Roadside hill",V(x,2+i*.3f,11+z),V(5,4+i*.6f,9),"green");
                }
            }
            const string folder=Art+"/ArchitectureMeshes";Directory.CreateDirectory(folder);
            int rounded=0;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                if(filter.sharedMesh==null||filter.sharedMesh.name!="Cube")continue;
                var s=filter.transform.lossyScale;var size=new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));
                float min=Mathf.Min(size.x,Mathf.Min(size.y,size.z));if(min<.06f||Mathf.Max(size.x,Mathf.Max(size.y,size.z))>50)continue;
                string path=$"{folder}/completion_{Hash128.Compute(size.ToString("R"))}.asset";
                var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(mesh==null)
                {
                    mesh=RoomiesArtLibraryBuilder.RoundedBoxMesh(size,Mathf.Min(.06f,min*.1f));mesh.name="Completion rounded box";
                    var vertices=mesh.vertices;var normals=mesh.normals;
                    for(int i=0;i<vertices.Length;i++){vertices[i]=new Vector3(vertices[i].x/size.x,vertices[i].y/size.y,vertices[i].z/size.z);normals[i]=Vector3.Scale(normals[i],size).normalized;}
                    mesh.vertices=vertices;mesh.normals=normals;mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
                }
                filter.sharedMesh=mesh;rounded++;
            }
            // Bake static background geometry for the renderer; moving checkout visuals remain dynamic.
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
                if(filter.GetComponentInParent<NetworkObject>()==null)GameObjectUtility.SetStaticEditorFlags(filter.gameObject,StaticEditorFlags.BatchingStatic);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log($"[Completion] Checkout spatial audio assigned; {rounded} new meshes rounded; town backdrop extended.");
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        ValidateAndPreview();
    }
    static void BuildDepth(Transform root)
    {
        // Distant landforms and stepped housing are outside the existing job routes.
        for(int i=0;i<9;i++)
        {
            var hill=GameObject.CreatePrimitive(PrimitiveType.Sphere);hill.name="Distant rolling hill";hill.transform.SetParent(root,false);
            hill.transform.localPosition=V(-90+i*23,-5,65+(i%3)*15);hill.transform.localScale=V(45,22+(i%3)*10,40);
            hill.GetComponent<Renderer>().sharedMaterial=Mat(i%2==0?"green":"teal");Object.DestroyImmediate(hill.GetComponent<Collider>());
        }
        for(int row=0;row<2;row++)for(int i=0;i<8;i++)
        {
            float x=-60+i*17,z=-40-row*23,h=5+(i*7+row*3)%11;
            Box(root,"Raised town foundation",V(x,row*2-1,z),V(15,4+row*4,12),"wood");
            Box(root,"Distant apartment",V(x,h/2+1+row*4,z),V(10,h,9),i%2==0?"cream":"silver");
            Box(root,"Roof silhouette",V(x,h+1.2f+row*4,z),V(10.5f,.4f,9.5f),"teal");
            for(int f=0;f<h/2;f++)for(int w=0;w<3;w++)Box(root,"Apartment window",V(x-3+w*3,2+f*2+row*4,z+4.53f),V(1.25f,1,.04f),"blue");
        }
        Box(root,"Elevated rail viaduct",V(57,8,0),V(8,.7f,110),"silver");
        foreach(float x in new[]{54f,60f})Box(root,"Rail parapet",V(x,8.65f,0),V(.25f,.8f,110),"teal");
        for(int z=-45;z<=45;z+=15)Box(root,"Viaduct pier",V(57,3.5f,z),V(2,8,2),"cream");
        for(int car=0;car<3;car++)
        {
            Box(root,"Distant train",V(57,10,-20+car*9),V(3.7f,2.6f,8.5f),"coral");
            for(int w=0;w<4;w++)Box(root,"Train window",V(55.12f,10.3f,-23+car*9+w*1.9f),V(.05f,1,1.35f),"blue");
        }
        // A small reachable terrace adds actual foreground height without changing the main road.
        var terrace=Group(root,"Pocket park terrace",V(-28,0,10));
        Box(terrace,"Terrace base",V(0,.6f,0),V(10,1.2f,5),"wood",true);
        Box(terrace,"Terrace paving",V(0,1.22f,0),V(10,.04f,5),"cream",true);
        for(int step=0;step<6;step++)Box(terrace,"Park step",V(0,(step+1)*.1f,-5+step*.44f),V(3,(step+1)*.2f,.46f),"cream",true);
        foreach(float x in new[]{-4.9f,4.9f})Box(terrace,"Park side rail",V(x,1.75f,0),V(.1f,1,5),"teal",true);
        Box(terrace,"Park rear rail",V(0,1.75f,2.45f),V(10,1,.1f),"teal",true);
        Prop(terrace,"bench",V(-2.6f,1.25f,.6f),180);Prop(terrace,"planter",V(3.5f,1.25f,1.1f));
    }
    static void SetFivePlayers()
    {
        const string path="Assets/_Scenes/MainMenuSteam.unity";
        var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty)throw new InvalidOperationException("Save MainMenuSteam before changing lobby capacity.");
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            foreach(var lobby in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SteamLobby>(true)))
            {var so=new SerializedObject(lobby);so.FindProperty("maxMembers").intValue=5;so.ApplyModifiedProperties();}
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
    [MenuItem("Roomies/Completion/Validate and Preview")]
    static void ValidateAndPreview()
    {
        var scene=SceneManager.GetSceneByPath(GamePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(GamePath,OpenSceneMode.Additive);
        try
        {
            var report=new StringBuilder();var roots=scene.GetRootGameObjects();
            var registers=roots.SelectMany(g=>g.GetComponentsInChildren<CashRegister>()).ToArray();
            if(registers.Length!=2)throw new InvalidOperationException("Expected two checkout lanes.");
            foreach(var r in registers)
            {
                if(r.display==null||r.clerkPoint==null||r.customer==null||r.choiceLabels.Length!=3||r.goods.Length!=3||r.GetComponentsInChildren<CashRegisterTarget>().Length!=4)throw new InvalidOperationException("Incomplete checkout wiring.");
                if(r.GetComponent<NetworkObject>()==null)throw new InvalidOperationException("Missing checkout NetworkObject.");
            }
            var spawns=roots.SelectMany(g=>g.GetComponentsInChildren<SpawnPoint>()).ToArray();if(spawns.Length<5)throw new InvalidOperationException("Five spawn points required.");
            for(int order=1;order<=100;order++)
            {int matches=Enumerable.Range(0,3).Count(i=>CashRegister.Change(order,i)==1000-CashRegister.Total(order));if(matches!=1)throw new InvalidOperationException("Ambiguous change choices.");}
            report.AppendLine("PASS: two networked checkout lanes; scanner and 3 change choices per lane.");
            report.AppendLine("PASS: 100 order cases have exactly one correct change; 5 spawn points available.");
            foreach(var go in roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true)))if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go.gameObject)>0)throw new InvalidOperationException("Missing script: "+go.name);
            report.AppendLine("PASS: no missing scripts in GameRoom.");
            Physics.SyncTransforms();
            foreach(var point in new[]{V(26.15f,1.22f,-8.4f),V(26.15f,1.22f,-12),V(22.75f,1.22f,-13.6f),V(29.55f,1.22f,-13.6f)})
            {
                var obstacles=Physics.OverlapCapsule(point+Vector3.down*.45f,point+Vector3.up*.45f,.45f,~0,QueryTriggerInteraction.Ignore).Where(c=>c.gameObject.scene==scene).ToArray();
                if(obstacles.Length>0)throw new InvalidOperationException("Blocked store route: "+point+" / "+string.Join(",",obstacles.Select(c=>c.name)));
            }
            report.AppendLine("PASS: entrance, aisle and both clerk standing capsules clear.");
            RoomiesArtAudit.Render(scene,V(32,4.8f,-3),V(25.8f,1.6f,-12),"completion-store");
            RoomiesArtAudit.Render(scene,V(26.15f,2.2f,-16),V(22.75f,1.7f,-11.8f),"completion-checkout");
            RoomiesArtAudit.Render(scene,V(-27,4.2f,11),V(25,3.5f,-12),"completion-street");
            RoomiesArtAudit.Render(scene,V(45,32,42),V(0,1,-6),"completion-town");
            var preview=EditorSceneManager.NewPreviewScene();
            try
            {
                var go=new GameObject("Settings preview");SceneManager.MoveGameObjectToScene(go,preview);var canvas=go.AddComponent<EscMenuUI>().BuildSettingsPreview();
                RoomiesArtAudit.Render(preview,V(0,0,-10),Vector3.zero,"completion-settings-wide",canvas,1920,1080);
                RoomiesArtAudit.Render(preview,V(0,0,-10),Vector3.zero,"completion-settings-4x3",canvas,1024,768);
                Object.DestroyImmediate(go);
                go=new GameObject("Result preview");SceneManager.MoveGameObjectToScene(go,preview);canvas=go.AddComponent<SessionEndUI>().BuildPreview();
                RoomiesArtAudit.Render(preview,V(0,0,-10),Vector3.zero,"completion-result",canvas,1600,900);
            }
            finally{EditorSceneManager.ClosePreviewScene(preview);}
            Directory.CreateDirectory("Temp/RoomiesArt");File.WriteAllText("Temp/RoomiesArt/completion-validation.txt",report.ToString());
            Debug.Log(report.ToString());
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
}
#endif
