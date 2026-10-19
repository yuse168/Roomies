using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomiesLobbyWorldBuilder
{
    const string Folder="Assets/RoomiesAuthoring/Lobby";
    [MenuItem("Roomies/Art/Refresh lobby world and plain Bob")]
    static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new InvalidOperationException("Save scene changes first.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        try
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Prefabs","Assets/Resources/RoomiesArt"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);var prefab=PrefabUtility.LoadPrefabContents(path);
                if(PlainBob(prefab)>0)PrefabUtility.SaveAsPrefabAsset(prefab,path);
                PrefabUtility.UnloadPrefabContents(prefab);
            }
            var game=EditorSceneManager.OpenScene("Assets/_Scenes/GameRoom.unity",OpenSceneMode.Single);
            foreach(var root in game.GetRootGameObjects())PlainBob(root);
            EditorSceneManager.MarkSceneDirty(game);EditorSceneManager.SaveScene(game);
            var world=new GameObject("MenuWorldBackdrop");
            var roots=game.GetRootGameObjects().Where(r=>new[]{"Demo","Roomies_EnvironmentArt","Roomies_Completion","Roomies_Mining","DoorPivot"}.Contains(r.name)).ToArray();
            int count=0;
            foreach(var root in roots)
            {
                var container=new GameObject(root.name);container.transform.SetParent(world.transform,false);
                foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
                {
                    var filter=renderer.GetComponent<MeshFilter>();
                    if(!renderer.enabled||renderer.bounds.max.y< -3||filter==null||filter.sharedMesh==null||renderer.GetComponentInParent<Canvas>()!=null||renderer.GetComponent<TMP_Text>()!=null)continue;
                    var visual=new GameObject(renderer.name,typeof(MeshFilter),typeof(MeshRenderer));
                    visual.transform.SetParent(container.transform,false);visual.transform.SetPositionAndRotation(renderer.transform.position,renderer.transform.rotation);visual.transform.localScale=renderer.transform.lossyScale;
                    visual.GetComponent<MeshFilter>().sharedMesh=filter.sharedMesh;
                    var copy=visual.GetComponent<MeshRenderer>();copy.sharedMaterials=renderer.sharedMaterials;copy.shadowCastingMode=renderer.shadowCastingMode;copy.receiveShadows=renderer.receiveShadows;
                    count++;
                }
                foreach(var label in root.GetComponentsInChildren<TextMeshPro>())
                {
                    if(label.transform.position.y< -3)continue;
                    var go=new GameObject(label.name,typeof(TextMeshPro));go.transform.SetParent(container.transform,false);
                    go.transform.SetPositionAndRotation(label.transform.position,label.transform.rotation);go.transform.localScale=label.transform.lossyScale;
                    var copy=go.GetComponent<TextMeshPro>();copy.font=label.font;copy.fontSharedMaterial=label.fontSharedMaterial;copy.text=label.text;copy.fontSize=label.fontSize;copy.color=label.color;copy.alignment=label.alignment;copy.fontStyle=label.fontStyle;
                    copy.rectTransform.sizeDelta=label.rectTransform.rect.size;copy.textWrappingMode=label.textWrappingMode;
                }
            }
            PrefabUtility.SaveAsPrefabAsset(world,Folder+"/MenuWorldBackdrop.prefab");UnityEngine.Object.DestroyImmediate(world);
            var menu=EditorSceneManager.OpenScene("Assets/_Scenes/MainMenuSteam.unity",OpenSceneMode.Single);
            var host=menu.GetRootGameObjects().Single(g=>g.name=="MenuRoomShowcase");
            var legacy=host.transform.Find("MenuRoom");if(legacy!=null)legacy.gameObject.SetActive(false);
            var old=host.transform.Find("MenuWorldBackdrop");if(old!=null)UnityEngine.Object.DestroyImmediate(old.gameObject);
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/MenuWorldBackdrop.prefab"),menu);
            instance.transform.SetParent(host.transform,false);
            var characters=host.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("MenuRoomie_")).OrderBy(t=>t.name).ToArray();
            for(int i=0;i<characters.Length;i++)
            {
                var character=characters[i];character.position=new Vector3(17+i*1.9f,1,5.5f+(i==1?.6f:0));character.rotation=Quaternion.Euler(0,145,0);
                var body=character.GetComponentsInChildren<Renderer>().First(r=>r.name.Contains("BobBody"));
                character.position+=Vector3.up*(.06f-body.bounds.min.y);
                if(PrefabUtility.IsPartOfPrefabInstance(character))PrefabUtility.RecordPrefabInstancePropertyModifications(character);
            }
            PlainBob(host);
            Color[] colors={new(.98f,.31f,.58f),new(.20f,.70f,.96f),new(.35f,.82f,.42f)};
            for(int i=0;i<characters.Length;i++)
                foreach(var body in characters[i].GetComponentsInChildren<Renderer>().Where(r=>r.name.Contains("BobBody")))
                {
                    string path=Folder+$"/Bob_menu_{i+1}.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(mat==null){mat=new Material(body.sharedMaterial);AssetDatabase.CreateAsset(mat,path);}
                    mat.SetColor("_BaseColor",colors[i%colors.Length]);EditorUtility.SetDirty(mat);body.sharedMaterial=mat;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                }
            var showcase=host.GetComponent<MainMenuRoomShowcase>();
            var sky=AssetDatabase.LoadAssetAtPath<Material>("Assets/AllSkyFree/Epic_GloriousPink/Epic_GloriousPink.mat");
            showcase.Configure(instance.transform,characters,sky);
            Vector3 position=new(28,6,-6),target=new(15.5f,2.8f,7);
            showcase.ConfigureWorldView(position,target,48);EditorUtility.SetDirty(showcase);
            var camera=menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()).First();camera.transform.position=position;camera.transform.LookAt(target);camera.fieldOfView=48;
            RenderSettings.skybox=sky;RenderSettings.ambientIntensity=.9f;
            EditorSceneManager.MarkSceneDirty(menu);EditorSceneManager.SaveScene(menu);AssetDatabase.SaveAssets();
            if(instance.GetComponentsInChildren<MonoBehaviour>(true).Any(b=>b is not TMP_Text)||instance.GetComponentsInChildren<Collider>(true).Length!=0)throw new Exception("Gameplay components in backdrop");
            Directory.CreateDirectory("ArtReview/2026-09-09");
            File.WriteAllText("ArtReview/2026-09-09/lobby-world.txt",$"{DateTime.UtcNow:o}\nWorld meshes: {count}\nBackground gameplay behaviours and colliders: 0\nBob figures: {characters.Length}\n");
            RoomiesArtAudit.Render(menu,position,target,"lobby-world",null,1600,900);
            var ui=menu.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).First();
            RoomiesArtAudit.Render(menu,position,target,"lobby-world-ui",ui,1600,900);
            var mainPanel=UITheme.FindDeep("MainMenuPanel");var lobbyPanel=UITheme.FindDeep("LobbyPanel");
            if(mainPanel!=null&&lobbyPanel!=null)
            {mainPanel.SetActive(false);lobbyPanel.SetActive(true);RoomiesArtAudit.Render(menu,position,target,"lobby-world-waiting",ui,1600,900);}
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    [MenuItem("Roomies/Art/Validate plain Bob and lobby")]
    static void Validate()
    {
        var setup=EditorSceneManager.GetSceneManagerSetup();int bodies=0,eyes=0;
        try
        {
            foreach(string name in new[]{"GameRoom","MainMenuSteam"})
            {
                var scene=EditorSceneManager.OpenScene("Assets/_Scenes/"+name+".unity",OpenSceneMode.Single);
                foreach(var renderer in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Where(r=>r.name.Contains("BobBody")))
                {
                    foreach(var material in renderer.sharedMaterials)
                    {
                        if(material==null)throw new Exception("Bob material missing");
                        foreach(string property in new[]{"_BaseMap","_BumpMap"})
                            if(material.HasProperty(property)&&material.GetTexture(property)!=null)throw new Exception("Bob texture remains: "+material.name);
                    }
                    bodies++;
                }
                foreach(var eye in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Where(r=>r.name.StartsWith("Bobeye",StringComparison.OrdinalIgnoreCase)))
                {
                    if(eye.sharedMaterials.Any(m=>m==null||AssetDatabase.GetAssetPath(m)!="Assets/_Materials/BobEye.mat"||m.GetColor("_BaseColor")!=Color.black||m.GetTexture("_BaseMap")!=null))throw new Exception("Bob eye is not black: "+eye.name);
                    eyes++;
                }
                if(name=="MainMenuSteam")
                {
                    var host=scene.GetRootGameObjects().Single(g=>g.name=="MenuRoomShowcase");
                    if(host.transform.Find("MenuRoom").gameObject.activeSelf)throw new Exception("Legacy background still active");
                    var world=host.transform.Find("MenuWorldBackdrop");
                    if(world==null||world.GetComponentsInChildren<MeshRenderer>().Length<100)throw new Exception("World backdrop missing");
                    var so=new SerializedObject(host.GetComponent<MainMenuRoomShowcase>());
                    if(!so.FindProperty("useWorldView").boolValue)throw new Exception("Runtime camera override missing");
                }
            }
            File.AppendAllText("ArtReview/2026-09-09/lobby-world.txt",$"Reload validation PASS: {bodies} Bob bodies have no line/normal textures; world backdrop and runtime camera saved.\n");
            File.AppendAllText("ArtReview/2026-09-09/lobby-world.txt",$"Black eyes PASS: {eyes} eye renderers use the plain black BobEye material.\n");
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static int PlainBob(GameObject root)
    {
        int count=0;
        foreach(var eye in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.StartsWith("Bobeye",StringComparison.OrdinalIgnoreCase)))
        {
            var black=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Materials/BobEye.mat");
            foreach(string property in new[]{"_BaseMap","_MainTex","_BumpMap","_DetailAlbedoMap","_DetailNormalMap","_EmissionMap"})if(black.HasProperty(property))black.SetTexture(property,null);
            foreach(string property in new[]{"_BaseColor","_Color","_EmissionColor"})if(black.HasProperty(property))black.SetColor(property,Color.black);
            black.SetFloat("_Smoothness",0);black.SetFloat("_SpecularHighlights",0);black.SetFloat("_EnvironmentReflections",0);
            black.DisableKeyword("_NORMALMAP");black.DisableKeyword("_EMISSION");black.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");black.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            EditorUtility.SetDirty(black);
            eye.sharedMaterials=eye.sharedMaterials.Select(_=>black).ToArray();EditorUtility.SetDirty(eye);
            if(PrefabUtility.IsPartOfPrefabInstance(eye))PrefabUtility.RecordPrefabInstancePropertyModifications(eye);
            count++;
        }
        foreach(var renderer in root.GetComponentsInChildren<Renderer>(true).Where(r=>r.name.Contains("BobBody")))
        {
            var materials=renderer.sharedMaterials;
            for(int i=0;i<materials.Length;i++)
            {
                var original=materials[i];if(original==null)continue;
                string originalPath=AssetDatabase.GetAssetPath(original);
                string path=originalPath.StartsWith(Folder+"/Bob_")?originalPath:Folder+"/Bob_"+Hash128.Compute(originalPath+original.name)+".mat";
                var plain=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(plain==null){plain=new Material(original);plain.name="Bob plain "+original.name;AssetDatabase.CreateAsset(plain,path);}
                foreach(string property in new[]{"_BaseMap","_MainTex","_BumpMap","_DetailAlbedoMap","_DetailNormalMap"})if(plain.HasProperty(property))plain.SetTexture(property,null);
                plain.DisableKeyword("_NORMALMAP");plain.DisableKeyword("_DETAIL_MULX2");EditorUtility.SetDirty(plain);materials[i]=plain;
            }
            renderer.sharedMaterials=materials;EditorUtility.SetDirty(renderer);
            if(PrefabUtility.IsPartOfPrefabInstance(renderer))PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            count++;
        }
        return count;
    }
    [MenuItem("Roomies/Art/Inspect lobby world")]
    static void Inspect()
    {
        var setup=EditorSceneManager.GetSceneManagerSetup();
        var report=new System.Text.StringBuilder();
        foreach(string name in new[]{"GameRoom","MainMenuSteam"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Scenes/"+name+".unity",OpenSceneMode.Single);
            foreach(var root in scene.GetRootGameObjects())report.AppendLine(name+" / "+root.name);
            foreach(var r in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).Where(r=>r.name.Contains("Bob")||r.name.IndexOf("eye",StringComparison.OrdinalIgnoreCase)>=0))
                report.AppendLine($"RENDERER {r.name} / {r.transform.parent.name}: {r.bounds}, materials {string.Join(",",r.sharedMaterials.Select(m=>AssetDatabase.GetAssetPath(m)))}");
        }
        Directory.CreateDirectory("Temp/RoomiesArt");File.WriteAllText("Temp/RoomiesArt/lobby-inspect.txt",report.ToString());
        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }
}
