#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class RoomiesArtAudit
{
    [MenuItem("Roomies/Art/Audit Environment")]
    private static void Audit()
    {
        const string path = "Assets/_Scenes/GameRoom.unity";
        var scene = SceneManager.GetSceneByPath(path);
        bool opened = !scene.IsValid() || !scene.isLoaded;
        if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
        try
        {
            var report = new StringBuilder();
            foreach (var root in scene.GetRootGameObjects())
            {
                report.AppendLine($"ROOT {root.name}: {root.transform.position} scale {root.transform.lossyScale}");
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                    report.AppendLine($"  {r.name}: center {r.bounds.center:F2} size {r.bounds.size:F2} active {r.gameObject.activeInHierarchy}");
            }
            Directory.CreateDirectory("Temp/RoomiesArt");
            File.WriteAllText("Temp/RoomiesArt/audit.txt", report.ToString());
            Render(scene, new Vector3(45, 36, 47), new Vector3(0, 0, 0), "overview");
            Render(scene, new Vector3(10, 11, 8), new Vector3(18, 1.6f, 18), "house");
            Debug.Log("[Roomies Art] Audit written to Temp/RoomiesArt/audit.txt");
        }
        finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
    }

    internal static void Render(Scene scene, Vector3 position, Vector3 target, string name, Canvas overlay = null, int width = 1400, int height = 1000)
    {
        var go = new GameObject("Art Review Camera");
        SceneManager.MoveGameObjectToScene(go, scene);
        var camera = go.AddComponent<Camera>();
        camera.overrideSceneCullingMask = EditorSceneManager.GetSceneCullingMask(scene);
        if(overlay!=null)
        {
            camera.overrideSceneCullingMask |= EditorSceneManager.GetSceneCullingMask(overlay.gameObject.scene);
            overlay.renderMode=RenderMode.ScreenSpaceCamera;overlay.worldCamera=camera;overlay.planeDistance=.5f;
        }
        camera.transform.position = position;
        camera.transform.LookAt(target);
        camera.fieldOfView = 48;
        camera.clearFlags = EditorSceneManager.IsPreviewScene(scene) ? CameraClearFlags.SolidColor : CameraClearFlags.Skybox;
        camera.backgroundColor = new Color(.23f, .31f, .38f);
        var rt = new RenderTexture(width, height, 24);
        var previous = RenderTexture.active;
        var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Temp/RoomiesArt");
            File.WriteAllBytes($"Temp/RoomiesArt/{name}.png", tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
#endif
