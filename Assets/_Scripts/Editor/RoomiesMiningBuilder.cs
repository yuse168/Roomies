#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

internal static class RoomiesMiningBuilder
{
    const string ScenePath="Assets/_Scenes/GameRoom.unity";
    const string Root="Assets/Resources/RoomiesArt";
    static readonly List<MiningInteractable> Targets=new();
    static readonly List<Light> Lights=new();
    static Material Mat(string key)=>AssetDatabase.LoadAssetAtPath<Material>($"{Root}/Materials/{key}.mat");
    static Vector3 V(float x,float y,float z)=>new(x,y,z);

    [MenuItem("Roomies/Mining/Build and Validate Mining Job")]
    static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before authoring.");
        var scene=SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty)throw new InvalidOperationException("Save current GameRoom edits before building.");
        RoomiesArtLibraryBuilder.BuildMiningArt();
        var ores=BuildOrePrefabs();
        InstallPlayer();
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        var previous=SceneManager.GetActiveScene();
        try
        {
            SceneManager.SetActiveScene(scene);
            var existing=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Roomies_Mining");
            if(existing!=null)throw new InvalidOperationException("Mining already installed. Use Validate Mining Job; do not overwrite authored edits.");
            Targets.Clear();Lights.Clear();
            var root=new GameObject("Roomies_Mining");Undo.RegisterCreatedObjectUndo(root,"Build underground mining job");
            root.AddComponent<NetworkObject>();var site=root.AddComponent<MiningSite>();site.ores=ores;
            var surface=Group(root.transform,"Surface - ONE MORE ROCK",Vector3.zero);
            var safe=Group(root.transform,"Surface return",V(27,1.2f,10));safe.localEulerAngles=V(0,180,0);site.surfaceReturn=safe;
            var arrivals=new Transform[3];var upperReturns=new Transform[3];
            var floors=new Transform[3];site.eventCenters=new Transform[3];
            for(int t=0;t<3;t++)
            {
                float y=-12-t*10;
                floors[t]=Group(root.transform,$"Level {t+1} - "+new[]{"COPPER COMMONS","SILVER SHIFT","GOLD FEVER"}[t],V(0,y,60));
                arrivals[t]=Group(root.transform,"Lift arrival "+t,V(0,y+1.2f,58));
                upperReturns[t]=Group(root.transform,"Upper return "+t,V(0,y+1.2f,80));upperReturns[t].localEulerAngles=V(0,180,0);
                site.eventCenters[t]=Group(root.transform,"Hazard center "+t,V(0,y+1,73));
            }
            BuildSurface(surface,site,arrivals[0]);
            for(int t=0;t<3;t++)BuildFloor(floors[t],site,t+1,t==0?safe:upperReturns[t-1],t==2?null:arrivals[t+1]);
            site.targets=Targets.ToArray();site.mineLights=Lights.ToArray();
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))
                if(collider.GetComponentInParent<MiningInteractable>()==null)collider.gameObject.layer=LayerMask.NameToLayer("Wall");
            site.hitSound=Tone("mining_hit",150,.14f,true);site.breakSound=Tone("mining_break",90,.35f,true);
            site.saleSound=Tone("mining_sale",760,.35f,false);site.warningSound=Tone("mining_warning",430,.5f,false);
            EditorUtility.SetDirty(site);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Validate(scene);
            Capture(scene);
        }
        finally {SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    static MiningOre[] BuildOrePrefabs()
    {
        const string folder="Assets/_Prefabs/Mining";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
        string[] names={"石","石炭","銅鉱石","鉄鉱石","銀鉱石","金鉱石","宝石","巨大金鉱","化石になった空き缶","古いゲーム機","笑っている謎の壺"};
        int[] prices={10,20,35,60,110,220,350,650,15,80,40};int[] weights={1,1,1,2,2,3,1,5,1,2,2};
        var list=AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        var result=new MiningOre[names.Length];
        for(int i=0;i<names.Length;i++)
        {
            var go=new GameObject("MiningOre_"+i);
            try
            {
                go.AddComponent<NetworkObject>();go.AddComponent<NetworkTransform>();
                var rb=go.AddComponent<Rigidbody>();rb.mass=weights[i];rb.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
                var visual=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/Mining/ore_{i}.prefab"),go.transform);
                var bounds=visual.GetComponent<MeshFilter>().sharedMesh.bounds;
                Object.DestroyImmediate(visual.GetComponent<Collider>());
                Collider collider;
                if(i==7){var sphere=go.AddComponent<SphereCollider>();sphere.radius=.78f;collider=sphere;}
                else{var box=go.AddComponent<BoxCollider>();box.center=bounds.center;box.size=bounds.size;collider=box;}
                var carry=go.AddComponent<CarryableObject>();carry.rb=rb;carry.objectCollider=collider;carry.weightLevel=weights[i];
                go.AddComponent<NetworkRigidbody>();
                var ore=go.AddComponent<MiningOre>();ore.displayName=names[i];ore.price=prices[i];ore.mystery=i==10;ore.rare=i>=5&&i<=7;
                if(ore.rare){var glow=new GameObject("Mineral glow");glow.transform.SetParent(go.transform,false);var light=glow.AddComponent<Light>();light.type=LightType.Point;light.color=i==6?new Color(.4f,1,1):new Color(1,.7f,.2f);light.range=2;light.intensity=.6f;}
                var prefab=PrefabUtility.SaveAsPrefabAsset(go,$"{folder}/MiningOre_{i}.prefab");
                result[i]=prefab.GetComponent<MiningOre>();
                if(!list.PrefabList.Any(p=>p.Prefab==prefab))list.Add(new NetworkPrefab{Prefab=prefab});
            }
            finally{Object.DestroyImmediate(go);}
        }
        EditorUtility.SetDirty(list);AssetDatabase.SaveAssets();return result;
    }
    static void InstallPlayer()
    {
        const string path="Assets/_Prefabs/Player.prefab";
        var player=PrefabUtility.LoadPrefabContents(path);
        try {if(player.GetComponent<MiningPlayer>()==null)player.AddComponent<MiningPlayer>();PrefabUtility.SaveAsPrefabAsset(player,path);}
        finally{PrefabUtility.UnloadPrefabContents(player);}
    }
    static void BuildSurface(Transform parent,MiningSite site,Transform destination)
    {
        var entrance=Group(parent,"Mine entrance beside house",V(29,0,14));
        Box(entrance,"Concrete apron",V(0,.1f,0),V(7,.2f,6),"cream",true);
        foreach(int s in new[]{-1,1})Box(entrance,"Lift tower",V(s*1.65f,2,0),V(.35f,4,.5f),"teal",true);
        Box(entrance,"Lift cap",V(0,4,0),V(4.2f,.5f,2.4f),"teal",true);
        Box(entrance,"Lift back",V(0,1.7f,1.1f),V(3.5f,3.2f,.2f),"ink",true);
        for(int i=0;i<7;i++)Box(entrance,"Cage bar",V(-1.5f+i*.5f,1.7f,1),V(.055f,3.1f,.07f),"silver");
        Sign(entrance,"ONE MORE ROCK",V(0,4.05f,-1.25f),3.8f,.48f);
        Sign(entrance,"MINING CO.  /  OPEN UNTIL SUNSET",V(0,3.54f,-.55f),3.6f,.26f);
        var lift=Box(entrance,"Lift call button",V(-1.2f,1.1f,-1.1f),V(.5f,.7f,.3f),"mustard",true);
        Target(lift,site,MiningAction.Travel,1,destination,"地下へ");
        Sign(entrance,"E  /  DOWN",V(-1.2f,1.65f,-1.29f),1.2f,.3f);
        var cashier=Prop(entrance,"Mining/mine_cashier",V(3.6f,.1f,-.4f));
        Target(cashier,site,MiningAction.Sell,0,null,"換金");
        Sign(entrance,"ORE BUYBACK",V(3.6f,2,-.83f),2.5f,.35f);
        Sign(entrance,"HOLD ORE + E",V(3.6f,.5f,-.84f),1.6f,.22f);
        Prop(entrance,"Props/parcel_stack",V(-2.5f,.2f,1.5f));Prop(entrance,"Props/cafe_sign",V(-2.7f,.2f,-1.6f));
        Sign(entrance,"ONE MORE?",V(-2.7f,.9f,-1.92f),1,.26f);
        LightAt(entrance,V(0,3,-.5f),1.6f,8,false);
    }
    static void BuildFloor(Transform parent,MiningSite site,int tier,Transform up,Transform down)
    {
        string tint=tier==1?"wood":tier==2?"blue":"pink";
        Box(parent,"Solid floor",V(0,-.2f,10),V(23,.4f,30),"wood",true);
        Box(parent,"Stone ceiling",V(0,4.4f,10),V(23,.7f,30),tint,true);
        foreach(int side in new[]{-1,1})
        {
            Box(parent,"Outer rock wall",V(side*11.5f,2,10),V(.6f,4.8f,30),tint,true);
            for(int z=-3;z<25;z+=3){var rock=Prop(parent,"Mining/cave_boulder",V(side*11,1.8f,z),false);rock.localScale=V(1.2f,1.6f,1.7f);}
        }
        foreach(float z in new[]{-5f,25f})Box(parent,"End wall",V(0,2,z),V(23,4.8f,.6f),tint,true);
        for(int z=0;z<25;z+=5){Prop(parent,"Mining/pit_support",V(0,0,z),false);LightAt(parent,V(0,3.15f,z),2.8f,10,true);}
        // Side alcoves connect to the central tunnel through generous 3m gaps.
        foreach(int side in new[]{-1,1})for(int z=2;z<=20;z+=6)
        {
            Box(parent,"Alcove divider",V(side*7,1.55f,z),V(8,3.1f,.35f),tint,true);
            var b=Prop(parent,"Mining/cave_boulder",V(side*10.5f,2.1f,z),false);b.localScale=V(1.1f,1.6f,.8f);
        }
        Box(parent,"Central walk strip",V(0,.009f,10),V(3.6f,.015f,26),"cream");
        foreach(float x in new[]{-.65f,.65f})Box(parent,"Rail",V(x,.06f,10),V(.07f,.12f,24),"silver");
        for(int z=-2;z<23;z++)Box(parent,"Sleeper",V(0,.025f,z),V(1.8f,.05f,.17f),"wood");
        var cart=Prop(parent,"Mining/pit_cart",V(7.5f,0,23));cart.localEulerAngles=V(0,90,0);
        foreach(float z in new[]{22.35f,23.65f})Box(parent,"Siding rail",V(5,.06f,z),V(9,.1f,.07f),"silver");
        Sign(parent,$"B{tier}  /  "+new[]{"COPPER COMMONS","SILVER SHIFT","GOLD FEVER"}[tier-1],V(0,3.55f,1),4.7f,.42f);
        var upButton=Box(parent,"Return lift button",V(-2,1.1f,-2),V(.5f,.8f,.4f),"screen",true);
        Target(upButton,site,MiningAction.Travel,tier-1,up,"帰還");
        Sign(parent,tier==1?"E / SURFACE":"E / UP ONE FLOOR",V(-1.4f,2,-2.25f),2.4f,.4f);
        Box(parent,"Lift pad",V(0,.04f,-2),V(3.2f,.08f,3.2f),"teal");
        var rack=Box(parent,"Tool rack",V(2,1.2f,-2),V(.55f,1.5f,.35f),"teal",true);
        Target(rack,site,MiningAction.Pickaxe,tier,null,"ツルハシ");
        var tool=Prop(parent,"Mining/pickaxe",V(2,1.5f,-2.25f),false);
        Sign(parent,"E / FREE PICKAXE",V(2.1f,2.5f,-2.3f),2.4f,.32f);
        if(down!=null)
        {
            var control=Box(parent,"Deep lift button",V(0,1.2f,23),V(.7f,1.4f,.5f),"coral",true);
            Target(control,site,MiningAction.Travel,tier+1,down,"深層へ");
            Sign(parent,$"E / B{tier+1}  -  BETTER ORE",V(0,2.6f,22.6f),3.8f,.5f);
            Sign(parent,"LONGER WAY HOME",V(0,2.05f,22.6f),3.4f,.3f);
        }
        else Sign(parent,"JUST ONE MORE...",V(0,2.5f,24.5f),4.6f,.65f);
        for(int side=-1;side<=1;side+=2)for(int row=0;row<3;row++)
        {
            var rock=Prop(parent,"Mining/vein_"+tier,V(side*8.2f,0,5+row*6));
            rock.localEulerAngles=V(0,side==1?90:-90,0);
            var target=Target(rock,site,MiningAction.Rock,tier,null,new[]{"やわらかい鉱脈","鉄と銀の鉱脈","金色の硬い鉱脈"}[tier-1]);
            target.maxHits=tier==1?4:tier==2?5:7;
            target.rockVisual=rock;target.dropPoint=Group(parent,"Public ore drop",V(side*6.55f,.5f,5+row*6));
            LightAt(parent,V(side*6,3,5+row*6),1.4f,7,true);
        }
        Prop(parent,"Props/bench",V(-7,0,23));Prop(parent,"Props/drink_can",V(-7,.52f,23));
        Prop(parent,"Props/parcel_stack",V(8,0,-2));Prop(parent,"Props/street_bin",V(10,0,-2));
        for(int i=0;i<3;i++)Box(parent,"Worker locker",V(-7-i*1.1f,1,-3.7f),V(.85f,2,.6f),i%2==0?"teal":"mustard",true);
        Sign(parent,"TAKE BREAKS. PAY RENT.",V(-7.7f,2.55f,-3.7f),3.8f,.36f);
        for(int z=-2;z<24;z+=4)Box(parent,"Utility cable",V(2.4f,3.55f,z),V(.05f,.05f,4.1f),"ink");
        if(tier>1){Sign(parent,"CAUTION / FALLING ROCKS",V(0,3,12),4,.4f);Box(parent,"Gas pipe",V(10.4f,.6f,13),V(.18f,.18f,12),"green");}
    }
    static MiningInteractable Target(Transform transform,MiningSite site,MiningAction action,int level,Transform destination,string label)
    {
        var t=transform.gameObject.AddComponent<MiningInteractable>();t.site=site;t.index=Targets.Count;t.action=action;t.level=level;t.destination=destination;t.displayName=label;t.targetCollider=transform.GetComponent<Collider>();Targets.Add(t);return t;
    }
    static Transform Group(Transform parent,string name,Vector3 p)
    {var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=p;return go.transform;}
    static Transform Box(Transform parent,string name,Vector3 p,Vector3 size,string color,bool solid=false)
    {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=Mat(color);if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;}
    static Transform Prop(Transform parent,string path,Vector3 p,bool solid=true)
    {var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>($"{Root}/{path}.prefab"),parent);go.transform.localPosition=p;if(!solid)foreach(var c in go.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);return go.transform;}
    static void Sign(Transform parent,string text,Vector3 p,float width,float height)
    {var tr=Group(parent,"Sign - "+text,p);var tmp=tr.gameObject.AddComponent<TextMeshPro>();tmp.text=text;tmp.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");tmp.fontSize=4;tmp.enableAutoSizing=true;tmp.fontSizeMin=1;tmp.fontSizeMax=6;tmp.alignment=TextAlignmentOptions.Center;tmp.color=new Color(1,.91f,.68f);tmp.rectTransform.sizeDelta=new Vector2(width,height);}
    static void LightAt(Transform parent,Vector3 p,float intensity,float range,bool underground)
    {var t=Group(parent,"Warm work light",p);var l=t.gameObject.AddComponent<Light>();l.type=LightType.Point;l.intensity=intensity;l.range=range;l.color=new Color(1,.86f,.67f);l.shadows=LightShadows.None;if(underground)Lights.Add(l);}
    static AudioClip Tone(string name,float hz,float duration,bool noise)
    {
        string folder="Assets/_Audio/Mining";Directory.CreateDirectory(folder);string path=$"{folder}/{name}.wav";
        const int rate=22050;int samples=(int)(rate*duration);var random=new System.Random(31);
        using(var writer=new BinaryWriter(File.Open(path,FileMode.Create))){writer.Write(Encoding.ASCII.GetBytes("RIFF"));writer.Write(36+samples*2);writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));writer.Write(16);writer.Write((short)1);writer.Write((short)1);writer.Write(rate);writer.Write(rate*2);writer.Write((short)2);writer.Write((short)16);writer.Write(Encoding.ASCII.GetBytes("data"));writer.Write(samples*2);for(int i=0;i<samples;i++){float t=(float)i/rate;float envelope=Mathf.Pow(1-(float)i/samples,2);float signal=noise?(float)(random.NextDouble()*2-1)*.6f+Mathf.Sin(t*hz*6.283f)*.4f:Mathf.Sin(t*hz*(t>duration*.5f?1.5f:1)*6.283f);writer.Write((short)(signal*envelope*14000));}}
        AssetDatabase.ImportAsset(path);return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
    }
    [MenuItem("Roomies/Mining/Validate Mining Job")]
    static void ValidateMenu()
    {
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try{Validate(scene);Capture(scene);}finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    [MenuItem("Roomies/Mining/Polish Mining Presentation")]
    static void Polish()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before authoring.");
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(!opened&&scene.isDirty)throw new InvalidOperationException("Save GameRoom first.");
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        try
        {
            var root=scene.GetRootGameObjects().Single(g=>g.name=="Roomies_Mining").transform;
            foreach(var collider in root.GetComponentsInChildren<Collider>(true))
                if(collider.GetComponentInParent<MiningInteractable>()==null)collider.gameObject.layer=LayerMask.NameToLayer("Wall");
            string path=Root+"/Materials/mining_particles.mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",0);
            mat.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;EditorUtility.SetDirty(mat);
            foreach(var text in root.GetComponentsInChildren<TextMeshPro>())
                if(text.transform.Find("Painted sign backing")==null)
                    Box(text.transform,"Painted sign backing",V(0,0,.08f),V(text.rectTransform.sizeDelta.x+.12f,text.rectTransform.sizeDelta.y+.09f,.08f),"teal");
            for(int tier=1;tier<=3;tier++)
            {
                var floor=root.Cast<Transform>().Single(t=>t.name.StartsWith("Level "+tier+" -"));
                if(floor.Find("Finishing details")!=null)continue;
                var detail=Group(floor,"Finishing details",Vector3.zero);
                string tint=tier==1?"wood":tier==2?"blue":"pink";
                foreach(int side in new[]{-1,1})for(int row=0;row<4;row++)
                {
                    var outcrop=Prop(detail,"Mining/cave_boulder",V(side*4.2f,1.5f,2+row*6),false);
                    outcrop.localScale=V(1,1.05f,.7f);outcrop.GetComponent<Renderer>().sharedMaterial=Mat(tint);
                    if(row<3)
                    {
                        Sign(detail,(side<0?"A":"B")+(row+1)+" / ORE",V(side*4.2f,2.65f,2+row*6-.77f),1.5f,.3f);
                        Box(detail,"Painted route",V(side*4.8f,.017f,5+row*6),V(3.1f,.022f,.1f),"mustard");
                    }
                }
                foreach(float z in new[]{-3.8f,24.2f})
                {
                    foreach(int side in new[]{-1,1})Box(detail,"Lift steel jamb",V(side*1.6f,1.5f,z),V(.16f,3,.25f),"teal");
                    Box(detail,"Lift lintel",V(0,3,z),V(3.5f,.23f,.3f),"teal");
                    for(int i=0;i<9;i++)Box(detail,"Lift cage",V(-1.4f+i*.35f,1.5f,z+.15f),V(.04f,3,.04f),"silver");
                }
                for(int i=0;i<5;i++)
                {
                    var patch=Prop(detail,"Mining/cave_boulder",V(-1.7f+i*.85f,.01f,5+i*3.1f),false);
                    patch.localScale=V(.35f,.008f,.5f);patch.GetComponent<Renderer>().sharedMaterial=Mat("wood");
                }
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Validate(scene);Capture(scene);
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
    static void Validate(Scene scene)
    {
        var site=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<MiningSite>()).Single();
        var report=new StringBuilder();int failures=0;
        void Check(bool valid,string text){report.AppendLine((valid?"PASS ":"FAIL ")+text);if(!valid)failures++;}
        var list=AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        Check(site.ores.Length==11,"11 ore/junk types");
        foreach(var ore in site.ores){Check(ore!=null&&list.PrefabList.Any(p=>p.Prefab==ore.gameObject),"Registered "+ore.name);Check(ore.GetComponentsInChildren<NetworkObject>(true).Length==1&&ore.GetComponent<NetworkTransform>()!=null&&ore.GetComponent<NetworkRigidbody>()!=null,"Authority/physics "+ore.name);Check(ore.tag!="DeliveryBox"&&ore.GetComponent<DeliveryItem>()==null,"Not a delivery parcel "+ore.name);}
        Check(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab").GetComponent<MiningPlayer>()!=null,"Existing player extended");
        Physics.SyncTransforms();
        foreach(var target in site.targets)
        {
            Check(target.site==site&&target.index>=0&&site.targets[target.index]==target&&target.targetCollider!=null,"Target "+target.index);
            if(target.action==MiningAction.Travel){Check(target.destination!=null,"Travel destination "+target.index);var p=target.destination.position;Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p-Vector3.up*.5f,.48f,~0,QueryTriggerInteraction.Ignore),"Clear arrival "+target.index);}
            if(target.action==MiningAction.Rock)
            {
                Check(target.dropPoint!=null&&target.maxHits>=3&&target.maxHits<=8,"Rock HP/drop "+target.index);
                Check(MiningSite.CanReach(target.dropPoint.position+Vector3.up*.65f,target),"Rock reachable at pickup side "+target.index);
            }
            Check(!MiningSite.CanReach(new Vector3(float.NaN,0,0),target),"Reject NaN "+target.index);
            Check(!MiningSite.CanReach(target.transform.position+Vector3.up*20,target),"Reject remote use "+target.index);
        }
        Check(site.targets.Count(t=>t.action==MiningAction.Rock)==18,"18 authored rocks over 3 levels");
        Check(site.eventCenters.Length==3&&site.mineLights.Length>0,"Events and warning lights wired");
        Check(site.GetComponentsInChildren<Collider>().Where(c=>c.GetComponentInParent<MiningInteractable>()==null).All(c=>c.gameObject.layer==LayerMask.NameToLayer("Wall")),"Cave geometry participates in existing carry wall mask");
        Check(site.hitSound!=null&&site.breakSound!=null&&site.saleSound!=null&&site.warningSound!=null,"4 sound assets");
        report.AppendLine("Failures="+failures);report.AppendLine("Editor asset/physics checks only; not a Host/Client playthrough.");
        Directory.CreateDirectory("Temp/RoomiesArt");File.WriteAllText("Temp/RoomiesArt/mining-validation.txt",report.ToString());
        Debug.Log("[Mining] Validation failures="+failures);
        if(failures>0)throw new InvalidOperationException(report.ToString());
    }
    static void Capture(Scene scene)
    {
        RoomiesArtAudit.Render(scene,V(25,4.5f,6),V(29,1.7f,14),"mining-entrance");
        RoomiesArtAudit.Render(scene,V(0,-10.1f,62),V(0,-10.1f,77),"mining-level1");
        RoomiesArtAudit.Render(scene,V(3.5f,-29.6f,68),V(8,-30.5f,71),"mining-deep");
    }
}
#endif
