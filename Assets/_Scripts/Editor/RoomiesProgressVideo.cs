#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

/// <summary>
/// 制作進捗の動画を撮る道具（2026/11/24）。Play Mode で、町に入ってから実行する（ゲームのコードは変えない。撮るための道具だけ）。
///   Roomies/Video/Test Capture (Play Mode)     … 3 秒だけ撮って、大きさ・向き・音を確かめる
///   Roomies/Video/Record Progress (Play Mode)  … 場面ごとに Temp/RoomiesVideo/clips/ に mp4（映像）と .f32（音）を書く
/// 撮り方：Game ビューを 1920×1080 に固定し、毎フレーム Game ビューの絵（画面の上の UI も含む）を ffmpeg に渡す。
/// 時間は 1/30 秒ずつ進める（Time.captureFramerate）。音は AudioRenderer でゲームの音をそのまま書き出す。
/// 本当に遊ぶ（お金・鍵・冷蔵庫・セーブが動く）。必ずセーブの控えを取ってから Play Mode にし、あとで戻すこと。
/// </summary>
internal static partial class RoomiesCasinoDemo
{
    const string VideoDir = "Temp/RoomiesVideo";
    const string FfmpegPath = @"C:\Users\yuse2\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1.2-full_build\bin\ffmpeg.exe";
    const int VW = 1920, VH = 1080, Fps = 30;

    // ================================================================ 録る仕組み
    class VideoRecorder : MonoBehaviour
    {
        public bool Active { get; private set; }
        public int Frames { get; private set; }
        public string Log = "";
        Process ff;
        Stream vin;
        FileStream aout;
        RenderTexture rt;
        Texture2D tex;
        byte[] frameBytes;
        float[] audioFloats = new float[0];
        byte[] audioBytes = new byte[0];
        int channels = 2;
        long samples;
        bool muteBefore;
        float volumeBefore;

        public void Begin(string name)
        {
            Directory.CreateDirectory(VideoDir + "/clips");
            string mp4 = Path.GetFullPath($"{VideoDir}/clips/{name}.mp4");
            if (rt == null)
            {
                rt = new RenderTexture(VW, VH, 0, RenderTextureFormat.ARGB32) { name = "Video Capture" };
                tex = new Texture2D(VW, VH, TextureFormat.RGBA32, false);
                frameBytes = new byte[VW * VH * 4];
            }
            var psi = new ProcessStartInfo(FfmpegPath,
                $"-y -loglevel error -f rawvideo -pix_fmt rgba -s {VW}x{VH} -r {Fps} -i - -c:v libx264 -preset fast -crf 16 -pix_fmt yuv420p \"{mp4}\"")
            { UseShellExecute = false, RedirectStandardInput = true, CreateNoWindow = true };
            ff = Process.Start(psi);
            vin = ff.StandardInput.BaseStream;
            aout = File.Create(Path.ChangeExtension(mp4, ".f32"));
            switch (AudioSettings.GetConfiguration().speakerMode)
            {
                case AudioSpeakerMode.Mono: channels = 1; break;
                case AudioSpeakerMode.Quad: channels = 4; break;
                case AudioSpeakerMode.Surround: channels = 5; break;
                case AudioSpeakerMode.Mode5point1: channels = 6; break;
                case AudioSpeakerMode.Mode7point1: channels = 8; break;
                default: channels = 2; break;
            }
            Time.captureFramerate = Fps;
            // エディタの「音を消す」が入っていると、録る音も無音になる。録る間だけ外す（録る間はスピーカーからは鳴らない）
            muteBefore = EditorUtility.audioMasterMute;
            EditorUtility.audioMasterMute = false;
            volumeBefore = AudioListener.volume;
            AudioListener.volume = 1f;
            Log += $"  (editor mute was {muteBefore}, listener volume {AudioListener.volume}, pause {AudioListener.pause})\n";
            AudioRenderer.Start();
            Frames = 0; samples = 0;
            Active = true;
            Log += $"{name}: screen {Screen.width}x{Screen.height}, audio {AudioSettings.outputSampleRate}Hz x{channels}\n";
        }

        public void End()
        {
            if (!Active) return;
            Active = false;
            AudioRenderer.Stop();
            EditorUtility.audioMasterMute = muteBefore;
            AudioListener.volume = volumeBefore;
            vin.Flush(); vin.Close();
            ff.WaitForExit(60000);
            aout.Close();
            Log += $"  frames {Frames} ({Frames / (float)Fps:F2}s), audio samples {samples} ({samples / (float)AudioSettings.outputSampleRate:F2}s)\n";
        }

        IEnumerator Start()
        {
            var eof = new WaitForEndOfFrame();
            while (true)
            {
                yield return eof;
                if (Active) Grab();
            }
        }

        void Grab()
        {
            ScreenCapture.CaptureScreenshotIntoRenderTexture(rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, VW, VH), 0, 0, false);
            RenderTexture.active = prev;
            tex.GetRawTextureData<byte>().CopyTo(frameBytes);
            vin.Write(frameBytes, 0, frameBytes.Length);
            int n = AudioRenderer.GetSampleCountForCaptureFrame();
            if (n > 0)
            {
                var buf = new NativeArray<float>(n * channels, Allocator.Temp);
                AudioRenderer.Render(buf);
                if (audioFloats.Length < buf.Length) { audioFloats = new float[buf.Length]; audioBytes = new byte[buf.Length * 4]; }
                NativeArray<float>.Copy(buf, audioFloats, buf.Length);
                Buffer.BlockCopy(audioFloats, 0, audioBytes, 0, buf.Length * 4);
                aout.Write(audioBytes, 0, buf.Length * 4);
                samples += n;
                buf.Dispose();
            }
            Frames++;
        }

        void OnDestroy()
        {
            if (Active) End();
            Time.captureFramerate = 0;
            if (rt != null) rt.Release();
        }
    }

    // ================================================================ Game ビューの大きさ（1920×1080 に固定。終わったら戻す）
    static int SetGameViewSize(int width, int height)
    {
        var asm = typeof(Editor).Assembly;
        var gvType = asm.GetType("UnityEditor.GameView");
        var sizesType = asm.GetType("UnityEditor.GameViewSizes");
        var single = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        object sizes = single.GetProperty("instance").GetValue(null);
        object groupType = sizesType.GetProperty("currentGroupType").GetValue(sizes);
        object group = sizesType.GetMethod("GetGroup").Invoke(sizes, new[] { groupType });
        Type gt = group.GetType();
        int total = (int)gt.GetMethod("GetTotalCount").Invoke(group, null);
        int index = -1;
        for (int i = 0; i < total; i++)
        {
            object s = gt.GetMethod("GetGameViewSize").Invoke(group, new object[] { i });
            Type st = s.GetType();
            if ((int)st.GetProperty("width").GetValue(s) == width && (int)st.GetProperty("height").GetValue(s) == height
                && st.GetProperty("sizeType").GetValue(s).ToString() == "FixedResolution") { index = i; break; }
        }
        if (index < 0)
        {
            Type sizeType = asm.GetType("UnityEditor.GameViewSize"), kind = asm.GetType("UnityEditor.GameViewSizeType");
            object size = sizeType.GetConstructor(new[] { kind, typeof(int), typeof(int), typeof(string) })
                .Invoke(new[] { Enum.Parse(kind, "FixedResolution"), width, height, "Roomies Video" });
            gt.GetMethod("AddCustomSize").Invoke(group, new[] { size });
            index = total;
        }
        var gv = EditorWindow.GetWindow(gvType);
        var prop = gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        int previous = (int)prop.GetValue(gv);
        prop.SetValue(gv, index);
        gv.Repaint();
        return previous;
    }

    static void RestoreGameViewSize(int index)
    {
        if (index < 0) return;
        var gvType = typeof(Editor).Assembly.GetType("UnityEditor.GameView");
        var gv = EditorWindow.GetWindow(gvType);
        gvType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(gv, index);
    }

    // ================================================================ 撮影の道具
    class Rig
    {
        public VideoRecorder rec;
        public Camera cine;
        public AudioListener cineEar;
        public CharacterController cc;
        public PlayerMovement pm;
        public Camera playerCam;
        public AudioListener playerEar;
        public Canvas[] hud;
        public FieldInfo xRot;
        public string playerTag;
    }

    static Rig MakeRig(Runner runner)
    {
        var r = new Rig();
        r.rec = runner.gameObject.AddComponent<VideoRecorder>();
        r.cc = MyController();
        r.pm = r.cc.GetComponentInParent<PlayerMovement>();
        r.playerCam = r.pm.cameraTransform.GetComponent<Camera>();
        r.playerEar = r.pm.cameraTransform.GetComponent<AudioListener>();
        r.xRot = typeof(PlayerMovement).GetField("xRotation", BindingFlags.Instance | BindingFlags.NonPublic);
        var go = new GameObject("Video Cine Camera") { hideFlags = HideFlags.DontSave };
        go.transform.SetParent(runner.transform, false);
        r.cine = go.AddComponent<Camera>();
        r.cine.depth = 100;
        r.cine.nearClipPlane = .05f; r.cine.farClipPlane = r.playerCam.farClipPlane;
        r.cine.clearFlags = r.playerCam.clearFlags; r.cine.backgroundColor = r.playerCam.backgroundColor; r.cine.cullingMask = r.playerCam.cullingMask;
        var src = r.playerCam.GetUniversalAdditionalCameraData();
        var dst = r.cine.GetUniversalAdditionalCameraData();
        dst.renderPostProcessing = true;
        dst.antialiasing = src.antialiasing; dst.antialiasingQuality = src.antialiasingQuality;
        dst.volumeLayerMask = src.volumeLayerMask;
        dst.renderShadows = src.renderShadows;
        r.cineEar = go.AddComponent<AudioListener>();
        r.cine.enabled = false; r.cineEar.enabled = false;
        var ui = UnityEngine.SceneManagement.SceneManager.GetSceneByName("GameRoomUI");
        r.hud = ui.IsValid()
            ? ui.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<Canvas>(true)).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.enabled).ToArray()
            : new Canvas[0];
        return r;
    }

    /// <summary>映画のようなカメラ（HUD は隠す）か、プレイヤーの目（HUD を出す）か。</summary>
    static void UseCine(Rig r, bool cine)
    {
        r.cine.enabled = cine;
        r.cineEar.enabled = cine;
        // speech bubbles / floating text face Camera.main: while filming, the film camera is the main camera
        if (r.playerTag == null) r.playerTag = r.playerCam.tag;
        r.playerCam.tag = cine ? "Untagged" : r.playerTag;
        r.cine.tag = cine ? "MainCamera" : "Untagged";
        if (r.playerEar != null) r.playerEar.enabled = !cine;
        foreach (var c in r.hud) if (c != null) c.enabled = !cine;
    }

    static void Place(Rig r, Vector3 eye, Vector3 look, float fov = 55)
    {
        r.cine.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(look - eye, Vector3.up));
        r.cine.fieldOfView = fov;
    }

    /// <summary>カメラを a から b へ、なめらかに動かしながら seconds 秒撮る。</summary>
    static IEnumerator Move(Rig r, Vector3 eyeA, Vector3 lookA, Vector3 eyeB, Vector3 lookB, float seconds, float fov = 55, Action<float> each = null)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            float u = Mathf.SmoothStep(0, 1, t / seconds);
            Place(r, Vector3.Lerp(eyeA, eyeB, u), Vector3.Lerp(lookA, lookB, u), fov);
            each?.Invoke(t);
            yield return null;
        }
    }

    /// <summary>プレイヤーの目を、その点へ向ける（体の向きと、首の上下）。</summary>
    static void Aim(Rig r, Vector3 target)
    {
        Vector3 eye = r.pm.cameraTransform.position;
        Vector3 d = target - eye;
        float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
        r.cc.transform.rotation = Quaternion.Euler(0, yaw, 0);
        r.xRot.SetValue(r.pm, pitch);
    }

    static IEnumerator AimOver(Rig r, Vector3 from, Vector3 to, float seconds)
    {
        for (float t = 0; t < seconds; t += Time.deltaTime)
        {
            Aim(r, Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t / seconds)));
            yield return null;
        }
        Aim(r, to);
    }

    /// <summary>歩く（CharacterController を少しずつ動かす。目は進む先へ）。</summary>
    static IEnumerator WalkTo(Rig r, Vector3 feet, float speed, Vector3? lookAt = null)
    {
        for (int i = 0; i < 600; i++)
        {
            Vector3 p = r.cc.transform.position; Vector3 d = feet - p; d.y = 0;
            if (d.magnitude < .12f) break;
            Vector3 step = d.normalized * Mathf.Min(d.magnitude, speed * Time.deltaTime);
            r.cc.Move(step + Vector3.down * .05f);
            if (lookAt.HasValue) Aim(r, lookAt.Value); else Aim(r, r.pm.cameraTransform.position + d.normalized * 4f + Vector3.down * .3f);
            yield return null;
        }
    }

    static IEnumerator Hold(float seconds) { for (float t = 0; t < seconds; t += Time.deltaTime) yield return null; }

    static void SetTime(int night)
    {
        var dm = DayManager.Instance;
        dm.ServerApplySavedState(dm.CurrentDay, night, 0, 0);   // 朝か夜にして、残り時間を満タンに（撮っている間に日が変わらないように）
    }

    // ================================================================ テスト
    [MenuItem("Roomies/Video/Test Capture (Play Mode)")]
    static void VideoTest()
    {
        if (!Application.isPlaying) { Debug.LogWarning("[Video] Play Mode で実行してください"); return; }
        var runner = NewRunner();
        runner.StartCoroutine(VideoTestRoutine(runner));
    }

    static IEnumerator VideoTestRoutine(Runner runner)
    {
        Directory.CreateDirectory(VideoDir);
        File.Delete(VideoDir + "/test_done.txt");
        int prevSize = SetGameViewSize(VW, VH);
        bool asyncBefore = ShaderUtil.allowAsyncCompilation;
        ShaderUtil.allowAsyncCompilation = false;
        var r = MakeRig(runner);
        for (int i = 0; i < 10; i++) yield return null;
        SetTime(0);
        // カジノの前で、HUD ありのプレイヤーの目 1.5 秒 → 映画のカメラ 1.5 秒（カジノの音がする所）
        Teleport(r.cc, new Vector3(7.8f, .05f, -3.5f));
        yield return Hold(.5f);
        Aim(r, new Vector3(7.8f, 4f, -8f));
        UseCine(r, false);
        r.rec.Begin("test");
        GameAudio.PlayAt("store/job_end", r.cc.transform.position, 1f, 0f, 10f);
        yield return AimOver(r, new Vector3(7.8f, 4f, -8f), new Vector3(3f, 2f, -8f), 1.5f);
        UseCine(r, true);
        yield return Move(r, new Vector3(7.8f, 1.7f, 4f), new Vector3(7.8f, 7f, -8f), new Vector3(7.8f, 2f, 1f), new Vector3(7.8f, 7.5f, -8f), 1.5f, 60);
        r.rec.End();
        UseCine(r, false);
        ShaderUtil.allowAsyncCompilation = asyncBefore;
        RestoreGameViewSize(prevSize);
        File.WriteAllText(VideoDir + "/test_done.txt", r.rec.Log);
        Object.Destroy(runner.gameObject);
    }
}
#endif
