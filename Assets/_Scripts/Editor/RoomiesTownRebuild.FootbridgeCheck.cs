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
/// 歩道橋の確認（Play Mode 中だけ。ホストで GameRoom に入ってから）。結果は Temp/RoomiesTown/footbridge.txt。
/// a) 自分の CharacterController を Move で動かして、両方の階段とスロープを上り下りする。橋の上から横へ出ようとして止まるか
///    （歩く・跳ぶ・しゃがむ）。
/// b) 箱を持って（配達の通し確認と同じ RPC：PickupServerRpc → UpdateHeldPositionServerRpc → DropServerRpc）橋を渡り、荷台に落とす。
/// c) 橋の上から手すりの外へ箱を落とす（値段が下がるか）。小さな箱を橋の床から柵の間へ滑らせて、道へ落ちるか。
/// d) 発車させて、荷台の箱がお金になるか。
/// ゲームの値は変えない。発車を早めるために DayManager の残り時間だけをリフレクションで縮める（配達の通し確認と同じ）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    static readonly StringBuilder fbLog = new StringBuilder();
    static int fbOk, fbNg;
    static readonly List<string> fbMoney = new List<string>();

    class FootbridgeRunner : MonoBehaviour { }

    [MenuItem("Roomies/Art/Town/Footbridge Check (Play Mode)")]
    static void FootbridgeCheck()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Town] footbridge check works only in Play Mode"); return; }
        fbLog.Clear(); fbMoney.Clear(); fbOk = fbNg = 0;
        var runner = new GameObject("Footbridge Check Runner").AddComponent<FootbridgeRunner>();
        Object.DontDestroyOnLoad(runner.gameObject);
        runner.StartCoroutine(FbMain(runner));
    }

    static void FbCheck(bool cond, string what) { if (cond) fbOk++; else fbNg++; fbLog.AppendLine($"{(cond ? "OK " : "NG ")} {what}"); }

    static void FbOnLog(string message, string stack, LogType type)
    {
        if (message.StartsWith("[Money")) fbMoney.Add(message);
        if (type == LogType.Exception || type == LogType.Error) fbMoney.Add("ERROR: " + message.Split('\n')[0]);
    }

    static IEnumerator FbMain(MonoBehaviour runner)
    {
        Application.logMessageReceived += FbOnLog;
        var body = FbBody();
        while (true)
        {
            object cur;
            try { if (!body.MoveNext()) break; cur = body.Current; }
            catch (System.Exception e) { fbLog.AppendLine("EXCEPTION " + e); fbNg++; break; }
            yield return cur;
        }
        Application.logMessageReceived -= FbOnLog;
        System.IO.Directory.CreateDirectory("Temp/RoomiesTown");
        System.IO.File.WriteAllText("Temp/RoomiesTown/footbridge.txt", $"ok {fbOk}, ng {fbNg}\n{fbLog}\n---- money log ----\n{string.Join("\n", fbMoney)}");
        Debug.Log($"[Town] footbridge check ok {fbOk} ng {fbNg}");
        Object.Destroy(runner.gameObject);
    }

    // 上から下へ探して、いちばん上の床（橋の上なら橋の床）の高さ
    static float FbSurface(Vector2 xz, float from = 8f) =>
        Physics.RaycastAll(new Vector3(xz.x, from, xz.y), Vector3.down, from + 2f, ~0, QueryTriggerInteraction.Ignore)
            .Where(h => h.collider.GetComponentInParent<CharacterController>() == null && h.collider.GetComponentInParent<DeliveryItem>() == null)
            .Select(h => h.point.y).DefaultIfEmpty(0).Max();

    /// <summary>道すじを Move で歩く（同じフレームの中で）。着いたか・最高の高さ・最後の位置を返す。</summary>
    static StringBuilder jumpLog;

    static (bool reached, float maxY, Vector3 last, string near) FbWalk(CharacterController cc, Vector2[] pts, float startFrom = 8f, float jumpEvery = 0f)
    {
        jumpLog = jumpEvery > 0 ? new StringBuilder() : null;
        Teleport(cc, new Vector3(pts[0].x, FbSurface(pts[0], startFrom) + cc.height / 2 - cc.center.y + .05f, pts[0].y));
        float maxY = cc.transform.position.y;
        for (int i = 1; i < pts.Length; i++)
        {
            var target = new Vector3(pts[i].x, 0, pts[i].y);
            int steps = Mathf.CeilToInt(Vector2.Distance(pts[i - 1], pts[i]) / .08f) * 3;
            float vy = 0, t = 0;
            for (int s = 0; s < steps; s++)
            {
                var p = cc.transform.position; var d = target - new Vector3(p.x, 0, p.z);
                if (d.magnitude < .1f) break;
                t += .02f;
                if (jumpEvery > 0 && cc.isGrounded && t >= jumpEvery)
                {
                    vy = Mathf.Sqrt(2f * 18f * 1.5f); t = 0;   // PlayerMovement と同じ跳び（1.5m・重力 -18）
                    if (jumpLog != null && Physics.SphereCast(p + cc.center - Vector3.up * (cc.height / 2 - cc.radius - .3f), cc.radius * .9f, Vector3.down, out var gh, 3f, ~0, QueryTriggerInteraction.Ignore))
                    {
                        var touching = Physics.OverlapCapsule(p + cc.center + Vector3.up * (cc.height / 2 - cc.radius), p + cc.center - Vector3.up * (cc.height / 2 - cc.radius), cc.radius + .12f, ~0, QueryTriggerInteraction.Ignore)
                            .Where(h => !h.transform.IsChildOf(cc.transform)).Select(h => $"{h.name}@({h.bounds.center.x:F2},{h.bounds.max.y:F2},{h.bounds.center.z:F2})");
                        jumpLog.Append($" [jump at ({p.x:F2}, {p.y:F2}, {p.z:F2}) on {gh.collider.name} y {gh.point.y:F2}; touching {string.Join(", ", touching)}]");
                    }
                }
                else if (cc.isGrounded && vy <= 0) vy = -1f;
                else vy = Mathf.Max(-20f, vy - 18f * .02f);
                cc.Move(Vector3.ClampMagnitude(d, .08f) + Vector3.up * vy * .02f);
                Physics.SyncTransforms();
                maxY = Mathf.Max(maxY, cc.transform.position.y);
            }
            var q = cc.transform.position;
            if (Vector2.Distance(new Vector2(q.x, q.z), pts[i]) > .6f)
            {
                var hits = Physics.OverlapCapsule(q + Vector3.up * (cc.height / 2 - cc.radius), q - Vector3.up * (cc.height / 2 - cc.radius), cc.radius + .25f, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => !h.transform.IsChildOf(cc.transform)).Select(h => h.name).Distinct().Take(4);
                return (false, maxY, q, string.Join(" | ", hits));
            }
        }
        return (true, maxY, cc.transform.position, "");
    }

    static IEnumerator FbBody()
    {
        var nm = NetworkManager.Singleton;
        float waitEnd = Time.realtimeSinceStartup + 20f;
        while ((DayManager.Instance == null || !DayManager.Instance.IsSpawned || nm.LocalClient?.PlayerObject == null) && Time.realtimeSinceStartup < waitEnd) yield return null;
        var player = nm.LocalClient.PlayerObject;
        var cc = player.GetComponent<CharacterController>();
        var truck = Object.FindAnyObjectByType<DeliveryTruck>();
        var dm = DayManager.Instance;
        Vector3 home = cc.transform.position;
        float deckY = FbTop, railX = FbX + FbHalf + FbGirder / 2;
        fbLog.AppendLine($"day {dm.CurrentDay} night {dm.IsNight} remaining {dm.RemainingSeconds:F0}  bridge x {FbX} deck y {deckY} rail x ±{FbHalf + FbGirder / 2:F2}");
        yield return new WaitForSeconds(2f);

        // ---- a) 歩く
        var south = new Vector2(-27.4f, -7.5f); var deckS = new Vector2(FbX, -7.5f); var deckN = new Vector2(FbX, 7.5f);
        var north = new Vector2(FbX, 15.2f); var rampFoot = new Vector2(-5.0f, 7.5f);
        var walks = new (string name, Vector2[] pts, bool pass, bool needDeck, float from, float jump)[]
        {
            ("south stairs up -> deck -> north stairs down", new[] { south, deckS, deckN, north }, true, true, 8f, 0f),
            ("north stairs up -> deck -> south stairs down", new[] { north, deckN, deckS, south }, true, true, 8f, 0f),
            ("ramp up -> deck -> south stairs down", new[] { rampFoot, deckN, deckS, south }, true, true, 8f, 0f),
            ("south stairs up -> deck -> ramp down", new[] { south, deckS, deckN, rampFoot }, true, true, 8f, 0f),
            ("deck: walk off east side (should stop)", new[] { new Vector2(FbX, 0), new Vector2(FbX + 4f, 0) }, false, true, 8f, 0f),
            ("deck: walk off west side (should stop)", new[] { new Vector2(FbX, -2.3f), new Vector2(FbX - 4f, -2.3f) }, false, true, 8f, 0f),
            ("deck: jump off east side (should stop)", new[] { new Vector2(FbX, 1.3f), new Vector2(FbX + 4f, 1.3f) }, false, true, 8f, .35f),
            ("south landing: walk off south end (should stop)", new[] { new Vector2(FbX, -7.5f), new Vector2(FbX, -12f) }, false, true, 8f, 0f),
            ("south stairs: walk off sideways to the road (should stop)", new[] { south, new Vector2(-23.4f, -7.5f), new Vector2(-23.4f, -3f) }, false, true, 8f, 0f),
            ("ramp: walk off sideways to the sidewalk (should stop)", new[] { rampFoot, new Vector2(-12f, 7.5f), new Vector2(-12f, 3f) }, false, true, 8f, 0f),
            ("ramp: jump off sideways (should stop)", new[] { new Vector2(-10f, 7.5f), new Vector2(-10f, 3f) }, false, true, 8f, .35f),
            ("north stairs: walk off sideways (should stop)", new[] { north, new Vector2(FbX, 11f), new Vector2(FbX + 4f, 11f) }, false, true, 8f, 0f),
            ("under the deck along the south sidewalk", new[] { new Vector2(-30f, -5.2f), new Vector2(-10f, -5.2f) }, true, false, 2.5f, 0f),
            ("under the high part of the south stairs (walk past)", new[] { new Vector2(-21.8f, -5.8f), new Vector2(-21.8f, -8.9f) }, true, false, 2.5f, 0f),
        };
        foreach (var w in walks)
        {
            var (reached, maxY, last, near) = FbWalk(cc, w.pts, w.from, w.jump);
            bool onDeck = maxY > deckY + .8f;
            bool good = w.pass ? reached && (!w.needDeck || onDeck) : !reached && last.y > deckY * .3f;
            if (!w.pass && w.name.StartsWith("deck")) good = !reached && last.y > deckY + .5f && Mathf.Abs(last.x - FbX) < FbHalf + .5f && last.z > -FbLandOut - .3f;
            if (!w.pass && w.name.StartsWith("south landing")) good = !reached && last.y > deckY + .5f && last.z > -FbLandOut - .3f;
            FbCheck(good, $"{w.name}: {(reached ? "reached" : "stopped")} at ({last.x:F2}, {last.y:F2}, {last.z:F2}) max y {maxY:F2}{(near.Length > 0 ? "  near: " + near : "")}");
            if (jumpLog != null && jumpLog.Length > 0) fbLog.AppendLine("  jumps:" + jumpLog);
        }
        // しゃがみ（CharacterController の高さ 1m）でも柵の間を抜けない
        {
            float h0 = cc.height; var c0 = cc.center;
            cc.height = 1f; cc.center = new Vector3(0, -.5f, 0);
            var (reached, maxY, last, near) = FbWalk(cc, new[] { new Vector2(FbX, .45f), new Vector2(FbX + 4f, .45f) }, 8f, 0f);
            cc.height = h0; cc.center = c0;
            FbCheck(!reached && Mathf.Abs(last.x - FbX) < FbHalf + .5f, $"deck: crouched (height 1) walk off east side (should stop): stopped at ({last.x:F2}, {last.y:F2}, {last.z:F2})  near: {near}");
        }
        Teleport(cc, home);
        yield return new WaitForSeconds(.5f);

        // ---- b) 箱を持って渡る
        var boxes = Object.FindObjectsByType<DeliveryItem>().Where(b => b.IsSpawned && !b.IsBroken && b.name.Contains("(Clone)") && !truck.ServerContains(b.NetworkObject)).ToList();
        var b1 = boxes.FirstOrDefault(b => !b.IsRareItem()) ?? boxes.First();
        var c1 = b1.GetComponent<CarryableObject>();
        var col1 = b1.GetComponentInChildren<Collider>();
        fbLog.AppendLine($"box b: {(b1.IsRareItem() ? "rare" : "normal")} ¥{b1.CurrentValue} size {col1.bounds.size}");
        Teleport(cc, new Vector3(south.x, FbSurface(south, 2.5f) + 1.1f, south.y));
        cc.transform.rotation = Quaternion.LookRotation(Vector3.right);
        yield return new WaitForSeconds(.3f);
        c1.ServerRelocate(cc.transform.position + Vector3.right * 1.2f);
        yield return new WaitForSeconds(.8f);
        c1.PickupServerRpc(nm.LocalClientId, player.NetworkObjectId);
        yield return new WaitForSeconds(.2f);
        FbCheck(c1.IsHeldBy(nm.LocalClientId), "b) picked up a box at the south stairs");
        int bStart = b1.CurrentValue;
        var route = new[] { deckS, deckN, rampFoot, new Vector2(-3.8f, 12f), new Vector2(-3.8f, 15.0f) };
        float bMax = 0; int dropped = 0; string bStop = null; var dir = Vector3.right;
        foreach (var target in route)
        {
            float vy = 0, until = Time.realtimeSinceStartup + 25f;
            while (Time.realtimeSinceStartup < until)
            {
                var p = cc.transform.position; var d = new Vector3(target.x - p.x, 0, target.y - p.z);
                if (d.magnitude < .15f) break;
                vy = cc.isGrounded ? -1f : Mathf.Max(-20f, vy - 18f * Time.deltaTime);
                var step = Vector3.ClampMagnitude(d, 4.5f * Time.deltaTime);
                cc.Move(step + Vector3.up * vy * Time.deltaTime);
                // 向きは PlayerMovement がカメラに合わせて戻すので、歩く向きで持つ
                if (step.sqrMagnitude > 1e-6f) dir = step.normalized;
                c1.UpdateHeldPositionServerRpc(cc.transform.position + dir * 1.4f + Vector3.up * .3f, Quaternion.LookRotation(dir));
                bMax = Mathf.Max(bMax, cc.transform.position.y);
                if (!c1.IsHeldBy(nm.LocalClientId)) dropped++;
                yield return null;
            }
            var q = cc.transform.position;
            if (new Vector2(q.x - target.x, q.z - target.y).magnitude > .6f) { bStop = $"stopped at ({q.x:F1}, {q.y:F1}, {q.z:F1}) going to {target}"; break; }
        }
        FbCheck(bStop == null && bMax > deckY + .8f, $"b) walked across the bridge holding the box (max y {bMax:F2}){(bStop != null ? " " + bStop : "")}");
        FbCheck(dropped == 0 && c1.IsHeld, $"b) the box stayed held the whole way (frames not held {dropped}), ¥{bStart} → ¥{b1.CurrentValue}");
        Vector3 overBed = truck.transform.TransformPoint(new Vector3(0f, 1.9f, -1.4f));
        c1.UpdateHeldPositionServerRpc(overBed, truck.transform.rotation);
        yield return new WaitForSeconds(.4f);
        int bedBefore = truck.BedCount, bHeld = b1.CurrentValue;
        c1.DropServerRpc(overBed, Vector3.zero);
        yield return new WaitForSeconds(1.5f);
        FbCheck(truck.ServerContains(b1.NetworkObject), $"b) dropped into the truck bed (bed {bedBefore} → {truck.BedCount}, ¥{bHeld} → ¥{b1.CurrentValue}, bed value ¥{truck.BedValue})");

        // ---- c) 橋の上から手すりの外へ落とす
        var free = Object.FindObjectsByType<DeliveryItem>().Where(b => b.IsSpawned && !b.IsBroken && b != b1 && !truck.ServerContains(b.NetworkObject)).ToList();
        var b2 = free.FirstOrDefault(b => !b.IsRareItem() && b.name.Contains("(Clone)")) ?? free.First();
        var c2 = b2.GetComponent<CarryableObject>();
        Teleport(cc, new Vector3(FbX, deckY + 1.1f, 0f));
        cc.transform.rotation = Quaternion.LookRotation(Vector3.right);
        yield return new WaitForSeconds(.3f);
        c2.ServerRelocate(cc.transform.position + Vector3.right * .6f + Vector3.up * .2f);
        yield return new WaitForSeconds(.8f);
        c2.PickupServerRpc(nm.LocalClientId, player.NetworkObjectId);
        yield return new WaitForSeconds(.2f);
        bool held2 = c2.IsHeldBy(nm.LocalClientId);
        // 持った位置（ふつうに持つと 2m 先なので、手すりの外になる）
        var outside = new Vector3(railX + 1.0f, deckY + 1.0f, 0f);
        c2.UpdateHeldPositionServerRpc(outside, cc.transform.rotation);
        yield return new WaitForSeconds(.3f);
        int v2 = b2.CurrentValue;
        c2.DropServerRpc(outside, Vector3.right);
        yield return new WaitForSeconds(3f);
        // 割れて消えたら値段 0 として数える
        bool broken2 = b2 == null || !b2.IsSpawned;
        var p2 = broken2 ? Vector3.zero : b2.transform.position;
        int after2 = broken2 ? 0 : b2.CurrentValue;
        FbCheck(held2 && p2.y < 1.5f && after2 < v2,
            $"c) held box dropped over the east railing fell to the road: ({p2.x:F1}, {p2.y:F2}, {p2.z:F1}) ¥{v2} → ¥{after2}{(broken2 ? " (broken)" : "")}");

        // 小さな箱を床から柵の間へ滑らせる（本当にすき間を抜けるか）。一度持ってから
        var bars = Object.FindObjectsByType<BoxCollider>().Where(c => c.name == "Rail bar collider" && Mathf.Abs(c.transform.position.x - railX) < .05f && Mathf.Abs(c.transform.position.z) < 3f)
            .Select(c => c.transform.position.z).OrderBy(z => z).ToList();
        float gapZ = 0;
        for (int i = 0; i + 1 < bars.Count; i++) if (bars[i] <= 0 && bars[i + 1] > 0) gapZ = (bars[i] + bars[i + 1]) / 2;
        fbLog.AppendLine($"east rail bars near z 0: {string.Join(", ", bars.Select(z => z.ToString("F2")))}  → gap at z {gapZ:F2}");
        foreach (bool small in new[] { true, false })
        {
            var pool = Object.FindObjectsByType<DeliveryItem>().Where(b => b.IsSpawned && !b.IsBroken && b != b1 && b != b2 && !truck.ServerContains(b.NetworkObject)).ToList();
            var b3 = small ? pool.OrderBy(b => b.GetComponentInChildren<Collider>().bounds.size.magnitude).First() : pool.FirstOrDefault(b => !b.IsRareItem());
            if (b3 == null) { fbLog.AppendLine("（試す箱がない）"); continue; }
            var c3 = b3.GetComponent<CarryableObject>();
            var size = b3.GetComponentInChildren<Collider>().bounds.size;
            // 持ったことのある箱にする（橋の上で一度持って置く）
            Teleport(cc, new Vector3(FbX - .5f, deckY + 1.1f, gapZ + 2.5f));
            yield return new WaitForSeconds(.2f);
            c3.ServerRelocate(cc.transform.position + Vector3.right * .8f);
            yield return new WaitForSeconds(.8f);
            c3.PickupServerRpc(nm.LocalClientId, player.NetworkObjectId);
            yield return new WaitForSeconds(.2f);
            bool armed = c3.IsHeldBy(nm.LocalClientId);
            c3.DropServerRpc(new Vector3(FbX, deckY + .6f, gapZ + 2.5f), Vector3.zero);
            yield return new WaitForSeconds(1.2f);
            int v3 = b3.CurrentValue;
            c3.ServerRelocate(new Vector3(FbX + .2f, deckY + size.y / 2 + .05f, gapZ));
            yield return new WaitForFixedUpdate();
            var rb3 = b3.GetComponent<Rigidbody>();
            rb3.rotation = Quaternion.identity; rb3.angularVelocity = Vector3.zero;
            rb3.linearVelocity = new Vector3(4f, .8f, 0f);
            // 割れて消える前の最後の位置を取っておく（レアの箱は橋の高さから落ちると割れることがある）
            var p3 = b3.transform.position; float minY3 = p3.y, maxX3 = p3.x;
            for (float w = 0; w < 3f; w += Time.deltaTime)
            {
                if (b3 == null || !b3.IsSpawned) break;
                p3 = b3.transform.position; minY3 = Mathf.Min(minY3, p3.y); maxX3 = Mathf.Max(maxX3, p3.x);
                yield return null;
            }
            bool broken3 = b3 == null || !b3.IsSpawned;
            fbLog.AppendLine($"  path: furthest x {maxX3:F2} (rail x {railX:F2}), lowest y {minY3:F2}, last seen ({p3.x:F2}, {p3.y:F2}, {p3.z:F2})");
            int after3 = broken3 ? 0 : b3.CurrentValue;
            bool through = p3.x > railX + .3f && p3.y < 1.5f;
            string res = $"c) {(small ? "small" : "normal")} box (size {size.x:F2}×{size.y:F2}×{size.z:F2}, held once {armed}) slid at the rail gap: ended ({p3.x:F1}, {p3.y:F2}, {p3.z:F1}) {(broken3 ? "broke" : through ? "fell through to the road" : "stayed on the bridge")}, ¥{v3} → ¥{after3}";
            if (small) FbCheck(through && after3 < v3, res);
            else fbLog.AppendLine("info " + res);
            if (!through && !broken3) c3.ServerRelocate(GroundSpot(new Vector3(-24f, 0, -11f)));
            yield return new WaitForSeconds(.5f);
        }

        // ---- d) 発車させる
        Teleport(cc, new Vector3(-3.8f, FbSurface(new Vector2(-3.8f, 14.5f), 2.5f) + 1.1f, 14.5f));
        yield return new WaitForSeconds(.3f);
        int bedCount = truck.BedCount, bedValue = truck.BedValue;
        bool b1In = truck.ServerContains(b1.NetworkObject);
        fbMoney.Add("---- depart ----");
        var f = typeof(DayManager).GetField("remainingTime", BindingFlags.NonPublic | BindingFlags.Instance);
        ((NetworkVariable<float>)f.GetValue(dm)).Value = 11.5f;
        float end = Time.realtimeSinceStartup + 25f;
        while (!dm.IsNight && Time.realtimeSinceStartup < end)
        {
            bedCount = truck.BedCount; bedValue = truck.BedValue;
            if (b1 != null && b1.IsSpawned) b1In = truck.ServerContains(b1.NetworkObject);
            yield return null;
        }
        yield return new WaitForSeconds(1f);
        var truckLines = fbMoney.SkipWhile(l => l != "---- depart ----").Where(l => l.Contains("(Truck)")).ToList();
        int credited = truckLines.Sum(l => int.Parse(System.Text.RegularExpressions.Regex.Match(l, @"\+(\d+)R").Groups[1].Value));
        FbCheck(dm.IsNight && b1In && credited == bedValue && truckLines.Count == bedCount,
            $"d) truck departed with the box carried over the bridge (bed {bedCount} ¥{bedValue}, bridge box in bed {b1In} → credited {truckLines.Count} ¥{credited}, night {dm.IsNight})");
        fbLog.AppendLine($"end: day {dm.CurrentDay} night {dm.IsNight}");
    }

    static Vector3 GroundSpot(Vector3 p) => new Vector3(p.x, FbSurface(new Vector2(p.x, p.z), 2.5f) + .6f, p.z);
}
#endif
