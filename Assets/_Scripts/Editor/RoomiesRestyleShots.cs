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
/// 7つ目に nopp と書くと画面の仕上げ（ポストプロセス）なしで撮る。
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
        // 素材の設定を変えた直後は、シェーダーが裏で準備中のまま透明に写ることがある。撮るあいだは待つ
        bool asyncShaders = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        try { CaptureAll(inv); }
        finally { ShaderUtil.allowAsyncCompilation = asyncShaders; }
        Debug.Log("[Roomies Restyle] Shots written to " + OutDir);
    }

    static void CaptureAll(CultureInfo inv)
    {
        foreach (string raw in File.ReadAllLines(SpecPath))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] p = line.Split('|');
            Vector3 pos, target; float fov;
            if (p[1].Trim() == "menu")
            {
                // タイトルの構図（MainMenuRoomShowcase が実行時に決めるのと同じ計算）
                var so = new SerializedObject(Object.FindAnyObjectByType<MainMenuRoomShowcase>());
                pos = so.FindProperty("cameraAnchor").vector3Value;
                var roomies = so.FindProperty("characters"); Vector3 sum = Vector3.zero;
                for (int i = 0; i < roomies.arraySize; i++) sum += ((Transform)roomies.GetArrayElementAtIndex(i).objectReferenceValue).position;
                Vector3 subject = sum / roomies.arraySize;
                Vector3 right = Vector3.Cross(Vector3.up, (subject - pos).normalized).normalized;
                target = subject + Vector3.up * so.FindProperty("subjectEyeHeight").floatValue - right * so.FindProperty("frameBias").floatValue;
                fov = so.FindProperty("fieldOfView").floatValue;
                // menu|x y z|z y z|画角 と書くと、タイトルの照明のまま好きな位置から撮れる（キャラのアップ用）
                if (p.Length > 3 && p[2].Trim().Length > 0) { pos = Parse(p[2].Split('>')[0]); target = Parse(p[2].Split('>')[1]); }
                if (p.Length > 3 && float.TryParse(p[3], NumberStyles.Float, inv, out float f) && f > 0) fov = f;
                // キャラの色は実行時に付く。撮るあいだだけ同じ色を当てる（保存されない）
                Color[] tints = { new Color(0.98f, 0.31f, 0.58f), new Color(0.20f, 0.70f, 0.96f), new Color(0.35f, 0.82f, 0.42f) };
                for (int i = 0; i < roomies.arraySize; i++)
                    foreach (var r in ((Transform)roomies.GetArrayElementAtIndex(i).objectReferenceValue).GetComponentsInChildren<Renderer>())
                    {
                        if (!r.name.Contains("BobBody")) continue;
                        var block = new MaterialPropertyBlock();
                        block.SetColor("_BaseColor", tints[i % tints.Length]);
                        r.SetPropertyBlock(block);
                    }
            }
            else { pos = Parse(p[1]); target = Parse(p[2]); fov = float.Parse(p[3], inv); }
            bool night = p.Length > 4 && p[4].Trim() == "night";
            string uiSpec = p.Length > 5 ? p[5].Trim() : "noui";
            bool ui = uiSpec.StartsWith("ui");
            string[] only = uiSpec.Contains(":") ? uiSpec.Substring(uiSpec.IndexOf(':') + 1).Split(',') : null;
            // 7つ目 nopp：画面の仕上げなし、8つ目 noaa：アンチエイリアスなし（ゲームのカメラの設定を再現するとき）
            Shot(p[0].Trim(), pos, target, fov, night, ui, only, p.Length > 6 && p[6].Trim() == "nopp", p.Length > 7 && p[7].Trim() == "noaa");
        }
    }

    static Vector3 Parse(string s)
    {
        string[] v = s.Trim().Split(' ');
        var inv = CultureInfo.InvariantCulture;
        return new Vector3(float.Parse(v[0], inv), float.Parse(v[1], inv), float.Parse(v[2], inv));
    }

    static void Shot(string name, Vector3 position, Vector3 target, float fov, bool night, bool ui, string[] only, bool noPost, bool noAA = false)
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
        var menu = day == null ? Object.FindAnyObjectByType<MainMenuRoomShowcase>() : null;
        if (menu != null)
        {
            var so = new SerializedObject(menu);
            nightSky = so.FindProperty("skyboxMaterial").objectReferenceValue as Material;
            nightAmbient = so.FindProperty("ambientIntensity").floatValue;
            nightReflection = so.FindProperty("reflectionIntensity").floatValue;
            nightSun = so.FindProperty("sunIntensity").floatValue;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Exclude)) if (l.type == LightType.Directional) sun = l;
        }
        var oldSky = RenderSettings.skybox; var oldMode = RenderSettings.ambientMode;
        float oldAmbient = RenderSettings.ambientIntensity, oldReflection = RenderSettings.reflectionIntensity;
        float oldSunIntensity = sun ? sun.intensity : 0; Color oldSunColor = sun ? sun.color : Color.white;

        var go = new GameObject("Restyle Shot Camera") { hideFlags = HideFlags.HideAndDontSave };
        var camera = go.AddComponent<Camera>();
        var data = go.AddComponent<UniversalAdditionalCameraData>();
        data.renderPostProcessing = !noPost;
        data.antialiasing = noAA ? AntialiasingMode.None : AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        data.antialiasingQuality = AntialiasingQuality.High;
        camera.transform.position = position;
        camera.transform.LookAt(target);
        camera.fieldOfView = fov;
        camera.nearClipPlane = .05f; camera.farClipPlane = 400;
        camera.allowHDR = true;
        camera.clearFlags = CameraClearFlags.Skybox;

        // ゲームの UI は Overlay なので画面の仕上げがかからない。撮るときも UI だけ別のカメラ（仕上げなし）で上に重ねる
        const int uiLayer = 5;
        camera.cullingMask = ~(1 << uiLayer);
        var uiGo = new GameObject("Restyle Shot UI Camera") { hideFlags = HideFlags.HideAndDontSave };
        var uiCamera = uiGo.AddComponent<Camera>();
        uiGo.AddComponent<UniversalAdditionalCameraData>().renderPostProcessing = false;
        uiCamera.transform.SetPositionAndRotation(position, camera.transform.rotation);
        uiCamera.fieldOfView = fov; uiCamera.nearClipPlane = .05f; uiCamera.farClipPlane = 10;
        uiCamera.clearFlags = CameraClearFlags.SolidColor; uiCamera.backgroundColor = Color.black;
        uiCamera.cullingMask = 1 << uiLayer; uiCamera.allowHDR = false;
        // 仕上げ済みの画面を UI カメラのいちばん奥に貼る（撮り終わったら消す）
        var backGo = new GameObject("Restyle Shot Backdrop", typeof(RectTransform), typeof(Canvas), typeof(UnityEngine.UI.RawImage)) { hideFlags = HideFlags.HideAndDontSave, layer = uiLayer };
        var backCanvas = backGo.GetComponent<Canvas>();
        backCanvas.renderMode = RenderMode.ScreenSpaceCamera; backCanvas.worldCamera = uiCamera; backCanvas.planeDistance = 1; backCanvas.sortingOrder = -32000;
        var backdrop = backGo.GetComponent<UnityEngine.UI.RawImage>();
        backdrop.rectTransform.anchorMin = Vector2.zero; backdrop.rectTransform.anchorMax = Vector2.one;
        backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
        backGo.SetActive(false);

        var canvases = new List<(Canvas canvas, RenderMode mode, Camera cam, float dist)>();
        var hidden = new List<Canvas>();
        var layers = new List<(GameObject go, int layer)>();
        if (ui)
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (only != null && Array.IndexOf(only, canvas.name) < 0) { hidden.Add(canvas); canvas.enabled = false; continue; }
                canvases.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = uiCamera; canvas.planeDistance = .3f;
                foreach (var t in canvas.GetComponentsInChildren<Transform>(true)) { layers.Add((t.gameObject, t.gameObject.layer)); t.gameObject.layer = uiLayer; }
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
        RenderTexture uiRt = null;
        var timeOfDay = Object.FindAnyObjectByType<RoomTimeOfDayLighting>();
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
            // 部屋の明かりと画面の仕上げも、撮る時間帯に合わせる（撮ったら夜に戻す。シーンは夜で保存されている）
            if (timeOfDay != null) timeOfDay.ApplyImmediate(night);
            camera.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            camera.Render();
            camera.Render();
            var result = rt;
            if (ui)
            {
                uiRt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
                backdrop.texture = rt; backGo.SetActive(true);
                uiCamera.targetTexture = uiRt;
                Canvas.ForceUpdateCanvases();
                uiCamera.Render(); uiCamera.Render();
                uiCamera.targetTexture = null;
                result = uiRt;
            }
            RenderTexture.active = result;
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            File.WriteAllBytes($"{OutDir}/{name}.png", tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            if (timeOfDay != null) timeOfDay.ApplyImmediate(true);
            foreach (var s in samples) { s.text.text = s.old; s.text.color = s.color; }
            foreach (var c in hidden) c.enabled = true;
            foreach (var l in layers) l.go.layer = l.layer;
            foreach (var c in canvases) { c.canvas.renderMode = c.mode; c.canvas.worldCamera = c.cam; c.canvas.planeDistance = c.dist; }
            RenderSettings.skybox = oldSky; RenderSettings.ambientMode = oldMode;
            RenderSettings.ambientIntensity = oldAmbient; RenderSettings.reflectionIntensity = oldReflection;
            if (sun) { sun.intensity = oldSunIntensity; sun.color = oldSunColor; }
            DynamicGI.UpdateEnvironment();
            Object.DestroyImmediate(tex);
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(go);
            Object.DestroyImmediate(uiGo);
            Object.DestroyImmediate(backGo);
            if (uiRt != null) Object.DestroyImmediate(uiRt);
        }
    }
}
#endif
