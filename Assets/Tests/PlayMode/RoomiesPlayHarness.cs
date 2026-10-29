using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object = UnityEngine.Object;

/// <summary>
/// PlayMode テストの共通の道具。ゲームのコードは Assembly-CSharp にあり asmdef から参照できないので、
/// RoomiesRegressionTests と同じくリフレクションで触る。
/// ホスト1人・Steam なし（ソケットを使わないテスト用トランスポート）で GameRoom を NGO 経由で読み込む。
/// </summary>
public static class RoomiesPlayHarness
{
    public const BindingFlags All = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

    public static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    public static object Get(object target, string name)
    {
        var type = target as Type ?? target.GetType();
        object inst = target is Type ? null : target;
        for (var t = type; t != null; t = t.BaseType)
        {
            var p = t.GetProperty(name, All | BindingFlags.DeclaredOnly);
            if (p != null) return p.GetValue(inst);
            var f = t.GetField(name, All | BindingFlags.DeclaredOnly);
            if (f != null) return f.GetValue(inst);
        }
        throw new MissingMemberException(type.Name, name);
    }

    public static T Get<T>(object target, string name) => (T)Get(target, name);

    public static object Call(object target, string name, params object[] args)
    {
        var type = target as Type ?? target.GetType();
        object inst = target is Type ? null : target;
        for (var t = type; t != null; t = t.BaseType)
        {
            var m = t.GetMethods(All | BindingFlags.DeclaredOnly).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length);
            if (m != null) return m.Invoke(inst, args);
        }
        throw new MissingMethodException(type.Name, name);
    }

    public static object Instance(string typeName) => Get(GameType(typeName), "Instance");

    public static string Text(Component tmp) => tmp == null ? null : (string)tmp.GetType().GetProperty("text").GetValue(tmp);

    public static IEnumerator WaitUntil(Func<bool> condition, float seconds, string what)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > end) throw new TimeoutException($"{seconds}秒待っても「{what}」にならなかった");
            yield return null;
        }
    }

    // ---------------- ネットワークとシーン ----------------

    public static NetworkManager Network { get; private set; }

    /// <summary>ホストを立てて GameRoom を NGO の SceneManager で読み込み、UIシーンとマップの準備ができるまで待つ。</summary>
    public static IEnumerator BootHost(object pendingLoad = null)
    {
        var go = new GameObject("TestNetworkManager");
        go.SetActive(false);
        Network = go.AddComponent<NetworkManager>();
        var transport = go.AddComponent<PlayModeTestTransport>();
        Network.NetworkConfig = new NetworkConfig
        {
            NetworkTransport = transport,
            EnableSceneManagement = true,
            ForceSamePrefabs = false,
            ConnectionApproval = false,
        };
#if UNITY_EDITOR
        Network.NetworkConfig.PlayerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        var list = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>("Assets/DefaultNetworkPrefabs.asset");
        if (list != null) Network.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(list);
#endif
        go.SetActive(true); // Awake/OnEnable（Singleton・DontDestroyOnLoad）

        var save = GameType("SaveSystem");
        if (pendingLoad != null) Call(save, "SetPendingLoad", pendingLoad);

        if (!Network.StartHost()) throw new Exception("StartHost に失敗");
        var status = Network.SceneManager.LoadScene("GameRoom", LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started) throw new Exception("GameRoom の読み込みを始められない: " + status);

        yield return WaitUntil(() =>
        {
            var day = Instance("DayManager") as NetworkBehaviour;
            return day != null && day.IsSpawned && Instance("GameUIRefs") != null && Instance("SharedMoneyManager") != null;
        }, 90f, "GameRoom と GameRoomUI の準備");
        yield return WaitUntil(() => Network.LocalClient?.PlayerObject != null, 20f, "ホストのプレイヤーが出る");
        // UIの初期化（WhenReady・Start）を1〜2フレーム待つ
        yield return null; yield return null;
    }

    public static IEnumerator ShutdownToEmptyScene()
    {
        if (Network != null)
        {
            Network.Shutdown();
            yield return WaitUntil(() => !Network.ShutdownInProgress, 10f, "通信の停止");
            Object.Destroy(Network.gameObject);
            Network = null;
        }
        var empty = SceneManager.CreateScene("RoomiesTestEmpty_" + Time.frameCount);
        SceneManager.SetActiveScene(empty);
        var toUnload = new List<Scene>();
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var s = SceneManager.GetSceneAt(i);
            if (s != empty && s.isLoaded) toUnload.Add(s);
        }
        foreach (var s in toUnload)
        {
            var op = SceneManager.UnloadSceneAsync(s);
            if (op != null) while (!op.isDone) yield return null;
        }
        yield return null;
    }

    public static int LoadedSceneCount(string name)
    {
        int n = 0;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).name == name && SceneManager.GetSceneAt(i).isLoaded) n++;
        return n;
    }

    // ---------------- 日付を進める（サーバー側） ----------------

    public static NetworkBehaviour Day => Instance("DayManager") as NetworkBehaviour;
    public static int CurrentDay => Get<int>(Day, "CurrentDay");
    public static bool IsNight => Get<bool>(Day, "IsNight");
    public static bool IsTransitioning => Get<bool>(Day, "IsTransitioning");
    public static bool IsGameOver => Get<bool>(Day, "IsGameOver");
    public static int Money => Get<int>(Instance("SharedMoneyManager"), "CurrentMoney");

    public static IEnumerator GoToNight()
    {
        if (IsNight) yield break;
        Call(Day, "AdvanceTurnServer");
        yield return WaitUntil(() => IsNight, 5f, "夜になる");
        yield return null;
    }

    /// <summary>夜を飛ばして次の朝（演出の終わり）まで待つ。家賃の日なら支払いの演出も入る。</summary>
    public static IEnumerator SkipNightAndWaitMorning(int expectedDay, float timeout = 90f)
    {
        Call(Day, "ServerSkipToMorning");
        yield return WaitUntil(() => IsGameOver || (CurrentDay == expectedDay && !IsNight && !IsTransitioning), timeout, $"{expectedDay}日目の朝");
    }

    // ---------------- スクリーンショット ----------------

    /// <summary>ROOMIES_CAPTURE_DIR があればそこへ。無ければ撮らない。</summary>
    public static string CaptureDir => Environment.GetEnvironmentVariable("ROOMIES_CAPTURE_DIR");

    /// <summary>
    /// バッチモードには Game ビューが無いので、カメラを RenderTexture に描いて保存する。
    /// 画面に重ねる UI（Screen Space - Overlay）はカメラに写らないので、撮るあいだだけ Screen Space - Camera にして戻す。
    /// </summary>
    public static string Capture(string fileName, int width = 1600, int height = 900, Camera cam = null)
    {
        string dir = CaptureDir;
        if (string.IsNullOrEmpty(dir)) return null;
        Directory.CreateDirectory(dir);
        cam ??= FindGameCamera();
        if (cam == null) { Debug.LogWarning("[Capture] カメラが無い"); return null; }

        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var prevTarget = cam.targetTexture;
        var changed = new List<(Canvas c, RenderMode mode, Camera cam, float dist)>();
        try
        {
            cam.targetTexture = rt;
            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude))
            {
                if (!canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                changed.Add((canvas, canvas.renderMode, canvas.worldCamera, canvas.planeDistance));
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = cam;
                // 並び順（sortingOrder）が大きいほど手前に
                canvas.planeDistance = Mathf.Max(cam.nearClipPlane + 0.02f, 1f - Mathf.Clamp(canvas.sortingOrder, -50, 500) * 0.0015f);
            }
            Canvas.ForceUpdateCanvases();
            cam.Render();
            var prevActive = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = prevActive;
            string path = Path.Combine(dir, fileName.EndsWith(".png") ? fileName : fileName + ".png");
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            Debug.Log("[Capture] " + path);
            return path;
        }
        finally
        {
            foreach (var (c, mode, wc, dist) in changed)
            {
                if (c == null) continue;
                c.renderMode = mode; c.worldCamera = wc; c.planeDistance = dist;
            }
            cam.targetTexture = prevTarget;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    public static Camera FindGameCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;
        return Object.FindObjectsByType<Camera>(FindObjectsInactive.Exclude)
            .Where(c => c.isActiveAndEnabled && c.targetTexture == null)
            .OrderByDescending(c => c.depth).FirstOrDefault();
    }

    // ---------------- 実際のセーブファイルを守る ----------------

    static string realSavePath, backupPath;

    /// <summary>セーブの場所（LocalLow\DefaultCompany\Roomies）は元のプロジェクトと共通なので、テスト前に退避する。</summary>
    public static void BackupRealSave()
    {
        realSavePath = (string)Get(GameType("SaveSystem"), "FilePath");
        backupPath = realSavePath + ".playmode-test-backup";
        if (File.Exists(backupPath)) throw new Exception("前のテストの退避ファイルが残っている。中身を確認して手で戻すこと: " + backupPath);
        if (File.Exists(realSavePath)) File.Move(realSavePath, backupPath);
    }

    public static void RestoreRealSave()
    {
        if (realSavePath == null) return;
        if (File.Exists(realSavePath)) File.Delete(realSavePath); // テストでできたセーブ
        if (File.Exists(backupPath)) File.Move(backupPath, realSavePath);
        realSavePath = null;
    }
}

/// <summary>ホストだけのテスト用トランスポート（ソケット・Steam を使わない）。</summary>
public class PlayModeTestTransport : NetworkTransport
{
    public override ulong ServerClientId => 0;
    public override bool StartClient() => false;
    public override bool StartServer() => true;
    public override void Initialize(NetworkManager networkManager = null) { }
    public override void Shutdown() { }
    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery delivery) { }
    public override void DisconnectLocalClient() { }
    public override void DisconnectRemoteClient(ulong clientId) { }
    public override ulong GetCurrentRtt(ulong clientId) => 0;
    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        clientId = 0; payload = default; receiveTime = Time.realtimeSinceStartup;
        return NetworkEvent.Nothing;
    }
}
