#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

internal static class RoomiesPlayabilityPolish
{
    const string Art = "Assets/Resources/RoomiesArt";
    const string ScenePath = "Assets/_Scenes/GameRoom.unity";
    static Vector3 V(float x,float y,float z)=>new Vector3(x,y,z);
    static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>($"{Art}/Materials/{name}.mat");

    [MenuItem("Roomies/Polish/Repair Slot and Build Jail")]
    static void Apply()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Exit Play Mode before authoring.");
        PrepareSlotPrefab();
        var scene=SceneManager.GetSceneByPath(ScenePath);
        bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        var previous=SceneManager.GetActiveScene();
        try
        {
            if(scene.isDirty)throw new InvalidOperationException("Save GameRoom edits first.");
            SceneManager.SetActiveScene(scene);
            var roots=scene.GetRootGameObjects();
            var environment=roots.First(g=>g.name=="Roomies_EnvironmentArt").transform;
            if(environment.Find("Community Jail")==null)
            {
                BuildJail(environment);
                var zone=roots.First(g=>g.GetComponent<DeliveryZone>()!=null);
                Vector3 delta=V(-3.8f,.1f,18.7f)-zone.transform.position;
                Undo.RecordObject(zone.transform,"Separate delivery bay from jail");zone.transform.position+=delta;
                var button=roots.First(g=>g.GetComponent<DeliveryButton>()!=null);
                Undo.RecordObject(button.transform,"Move delivery button");button.transform.position=V(-7.1f,.88f,14.72f);
                var workplace=environment.Find("Workplaces - facades and loading details");
                foreach(Transform t in workplace)
                    if(t.name=="Delivery bay edge"||t.name=="Bay sign"||t.name=="Bay signpost"||t.name=="Sign - DELIVERY BAY")t.position+=delta;
                var gardens=environment.Find("Pocket gardens and distant silhouettes");
                foreach(Transform t in gardens)
                    if(t.position.x> -7 && t.position.x< -3 && t.position.z>19 && t.position.z<23)t.position+=V(-8,0,0);
                var kiosk=NewGroup(environment,"Delivery counter");
                Box(kiosk,"Button pedestal",V(-7.1f,.4f,14.72f),V(.35f,.8f,.35f),"teal",true);
                Box(kiosk,"Delivery instruction plate",V(-5.65f,1.25f,14.8f),V(2.1f,.65f,.1f),"teal");
                Sign(kiosk,"BOXES HERE  /  E TO DELIVER",V(-5.65f,1.25f,14.735f),1.9f,.45f);
                // Retire only the original blank sign meshes; the delivery logic remains on its existing objects.
                var demo=roots.First(g=>g.name=="Demo");
                foreach(var t in demo.GetComponentsInChildren<Transform>())if(t.name=="pCube10"||t.name=="pCylinder2")t.gameObject.SetActive(false);
                var jail=roots.First(g=>g.GetComponent<SmugglingJailPoint>()!=null);
                Undo.RecordObject(jail.transform,"Place jail arrival inside cell");jail.transform.SetPositionAndRotation(V(8.4f,1.22f,18.1f),Quaternion.identity);
                var labor=roots.First(g=>g.GetComponent<SmugglingJailLabor>()!=null);
                Undo.RecordObject(labor.transform,"Move labor station inside jail");labor.transform.position=V(8.4f,.16f,20.7f);
                foreach(var r in labor.GetComponentsInChildren<Renderer>())r.enabled=false;
                var table=NewGroup(labor.transform,"Workstation Art");
                Box(table,"Workbench top",V(0,1,0),V(1.55f,.13f,.77f),"wood");
                foreach(float x in new[]{-.62f,.62f})foreach(float z in new[]{-.27f,.27f})Box(table,"Steel leg",V(x,.46f,z),V(.09f,.92f,.09f),"teal");
                Box(table,"Work mat",V(0,1.071f,0),V(1.25f,.009f,.55f),"teal");
                for(int i=0;i<3;i++)Box(table,"Sorting block",V(-.4f+i*.3f,1.15f,.06f),V(.2f,.15f,.23f),i==1?"mustard":"cream");
                Box(table,"Hammer handle",V(.45f,1.09f,-.17f),V(.055f,.05f,.32f),"wood");
                Box(table,"Hammer head",V(.45f,1.12f,-.29f),V(.19f,.08f,.08f),"silver");
            }
            PrefabUtility.SaveAsPrefabAssetAndConnect(environment.gameObject,"Assets/_Prefabs/Environment/RoomiesNeighborhood.prefab",InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Validate(scene);
            RoomiesArtAudit.Render(scene,V(-14,12,7),V(6.2f,1.5f,19),"jail-delivery-layout");
            RoomiesArtAudit.Render(scene,V(8.4f,2.3f,16.25f),V(8.4f,1.5f,21),"jail-interior");
            var slot=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<SlotMachine>()).First();
            var box=(BoxCollider)new SerializedObject(slot).FindProperty("interactionCollider").objectReferenceValue;var normal=box.transform.TransformDirection(Vector3.right);
            RoomiesArtAudit.Render(scene,box.bounds.center+normal*2.1f+V(0,.05f,0),box.bounds.center+V(0,.1f,0),"slot-ui");
            Debug.Log("[Roomies Polish] Slot repaired; jail and delivery separated. Validation written to Temp/RoomiesArt/polish-validation.txt");
        }
        finally{SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }

    static void PrepareSlotPrefab()
    {
        const string spritePath="Assets/_Art/UI/RoomiesPanel.png";
        if(!File.Exists(spritePath))
        {
            File.WriteAllBytes(spritePath,UITheme.RoundedSprite.texture.EncodeToPNG());AssetDatabase.ImportAsset(spritePath);
        }
        var importer=(TextureImporter)AssetImporter.GetAtPath(spritePath);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;
        importer.spriteBorder=new Vector4(17,17,17,17);importer.mipmapEnabled=false;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
        var prefab=PrefabUtility.LoadPrefabContents("Assets/_Prefabs/Slot.prefab");
        try
        {
            var slot=prefab.GetComponentInChildren<SlotMachine>();
            var model=slot.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Model");
            var collider=model.GetComponent<Collider>();
            if(collider==null)throw new InvalidOperationException("Slot cabinet collider missing.");
            if(collider is BoxCollider cabinet){cabinet.size=Vector3.one;cabinet.center=Vector3.zero;}
            Physics.SyncTransforms();
            var so=new SerializedObject(slot);so.FindProperty("interactionCollider").objectReferenceValue=collider;
            so.FindProperty("panelSprite").objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            string[] symbols={"cherry","star","clover","gem","seven"};
            var array=so.FindProperty("symbolSprites");array.arraySize=5;
            for(int i=0;i<5;i++)array.GetArrayElementAtIndex(i).objectReferenceValue=AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/_Art/UI/SlotEmoji/{symbols[i]}.png");
            so.ApplyModifiedPropertiesWithoutUndo();
            var canvas=slot.GetComponentInChildren<Canvas>();var rect=(RectTransform)canvas.transform;
            Vector3 normal=model.TransformDirection(Vector3.right).normalized;
            float depth=Vector3.Dot(collider.bounds.extents,new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)));
            rect.SetParent(slot.transform,false);
            rect.position=collider.bounds.center+normal*(depth+.055f)+V(0,.13f,0);
            rect.rotation=Quaternion.LookRotation(-normal,Vector3.up);
            Vector3 scale=slot.transform.lossyScale;rect.localScale=V(.00155f/scale.x,.00155f/scale.y,.00155f/scale.z);
            rect.sizeDelta=new Vector2(520,760);canvas.renderMode=RenderMode.WorldSpace;
            slot.RefreshPresentation();
            PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/_Prefabs/Slot.prefab");
        }
        finally{PrefabUtility.UnloadPrefabContents(prefab);}
    }
    static Transform NewGroup(Transform parent,string name){var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
    static void Box(Transform parent,string name,Vector3 position,Vector3 size,string material,bool solid=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=Mat(material);
        if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;
    }
    static void Sign(Transform parent,string text,Vector3 position,float width,float height)
    {
        var go=new GameObject("Sign - "+text);go.transform.SetParent(parent,false);go.transform.localPosition=position;
        var label=go.AddComponent<TextMeshPro>();label.text=text;label.font=TMP_Settings.defaultFontAsset;
        label.alignment=TextAlignmentOptions.Center;label.fontStyle=FontStyles.Bold;label.enableAutoSizing=true;label.fontSizeMin=.1f;label.fontSizeMax=12;
        label.rectTransform.sizeDelta=new Vector2(width,height);label.color=new Color(.98f,.9f,.73f);
    }
    static void BuildJail(Transform parent)
    {
        var jail=NewGroup(parent,"Community Jail");
        Box(jail,"Cell floor",V(8.4f,.08f,18.7f),V(7.3f,.16f,7.3f),"silver",true);
        Box(jail,"Back wall",V(8.4f,1.8f,22.3f),V(7.3f,3.3f,.18f),"cream",true);
        foreach(float x in new[]{4.8f,12f})
        {
            Box(jail,"Side wall",V(x,1.8f,18.7f),V(.18f,3.3f,7.3f),"cream",true);
            Box(jail,"Wall color band",V(x+(x<8? .1f:-.1f),.72f,18.7f),V(.025f,1.1f,7.1f),"teal");
        }
        Box(jail,"Back color band",V(8.4f,.72f,22.19f),V(7.1f,1.1f,.025f),"teal");
        Box(jail,"Roof",V(8.4f,3.55f,18.7f),V(7.65f,.2f,7.65f),"teal",true);
        Box(jail,"Front lintel",V(8.4f,3.12f,15.1f),V(7.4f,.68f,.25f),"teal",true);
        Sign(jail,"COMMUNITY JAIL",V(8.4f,3.12f,14.954f),5.2f,.43f);
        for(int i=0;i<26;i++)Box(jail,"Cell bar",V(4.95f+i*.277f,1.5f,15.1f),V(.065f,2.7f,.075f),"ink",true);
        foreach(float y in new[]{.27f,1.18f,2.7f})Box(jail,"Bar rail",V(8.4f,y,15.1f),V(7.1f,.075f,.075f),"ink",true);
        foreach(float x in new[]{7.72f,9.08f})Box(jail,"Gate frame",V(x,1.5f,15.02f),V(.1f,2.75f,.13f),"teal");
        Box(jail,"Gate lock",V(8.96f,1.3f,14.92f),V(.16f,.2f,.11f),"mustard");
        Box(jail,"Service board",V(8.4f,2.55f,22.17f),V(4.9f,.75f,.06f),"teal");
        Sign(jail,"MORNING SHIFT  /  E x 10",V(8.4f,2.55f,22.12f),4.5f,.5f);
        // Sleep corner and a wall bench leave a clear path between arrival and the work station.
        var bedAsset=AssetDatabase.LoadAssetAtPath<GameObject>($"{Art}/Furniture/comfy_bed.prefab");
        var bed=(GameObject)PrefabUtility.InstantiatePrefab(bedAsset);bed.transform.SetParent(jail,false);bed.transform.localPosition=V(5.7f,.4f,20.6f);
        Box(jail,"Wall bench",V(11.4f,.53f,20.55f),V(.6f,.16f,2.2f),"wood",true);
        Box(jail,"Bench support",V(11.4f,.29f,20.55f),V(.38f,.42f,1.8f),"teal");
        Box(jail,"Ceiling diffuser",V(8.4f,3.425f,19.4f),V(1.1f,.045f,.55f),"paper");
        var light=new GameObject("Cell light");light.transform.SetParent(jail,false);light.transform.localPosition=V(8.4f,3.16f,19.4f);var l=light.AddComponent<Light>();l.type=LightType.Point;l.intensity=1.5f;l.range=7;l.shadows=LightShadows.None;l.color=new Color(1,.89f,.72f);
    }
    static void Validate(Scene scene)
    {
        Physics.SyncTransforms();var report=new StringBuilder();int failures=0;
        var roots=scene.GetRootGameObjects();
        foreach(var slot in roots.SelectMany(g=>g.GetComponentsInChildren<SlotMachine>()))
        {
            var collider=(Collider)new SerializedObject(slot).FindProperty("interactionCollider").objectReferenceValue;Vector3 center=collider.bounds.center;
            bool near=slot.IsWithinInteractionRange(center+Vector3.forward*1.6f);
            bool far=slot.IsWithinInteractionRange(center+Vector3.forward*8);
            bool invalid=slot.IsWithinInteractionRange(V(float.NaN,0,0));
            bool valid=near&&!far&&!invalid;if(!valid)failures++;
            report.AppendLine($"Slot {slot.name}: {(valid?"PASS":"FAIL")} near={near} far={far} invalid={invalid} root offset={Vector3.Distance(slot.transform.position,center):F2}m");
        }
        var jail=roots.First(g=>g.GetComponent<SmugglingJailPoint>()!=null);
        var zone=roots.First(g=>g.GetComponent<DeliveryZone>()!=null).GetComponent<Collider>();
        bool separate=!zone.bounds.Contains(jail.transform.position);if(!separate)failures++;
        report.AppendLine($"Jail outside delivery trigger: {separate}");
        Vector3 p=jail.transform.position;
        bool clear=!Physics.OverlapCapsule(p+Vector3.up*.5f,p-Vector3.up*.5f,.5f,~0,QueryTriggerInteraction.Ignore).Any(c=>c.gameObject.scene==scene);
        report.AppendLine($"Jail arrival capsule clear: {clear}");if(!clear)failures++;
        var labor=roots.First(g=>g.GetComponent<SmugglingJailLabor>()!=null);
        report.AppendLine($"Labor station distance from arrival: {Vector3.Distance(labor.transform.position,p):F2}m");
        report.AppendLine($"Failures: {failures}");Directory.CreateDirectory("Temp/RoomiesArt");File.WriteAllText("Temp/RoomiesArt/polish-validation.txt",report.ToString());
        if(failures>0)throw new InvalidOperationException(report.ToString());
    }

    [MenuItem("Roomies/Polish/Render HUD Review")]
    static void RenderHud()
    {
        var scene=SceneManager.GetSceneByPath(ScenePath);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);
        var previous=SceneManager.GetActiveScene();var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            SceneManager.SetActiveScene(scene);
            var host=new GameObject("HUD Review");SceneManager.MoveGameObjectToScene(host,preview);
            var theme=host.AddComponent<HudThemer>();
            var day=UITheme.Label(host.transform,"PreviewDay","DAY 2  朝",30,Color.white,TextAlignmentOptions.Left,true);
            var timer=UITheme.Label(host.transform,"PreviewTimer","01:54",40,Color.white,TextAlignmentOptions.Right,true);
            var source=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMP_Text>(true)).FirstOrDefault(t=>t.name=="DayText");
            if(source!=null){day.font=source.font;timer.font=source.font;}
            var canvas=theme.BuildPreview(day,timer);
            RoomiesArtAudit.Render(scene,V(18,2.1f,4),V(-1,2,-10),"hud-review",canvas,1920,1080);
        }
        finally{EditorSceneManager.ClosePreviewScene(preview);SceneManager.SetActiveScene(previous);if(opened)EditorSceneManager.CloseScene(scene,true);}
    }
}
#endif
