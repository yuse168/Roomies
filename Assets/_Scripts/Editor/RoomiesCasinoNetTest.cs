#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// カジノの 2 人での確認（ホストとクライアント）。同じ PC の 2 つのエディタ（Multiplayer Play Mode の Player 2）で動かす。
///
/// ふだんは何もしない。プロジェクト直下の Temp/RoomiesCasino/net/run.flag があるときだけ、Play Mode に入ると自動で動く：
///  ・この確認のあいだだけ、通信の口を Steam から同じ PC の中の接続（UnityTransport・127.0.0.1）に差し替える（保存されない）
///  ・メインのエディタがホスト、Player 2 がクライアント。足並みは Temp/RoomiesCasino/net/ の合図のファイルでそろえる
///  ・確かめること：2 人が同時に鍵を買う（1 回ぶんだけ引かれる）／同時に鍵を挿す／一緒にエレベーターに乗る／同じ回のルーレットに賭ける／
///    クライアントだけが 3 階へ上がる（ホストは動かない）／いったん抜けて入り直す（鍵・エレベーターの位置が合っている）
///  ・結果は Temp/RoomiesCasino/net/host.txt と client.txt
/// 確認のために共同口座へお金を足す（DebugGrant）。Play Mode の中だけのこと。
/// </summary>
[InitializeOnLoad]
internal static class RoomiesCasinoNetTest
{
    const ushort Port = 7788;

    static RoomiesCasinoNetTest()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !File.Exists(Dir + "/run.flag")) return;
            File.WriteAllText($"{Dir}/hook_{Who}.txt", $"dataPath {Application.dataPath} | cwd {Directory.GetCurrentDirectory()} | args {Environment.CommandLine} | root {ProjectRoot}");
            var go = new GameObject("Casino Net Test");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var runner = go.AddComponent<Runner>();
            runner.StartCoroutine(IsClone ? ClientRun(runner) : HostRun(runner));
        };
    }

    class Runner : MonoBehaviour { }

    /// <summary>Multiplayer Play Mode の Player 2（2 つ目のエディタ）を起こす／止める。ウィンドウのチェックを入れるのと同じこと。</summary>
    [MenuItem("Roomies/Casino/Net Test/Activate Player 2")]
    static void ActivatePlayer2() => SetPlayer2(true);

    [MenuItem("Roomies/Casino/Net Test/Deactivate Player 2")]
    static void DeactivatePlayer2() => SetPlayer2(false);

    static void SetPlayer2(bool active)
    {
        var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("Unity.Multiplayer.PlayMode.Editor.MultiplayerPlaymode")).FirstOrDefault(t => t != null);
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance;
        var players = type != null ? type.GetProperty("Players", flags)?.GetValue(null) as Array : null;
        string report;
        if (players == null || players.Length < 2) report = "players not found";
        else
        {
            object player = players.GetValue(1);
            var method = player.GetType().GetMethod(active ? "Activate" : "Deactivate", flags);
            object[] args = active ? new object[] { null, null } : new object[] { null };
            object result = method.Invoke(player, args);
            report = $"{(active ? "Activate" : "Deactivate")} Player 2 -> {result}, error {args[0]}, state {player.GetType().GetProperty("PlayerState", flags)?.GetValue(player)}";
        }
        Directory.CreateDirectory(ProjectRoot + "/Temp/RoomiesCasino");
        File.WriteAllText(ProjectRoot + "/Temp/RoomiesCasino/mppm_player2.txt", report);
        Debug.Log("[Casino] " + report);
    }

    /// <summary>調べるだけ：Multiplayer Play Mode の中の型と、プレイヤーを有効にする手続きの名前を Temp/RoomiesCasino/mppm_api.txt に書く。</summary>
    [MenuItem("Roomies/Casino/Net Test/Dump MPPM API")]
    static void DumpApi()
    {
        var sb = new StringBuilder();
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type[] types; try { types = asm.GetTypes(); } catch (System.Reflection.ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            types = types.Where(t => t.FullName != null && t.FullName.Contains("Multiplayer.PlayMode")).ToArray();
            if (types.Length == 0) continue;
            sb.AppendLine("== " + asm.GetName().Name + " (" + types.Length + " types)");
            foreach (var t in types)
            {
                var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
                var ms = t.GetMethods(flags).Where(m => System.Text.RegularExpressions.Regex.IsMatch(m.Name, "Activat|Deactivat|Launch|SetActive|Enable|Players|GetPlayer", System.Text.RegularExpressions.RegexOptions.IgnoreCase)).ToArray();
                if (ms.Length == 0) continue;
                sb.AppendLine(t.FullName);
                foreach (var m in ms) sb.AppendLine("   " + (m.IsStatic ? "static " : "") + m.ReturnType.Name + " " + m.Name + "(" + string.Join(", ", m.GetParameters().Select(p => p.ParameterType.Name + " " + p.Name)) + ")");
            }
        }
        Directory.CreateDirectory(ProjectRoot + "/Temp/RoomiesCasino");
        File.WriteAllText(ProjectRoot + "/Temp/RoomiesCasino/mppm_api.txt", sb.ToString());
        Debug.Log("[Casino] MPPM API written");
    }

    // Player 2 のエディタは、メインのプロジェクトの Library/VP/ の下で動く（dataPath・作業フォルダ・起動の引数のどれかに出る）
    static string ClonePath
    {
        get
        {
            foreach (string raw in new[] { Application.dataPath, Directory.GetCurrentDirectory(), Environment.CommandLine })
            {
                string p = raw.Replace((char)92, '/');
                if (p.Contains("/Library/VP/")) return p;
            }
            return null;
        }
    }
    static bool IsClone => ClonePath != null;
    static string ProjectRoot
    {
        get
        {
            string c = ClonePath;
            if (c != null)
            {
                string head = c.Substring(0, c.IndexOf("/Library/VP/", StringComparison.Ordinal));
                int drive = head.LastIndexOf(":/", StringComparison.Ordinal);   // 引数の途中から取ったときは、ドライブ名から後ろだけ使う
                return drive >= 1 ? head.Substring(drive - 1) : head;
            }
            string p = Application.dataPath.Replace((char)92, '/');
            return p.Substring(0, p.Length - "/Assets".Length);
        }
    }
    static string Dir => ProjectRoot + "/Temp/RoomiesCasino/net";

    static readonly StringBuilder log = new StringBuilder();
    static int ok, ng;
    static string Who => IsClone ? "client" : "host";

    static void Expect(bool good, string what)
    {
        if (good) ok++; else ng++;
        log.AppendLine((good ? "OK  " : "NG  ") + what);
        File.WriteAllText($"{Dir}/{Who}.txt", $"{Who}: ok {ok}, ng {ng}\n" + log);
    }

    static void Note(string what)
    {
        log.AppendLine("    " + what);
        File.WriteAllText($"{Dir}/{Who}.txt", $"{Who}: ok {ok}, ng {ng}\n" + log);
    }

    static void Mark(string name) => File.WriteAllText($"{Dir}/{name}", DateTime.Now.ToString("HH:mm:ss.fff"));

    static IEnumerator WaitFile(string name, float timeout = 120f)
    {
        float t = 0;
        while (!File.Exists($"{Dir}/{name}") && t < timeout) { yield return new WaitForSecondsRealtime(.1f); t += .1f; }
        if (!File.Exists($"{Dir}/{name}")) Expect(false, $"timed out waiting for the other side ({name})");
    }

    static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float t = 0;
        while (!condition() && t < timeout) { yield return new WaitForSecondsRealtime(.1f); t += .1f; }
    }

    /// <summary>この確認のあいだだけ、同じ PC の中でつなぐ口に替える。</summary>
    static void UseLocalTransport(NetworkManager nm)
    {
        var utp = nm.GetComponent<UnityTransport>();
        if (utp == null) utp = nm.gameObject.AddComponent<UnityTransport>();
        utp.SetConnectionData("127.0.0.1", Port);
        nm.NetworkConfig.NetworkTransport = utp;
    }

    static CharacterController MyController()
    {
        var nm = NetworkManager.Singleton;
        var player = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return player != null ? player.GetComponentInChildren<CharacterController>() : null;
    }

    static void Teleport(CharacterController cc, Vector3 feet)
    {
        cc.enabled = false; cc.transform.position = feet + Vector3.up * (cc.height / 2 + .08f); Physics.SyncTransforms(); cc.enabled = true;
    }

    static CasinoElevatorButton Button(CasinoElevator ev, int cabinFloor, CasinoElevatorButton.Kind kind, int index)
    {
        foreach (var b in ev.transform.Find($"Floor_{cabinFloor + 1}F").GetComponentsInChildren<CasinoElevatorButton>(true))
        {
            var so = new SerializedObject(b);
            if (so.FindProperty("kind").enumValueIndex == (int)kind && (kind == CasinoElevatorButton.Kind.KeySlot || so.FindProperty("floor").intValue == index)) return b;
        }
        return null;
    }

    static Transform Cabin(CasinoElevator ev, int floor) => ev.transform.Find($"Floor_{floor + 1}F/Anchor_Cabin");

    static int RedSpot(RouletteTable t)
    {
        for (int i = 0; i < t.SpotCount; i++) if (t.SpotAt(i).kind == RouletteBetKind.Red) return i;
        return -1;
    }

    /// <summary>スロットの真ん中の段に止まっている絵（左・中・右）と、結果の文字。</summary>
    static string SlotLine(SlotMachine slot)
    {
        var cells = slot.GetComponentsInChildren<UnityEngine.UI.Image>(true).Where(i => i.name == "Cell_2").OrderBy(i => i.transform.parent.name)
            .Select(i => i.sprite != null ? i.sprite.name : "?");
        var result = slot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).FirstOrDefault(x => x.name == "ResultText");
        return string.Join(",", cells) + " / " + (result != null ? result.text : "?");
    }

    static IEnumerator WaitWorld()
    {
        // GameRoom に入り、自分のプレイヤーとカジノの物がそろうまで
        yield return Until(() => SceneManager.GetActiveScene().name == "GameRoom" && MyController() != null
            && CasinoAccess.Instance != null && CasinoAccess.Instance.IsSpawned
            && UnityEngine.Object.FindAnyObjectByType<CasinoElevator>() != null, 180f);
    }

    // ================================================================ ホスト（メインのエディタ）
    static IEnumerator HostRun(MonoBehaviour runner)
    {
        Directory.CreateDirectory(Dir);
        foreach (var f in Directory.GetFiles(Dir).Where(f => !f.EndsWith("run.flag"))) File.Delete(f);
        log.Clear(); ok = ng = 0;

        yield return Until(() => NetworkManager.Singleton != null, 60f);
        var nm = NetworkManager.Singleton;
        yield return new WaitForSecondsRealtime(5f);   // 起動の画面からタイトルへ移るのを待つ
        Note($"before start: listening {nm.IsListening}, approval {nm.NetworkConfig.ConnectionApproval}, transport {nm.NetworkConfig.NetworkTransport?.GetType().Name}");
        UseLocalTransport(nm);
        nm.NetworkConfig.ConnectionApproval = false;
        nm.OnClientConnectedCallback += id => Note($"host: client {id} connected");
        nm.OnClientDisconnectCallback += id => Note($"host: client {id} disconnected ({nm.DisconnectReason})");
        bool started = nm.IsListening || nm.StartHost();
        Note($"host started {started}: host {nm.IsHost}, transport {nm.NetworkConfig.NetworkTransport?.GetType().Name}");
        // クライアントを呼ぶ合図は、本当のゲーム（SteamLobby）と同じ道具で出す：本編と UI のシーンを読み込み終わった瞬間。
        // run.flag に early と書いてあるときは、直す前と同じく「本編を読み込み終わった瞬間」に出す（UI の読み込み中に入るとどうなるかを見る）
        bool early = File.ReadAllText(Dir + "/run.flag").Trim() == "early";
        float loadStart = Time.realtimeSinceStartup;
        if (early)
        {
            NetworkSceneManager.OnLoadCompleteDelegateHandler once = null;
            once = (id, scene, mode) =>
            {
                if (id != nm.LocalClientId || scene != "GameRoom") return;
                nm.SceneManager.OnLoadComplete -= once;
                Mark("host_started"); Note($"signal (early: GameRoom only) at {Time.realtimeSinceStartup - loadStart:F2}s");
            };
            nm.SceneManager.OnLoadComplete += once;
        }
        else
            HostSceneReady.WhenLoaded(nm, runner, "GameRoom", 15f, () => { Mark("host_started"); Note($"signal (GameRoom and UI loaded) at {Time.realtimeSinceStartup - loadStart:F2}s"); });
        nm.SceneManager.OnLoadComplete += (id, scene, mode) => { if (id == nm.LocalClientId) Note($"host loaded {scene} at {Time.realtimeSinceStartup - loadStart:F2}s"); };
        nm.SceneManager.LoadScene("GameRoom", LoadSceneMode.Single);
        yield return WaitWorld();
        Expect(MyController() != null, "host is in GameRoom with its player");
        yield return WaitFile("host_started", 60f);

        yield return Until(() => nm.ConnectedClientsList.Count >= 2 && nm.ConnectedClientsList.All(c => c.PlayerObject != null), 300f);
        Expect(nm.ConnectedClientsList.Count >= 2, $"client joined (connected {nm.ConnectedClientsList.Count})");
        if (nm.ConnectedClientsList.Count < 2) { Mark("done"); yield break; }
        var clientPlayer = nm.ConnectedClientsList.First(c => c.ClientId != nm.LocalClientId).PlayerObject;
        yield return WaitFile("c0", 300f);   // クライアントの画面でも町がそろうまで待つ

        var money = SharedMoneyManager.Instance; var access = CasinoAccess.Instance;
        var ev = UnityEngine.Object.FindAnyObjectByType<CasinoElevator>();
        var clerks = UnityEngine.Object.FindObjectsByType<CasinoClerk>().Where(c => c.SellsFloor >= 2).OrderBy(c => c.SellsFloor).ToArray();
        var cc = MyController();
        if (DailyRequestManager.Instance != null)
            ((NetworkVariable<bool>)typeof(DailyRequestManager).GetField("done", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(DailyRequestManager.Instance)).Value = true;
        money.TryAdd(40000, SharedMoneyReason.DebugGrant, "casino net test");
        yield return new WaitForSecondsRealtime(1f);

        // ---- 1. 2 人が同時に 2 階の鍵を買う：引かれるのは 1 回ぶんだけ
        int m0 = money.CurrentMoney;
        Teleport(cc, clerks[0].transform.position + clerks[0].transform.forward * 1.75f + Vector3.right * .4f);
        Mark("s1");
        yield return new WaitForSecondsRealtime(.6f);
        clerks[0].Interact();
        yield return new WaitForSecondsRealtime(2f);
        Expect(access.KeysBought == 1 && money.CurrentMoney == m0 - CasinoAccess.KeyPrice(2), $"both bought at once: keys {access.KeysBought}, money {m0} -> {money.CurrentMoney} (one key price only)");
        yield return WaitFile("c1");

        // ---- 2. 2 人が同時に鍵を挿す：開くのは 1 回だけ
        Teleport(cc, Cabin(ev, 0).position + new Vector3(-.6f, 0, .45f));   // 2 人が重ならないよう、斜めに離して立つ
        Mark("s2");
        yield return new WaitForSecondsRealtime(.6f);
        Button(ev, 0, CasinoElevatorButton.Kind.KeySlot, 0).Interact();
        yield return new WaitForSecondsRealtime(1.5f);
        Expect(access.UnlockedFloor == 2 && access.KeysUsed == 1, $"both used the key at once: unlocked floor {access.UnlockedFloor}, keys used {access.KeysUsed}");
        yield return WaitFile("c2");

        // ---- 3. 2 人で一緒にエレベーターに乗る（2 人とも 2 階のボタンを押す）
        Mark("s3");
        yield return new WaitForSecondsRealtime(.6f);
        Note($"before the ride: idle {ev.IsIdle}, car {ev.CarFloor}, host {cc.transform.position} in cabin {ev.IsInsideCabin(0, cc.transform.position)}, client {clientPlayer.transform.position} in cabin {ev.IsInsideCabin(0, clientPlayer.transform.position)}");
        Button(ev, 0, CasinoElevatorButton.Kind.Go, 1).Interact();
        yield return new WaitForSecondsRealtime(1f);
        Note($"1s after the press: idle {ev.IsIdle}, car {ev.CarFloor}, host {cc.transform.position}, client {clientPlayer.transform.position}");
        yield return Until(() => ev.IsIdle, 12f);
        yield return new WaitForSecondsRealtime(1.2f);
        Note($"after the ride: car {ev.CarFloor}, host {cc.transform.position}, client {clientPlayer.transform.position}");
        Expect(ev.CarFloor == 1 && ev.IsInsideCabin(1, cc.transform.position), $"host arrived at 2F (y {cc.transform.position.y:F2})");
        Expect(ev.IsInsideCabin(1, clientPlayer.transform.position), $"host sees the client inside the 2F cabin (client at {clientPlayer.transform.position})");
        yield return WaitFile("c3");

        // ---- 4. 同じ回のルーレットに 2 人で賭ける
        var t2 = UnityEngine.Object.FindObjectsByType<RouletteTable>().OrderBy(t => t.transform.position.y).First();
        int red = RedSpot(t2);
        var redCol = t2.SpotAt(red).GetComponent<Collider>();
        Teleport(cc, t2.transform.TransformPoint(new Vector3(-.9f, 0, 1.3f)));
        int r0 = money.CurrentMoney;
        Mark("s4");
        yield return new WaitForSecondsRealtime(.6f);
        t2.Interact(redCol, cc.GetComponentInParent<PlayerEarning>());
        yield return Until(() => t2.TotalOn(red) >= 200, 5f);
        Expect(t2.TotalOn(red) == 200 && money.CurrentMoney == r0 - 200, $"both bet 100 on red in the same round: on red {t2.TotalOn(red)}, money {r0} -> {money.CurrentMoney}");
        yield return Until(() => !t2.IsBettingOpen, 20f);
        yield return Until(() => t2.IsIdle, 20f);
        int result = t2.LastResult;
        int back = 200 * RouletteRules.ReturnMultiplier(RouletteBetKind.Red, 0, result, false);
        Expect(money.CurrentMoney == r0 - 200 + back, $"roulette result {result}: paid back {back} in total, money {money.CurrentMoney}");
        yield return WaitFile("c4");

        // ---- 4b. ブラックジャック：ホストが遊ぶ。台の上のカードが、クライアントの画面でも同じ
        var bj2 = UnityEngine.Object.FindObjectsByType<BlackjackTable>().OrderBy(b => b.transform.position.y).ElementAt(1);
        var view2 = bj2.GetComponent<BlackjackTableView>();
        Teleport(cc, bj2.transform.position + bj2.transform.forward * 1.5f - Vector3.up * bj2.transform.position.y + Vector3.up * RoomiesTownRebuild.KFloorY(2));
        yield return new WaitForSecondsRealtime(.5f);
        yield return Until(() => bj2.IsIdle, 10f);
        bj2.Interact(cc.GetComponentInParent<PlayerEarning>());
        yield return Until(() => view2.LandedPlayer.Length >= 2 && view2.LandedDealer.Length >= 2 && !view2.IsBusy, 6f);
        File.WriteAllText($"{Dir}/bj_deal", view2.LandedPlayer + "|" + view2.LandedDealer);
        Expect(view2.LandedPlayer.Length >= 2 && view2.LandedDealer.Length >= 2, $"blackjack: host sees the deal on the table (player '{view2.LandedPlayer}', dealer '{view2.LandedDealer}')");
        Mark("s4b");
        yield return WaitFile("c4b", 30f);
        if (bj2.CanLocalPlayerStand(nm.LocalClientId)) bj2.Stand();
        yield return Until(() => !view2.LandedDealer.Contains(BlackjackTableView.Hidden) && !view2.IsBusy, 8f);
        File.WriteAllText($"{Dir}/bj_final", view2.LandedPlayer + "|" + view2.LandedDealer);
        Mark("s4c");
        yield return WaitFile("c4c", 30f);
        yield return Until(() => bj2.IsIdle, 12f);

        // ---- 4d. スロット：ホストが回す。止まった絵が、クライアントの画面でも同じ
        var slot2 = UnityEngine.Object.FindObjectsByType<SlotMachine>().OrderByDescending(s2 => s2.transform.position.y).ThenBy(s2 => s2.name).First();
        Teleport(cc, slot2.transform.position + slot2.transform.forward * 1.3f);
        yield return new WaitForSecondsRealtime(.5f);
        var spinning = (NetworkVariable<bool>)typeof(SlotMachine).GetField("isSpinning", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(slot2);
        slot2.Interact(cc.GetComponentInParent<PlayerEarning>());
        yield return new WaitForSecondsRealtime(.6f);
        yield return Until(() => !spinning.Value, 12f);
        yield return new WaitForSecondsRealtime(.3f);
        File.WriteAllText($"{Dir}/slot_result", SlotLine(slot2));
        Note($"slot: host sees '{SlotLine(slot2)}'");
        Mark("s4d");
        yield return WaitFile("c4d", 30f);

        // ---- 5. クライアントだけが 3 階の鍵を買って上がる。ホストは 2 階の広間に残る
        Teleport(cc, Cabin(ev, 1).position + new Vector3(0, 0, 3f));
        float hostY = cc.transform.position.y;
        Mark("s5");
        yield return WaitFile("c5", 60f);
        yield return new WaitForSecondsRealtime(1f);
        Expect(access.KeysBought == 2 && access.UnlockedFloor == 3, $"client bought and used the 3F key: keys {access.KeysBought}, unlocked floor {access.UnlockedFloor}");
        Expect(ev.CarFloor == 2 && Mathf.Abs(cc.transform.position.y - hostY) < .3f, $"car went to 3F with the client; the host stayed on 2F (y {cc.transform.position.y:F2})");
        Expect(clientPlayer.transform.position.y > RoomiesTownRebuild.KF3, $"host sees the client on 3F (y {clientPlayer.transform.position.y:F2})");

        // ---- 6. クライアントがいったん抜ける。その間に箱を 2 階へ呼ぶ。入り直したとき、鍵と箱の位置が合っている
        Mark("s6");
        yield return Until(() => nm.ConnectedClientsList.Count == 1, 30f);
        Expect(nm.ConnectedClientsList.Count == 1, "client left");
        Button(ev, 1, CasinoElevatorButton.Kind.Call, 1).Interact();
        yield return new WaitForSecondsRealtime(1f);
        yield return Until(() => ev.IsIdle, 12f);
        Expect(ev.CarFloor == 1, "host called the car back to 2F while the client was away");
        Note($"money before the client rejoins: {money.CurrentMoney}");
        File.WriteAllText($"{Dir}/host_money", money.CurrentMoney.ToString());
        Mark("s6b");
        yield return Until(() => nm.ConnectedClientsList.Count >= 2, 120f);
        Expect(nm.ConnectedClientsList.Count >= 2, "client rejoined");
        yield return WaitFile("c6", 120f);

        Mark("done");
    }

    // ================================================================ クライアント（Player 2）
    static IEnumerator ClientRun(MonoBehaviour runner)
    {
        log.Clear(); ok = ng = 0;
        // 先に用意を済ませ、ホストの合図が出た瞬間につなぐ（合図と接続のあいだを空けない）
        yield return Until(() => NetworkManager.Singleton != null, 60f);
        var nm = NetworkManager.Singleton;
        yield return new WaitForSecondsRealtime(4f);
        yield return WaitFile("host_started", 300f);
        Note($"before start: listening {nm.IsListening}, approval {nm.NetworkConfig.ConnectionApproval}, transport {nm.NetworkConfig.NetworkTransport?.GetType().Name}");
        UseLocalTransport(nm);
        nm.NetworkConfig.ConnectionApproval = false;
        nm.OnClientDisconnectCallback += id => Note($"client: disconnected ({nm.DisconnectReason})");
        nm.OnTransportFailure += () => Note("client: transport failure");
        bool startedClient = nm.StartClient();
        Note($"client started {startedClient}: listening {nm.IsListening}, transport {nm.NetworkConfig.NetworkTransport?.GetType().Name}");
        float t0 = Time.realtimeSinceStartup;
        yield return Until(() => nm.IsConnectedClient, 300f);
        Expect(nm.IsConnectedClient, $"client connected to the host (after {Time.realtimeSinceStartup - t0:F0}s)");
        yield return WaitWorld();
        Expect(MyController() != null, "client is in GameRoom with its own player");
        if (MyController() == null) yield break;

        var money = SharedMoneyManager.Instance; var access = CasinoAccess.Instance;
        var ev = UnityEngine.Object.FindAnyObjectByType<CasinoElevator>();
        var clerks = UnityEngine.Object.FindObjectsByType<CasinoClerk>().Where(c => c.SellsFloor >= 2).OrderBy(c => c.SellsFloor).ToArray();
        var cc = MyController();
        var earning = cc.GetComponentInParent<PlayerEarning>();
        Expect(access.KeysBought == 0 && access.UnlockedFloor == 1, $"client sees no keys at first (keys {access.KeysBought})");
        Mark("c0");

        // ---- 1. 同時に買う
        yield return WaitFile("s1");
        Teleport(cc, clerks[0].transform.position + clerks[0].transform.forward * 1.75f + Vector3.left * .4f);
        yield return new WaitForSecondsRealtime(.6f);
        clerks[0].Interact();
        yield return Until(() => access.KeysBought == 1, 5f);
        Expect(access.KeysBought == 1 && clerks[0].Label == "店の人と話す", $"client sees the key bought (keys {access.KeysBought}, label '{clerks[0].Label}')");
        Note($"money seen by the client: {money.CurrentMoney}");
        Mark("c1");

        // ---- 2. 同時に挿す
        yield return WaitFile("s2");
        Teleport(cc, Cabin(ev, 0).position + new Vector3(.6f, 0, -.45f));
        yield return new WaitForSecondsRealtime(.6f);
        Note($"client in the cabin: {cc.transform.position}, cabin anchor {Cabin(ev, 0).position}");
        var key0 = Button(ev, 0, CasinoElevatorButton.Kind.KeySlot, 0);
        if (key0.CanInteract()) key0.Interact();
        yield return Until(() => access.UnlockedFloor == 2, 5f);
        Expect(access.UnlockedFloor == 2 && !access.HasUnusedKey, $"client sees 2F unlocked (floor {access.UnlockedFloor})");
        Mark("c2");

        // ---- 3. 一緒に乗る
        yield return WaitFile("s3");
        yield return new WaitForSecondsRealtime(.6f);
        Button(ev, 0, CasinoElevatorButton.Kind.Go, 1).Interact();
        yield return new WaitForSecondsRealtime(1f);
        yield return Until(() => ev.IsIdle && ev.CarFloor == 1, 12f);
        yield return new WaitForSecondsRealtime(.8f);
        Expect(ev.CarFloor == 1 && ev.IsInsideCabin(1, cc.transform.position), $"client was carried to 2F (y {cc.transform.position.y:F2})");
        var env = GameObject.Find("Roomies_EnvironmentArt");
        var g2 = env != null ? env.transform.Find(RoomiesTownRebuild.CasinoGroup2F) : null;
        Expect(g2 != null && g2.gameObject.activeSelf, "client screen shows the 2F interior");
        Mark("c3");

        // ---- 4. 同じ回のルーレット
        yield return WaitFile("s4");
        var t2 = UnityEngine.Object.FindObjectsByType<RouletteTable>().OrderBy(t => t.transform.position.y).First();
        int red = RedSpot(t2);
        Teleport(cc, t2.transform.TransformPoint(new Vector3(.2f, 0, 1.3f)));
        yield return new WaitForSecondsRealtime(.6f);
        t2.Interact(t2.SpotAt(red).GetComponent<Collider>(), earning);
        yield return Until(() => t2.TotalOn(red) >= 200, 5f);
        Expect(t2.TotalOn(red) == 200, $"client sees both bets on red ({t2.TotalOn(red)})");
        Expect(t2.GetLabel(t2.SpotAt(red).GetComponent<Collider>(), nm.LocalClientId).Contains("100"), "client label shows its own chips on red");
        yield return Until(() => !t2.IsBettingOpen, 20f);
        yield return Until(() => t2.IsIdle, 20f);
        Expect(t2.LastResult >= 0 && t2.TotalOn(red) == 0, $"client sees the result {t2.LastResult} and a cleared table");
        Mark("c4");

        // ---- 4b. ブラックジャック：ホストが配ったカードが、同じ並びで台の上に見える
        yield return WaitFile("s4b", 60f);
        var bj2 = UnityEngine.Object.FindObjectsByType<BlackjackTable>().OrderBy(b => b.transform.position.y).ElementAt(1);
        var view2 = bj2.GetComponent<BlackjackTableView>();
        yield return Until(() => view2.LandedPlayer.Length >= 2 && view2.LandedDealer.Length >= 2 && !view2.IsBusy, 6f);
        string deal = File.ReadAllText($"{Dir}/bj_deal");
        Expect(view2.LandedPlayer + "|" + view2.LandedDealer == deal, $"blackjack: client sees the same deal as the host ('{view2.LandedPlayer}|{view2.LandedDealer}' vs '{deal}')");
        int shown = bj2.transform.Cast<Transform>().Count(c => c.name.StartsWith("Pivot_Card_") && c.gameObject.activeSelf);
        Expect(shown == deal.Length - 1, $"blackjack: client has {shown} cards on the table (expected {deal.Length - 1})");
        Expect(bj2.GetInteractionLabel(nm.LocalClientId).Contains("ゲーム中"), "blackjack: client cannot play the host's hand");
        Mark("c4b");
        yield return WaitFile("s4c", 30f);
        yield return Until(() => !view2.LandedDealer.Contains(BlackjackTableView.Hidden) && !view2.IsBusy, 8f);
        yield return new WaitForSecondsRealtime(.3f);
        string final = File.ReadAllText($"{Dir}/bj_final");
        Expect(view2.LandedPlayer + "|" + view2.LandedDealer == final, $"blackjack: client sees the same final cards ('{view2.LandedPlayer}|{view2.LandedDealer}' vs '{final}')");
        Mark("c4c");

        // ---- 4d. スロット：ホストが回した結果が、同じ絵で止まる
        yield return WaitFile("s4d", 60f);
        var slot2 = UnityEngine.Object.FindObjectsByType<SlotMachine>().OrderByDescending(s2 => s2.transform.position.y).ThenBy(s2 => s2.name).First();
        yield return new WaitForSecondsRealtime(.5f);
        string hostLine = File.ReadAllText($"{Dir}/slot_result");
        Expect(SlotLine(slot2) == hostLine, $"slot: client sees the same stopped reels ('{SlotLine(slot2)}' vs '{hostLine}')");
        Mark("c4d");

        // ---- 5. クライアントだけ 3 階へ
        yield return WaitFile("s5");
        Teleport(cc, clerks[1].transform.position + clerks[1].transform.forward * 1.75f);
        yield return new WaitForSecondsRealtime(.6f);
        clerks[1].Interact();
        yield return Until(() => access.KeysBought == 2, 5f);
        Expect(access.KeysBought == 2, $"client bought the 3F key (keys {access.KeysBought})");
        Teleport(cc, Cabin(ev, 1).position);
        yield return new WaitForSecondsRealtime(.6f);
        Button(ev, 1, CasinoElevatorButton.Kind.KeySlot, 0).Interact();
        yield return Until(() => access.UnlockedFloor == 3, 5f);
        Button(ev, 1, CasinoElevatorButton.Kind.Go, 2).Interact();
        yield return new WaitForSecondsRealtime(1f);
        yield return Until(() => ev.IsIdle && ev.CarFloor == 2, 12f);
        yield return new WaitForSecondsRealtime(.8f);
        Expect(access.UnlockedFloor == 3 && ev.IsInsideCabin(2, cc.transform.position), $"client rode alone to 3F (y {cc.transform.position.y:F2})");
        Mark("c5");

        // ---- 6. 抜けて、入り直す
        yield return WaitFile("s6");
        nm.Shutdown();
        yield return Until(() => !nm.IsListening, 10f);
        Expect(!nm.IsListening, "client disconnected");
        // 本当のゲームと同じく、いったんタイトルへ戻ってから入り直す（町に残ったまま入り直すと、古いシーンが残って同期できない）
        SceneManager.LoadScene("MainMenuSteam");
        yield return Until(() => SceneManager.GetActiveScene().name == "MainMenuSteam", 30f);
        yield return WaitFile("s6b", 60f);
        yield return new WaitForSecondsRealtime(3f);
        nm = NetworkManager.Singleton;
        UseLocalTransport(nm);
        nm.NetworkConfig.ConnectionApproval = false;
        nm.StartClient();
        yield return Until(() => nm.IsConnectedClient, 60f);
        yield return WaitWorld();
        yield return new WaitForSecondsRealtime(2f);
        access = CasinoAccess.Instance; ev = UnityEngine.Object.FindAnyObjectByType<CasinoElevator>(); money = SharedMoneyManager.Instance;
        Expect(nm.IsConnectedClient && MyController() != null, "client rejoined with a new player");
        Expect(access != null && access.KeysBought == 2 && access.UnlockedFloor == 3, $"after rejoining: keys {(access != null ? access.KeysBought : -1)}, unlocked floor {(access != null ? access.UnlockedFloor : -1)}");
        Expect(ev != null && ev.CarFloor == 1 && ev.IsIdle, $"after rejoining: the car is where the host left it (floor index {(ev != null ? ev.CarFloor : -1)})");
        if (ev != null)
        {
            float open2 = Mathf.Abs(ev.transform.Find("Floor_2F/Pivot_DoorL").localPosition.x), open1 = Mathf.Abs(ev.transform.Find("Floor_1F/Pivot_DoorL").localPosition.x);
            Expect(open2 > .9f && open1 < .45f, $"after rejoining: 2F doors open ({open2:F2}), 1F doors closed ({open1:F2})");
        }
        int hostMoney = File.Exists($"{Dir}/host_money") ? int.Parse(File.ReadAllText($"{Dir}/host_money")) : -1;
        Expect(money != null && money.CurrentMoney == hostMoney, $"after rejoining: shared money matches the host ({(money != null ? money.CurrentMoney : -1)} vs {hostMoney})");
        Mark("c6");
    }
}
#endif
