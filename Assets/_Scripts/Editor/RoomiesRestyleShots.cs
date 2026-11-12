#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// 見た目の確認用スクリーンショット（Play しない）。
/// Temp/RoomiesRestyle/shots.txt の各行を1枚として Docs/restyle_20261113/ に書き出す。
/// 行の形: 名前|カメラ位置 x y z|見る先 x y z|画角|night か day|ui か noui（ui:名前,名前 でその Canvas だけ）
/// ui のときは、HUD の数字に見本の値を入れて撮り、撮り終わったら元へ戻す。
/// シーンには何も残さない（撮影用カメラと一時的な空の設定は撮り終わったら戻す）。
/// </summary>
internal static class RoomiesRestyleShots
{
    const string SpecPath = "Temp/RoomiesRestyle/shots.txt";
    const string OutDir = "Docs/restyle_20261113";
    const int Width = 1920, Height = 1080;

    [MenuItem("Roomies/Art/Restyle/Capture Shots")]
    internal static void Capture()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!File.Exists(SpecPath)) throw new FileNotFoundException(SpecPath);
        Directory.CreateDirectory(OutDir);
        var inv = CultureInfo.InvariantCulture;
        foreach (string raw in File.ReadAllLines(SpecPath))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] p = line.Split('|');
            Vector3 pos = Parse(p[1]), target = Parse(p[2]);
            float fov = float.Parse(p[3], inv);
            bool night = p.Length > 4 && p[4].Trim() == "night";
            string uiSpec = p.Length > 5 ? p[5].Trim() : "noui";
            bool ui = uiSpec.StartsWith("ui");
            string[] only = uiSpec.Contains(":") ? uiSpec.Substring(uiSpec.IndexOf(':') + 1).Split(',') : null;
            Shot(p[0].Trim(), pos, target, fov, night, ui, only);
        }
        Debug.Log("[Roomies Restyle] Shots written to " + OutDir);
    }

    static Vector3 Parse(string s)
    {
        string[] v = s.Trim().Split(' ');
        var inv = CultureInfo.InvariantCulture;
        return new Vector3(float.Parse(v[0], inv), float.Parse(v[1], inv), float.Parse(v[2], inv));
    }

    static void Shot(string name, Vector3 position, Vector3 target, float fov, bool night, bool ui, string[] only)
    {
        // 空・太陽は DayManager が実行時に切り替える。撮るあいだだけ同じ値を当てて、あとで戻す。
        var day = Object.FindAnyObjectByType<DayManager>();
        Light sun = null; Material nightSky = null; float nightAmbient = .5f, nightReflection = .45f, nightSun = .12f;
        if (day != null)
        {
            var so = new SerializedObject(day);
            sun = so.FindProperty("environmentSun").objectReferenceValue as Light;
            nightSky = so.FindProperty("nightSkyMaterial").objectReferenceValue as Material;
            nightAmbient = so.FindProperty("nightAmbientIntensity").floatValue;
            nightReflection = so.FindProperty("nightReflectionIntensity").floatValue;
            nightSun = so.FindProperty("nightSunIntensity").floatValue;
        }
        var oldSky = RenderSettings.skybox; var oldMode = RenderSettings.ambientMode;
        float oldAmbient = RenderSettings.ambientIntensity, oldReflection = RenderSettings.reflectionIntensity;
        float oldSunIntensity = sun ? sun.intensity : 0; Color oldSunColor = sun ? sun.color : Color.white;

        var go = new GameObject("Restyle Shot Camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = true;
        data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        camera.transform.position = position;
        camera.transform.LookAt(target);
        camera.fieldOfView = fov;
        camera.nearClipPlane = .05f; camera.farClipPlane = 400;
        camera.allowHDR = true;
        camera.clearFlags = CameraClearFlags.Skybox;

        var canvases = new List<(Canvas canvas, RenderMode mode, Camera cam, float dist)>();
        var hidden = new List<Canvas>();
        if (ui)
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (only != null && Array.IndexOf(only, canvas.name) < 0) { hidden.Add(canvas); canvas.enabled = false; continue; }
                canvases.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = .3f;
            }

        // 見本の値（実行時はゲームが入れる）。撮ったら戻す
        var samples = new List<(TMPro.TMP_Text text, string old, Color color)>();
        if (ui)
            foreach (var text in Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsInactive.Exclude))
            {
                string sample = text.name == "MoneyValue" ? "¥128,500" : text.name == "MoneyRequired" ? "あと ¥171,500"
                    : text.name == "DayText" ? "DAY 3 夜" : text.name == "Timer Text" ? "2:41" : null;
                if (sample == null) continue;
                samples.Add((text, text.text, text.color)); text.text = sample;
                if (text.name == "MoneyValue") text.color = UITheme.Lime;
                if (text.name == "MoneyRequired") text.color = UITheme.Sun;
            }

        var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
        try
        {
            if (night && nightSky != null)
            {
                RenderSettings.skybox = nightSky; RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = nightAmbient; RenderSettings.reflectionIntensity = nightReflection;
                if (sun) { sun.intensity = nightSun; sun.color = new Color(.6f, .72f, 1f); }
                DynamicGI.UpdateEnvironment();
            }
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            camera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{OutDir}/{name}.png", tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            foreach (var s in samples) { s.text.text = s.old; s.text.color = s.color; }
            foreach (var c in hidden) c.enabled = true;
            foreach (var c in canvases) { c.canvas.renderMode = c.mode; c.canvas.worldCamera = c.cam; c.canvas.planeDistance = c.dist; }
            RenderSettings.skybox = oldSky; RenderSettings.ambientMode = oldMode;
            RenderSettings.ambientIntensity = oldAmbient; RenderSettings.reflectionIntensity = oldReflection;
            if (sun) { sun.intensity = oldSunIntensity; sun.color = oldSunColor; }
            DynamicGI.UpdateEnvironment();
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
        }
    }
}
#endif
