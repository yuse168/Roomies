using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class RoomiesTAAuthoring
{
    const string Folder="Assets/RoomiesAuthoring";
    [MenuItem("Roomies/TA/Prepare editable scene UI")]
    public static void Prepare()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before authoring.");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var setup=EditorSceneManager.GetSceneManagerSetup();
        if(SceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Save scene changes first.");
        var scene=EditorSceneManager.OpenScene("Assets/_Scenes/GameRoom.unity",OpenSceneMode.Single);
        var root=GameObject.Find("Roomies_TA_UI") ?? new GameObject("Roomies_TA_UI");
        T Add<T>() where T:Component
        {var found=UnityEngine.Object.FindAnyObjectByType<T>();if(found!=null)return found;var go=new GameObject(typeof(T).Name);go.transform.SetParent(root.transform);return go.AddComponent<T>();}
        var transition=Add<DayTransitionUI>();transition.BuildUI();
        var result=Add<DayResultUI>();result.BuildUI();result.PrepareRows();
        var rent=Add<RentPaymentUI>();rent.BuildUI();
        var night=Add<NightEventUI>();night.BuildUI();
        var furniture=Add<FurnitureEditController>();furniture.BuildUI();
        var end=Add<SessionEndUI>();end.BuildUI();
        var esc=Add<EscMenuUI>();esc.BuildUI();
        Add<HudThemer>().BuildUI();
        Add<PlayerEarningListUI>().PrepareRows();
        var closet=UnityEngine.Object.FindAnyObjectByType<CharacterCloset>();
        if(closet!=null)
        {
            closet.EnsureUI();
            EditorUtility.SetDirty(closet);
            if(PrefabUtility.IsPartOfPrefabInstance(closet))PrefabUtility.RecordPrefabInstancePropertyModifications(closet);
        }
        var events=Add<NightEventManager>();
        foreach(var table in UnityEngine.Object.FindObjectsByType<BlackjackTable>())table.BuildDisplayIfNeeded();
        foreach(var slot in UnityEngine.Object.FindObjectsByType<SlotMachine>())slot.PrepareAuthoredPresentation();
        NormalizeColliders(scene);
        Set(events,"eventUI",night);
        var day=UnityEngine.Object.FindAnyObjectByType<DayManager>();
        Set(day,"transitionUI",transition);Set(day,"resultUI",result);Set(day,"rentPaymentUI",rent);
        foreach(var group in root.GetComponentsInChildren<CanvasGroup>(true))
        {group.alpha=0;group.blocksRaycasts=false;group.interactable=false;}
        // The existing UI builders create textures/sprites in memory. Persist every referenced
        // resource so an Editor restart does not remove borders, gradients or blur materials.
        var graphics=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Graphic>(true)).ToArray();
        foreach(var image in graphics.OfType<Image>()) PersistSprite(image.sprite);
        foreach(var graphic in graphics)
            if(graphic.material!=null&&!AssetDatabase.Contains(graphic.material)&&graphic.material!=Graphic.defaultGraphicMaterial)
                AssetDatabase.CreateAsset(graphic.material,AssetDatabase.GenerateUniqueAssetPath(Folder+"/UI material.mat"));
        foreach(var component in root.GetComponentsInChildren<Component>(true))if(component!=null)EditorUtility.SetDirty(component);
        FixFonts(scene);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        BuildAudio();
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Prefabs"}))
        {
            string path=AssetDatabase.GUIDToAssetPath(guid);var prefab=PrefabUtility.LoadPrefabContents(path);bool changed=false;
            foreach(var display in prefab.GetComponentsInChildren<PlayerNameDisplay>(true))
            {if(!display.enabled){display.enabled=true;changed=true;}}
            foreach(var interact in prefab.GetComponentsInChildren<PlayerInteract>(true))
            {
                foreach(var label in interact.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="InteractText"))
                {UITheme.BuildInteractChip(label).SetActive(false);changed=true;}
            }
            foreach(var mining in prefab.GetComponentsInChildren<MiningPlayer>(true))
            {
                Set(mining,"pickaxePrefab",Resources.Load<GameObject>("RoomiesArt/Mining/pickaxe"));
                var so=new SerializedObject(mining);
                if(so.FindProperty("headlamp").objectReferenceValue==null)
                {
                    var camera=mining.GetComponent<PlayerInteract>()?.cameraTransform;
                    if(camera!=null)
                    {
                        var lampObject=new GameObject("Mining helmet light");lampObject.transform.SetParent(camera,false);
                        var lamp=lampObject.AddComponent<Light>();lamp.type=LightType.Spot;lamp.range=18;lamp.spotAngle=90;lamp.intensity=3;lamp.color=new Color(1,.88f,.66f);lamp.enabled=false;
                        Set(mining,"headlamp",lamp);
                    }
                }
                changed=true;
            }
            foreach(var image in prefab.GetComponentsInChildren<Image>(true))PersistSprite(image.sprite);
            foreach(var graphic in prefab.GetComponentsInChildren<Graphic>(true))
                if(graphic.material!=null&&!AssetDatabase.Contains(graphic.material)&&graphic.material!=Graphic.defaultGraphicMaterial)
                    AssetDatabase.CreateAsset(graphic.material,AssetDatabase.GenerateUniqueAssetPath(Folder+"/Prefab UI material.mat"));
            if(changed)PrefabUtility.SaveAsPrefabAsset(prefab,path);
            PrefabUtility.UnloadPrefabContents(prefab);
        }
        AssetDatabase.SaveAssets();
        File.WriteAllText("Temp/RoomiesArt/ta-authoring.txt",$"Saved {DateTime.UtcNow:o}\nUI controllers: 7\nTransforms: {root.GetComponentsInChildren<Transform>(true).Length}\n");
        var menuScene=EditorSceneManager.OpenScene("Assets/_Scenes/MainMenuSteam.unity",OpenSceneMode.Single);
        NormalizeColliders(menuScene);
        var menu=UnityEngine.Object.FindAnyObjectByType<MenuThemer>();
        if(menu==null)menu=new GameObject("Roomies_TA_Menu").AddComponent<MenuThemer>();
        menu.BuildUI();
        foreach(var image in menuScene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Image>(true)))PersistSprite(image.sprite);
        FixFonts(menuScene);
        EditorSceneManager.MarkSceneDirty(menuScene);EditorSceneManager.SaveScene(menuScene);AssetDatabase.SaveAssets();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log("[Roomies TA] Authored UI saved in GameRoom / Roomies_TA_UI.");
    }
    static void Set(UnityEngine.Object obj,string field,UnityEngine.Object value)
    {var so=new SerializedObject(obj);so.FindProperty(field).objectReferenceValue=value;so.ApplyModifiedPropertiesWithoutUndo();}
    static void NormalizeColliders(Scene scene)
    {
        foreach(var collider in scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<MeshCollider>(true)).ToArray())
        {
            if(!collider.enabled||collider.sharedMesh==null||collider.sharedMesh.name!="pCube4")continue;
            var before=collider.bounds;
            string path=Folder+"/pCube4 collider.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(mesh==null)
            {
                mesh=UnityEngine.Object.Instantiate(collider.sharedMesh);mesh.name="pCube4 normalized collider";
                mesh.vertices=mesh.vertices.Select(v=>v*.01f).ToArray();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,path);
            }
            var child=new GameObject("Collider normalized");child.layer=collider.gameObject.layer;child.tag=collider.tag;
            child.transform.SetParent(collider.transform,false);child.transform.localScale=Vector3.one*100;
            var normalized=child.AddComponent<MeshCollider>();normalized.sharedMesh=mesh;normalized.convex=collider.convex;
            normalized.isTrigger=collider.isTrigger;normalized.sharedMaterial=collider.sharedMaterial;collider.enabled=false;
            Physics.SyncTransforms();
            if(Vector3.Distance(before.center,normalized.bounds.center)>.01f||Vector3.Distance(before.size,normalized.bounds.size)>.01f)
                throw new Exception("Collider normalization changed world bounds");
            UnityEngine.Object.DestroyImmediate(collider);
        }
    }
    static void BuildAudio()
    {
        string path="Assets/Resources/RoomiesAudio/Audio rig.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path)!=null)return;
        var go=new GameObject("Roomies Audio");var audio=go.AddComponent<RoomiesAudio>();
        foreach(string field in new[]{"musicA","musicB","effects"})
        {var source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.loop=field!="effects";Set(audio,field,source);}
        var clips=AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Resources/RoomiesAudio"}).Select(guid=>AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid))).ToArray();
        var so=new SerializedObject(audio);var sounds=so.FindProperty("sounds");sounds.arraySize=clips.Length;
        for(int i=0;i<clips.Length;i++){var entry=sounds.GetArrayElementAtIndex(i);entry.FindPropertyRelative("id").stringValue=clips[i].name;entry.FindPropertyRelative("clip").objectReferenceValue=clips[i];}
        so.ApplyModifiedPropertiesWithoutUndo();PrefabUtility.SaveAsPrefabAsset(go,path);UnityEngine.Object.DestroyImmediate(go);
        var boot=EditorSceneManager.OpenScene("Assets/_Scenes/BootScene.unity",OpenSceneMode.Additive);
        if(!boot.GetRootGameObjects().Any(x=>x.GetComponent<RoomiesAudio>()!=null))PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path),boot);
        EditorSceneManager.SaveScene(boot);EditorSceneManager.CloseScene(boot,true);
    }
    static void PersistSprite(Sprite sprite)
    {
        if(sprite==null||AssetDatabase.Contains(sprite))return;
        if(!AssetDatabase.Contains(sprite.texture))AssetDatabase.CreateAsset(sprite.texture,AssetDatabase.GenerateUniqueAssetPath(Folder+"/UI texture.asset"));
        AssetDatabase.CreateAsset(sprite,AssetDatabase.GenerateUniqueAssetPath(Folder+"/UI sprite.asset"));
    }
    static void FixFonts(Scene scene)
    {
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/MPLUSU-VariableFont_wght.ttf");
        string path=Folder+"/Roomies Japanese SDF.asset";
        var fallback=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
        if(fallback==null)
        {
            fallback=TMP_FontAsset.CreateFontAsset(font);fallback.name="Roomies Japanese SDF";fallback.isMultiAtlasTexturesEnabled=true;
            AssetDatabase.CreateAsset(fallback,path);
            foreach(var texture in fallback.atlasTextures)AssetDatabase.AddObjectToAsset(texture,fallback);
            AssetDatabase.AddObjectToAsset(fallback.material,fallback);
        }
        fallback.TryAddCharacters("☆▶◀✓",out string missing);
        foreach(string guid in AssetDatabase.FindAssets("t:TMP_FontAsset"))
        {
            var asset=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if(asset==fallback)continue;
            if(asset.fallbackFontAssetTable==null)asset.fallbackFontAssetTable=new List<TMP_FontAsset>();
            if(!asset.fallbackFontAssetTable.Contains(fallback)){asset.fallbackFontAssetTable.Add(fallback);EditorUtility.SetDirty(asset);}
        }
        foreach(var text in scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<TMP_Text>(true)))
        {if(missing!=null&&missing.Contains('✓'))text.text=text.text.Replace("✓","OK");}
        EditorUtility.SetDirty(fallback);
    }

    [MenuItem("Roomies/TA/Validate authored UI")]
    public static void Validate()
    {
        var setup=EditorSceneManager.GetSceneManagerSetup();var report=new System.Text.StringBuilder();
        foreach(string name in new[]{"GameRoom","MainMenuSteam","BootScene"})
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Scenes/"+name+".unity",OpenSceneMode.Single);
            var all=scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<Transform>(true)).ToArray();
            int missing=all.Sum(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject));
            report.AppendLine($"{name}: transforms {all.Length}; missing scripts {missing}");
            if(missing!=0)throw new Exception("Missing scripts in "+name);
            foreach(var collider in all.SelectMany(t=>t.GetComponents<MeshCollider>()))
                if(collider.sharedMesh!=null&&collider.sharedMesh.name=="pCube4")report.AppendLine($"pCube4 collider {collider.name}: local bounds {collider.sharedMesh.bounds}, world {collider.bounds}, scale {collider.transform.lossyScale}");
            foreach(var behaviour in all.SelectMany(t=>t.GetComponents<MonoBehaviour>()))
            {
                if(behaviour==null)continue;
                if(new[]{typeof(EscMenuUI),typeof(SessionEndUI),typeof(DayTransitionUI),typeof(DayResultUI),typeof(RentPaymentUI),typeof(NightEventUI),typeof(HudThemer),typeof(MenuThemer),typeof(MainMenuSettingsUI)}.Contains(behaviour.GetType()))
                {
                    var so=new SerializedObject(behaviour);var property=so.GetIterator();
                    while(property.NextVisible(true))if(property.propertyType==SerializedPropertyType.ObjectReference&&property.name!="m_Script"&&property.objectReferenceValue==null)
                        throw new Exception(behaviour.GetType().Name+" missing "+property.propertyPath);
                    report.AppendLine("  References OK: "+behaviour.GetType().Name);
                }
            }
            if(name=="GameRoom")
            {
                var esc=UnityEngine.Object.FindAnyObjectByType<EscMenuUI>();
                int count=esc.GetComponentsInChildren<Button>(true).Length;
                typeof(EscMenuUI).GetMethod("BindAuthoredUI",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(esc,null);
                var prepare=typeof(UnityEngine.Events.UnityEventBase).GetMethod("PrepareInvoke",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                foreach(var button in esc.GetComponentsInChildren<Button>(true))
                {
                    var calls=(System.Collections.ICollection)prepare.Invoke(button.onClick,null);
                    if(calls.Count!=1)throw new Exception(button.name+" listener count "+calls.Count);
                }
                report.AppendLine($"  ESC buttons: {count}, each has exactly one runtime callback after scene reload");
                var canvas=esc.BuildSettingsPreview();
                RoomiesArtAudit.Render(scene,new Vector3(25,10,7),new Vector3(18,4,18),"ta-settings",canvas);
            }
        }
        foreach(string guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/_Prefabs"}))
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            foreach(var player in prefab.GetComponentsInChildren<PlayerNameDisplay>(true))
            {report.AppendLine($"PlayerNameDisplay {prefab.name}: enabled {player.enabled}; nameCanvas {player.nameCanvasTransform}");if(!player.enabled)throw new Exception("Disabled name sync");}
        }
        File.WriteAllText("Temp/RoomiesArt/ta-validation.txt",report.ToString());
        // Discard only the temporary preview state, including alpha, selection and render camera.
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        EditorSceneManager.RestoreSceneManagerSetup(setup);
        Debug.Log(report.ToString());
    }
}
