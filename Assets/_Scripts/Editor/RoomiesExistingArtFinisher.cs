#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

internal static class RoomiesExistingArtFinisher
{
    [MenuItem("Roomies/Art/Finish Existing NPCs and Slots")]
    static void Finish()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        foreach(string role in new[]{"Giver","Dealer","Police"})
        {
            string path=$"Assets/_Prefabs/Smuggling/{role}_Placeholder.prefab";
            var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var modelRoot=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="ModelRoot");
                if(root.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="RoomiesCharacterArt"))
                { HidePlaceholder(root,modelRoot); PrefabUtility.SaveAsPrefabAsset(root,path); continue; }
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Models/Bob/Bobs.fbx");
                var character=(GameObject)PrefabUtility.InstantiatePrefab(asset); character.name="RoomiesCharacterArt";
                character.transform.SetParent(modelRoot,false);
                var renderers=character.GetComponentsInChildren<Renderer>();
                var bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                float scale=1.78f/bounds.size.y; character.transform.localScale=Vector3.one*scale;
                bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
                character.transform.position+=new Vector3(modelRoot.position.x-bounds.center.x,modelRoot.position.y+.05f-bounds.min.y,modelRoot.position.z-bounds.center.z);
                string color=role=="Police"?"blue":role=="Dealer"?"teal":"coral";
                var material=AssetDatabase.LoadAssetAtPath<Material>($"{RoomiesArtLibraryBuilder.Root}/Materials/{color}.mat");
                foreach(var r in renderers) if(r.name=="BobBody")r.sharedMaterial=material;
                foreach(var r in modelRoot.GetComponentsInChildren<Renderer>(true))
                    if(r.name=="Body"||r.name=="Head")r.enabled=false;
                // Give each role a simple recognizable accessory without changing its controller or collider.
                var badge=GameObject.CreatePrimitive(PrimitiveType.Cube); badge.name="Role badge"; badge.transform.SetParent(modelRoot,false);
                badge.transform.localPosition=new Vector3(-.18f,1.07f,.32f); badge.transform.localScale=new Vector3(.16f,.2f,.045f);
                badge.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>($"{RoomiesArtLibraryBuilder.Root}/Materials/mustard.mat"); Object.DestroyImmediate(badge.GetComponent<Collider>());
                var hat=modelRoot.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Hat");
                if(hat!=null)hat.localPosition=new Vector3(0,1.85f,0);
                HidePlaceholder(root,modelRoot);
                PrefabUtility.SaveAsPrefabAsset(root,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        const string slotPath="Assets/_Prefabs/Slot.prefab";
        var slot=PrefabUtility.LoadPrefabContents(slotPath);
        try
        {
            var model=slot.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Model");
            if(model.Find("RoomiesSlotArt")==null)
            {
                var asset=AssetDatabase.LoadAssetAtPath<GameObject>($"{RoomiesArtLibraryBuilder.Root}/Props/slot_cabinet.prefab");
                if(!asset)throw new InvalidOperationException("Build prop library first.");
                var skin=(GameObject)PrefabUtility.InstantiatePrefab(asset); skin.name="RoomiesSlotArt"; skin.transform.SetParent(model,false);
                skin.transform.localRotation=Quaternion.Euler(0,90,0);
                // Source Model is a nonuniformly scaled cube; cancel its scale before orienting the baked skin.
                skin.transform.localScale=new Vector3(1/model.localScale.z,1/model.localScale.y,1/model.localScale.x);
                foreach(var collider in skin.GetComponentsInChildren<Collider>())Object.DestroyImmediate(collider);
                model.GetComponent<Renderer>().enabled=false;
                PrefabUtility.SaveAsPrefabAsset(slot,slotPath);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(slot); }
        AssetDatabase.SaveAssets(); Debug.Log("[Roomies Art] Existing NPCs and slots now use finished visual assets; gameplay references retained.");
    }

    static void HidePlaceholder(GameObject root, Transform modelRoot)
    {
        var placeholder=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Placeholder");
        if(placeholder==null)return;
        var hat=placeholder.Find("Hat");
        if(hat!=null){hat.SetParent(modelRoot,false);hat.localPosition=new Vector3(0,1.85f,0);}
        placeholder.gameObject.SetActive(false);
    }
}
#endif
