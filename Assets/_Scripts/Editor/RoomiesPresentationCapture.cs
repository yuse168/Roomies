#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class RoomiesPresentationCapture
{
    [MenuItem("Roomies/Presentation/Capture current scenes")]
    static void Capture()
    {
        if(EditorApplication.isPlaying || Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))
            throw new InvalidOperationException("Capture requires stopped play mode and saved scenes.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory("output/pdf/screenshots");
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Scenes/GameRoom.unity",OpenSceneMode.Single);
            Shot(scene,new Vector3(32,4.8f,-3),new Vector3(25.8f,1.6f,-12),"store");
            Shot(scene,new Vector3(26.15f,2.2f,-16),new Vector3(22.75f,1.7f,-11.8f),"checkout");
            Shot(scene,new Vector3(21.4f,5.1f,14.8f),new Vector3(16.5f,4.3f,20.5f),"home");
            Shot(scene,new Vector3(0,-10.1f,62),new Vector3(0,-10.1f,77),"mine");
            Shot(scene,new Vector3(3.5f,-29.6f,68),new Vector3(8,-30.5f,71),"ore");
            Shot(scene,new Vector3(-14,12,7),new Vector3(6.2f,1.5f,19),"delivery-jail");
            Shot(scene,new Vector3(28,6,-6),new Vector3(15.5f,2.8f,7),"town");
            scene=EditorSceneManager.OpenScene("Assets/_Scenes/MainMenuSteam.unity",OpenSceneMode.Single);
            Shot(scene,new Vector3(24,3.3f,.2f),new Vector3(19,1.05f,5.8f),"bob");
            var canvas=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).First();
            var main=UITheme.FindDeep("MainMenuPanel");var lobby=UITheme.FindDeep("LobbyPanel");
            if(main!=null&&lobby!=null){main.SetActive(false);lobby.SetActive(true);}
            Shot(scene,new Vector3(28,6,-6),new Vector3(15.5f,2.8f,7),"lobby",canvas);
            File.WriteAllText("output/pdf/screenshots/capture.txt",DateTime.UtcNow.ToString("o")+"\nCaptured from current GameRoom and MainMenuSteam using Unity Camera.Render in Editor, not a multiplayer play session. Scene edits discarded after capture.\n");
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static void Shot(Scene scene,Vector3 position,Vector3 target,string name,Canvas canvas=null)
    {
        // Warm up skinned renderers and shaders before writing the final frame.
        for(int i=0;i<3;i++)RoomiesArtAudit.Render(scene,position,target,"presentation-"+name,canvas,1920,1080);
        File.Copy("Temp/RoomiesArt/presentation-"+name+".png","output/pdf/screenshots/"+name+".png",true);
    }
}
#endif
