using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class RoomiesClosetRepair
{
    [MenuItem("Roomies/TA/Repair closet startup")]
    public static void Repair()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        if(Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Save scene changes first.");
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Scenes/GameRoom.unity",OpenSceneMode.Single);
            int removed=0;
            foreach(var closet in scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<CharacterCloset>(true)))
            {
                closet.EnsureUI();
                var so=new SerializedObject(closet);
                var overlay=(CanvasGroup)so.FindProperty("overlay").objectReferenceValue;
                if(overlay==null)throw new Exception("Closet overlay missing");
                var keeper=overlay.GetComponentInParent<Canvas>();
                foreach(var canvas in closet.GetComponentsInChildren<Canvas>(true).Where(c=>c.name=="CharacterClosetCanvas").ToArray())
                    if(canvas!=keeper){UnityEngine.Object.DestroyImmediate(canvas.gameObject);removed++;}
                overlay.alpha=0;overlay.blocksRaycasts=false;overlay.interactable=false;
                EditorUtility.SetDirty(overlay);EditorUtility.SetDirty(closet);
                if(PrefabUtility.IsPartOfPrefabInstance(closet))PrefabUtility.RecordPrefabInstancePropertyModifications(closet);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            scene=EditorSceneManager.OpenScene("Assets/_Scenes/GameRoom.unity",OpenSceneMode.Single);
            int checkedClosets=0;
            foreach(var closet in scene.GetRootGameObjects().SelectMany(go=>go.GetComponentsInChildren<CharacterCloset>(true)))
            {
                var so=new SerializedObject(closet);
                var group=so.FindProperty("overlay").objectReferenceValue as CanvasGroup;
                if(group==null||group.alpha!=0||group.blocksRaycasts||group.interactable)throw new Exception("Closet initial state invalid");
                if(so.FindProperty("closeButton").objectReferenceValue==null||so.FindProperty("selectedLabel").objectReferenceValue==null)throw new Exception("Closet controls missing");
                var buttons=so.FindProperty("swatches");
                if(buttons.arraySize!=16)throw new Exception("Swatch count invalid");
                for(int i=0;i<16;i++)if(buttons.GetArrayElementAtIndex(i).objectReferenceValue==null)throw new Exception("Swatch missing");
                int before=closet.GetComponentsInChildren<Canvas>(true).Count(c=>c.name=="CharacterClosetCanvas");
                closet.EnsureUI();closet.EnsureUI();
                int after=closet.GetComponentsInChildren<Canvas>(true).Count(c=>c.name=="CharacterClosetCanvas");
                if(before!=1||after!=1)throw new Exception("Duplicate closet UI");
                checkedClosets++;
            }
            if(checkedClosets==0)throw new Exception("No closet found");
            Directory.CreateDirectory("ArtReview/2026-09-07");
            File.WriteAllText("ArtReview/2026-09-07/closet-repair.txt",$"{DateTime.UtcNow:o}\nRemoved duplicate canvases: {removed}\nClosets validated after reload: {checkedClosets}\nHidden, non-blocking, 16 swatches and close callback references: PASS\nRepeated EnsureUI creates no duplicates: PASS\n");
            Debug.Log("[Closet repair] Saved and validated after reload.");
        }
        finally
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            EditorSceneManager.RestoreSceneManagerSetup(setup);
        }
    }
}
