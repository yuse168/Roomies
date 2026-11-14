#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 配達の作り直し（第1段階）の通し確認。Play Mode でホストとして GameRoom に入ってから実行する
/// （Roomies/Art/Town/Walk - Enter GameRoom As Host）。結果は Temp/RoomiesDelivery/playtest.txt。
///
/// 朝1：値段の減り（そっと置く／強く落とす／割れる）→ 持って荷台へ → 荷台に投げ込む → 手に持ったままは数えない
///      → 発車（トラックの近くにいる＝乗った）
/// 夜1：運び屋の成功 → 家具を買う → ベッドで寝る（ひとりなら朝になる）
/// 朝2：トラックから離れたまま発車（置いていかれる）
/// 夜2：袋を持って逮捕 → 牢屋 → 朝3：作業10回で釈放
///
/// ゲームの値は変えない。時間を早めるために DayManager の残り時間だけをリフレクションで縮める。
/// </summary>
internal static class RoomiesDeliveryPlayTest
{
    static readonly StringBuilder log = new StringBuilder();
    static readonly List<string> moneyLines = new List<string>();
    static int ok, ng;
    static readonly List<string> netcodeWarnings = new List<string>();

    class Runner : MonoBehaviour { }

    [MenuItem("Roomies/Delivery/Play Test (Play Mode)")]
    static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[DeliveryTest] Play Mode で実行する"); return; }
        log.Clear(); moneyLines.Clear(); netcodeWarnings.Clear(); ok = ng = 0;
        var runner = new GameObject("Delivery Play Test Runner").AddComponent<Runner>();
        Object.DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(Main(runner));
    }

    static void Log(string s) { log.AppendLine(s); }
    static void Check(bool cond, string what)
    {
        if (cond) ok++; else ng++;
        log.AppendLine($"{(cond ? "OK " : "NG ")} {what}");
    }

    static void Write()
    {
        System.IO.Directory.CreateDirectory("Temp/RoomiesDelivery");
        System.IO.File.WriteAllText("Temp/RoomiesDelivery/playtest.txt",
            $"ok {ok}, ng {ng}\n" + log + "\n---- money log ----\n" + string.Join("\n", moneyLines));
    }

    static void OnLog(string message, string stack, LogType type)
    {
        if (message.StartsWith("[Money") || message.StartsWith("[DIAG")) moneyLines.Add(message);
        if (type == LogType.Exception || type == LogType.Error) { moneyLines.Add("ERROR: " + message); }
        if (message.Contains("[Netcode]") && type == LogType.Warning) netcodeWarnings.Add(message.Split('\n')[0]);
    }

    static IEnumerator Main(MonoBehaviour runner)
    {
        Application.logMessageReceived += OnLog;
        IEnumerator body = Body();
        while (true)
        {
            object current;
            try
            {
                if (!body.MoveNext()) break;
                current = body.Current;
            }
            catch (System.Exception e)
            {
                Log("EXCEPTION " + e);
                ng++;
                break;
            }
            yield return current;
        }
        Application.logMessageReceived -= OnLog;
        Write();
        Debug.Log($"[DeliveryTest] done ok {ok} ng {ng}");
        Object.Destroy(runner.gameObject);
    }

    // ---- 道具 ----

    static NetworkObject HostPlayer() =>
        NetworkManager.Singleton.LocalClient?.PlayerObject;

    static void TeleportHost(Vector3 pos)
    {
        var player = HostPlayer();
        var cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = pos;
        Physics.SyncTransforms();
        cc.enabled = true;
    }

    static void SetRemaining(float seconds)
    {
        var dm = DayManager.Instance;
        var f = typeof(DayManager).GetField("remainingTime", BindingFlags.NonPublic | BindingFlags.Instance);
        var nv = (NetworkVariable<float>)f.GetValue(dm);
        nv.Value = seconds;
    }

    static void AdvanceTurn()
    {
        typeof(DayManager).GetMethod("AdvanceTurnServer", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(DayManager.Instance, null);
    }

    static IEnumerator WaitUntil(System.Func<bool> cond, float timeout)
    {
        float end = Time.realtimeSinceStartup + timeout;
        while (!cond() && Time.realtimeSinceStartup < end) yield return null;
    }

    static List<DeliveryItem> Boxes() =>
        Object.FindObjectsByType<DeliveryItem>().Where(b => b.IsSpawned && !b.IsBroken).ToList();

    static Vector3 GroundAt(Vector3 p)
    {
        var hits = Physics.RaycastAll(new Vector3(p.x, p.y + 5f, p.z), Vector3.down, 30f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.collider.GetComponentInParent<DeliveryItem>() == null && h.collider.GetComponentInParent<PlayerMovement>() == null)
            .OrderBy(h => h.distance).ToArray();
        return hits.Length > 0 ? hits[0].point : p;
    }

    /// <summary>ホストが一度持って、その場に置く（持ったことのある箱にする）。</summary>
    static IEnumerator Arm(DeliveryItem box)
    {
        var player = HostPlayer();
        var c = box.GetComponent<CarryableObject>();
        Vector3 back = box.transform.position;
        Vector3 hostPos = player.transform.position;
        c.ServerRelocate(hostPos + player.transform.forward * 1.2f);
        yield return new WaitForSeconds(0.8f);
        c.PickupServerRpc(NetworkManager.Singleton.LocalClientId, player.NetworkObjectId);
        yield return new WaitForSeconds(0.2f);
        c.DropServerRpc(hostPos + player.transform.forward * 1.2f, Vector3.zero);
        yield return new WaitForSeconds(1.2f);
    }

    static int Money() => SharedMoneyManager.Instance != null ? SharedMoneyManager.Instance.CurrentMoney : -1;

    // ---- 本体 ----

    static IEnumerator Body()
    {
        var nm = NetworkManager.Singleton;
        yield return WaitUntil(() => DayManager.Instance != null && DayManager.Instance.IsSpawned && HostPlayer() != null && Boxes().Count > 0, 20f);
        var dm = DayManager.Instance;
        var truck = Object.FindAnyObjectByType<DeliveryTruck>();
        Check(truck != null && truck.IsSpawned, "トラックがシーンにあり、ネットワークで出ている");
        Check(Object.FindObjectsByType<DeliveryButton>().Length == 0, "古い納品ボタンは止まっている");
        Log($"day {dm.CurrentDay} night {dm.IsNight} remaining {dm.RemainingSeconds:F0} money {Money()} host {nm.IsHost}");
        yield return new WaitForSeconds(2f);   // 出てすぐの猶予（1.5秒）を過ぎるまで

        var boxes = Boxes();
        var balance = GameBalance.Current;
        Check(boxes.Count >= balance.deliveryLooseBoxes, $"町に箱が {boxes.Count}個（集荷場から {balance.deliveryLooseBoxes}個＋シーンに置いてある箱）");
        Log("boxes: " + string.Join(", ", boxes.Select(b => $"{(b.IsRareItem() ? "rare" : "normal")} ¥{b.CurrentValue}")));
        Check(boxes.All(b => b.CurrentValue == (b.IsRareItem() ? balance.rareBoxValue : balance.normalBoxValue)),
            "最初の値段が GameBalance どおり（集荷場の高い所から落ちても、まだ誰も持っていない箱は減らない）");

        Vector3 testSpot = truck.transform.TransformPoint(new Vector3(4f, 0f, -6f));
        testSpot = GroundAt(testSpot);

        // ---- まだ誰も持っていない箱は、強く落としても減らない
        var a = boxes.FirstOrDefault(b => !b.IsRareItem()) ?? boxes[0];
        var arb = a.GetComponent<Rigidbody>();
        {
            int u0 = a.CurrentValue;
            a.GetComponent<CarryableObject>().ServerRelocate(testSpot + Vector3.up * 4f);
            yield return new WaitForFixedUpdate();
            arb.linearVelocity = Vector3.down * 7f;
            yield return new WaitForSeconds(1.5f);
            Check(a.CurrentValue == u0, $"誰も持っていない箱を 4m から落とす: ¥{u0} → ¥{a.CurrentValue}（下がらない）");
        }

        // ここからは一度持った箱（持てば、そのあとの衝撃はすべて数える）
        yield return Arm(a);

        // ---- 値段：そっと置く
        int v0 = a.CurrentValue;
        a.GetComponent<CarryableObject>().ServerRelocate(testSpot + Vector3.up * 0.6f);
        arb.linearVelocity = Vector3.zero; arb.angularVelocity = Vector3.zero;
        yield return new WaitForSeconds(1.5f);
        Check(a.CurrentValue == v0, $"そっと置いた（0.6m から落とす）: ¥{v0} → ¥{a.CurrentValue}（下がらない）");

        // ---- 値段：強く落とす
        int v1 = a.CurrentValue;
        a.GetComponent<CarryableObject>().ServerRelocate(testSpot + Vector3.up * 4f);
        yield return new WaitForFixedUpdate();
        arb.linearVelocity = Vector3.down * 7f;
        yield return new WaitForSeconds(1.5f);
        Check(a.CurrentValue < v1, $"4m から下向き 7m/s で落とす: ¥{v1} → ¥{a.CurrentValue}（下がる）");

        // ---- 割れる（レア）：何度も叩きつける
        // 集荷場から出た箱（名前に (Clone)）で確かめる。シーンに直接置いた箱は割れても補充しない
        var r = Boxes().FirstOrDefault(b => b.IsRareItem() && b != a && b.name.Contains("(Clone)"));
        if (r == null)
        {
            // レアが出ていなければ、ふつうの箱で確かめる
            r = Boxes().FirstOrDefault(b => b != a && b.name.Contains("(Clone)"));
            Log("（レアが出ていないので、ふつうの箱で割れを確かめる）");
        }
        var rNo = r.NetworkObject;
        yield return Arm(r);
        int loosesBefore = Boxes().Count;
        for (int i = 0; i < 12 && rNo != null && rNo.IsSpawned; i++)
        {
            r.GetComponent<CarryableObject>().ServerRelocate(testSpot + new Vector3(1.5f, 5f, 0f));
            yield return new WaitForFixedUpdate();
            var rb = r.GetComponent<Rigidbody>();
            rb.linearVelocity = Vector3.down * 12f;
            yield return new WaitForSeconds(0.8f);
            if (rNo != null && rNo.IsSpawned) Log($"  hit {i + 1}: ¥{r.CurrentValue}");
        }
        Check(rNo == null || !rNo.IsSpawned, "値段が 0 になると割れて消える");
        yield return new WaitForSeconds(0.5f);
        Check(Boxes().Count == loosesBefore, $"割れたら集荷場に補充される（{loosesBefore} → {Boxes().Count}）");

        var player = HostPlayer();
        // ---- 強く投げ込んでも、後ろ（あおり）から跳ね出ない
        {
            int backOut = 0, inside = 0, sideOut = 0, notCleared = 0;
            // 荷台が空のうちに確かめる。投げる道すじに自分が立たないよう、トラックの横へよける
            TeleportHost(GroundAt(truck.transform.TransformPoint(new Vector3(4f, 0f, -3f))) + Vector3.up * 1.1f);
            yield return new WaitForSeconds(0.3f);
            foreach (float speed in new[] { 6f, 8f, 10f })
            {
                var hb = Boxes().FirstOrDefault(b => b != a && !truck.ServerContains(b.NetworkObject) && b.name.Contains("(Clone)"));
                if (hb == null) break;
                hb.GetComponent<CarryableObject>().ServerRelocate(truck.transform.TransformPoint(new Vector3(0f, 1.9f, -5.0f)));
                yield return new WaitForFixedUpdate();
                hb.GetComponent<Rigidbody>().linearVelocity = truck.transform.TransformDirection(new Vector3(0f, 1.6f, speed));
                float maxZ = -99f;
                var path = new StringBuilder();
                var hbCol = hb.GetComponentInChildren<Collider>();
                Log($"  box bounds size {hbCol.bounds.size}");
                float nextSample = 0f;
                for (float w = 0f; w < 3f && hb != null && hb.IsSpawned; w += Time.deltaTime)
                {
                    Vector3 lp = truck.transform.InverseTransformPoint(hb.transform.position);
                    maxZ = Mathf.Max(maxZ, lp.z);
                    if (w >= nextSample && w < 1.2f) { path.Append($" [{w:F2}s z{lp.z:F2} y{lp.y:F2} bot{hbCol.bounds.min.y - truck.transform.position.y:F2}]"); nextSample += 0.08f; }
                    yield return null;
                }
                Log("  path" + path);
                if (hb == null || !hb.IsSpawned) continue;
                Vector3 local = truck.transform.InverseTransformPoint(hb.transform.position);
                Log($"  (furthest z {maxZ:F1})");
                bool entered = maxZ > -2.3f;     // 箱の中心があおりを越えて荷台に入ったか
                if (truck.ServerContains(hb.NetworkObject)) inside++;
                else if (!entered) notCleared++;
                else if (local.z < -2.9f) backOut++;
                else sideOut++;
                Log($"  throw {speed}m/s: local ({local.x:F1}, {local.y:F1}, {local.z:F1}) in bed {truck.ServerContains(hb.NetworkObject)}");
                // 次の投げ込みのために荷台を空ける
                hb.GetComponent<CarryableObject>().ServerRelocate(testSpot + new Vector3(-2f + inside + backOut + sideOut + notCleared, 0.6f, 2f));
                yield return new WaitForSeconds(0.8f);
            }
            Check(backOut == 0 && inside >= 2,
                $"強く投げ込んでも後ろから跳ね出ない（荷台に残った {inside}・入ってから後ろへ {backOut}・横や前へ {sideOut}・あおりを越えず {notCleared}）");
        }

        // ---- 持って荷台へ（ホストの RPC 経路：拾う → 持ったまま動かす → 落とす）
        Vector3 board = truck.transform.TransformPoint(new Vector3(0f, 0f, -3.2f));
        TeleportHost(GroundAt(board) + Vector3.up * 1.1f);
        yield return new WaitForSeconds(0.3f);

        int looseBefore = Boxes().Count(b => !truck.ServerContains(b.NetworkObject));
        var b1 = Boxes().First(b => b != a && b.name.Contains("(Clone)"));
        var c1 = b1.GetComponent<CarryableObject>();
        c1.ServerRelocate(player.transform.position + player.transform.forward * 1.2f);
        yield return new WaitForSeconds(0.8f);
        c1.PickupServerRpc(nm.LocalClientId, player.NetworkObjectId);
        yield return new WaitForSeconds(0.2f);
        Check(c1.IsHeldBy(nm.LocalClientId), "ホストが箱を拾えた（PickupServerRpc）");

        Vector3 overBed = truck.transform.TransformPoint(new Vector3(0f, 1.9f, -1.4f));
        c1.UpdateHeldPositionServerRpc(overBed, truck.transform.rotation);
        yield return new WaitForSeconds(0.4f);
        int heldCount = truck.BedCount;
        Check(heldCount == 0, $"荷台の上で手に持っているうちは積んだことにならない（荷台 {heldCount}個）");

        int b1Before = b1.CurrentValue;
        c1.DropServerRpc(overBed, Vector3.zero);
        yield return new WaitForSeconds(1.5f);
        Check(truck.BedCount == 1, $"荷台に落とすと積んだことになる（荷台 {truck.BedCount}個 ¥{truck.BedValue}、落ちた衝撃で ¥{b1Before} → ¥{b1.CurrentValue}）");
        int looseAfter = Boxes().Count(b => !truck.ServerContains(b.NetworkObject));
        Check(looseAfter >= balance.deliveryLooseBoxes, $"積んだら集荷場に補充される（町の箱 {looseBefore} → {looseAfter}個、GameBalance は {balance.deliveryLooseBoxes}個以上）");

        // ---- 荷台に投げ込む（遠くから放物線で）
        var b2 = Boxes().First(b => b != a && b != b1 && !truck.ServerContains(b.NetworkObject));
        int b2Before = b2.CurrentValue;
        b2.GetComponent<CarryableObject>().ServerRelocate(truck.transform.TransformPoint(new Vector3(0f, 2.6f, -5.5f)));
        yield return new WaitForFixedUpdate();
        b2.GetComponent<Rigidbody>().linearVelocity = truck.transform.TransformDirection(new Vector3(0f, 2.5f, 5f));
        yield return new WaitForSeconds(2f);
        Check(truck.ServerContains(b2.NetworkObject), $"投げ込んだ箱が荷台に入った（¥{b2Before} → ¥{b2.CurrentValue}）");

        // ---- シーンに直接置いた箱も荷台へ（発車で消すときに NGO の警告が出ないか）
        var sceneBox = Boxes().FirstOrDefault(b => !b.name.Contains("(Clone)") && b != a && b != b1 && b != b2);
        if (sceneBox != null)
        {
            sceneBox.GetComponent<CarryableObject>().ServerRelocate(truck.transform.TransformPoint(new Vector3(0.5f, 1.4f, -1.0f)));
            yield return new WaitForSeconds(1.5f);
            Log($"scene box {sceneBox.name} in bed {truck.ServerContains(sceneBox.NetworkObject)}");
        }

        // ---- 手に持ったまま発車を迎える箱
        var b3 = Boxes().First(b => b != a && !truck.ServerContains(b.NetworkObject));
        var c3 = b3.GetComponent<CarryableObject>();
        c3.ServerRelocate(player.transform.position + player.transform.forward * 1.2f);
        yield return new WaitForSeconds(0.8f);
        c3.PickupServerRpc(nm.LocalClientId, player.NetworkObjectId);
        yield return new WaitForSeconds(0.2f);
        // 荷台の横で持ったまま（荷台の中で動かすと、積んである箱を押し出してしまうので）
        c3.UpdateHeldPositionServerRpc(truck.transform.TransformPoint(new Vector3(2.2f, 1.4f, -2.5f)), truck.transform.rotation);
        yield return new WaitForSeconds(0.5f);

        // ---- 発車（乗った）
        int earnBefore = player.GetComponent<PlayerEarning>().GetEarning();
        moneyLines.Add("---- depart 1 ----");
        SetRemaining(11.5f);
        // 発車直前の荷台（中で箱どうしがぶつかって値段が変わることがあるので、最後のフレームの値を使う）
        int bedValue = truck.BedValue, bedCount = truck.BedCount, b1Value = b1.CurrentValue;
        bool b1InBed = true;
        float until = Time.realtimeSinceStartup + 20f;
        while (!dm.IsNight && Time.realtimeSinceStartup < until)
        {
            bedValue = truck.BedValue; bedCount = truck.BedCount;
            if (b1 != null && b1.IsSpawned) { b1Value = b1.CurrentValue; b1InBed = truck.ServerContains(b1.NetworkObject); }
            yield return null;
        }
        yield return new WaitForSeconds(1.0f);
        int foodPerPlayer = moneyLines.SkipWhile(l => l != "---- depart 1 ----").Where(l => l.Contains("FoodBill"))
            .Sum(l => int.Parse(System.Text.RegularExpressions.Regex.Match(l, @"-(\d+)R").Groups[1].Value)) / Mathf.Max(1, nm.ConnectedClients.Count);
        var truckLines = moneyLines.Where(l => l.Contains("(Truck)")).ToList();
        int credited = truckLines.Sum(l => int.Parse(System.Text.RegularExpressions.Regex.Match(l, @"\+(\d+)R").Groups[1].Value));
        Check(credited == bedValue && truckLines.Count == bedCount,
            $"発車で荷台の荷物がお金になる（荷台 {bedCount}個 ¥{bedValue} → 入金 {truckLines.Count}件 ¥{credited}）");
        int earnDelta = player.GetComponent<PlayerEarning>().GetEarning() - earnBefore;
        int foundPerPlayer = moneyLines.SkipWhile(l => l != "---- depart 1 ----").Where(l => l.Contains("Found money"))
            .Sum(l => int.Parse(System.Text.RegularExpressions.Regex.Match(l, @"\+(\d+)R").Groups[1].Value)) / Mathf.Max(1, nm.ConnectedClients.Count);
        Check(earnDelta + foodPerPlayer - foundPerPlayer == (b1InBed ? b1Value : 0),
            $"積んだ人（乗った）の稼ぎに記録される（稼ぎ {earnDelta:+#;-#;0} ＝ 自分で積んだ箱 ¥{b1Value} − 食費 ¥{foodPerPlayer} ＋ 落とし物 ¥{foundPerPlayer}（同じ瞬間の夜イベント）。投げ込んだ箱は誰も持っていないので記録なし）");
        Check(truck.BedCount == 0 && (b1 == null || !b1.IsSpawned), "発車で荷台は空になる");
        Check(b3 != null && b3.IsSpawned && c3.IsHeld, "手に持っていた箱は数えず、手に残る");
        float distHome = Object.FindObjectsByType<SpawnPoint>().Min(s => Vector3.Distance(s.transform.position, player.transform.position));
        Check(distHome < 2.5f, $"乗った人は家（スポーン地点）へ帰る（いちばん近いスポーン地点まで {distHome:F1}m）");
        c3.DropServerRpc(player.transform.position + player.transform.forward, Vector3.zero);
        if (sceneBox != null)
            Check(!sceneBox.gameObject.activeSelf && netcodeWarnings.Count == 0,
                $"シーンに直接置いた箱を運び出しても NGO の警告が出ず、隠れる（表示 {sceneBox.gameObject.activeSelf}、警告 {netcodeWarnings.Count}件）");

        // ---- 走り去る（見た目だけ）
        var pivot = (Transform)typeof(DeliveryTruck).GetField("drivePivot", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(truck);
        Check(truck.IsAway, "発車したらトラックは出かけている（away）");
        yield return new WaitForSeconds(2.2f);
        Vector3 mid = pivot.localPosition;
        Check(pivot.gameObject.activeSelf && mid.magnitude > 0.5f, $"走り去る途中（軸の位置 ({mid.x:F1}, {mid.y:F1}, {mid.z:F1})・向き {pivot.localEulerAngles.y:F0}°）");
        yield return new WaitForSeconds(4f);
        Check(!pivot.gameObject.activeSelf, "走り去ったあとは見えなくなる");
        Check(truck.BedCount == 0, "出かけている間は荷台に何もない");

        // ---- 夜1：運び屋の成功
        var contacts = Object.FindObjectsByType<SmugglingContact>();
        var smug = player.GetComponent<SmugglingPlayer>();
        SmugglingContact Contact(SmugglingContact.ContactRole role)
        {
            var f = typeof(SmugglingContact).GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
                .FirstOrDefault(x => x.FieldType == typeof(SmugglingContact.ContactRole));
            return contacts.FirstOrDefault(c => f != null && (SmugglingContact.ContactRole)f.GetValue(c) == role);
        }
        var g = Contact(SmugglingContact.ContactRole.Giver);
        var d = System.Enum.GetValues(typeof(SmugglingContact.ContactRole)).Cast<SmugglingContact.ContactRole>()
            .Where(x => x != SmugglingContact.ContactRole.Giver).Select(Contact).FirstOrDefault(x => x != null);
        Log($"contacts {contacts.Length}: giver {(g ? g.name : "none")} dealer {(d ? d.name : "none")}");
        if (g != null && d != null)
        {
            int m0 = Money();
            TeleportHost(g.transform.position + g.transform.forward * 1.2f + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.4f);
            smug.RequestContactInteraction(SmugglingContact.ContactRole.Giver);
            yield return new WaitForSeconds(0.4f);
            Check(smug.HasBag, "運び屋：渡し人から袋を受け取れる");
            TeleportHost(d.transform.position + d.transform.forward * 1.2f + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.4f);
            var dealerRole = System.Enum.GetValues(typeof(SmugglingContact.ContactRole)).Cast<SmugglingContact.ContactRole>().First(x => x != SmugglingContact.ContactRole.Giver);
            smug.RequestContactInteraction(dealerRole);
            yield return new WaitForSeconds(0.4f);
            Check(!smug.HasBag && moneyLines.Any(l => l.Contains("SmugglingReward")), $"運び屋：売人に渡すと報酬（口座 {m0} → {Money()}）");
        }
        else Check(false, "運び屋の渡し人・売人が見つからない");

        // ---- 夜1：家具を買う
        {
            int m0 = Money();
            int furnBefore = Object.FindObjectsByType<NetworkFurniture>().Length;
            dm.BuyFurnitureServerRpc(0);
            yield return new WaitForSeconds(1f);
            int furnAfter = Object.FindObjectsByType<NetworkFurniture>().Length;
            Check(furnAfter == furnBefore + 1 || Money() < m0, $"家具を買える（家具 {furnBefore} → {furnAfter}、口座 {m0} → {Money()}）");
        }

        // ---- 夜1：ベッドで寝る → ひとりなら朝になる
        var bed = Object.FindObjectsByType<Bed>().FirstOrDefault(x => !x.IsOccupied);
        if (bed != null)
        {
            TeleportHost(bed.transform.position + bed.transform.forward * 1.0f + Vector3.up * 1.1f);
            yield return new WaitForSeconds(0.4f);
            int day0 = dm.CurrentDay;
            bed.Interact();
            yield return new WaitForSeconds(0.5f);
            Check(bed.IsMine, "ベッドで寝られる");
            yield return WaitUntil(() => !dm.IsNight && dm.CurrentDay == day0 + 1, 40f);
            Check(!dm.IsNight && dm.CurrentDay == day0 + 1, $"ひとりで寝たら朝になる（DAY {day0} → {dm.CurrentDay}）");
            yield return new WaitForSeconds(3.5f);
            Check(!truck.IsAway && pivot.gameObject.activeSelf && pivot.localPosition.magnitude < 0.01f,
                "朝になるとトラックは元の場所に戻る");
        }
        else Check(false, "ベッドが見つからない");

        // ---- 朝2：トラックから離れたまま発車（置いていかれる）
        {
            yield return WaitUntil(() => !dm.IsNight, 10f);
            var boxNow = Boxes().First(b => !truck.ServerContains(b.NetworkObject));
            var cb = boxNow.GetComponent<CarryableObject>();
            TeleportHost(GroundAt(board) + Vector3.up * 1.1f);
            yield return new WaitForSeconds(0.3f);
            cb.ServerRelocate(player.transform.position + player.transform.forward * 1.2f);
            yield return new WaitForSeconds(0.8f);
            cb.PickupServerRpc(nm.LocalClientId, player.NetworkObjectId);
            yield return new WaitForSeconds(0.2f);
            cb.DropServerRpc(overBed, Vector3.zero);
            yield return new WaitForSeconds(1.5f);
            int value = truck.BedValue;
            Vector3 far = GroundAt(truck.transform.TransformPoint(new Vector3(0f, 0f, -25f))) + Vector3.up * 1.1f;
            TeleportHost(far);
            int earnB = player.GetComponent<PlayerEarning>().GetEarning();
            int lines0 = moneyLines.Count(l => l.Contains("(Truck)"));
            moneyLines.Add("---- depart 2 ----");
            SetRemaining(2f);
            yield return WaitUntil(() => dm.IsNight, 10f);
            yield return new WaitForSeconds(1f);
            int lines1 = moneyLines.Count(l => l.Contains("(Truck)"));
            Check(lines1 == lines0 + 1, $"置いていかれても、荷台の荷物はお金になる（¥{value}）");
            int dEarn = player.GetComponent<PlayerEarning>().GetEarning() - earnB;
            // 同じ瞬間の夜イベント（食費・落とし物）も個人の記録を動かすので、その分を除く
            var after2 = moneyLines.SkipWhile(l => l != "---- depart 2 ----").ToList();
            int players = Mathf.Max(1, nm.ConnectedClients.Count);
            int food = after2.Where(l => l.Contains("FoodBill")).Sum(l => int.Parse(System.Text.RegularExpressions.Regex.Match(l, @"-(\d+)R").Groups[1].Value)) / players;
            int found = after2.Where(l => l.Contains("Found money")).Sum(l => int.Parse(System.Text.RegularExpressions.Regex.Match(l, @"\+(\d+)R").Groups[1].Value)) / players;
            Check(dEarn + food - found == 0, $"置いていかれた人の稼ぎには記録されない（稼ぎの増減 {dEarn} ＝ 落とし物 +{found} − 食費 {food}）");
            Check(Vector3.Distance(player.transform.position, far) < 1.5f, "置いていかれた人は町に残る（家へ帰らない）");
        }

        // ---- 夜2：逮捕 → 牢屋 → 朝3：作業で釈放
        if (g != null)
        {
            TeleportHost(g.transform.position + g.transform.forward * 1.2f + Vector3.up * 0.2f);
            yield return new WaitForSeconds(0.4f);
            smug.RequestContactInteraction(SmugglingContact.ContactRole.Giver);
            yield return new WaitForSeconds(0.4f);
            int m0 = Money();
            smug.ServerArrest();
            yield return new WaitForSeconds(4f);
            Check(smug.IsJailed, $"逮捕で牢屋へ（口座 {m0} → {Money()}）");
            AdvanceTurn();     // 夜→朝
            yield return WaitUntil(() => !dm.IsNight, 40f);
            yield return new WaitForSeconds(4f);
            Check(smug.CanDoJailLabor, "朝は牢屋で作業できる");
            var labor = Object.FindAnyObjectByType<SmugglingJailLabor>();
            if (labor != null)
            {
                TeleportHost(labor.transform.position + labor.transform.forward * 1.0f + Vector3.up * 1.1f);
                yield return new WaitForSeconds(0.3f);
                for (int i = 0; i < balance.jailLaborCount + 2 && smug.IsJailed; i++)
                {
                    smug.RequestJailLabor();
                    yield return new WaitForSeconds(0.15f);
                }
                Check(!smug.IsJailed, "作業を終えると釈放される");
            }
        }

        Check(netcodeWarnings.Count == 0, $"通しで NGO の警告なし（{netcodeWarnings.Count}件）" + (netcodeWarnings.Count > 0 ? "：" + netcodeWarnings[0] : ""));
        Log($"end: day {dm.CurrentDay} night {dm.IsNight} money {Money()}");
    }
}
#endif
