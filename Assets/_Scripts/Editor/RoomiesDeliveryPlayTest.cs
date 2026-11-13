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

    class Runner : MonoBehaviour { }

    [MenuItem("Roomies/Delivery/Play Test (Play Mode)")]
    static void Run()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[DeliveryTest] Play Mode で実行する"); return; }
        log.Clear(); moneyLines.Clear(); ok = ng = 0;
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

        // ---- 持って荷台へ（ホストの RPC 経路：拾う → 持ったまま動かす → 落とす）
        var player = HostPlayer();
        Vector3 board = truck.transform.TransformPoint(new Vector3(0f, 0f, -3.2f));
        TeleportHost(GroundAt(board) + Vector3.up * 1.1f);
        yield return new WaitForSeconds(0.3f);

        int looseBefore = Boxes().Count(b => !truck.ServerContains(b.NetworkObject));
        var b1 = Boxes().First(b => b != a);
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
        Check(looseAfter == looseBefore, $"積んだら集荷場に補充される（町の箱 {looseBefore} → {looseAfter}個）");

        // ---- 荷台に投げ込む（遠くから放物線で）
        var b2 = Boxes().First(b => b != a && b != b1 && !truck.ServerContains(b.NetworkObject));
        int b2Before = b2.CurrentValue;
        b2.GetComponent<CarryableObject>().ServerRelocate(truck.transform.TransformPoint(new Vector3(0f, 2.6f, -5.5f)));
        yield return new WaitForFixedUpdate();
        b2.GetComponent<Rigidbody>().linearVelocity = truck.transform.TransformDirection(new Vector3(0f, 2.5f, 5f));
        yield return new WaitForSeconds(2f);
        Check(truck.ServerContains(b2.NetworkObject), $"投げ込んだ箱が荷台に入った（¥{b2Before} → ¥{b2.CurrentValue}）");

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
        Check(earnDelta + foodPerPlayer == (b1InBed ? b1Value : 0),
            $"積んだ人（乗った）の稼ぎに記録される（稼ぎ {earnDelta:+#;-#;0} ＝ 自分で積んだ箱 ¥{b1Value} − 同じ瞬間の食費 ¥{foodPerPlayer}。投げ込んだ箱は誰も持っていないので記録なし）");
        Check(truck.BedCount == 0 && (b1 == null || !b1.IsSpawned), "発車で荷台は空になる");
        Check(b3 != null && b3.IsSpawned && c3.IsHeld, "手に持っていた箱は数えず、手に残る");
        float distHome = Object.FindObjectsByType<SpawnPoint>().Min(s => Vector3.Distance(s.transform.position, player.transform.position));
        Check(distHome < 2.5f, $"乗った人は家（スポーン地点）へ帰る（いちばん近いスポーン地点まで {distHome:F1}m）");
        c3.DropServerRpc(player.transform.position + player.transform.forward, Vector3.zero);
        yield return new WaitForSeconds(0.5f);

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
            SetRemaining(2f);
            yield return WaitUntil(() => dm.IsNight, 10f);
            yield return new WaitForSeconds(1f);
            int lines1 = moneyLines.Count(l => l.Contains("(Truck)"));
            Check(lines1 == lines0 + 1, $"置いていかれても、荷台の荷物はお金になる（¥{value}）");
            int dEarn = player.GetComponent<PlayerEarning>().GetEarning() - earnB;
            Check(dEarn <= 0, $"置いていかれた人の稼ぎには記録されない（稼ぎの増減 {dEarn}。同じ瞬間の夜イベントの食費だけ）");
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

        Log($"end: day {dm.CurrentDay} night {dm.IsNight} money {Money()}");
    }
}
#endif
