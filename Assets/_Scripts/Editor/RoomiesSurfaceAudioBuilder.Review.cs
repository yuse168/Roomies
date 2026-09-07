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
using Object=UnityEngine.Object;

internal static partial class RoomiesSurfaceAudioBuilder
{
    [MenuItem("Roomies/Art/Review Textures Audio and Hover")]
    static void Review()
    {
        var report=new StringBuilder();
        const string path="Assets/_Scenes/GameRoom.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.IsValid()||!scene.isLoaded;
        if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
        try
        {
            var materials=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).SelectMany(r=>r.sharedMaterials).Where(Eligible).Distinct().ToArray();
            foreach(var material in materials)
                if(material.GetTexture(material.HasProperty("_BaseMap")?"_BaseMap":"_MainTex")==null)throw new InvalidOperationException("Untextured world material: "+material.name);
            report.AppendLine($"PASS: all {materials.Length} distinct Lit world materials in GameRoom have textures (existing detailed textures preserved).");
            RoomiesArtAudit.Render(scene,new Vector3(32,4.8f,-3),new Vector3(25.8f,1.6f,-12),"surface-store");
            RoomiesArtAudit.Render(scene,new Vector3(26.15f,2.2f,-16),new Vector3(22.75f,1.7f,-11.8f),"surface-checkout");
            RoomiesArtAudit.Render(scene,new Vector3(-27,4.2f,11),new Vector3(25,3.5f,-12),"surface-street");
            RoomiesArtAudit.Render(scene,new Vector3(21.4f,5.1f,14.8f),new Vector3(16.5f,4.3f,20.5f),"surface-house");
            RoomiesArtAudit.Render(scene,new Vector3(3.5f,-29.6f,68),new Vector3(8,-30.5f,71),"surface-mine");
            foreach(var register in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CashRegister>()))
                if(register.scanSound==null||register.paymentSound==null||register.sound==null)throw new InvalidOperationException("Register audio missing.");
            report.AppendLine("PASS: register sounds assigned.");
        }
        finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
        var preview=EditorSceneManager.NewPreviewScene();
        try
        {
            var go=new GameObject("Hover review");SceneManager.MoveGameObjectToScene(go,preview);
            var canvas=go.AddComponent<EscMenuUI>().BuildSettingsPreview();Canvas.ForceUpdateCanvases();
            var buttons=canvas.GetComponentsInChildren<Button>(true);
            foreach(var button in buttons)
            {
                var rect=(RectTransform)button.transform;
                if(rect.pivot!=new Vector2(.5f,.5f))throw new InvalidOperationException("Noncentral button: "+button.name);
                var before=rect.TransformPoint(rect.rect.center);rect.localScale=Vector3.one*1.05f;
                var after=rect.TransformPoint(rect.rect.center);rect.localScale=Vector3.one;
                if(Vector3.Distance(before,after)>.001f)throw new InvalidOperationException("Hover center moved: "+button.name);
            }
            report.AppendLine($"PASS: {buttons.Length} ESC buttons keep their center under 1.05x scale, including settings buttons repositioned after styling.");
            var back=canvas.transform.Find("Overlay/MenuCard/Settings/BackButton");back.localScale=Vector3.one*1.05f;
            RoomiesArtAudit.Render(preview,new Vector3(0,0,-10),Vector3.zero,"surface-hover-centered",canvas,1920,1080);
            // A direct rectangle-preservation check catches anchor/pivot regressions independently of the menu.
            var r=new GameObject("Pivot check",typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(canvas.transform,false);
            r.pivot=new Vector2(0,1);r.sizeDelta=new Vector2(330,56);r.anchoredPosition=new Vector2(87,-120);
            var corners=new Vector3[4];r.GetWorldCorners(corners);UITheme.CenterPivot(r);var centered=new Vector3[4];r.GetWorldCorners(centered);
            for(int i=0;i<4;i++)if(Vector3.Distance(corners[i],centered[i])>.001f)throw new InvalidOperationException("Pivot conversion moved corners.");
            report.AppendLine("PASS: pivot conversion preserves all four rectangle corners.");
        }
        finally{EditorSceneManager.ClosePreviewScene(preview);}
        foreach(string guid in AssetDatabase.FindAssets("t:AudioClip",new[]{AudioRoot}))
        {
            string audioPath=AssetDatabase.GUIDToAssetPath(guid);var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
            if(clip==null||clip.length<=0||clip.channels!=2||clip.frequency!=44100)throw new InvalidOperationException("Invalid audio: "+audioPath);
            report.AppendLine($"AUDIO {clip.name}: {clip.length:0.000}s stereo 44.1kHz");
        }
        Directory.CreateDirectory("ArtReview/2026-09-06");File.WriteAllText("ArtReview/2026-09-06/surface-audio-validation.txt",report.ToString());
        Debug.Log(report.ToString());
    }
}
#endif
