#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

internal static class RoomiesPortfolioCapture
{
    const string Output="output/pdf/screenshots";
    [MenuItem("Roomies/Presentation/Capture varied story photographs")]
    static void CaptureVariety()
    {
        if(EditorApplication.isPlaying||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save scenes and stop Play before photography.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Scenes/MainMenuSteam.unity",OpenSceneMode.Single);
            Prepare(scene);
            var actors=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("MenuRoomie_")).OrderBy(t=>t.name).ToArray();
            actors[2].gameObject.SetActive(false);
            Place(actors[0],new Vector3(15.2f,3.255f,19),70);
            Place(actors[1],new Vector3(18.2f,3.255f,20.4f),240);
            Fill(scene,new Vector3(18,5.1f,18),new Color(1,.83f,.67f),5,10);
            Shoot(scene,new Vector3(20.8f,5.2f,15.9f),new Vector3(16.6f,4.05f,20.1f),"story-roommates",2560,1440,54);
            actors[0].gameObject.SetActive(false);
            Place(actors[1],new Vector3(20.55f,3.255f,14.35f),160);
            Shoot(scene,new Vector3(24.4f,6.2f,8.2f),new Vector3(20,4.1f,14.9f),"story-doorstep",2560,1440,48);
            scene=EditorSceneManager.OpenScene("Assets/_Scenes/GameRoom.unity",OpenSceneMode.Single);
            Prepare(scene);
            Shoot(scene,new Vector3(7,3.5f,1),new Vector3(25,2,-11),"story-shopping-street",2560,1440,53);
            File.WriteAllText(Output+"/story-capture.txt",DateTime.UtcNow.ToString("o")+"\nP4: two roommates inside the lounge. P7: shopping street and store. P12: one Bob at the front door. Staged presentation photographs from existing game assets, not multiplayer test evidence. Scene changes discarded.\n");
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    [MenuItem("Roomies/Presentation/Capture portfolio photography")]
    static void Capture()
    {
        if(EditorApplication.isPlaying||Enumerable.Range(0,SceneManager.sceneCount).Any(i=>SceneManager.GetSceneAt(i).isDirty))throw new Exception("Save scenes and stop Play before photography.");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try
        {
            var scene=EditorSceneManager.OpenScene("Assets/_Scenes/MainMenuSteam.unity",OpenSceneMode.Single);
            Prepare(scene);
            var actors=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name.StartsWith("MenuRoomie_")).OrderBy(t=>t.name).ToArray();
            Place(actors[0],new Vector3(17.2f,0,5.2f),145);
            Place(actors[1],new Vector3(20.4f,0,6.1f),160);
            Place(actors[2],new Vector3(22,0,8.6f),155);
            Shoot(scene,new Vector3(24.4f,3.1f,-.5f),new Vector3(18.8f,1.25f,7.8f),"portfolio-hero",1800,1500,43);
            Shoot(scene,new Vector3(26.8f,3.2f,-2.5f),new Vector3(18.8f,1.55f,9.1f),"portfolio-friends",2560,1440,42);
            Shoot(scene,new Vector3(29,5,-4),new Vector3(16.1f,2.3f,13),"portfolio-town",2560,1440,48);
            Shoot(scene,new Vector3(22.4f,2.4f,0),new Vector3(18.6f,1.2f,7.7f),"portfolio-closing",2560,1440,48);
            scene=EditorSceneManager.OpenScene("Assets/_Scenes/GameRoom.unity",OpenSceneMode.Single);
            Prepare(scene);
            // Remove on-screen controls and instructional 3D labels from the photography session.
            foreach(var label in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMP_Text>(true)))
                if(label.text.Contains("STAFF")||label.text.Contains("昼営業"))label.gameObject.SetActive(false);
            Fill(scene,new Vector3(23,3,-13),new Color(1,.83f,.64f),5,11);
            Shoot(scene,new Vector3(25.4f,2.45f,-15.7f),new Vector3(22.75f,1.2f,-11.6f),"portfolio-checkout",2400,1462,47);
            Fill(scene,new Vector3(18,5.2f,18),new Color(1,.82f,.65f),4,12);
            Shoot(scene,new Vector3(19.4f,4.85f,16.8f),new Vector3(16.5f,3.85f,20.4f),"portfolio-home",2400,1462,53);
            Fill(scene,new Vector3(4,-29.2f,68),new Color(1,.79f,.44f),10,14);
            Fill(scene,new Vector3(10,-30,73),new Color(.38f,.72f,1),5,10);
            Shoot(scene,new Vector3(3.5f,-29.6f,68),new Vector3(8,-30.5f,71),"portfolio-ore",2400,1462,47);
            Directory.CreateDirectory(Output);
            File.WriteAllText(Output+"/portfolio-capture.txt",DateTime.UtcNow.ToString("o")+"\nCurrent game assets, staged character positions and presentation lighting, UI hidden. Off-screen game-camera rendering (not Scene View or multiplayer evidence). No scene changes saved.\n");
        }
        finally{EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorSceneManager.RestoreSceneManagerSetup(setup);}
    }
    static void Place(Transform actor,Vector3 point,float yaw)
    {
        actor.position=point;actor.rotation=Quaternion.Euler(0,yaw,0);
        var body=actor.GetComponentsInChildren<Renderer>(true).First(r=>r.name.Contains("BobBody"));
        actor.position+=Vector3.up*(point.y+.04f-body.bounds.min.y);
        foreach(var r in actor.GetComponentsInChildren<SkinnedMeshRenderer>(true))r.updateWhenOffscreen=true;
    }
    static void Prepare(Scene scene)
    {
        foreach(var root in scene.GetRootGameObjects())
        {
            foreach(var canvas in root.GetComponentsInChildren<Canvas>(true))canvas.gameObject.SetActive(false);
            foreach(var volume in root.GetComponentsInChildren<Volume>(true))volume.enabled=false;
            foreach(var camera in root.GetComponentsInChildren<Camera>(true))camera.enabled=false;
            foreach(var light in root.GetComponentsInChildren<Light>(true))
                if(light.type==LightType.Directional)light.enabled=false;
        }
        RenderSettings.ambientMode=AmbientMode.Trilight;
        RenderSettings.ambientSkyColor=new Color(.65f,.72f,.84f);
        RenderSettings.ambientEquatorColor=new Color(.46f,.50f,.57f);
        RenderSettings.ambientGroundColor=new Color(.26f,.23f,.27f);
        RenderSettings.fog=false;RenderSettings.reflectionIntensity=.45f;
        var go=new GameObject("Photography sunlight");SceneManager.MoveGameObjectToScene(go,scene);
        var key=go.AddComponent<Light>();key.type=LightType.Directional;key.color=new Color(1,.89f,.76f);key.intensity=1.6f;
        key.shadows=LightShadows.Soft;key.shadowBias=.035f;key.shadowNormalBias=.18f;key.shadowResolution=LightShadowResolution.VeryHigh;
        go.transform.rotation=Quaternion.Euler(38,-35,0);
        Fill(scene,new Vector3(22,4.4f,3),new Color(.68f,.84f,1),7,18);
    }
    static void Fill(Scene scene,Vector3 p,Color color,float power,float range)
    {
        var go=new GameObject("Photography soft fill");SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=p;
        var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=color;l.intensity=power;l.range=range;l.shadows=LightShadows.None;
    }
    static void Shoot(Scene scene,Vector3 pos,Vector3 target,string name,int width,int height,float fov)
    {
        var go=new GameObject("Portfolio Game Camera");SceneManager.MoveGameObjectToScene(go,scene);
        var cam=go.AddComponent<Camera>();cam.enabled=false;cam.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
        cam.transform.position=pos;cam.transform.LookAt(target);cam.fieldOfView=fov;cam.clearFlags=CameraClearFlags.Skybox;
        cam.nearClipPlane=.08f;cam.farClipPlane=350;cam.allowHDR=true;cam.allowMSAA=true;
        var data=cam.GetUniversalAdditionalCameraData();data.renderPostProcessing=true;data.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
        var v=go.AddComponent<Volume>();v.isGlobal=true;v.priority=1000;var profile=ScriptableObject.CreateInstance<VolumeProfile>();v.sharedProfile=profile;
        var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
        var color=profile.Add<ColorAdjustments>(true);color.postExposure.Override(.25f);color.contrast.Override(8);color.saturation.Override(7);
        var bloom=profile.Add<Bloom>(true);bloom.threshold.Override(1.2f);bloom.intensity.Override(.12f);
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);rt.antiAliasing=4;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);var prev=RenderTexture.active;bool async=ShaderUtil.allowAsyncCompilation;
        try
        {
            ShaderUtil.allowAsyncCompilation=false;cam.targetTexture=rt;
            for(int i=0;i<4;i++)cam.Render();
            RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();Directory.CreateDirectory(Output);File.WriteAllBytes(Output+"/"+name+".png",tex.EncodeToPNG());
        }
        finally{RenderTexture.active=prev;ShaderUtil.allowAsyncCompilation=async;cam.targetTexture=null;Object.DestroyImmediate(rt);Object.DestroyImmediate(tex);Object.DestroyImmediate(profile);Object.DestroyImmediate(go);}
    }
}
#endif
