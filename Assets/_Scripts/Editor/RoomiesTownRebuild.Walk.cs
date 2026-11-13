#if UNITY_EDITOR
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 町の歩行確認（Play Mode 中だけ）：自分のプレイヤーの CharacterController を、道すじに沿って実際に Move で動かし、
/// 止まった所とぶつかった当たり判定を Temp/RoomiesTown/walk.txt に書く。終わったら元の位置へ戻す。
/// スポーン地点に当たり判定が重なっていないかも調べる。ゲームの値は変えない。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    // 道すじ：名前, 通る点（x, z。高さは地面から）, 最後まで行けるはずか
    static readonly (string name, Vector2[] points, bool shouldPass)[] Routes =
    {
        ("south sidewalk", new[] { new Vector2(-38, -5.2f), new Vector2(38, -5.2f) }, true),
        ("north sidewalk", new[] { new Vector2(-38, 5.2f), new Vector2(38, 5.2f) }, true),
        ("road", new[] { new Vector2(-38, 0), new Vector2(38, 0) }, true),
        ("CORNER STORE front apron", new[] { new Vector2(19.3f, -7.6f), new Vector2(33f, -7.6f) }, true),
        ("NEIGHBORHOOD WORKS front apron", new[] { new Vector2(-13.5f, -8.1f), new Vector2(-2.5f, -8.1f) }, true),
        ("CORNER STORE door (body is solid)", new[] { new Vector2(26.1f, -6f), new Vector2(26.1f, -9.5f) }, false),
        ("EXPRESS loading dock", new[] { new Vector2(-27.5f, -5.5f), new Vector2(-27.5f, -17.2f) }, true),
        ("gap between WORKS and EXPRESS", new[] { new Vector2(-16.3f, -5.5f), new Vector2(-16.3f, -21f) }, true),
        ("gap between arcade and CORNER", new[] { new Vector2(17.6f, -5.5f), new Vector2(17.6f, -21f) }, true),
        ("behind the shops", new[] { new Vector2(-36f, -22f), new Vector2(36f, -22f) }, true),
        ("south boundary (should stop)", new[] { new Vector2(0f, -22f), new Vector2(0f, -27f) }, false),
        ("house stairs to the 2F door", new[] { new Vector2(14.2f, 9.5f), new Vector2(14.2f, 12.8f), new Vector2(20.5f, 12.8f), new Vector2(20.5f, 14.8f) }, true),
        ("house porch under the landing", new[] { new Vector2(21.0f, 8.5f), new Vector2(21.0f, 12.9f) }, true),
        ("house stair side rail (should stop)", new[] { new Vector2(16.5f, 10.5f), new Vector2(16.5f, 13.0f) }, false),
        ("delivery zone", new[] { new Vector2(-4f, 6f), new Vector2(-4f, 18f) }, true),
        ("east lawn to ONE MORE ROCK side", new[] { new Vector2(27f, 6f), new Vector2(27f, 9.5f), new Vector2(35f, 9.5f) }, true),
        ("casino entrance to blackjack", new[] { new Vector2(7.0f, -5.5f), new Vector2(7.0f, -8.6f), new Vector2(5.2f, -9.3f), new Vector2(5.2f, -14.6f), new Vector2(6.2f, -14.6f) }, true),
        ("casino entrance to bar and cashier", new[] { new Vector2(8.2f, -5.5f), new Vector2(8.2f, -9.4f), new Vector2(2.0f, -9.4f), new Vector2(2.0f, -15.0f), new Vector2(10.5f, -15.0f), new Vector2(14.0f, -16.2f) }, true),
        ("casino entrance to slot row", new[] { new Vector2(7.8f, -5.5f), new Vector2(7.8f, -12.9f), new Vector2(14.2f, -12.9f) }, true),
        ("casino inside back out to street", new[] { new Vector2(7.8f, -13.0f), new Vector2(7.8f, -5.5f) }, true),
        ("casino right wall (should stop)", new[] { new Vector2(14f, -17.8f), new Vector2(19f, -17.8f) }, false),
        ("casino left wall (should stop)", new[] { new Vector2(2f, -14f), new Vector2(-3f, -14f) }, false),
        ("jail entrance to visitor corridor", new[] { new Vector2(2.85f, 10.5f), new Vector2(2.85f, 14.3f), new Vector2(11.5f, 14.3f) }, true),
        ("jail lobby to counter", new[] { new Vector2(2.85f, 10.5f), new Vector2(2.85f, 18.6f) }, true),
        ("behind jail to dealer", new[] { new Vector2(.3f, 18f), new Vector2(.3f, 23.3f), new Vector2(10.2f, 23.3f) }, true),
        ("west lawn", new[] { new Vector2(-30f, 6.5f), new Vector2(-30f, 20f), new Vector2(-12f, 20f) }, true),
    };

    /// <summary>Play Mode 中：ロビーと同じく、ホストを始めて GameRoom を読み込む（SteamLobby の StartHostRoutine と同じ呼び方）。</summary>
    [MenuItem("Roomies/Art/Town/Walk - Enter GameRoom As Host (Play Mode)")]
    internal static void EnterGameRoomAsHost()
    {
        if (!EditorApplication.isPlaying) return;
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null) { Debug.LogWarning("[Town] no NetworkManager"); return; }
        if (!nm.IsListening) nm.StartHost();
        var status = nm.SceneManager.LoadScene("GameRoom", UnityEngine.SceneManagement.LoadSceneMode.Single);
        Debug.Log("[Town] enter GameRoom: " + status);
    }

    /// <summary>Play Mode 中：自分のプレイヤーを通り（CORNER STORE の向かいの歩道）へ移し、店の並びの方を向かせる（重さを町で測るため）。</summary>
    [MenuItem("Roomies/Art/Town/Walk - Stand On Street (Play Mode)")]
    internal static void StandOnStreet()
    {
        if (!EditorApplication.isPlaying) return;
        var cc = Object.FindObjectsByType<CharacterController>().FirstOrDefault(c => c.GetComponentInParent<PlayerMovement>() != null);
        if (cc == null) return;
        Teleport(cc, new Vector3(12f, 1.15f, 5.2f));
        cc.transform.rotation = Quaternion.LookRotation(new Vector3(1f, 0, -1f));
        Debug.Log("[Town] stand on street");
    }

    [MenuItem("Roomies/Art/Town/Walk Check (Play Mode)")]
    internal static void WalkCheck()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Town] walk check works only in Play Mode"); return; }
        var all = Object.FindObjectsByType<CharacterController>();
        var cc = all.FirstOrDefault(c => c.GetComponentInParent<PlayerMovement>() != null || c.GetComponentInChildren<PlayerMovement>() != null) ?? all.FirstOrDefault();
        if (cc == null)
        {
            var nm = Object.FindAnyObjectByType<Unity.Netcode.NetworkManager>();
            Debug.LogWarning($"[Town] no player: controllers {all.Length}, network {(nm == null ? "none" : nm.IsListening ? "listening" : "not started")}");
            return;
        }
        var sb = new StringBuilder();
        Vector3 home = cc.transform.position;
        {
            sb.AppendLine($"debug: player {PathOf(cc.transform)} layer {LayerMask.LayerToName(cc.gameObject.layer)} pos {home} enabled {cc.enabled} center {cc.center} height {cc.height} radius {cc.radius} scene {cc.gameObject.scene.name}");
            foreach (var xz in new[] { new Vector2(0, -5.2f), new Vector2(0, 0), new Vector2(-4, 18), new Vector2(26, -7.6f) })
            {
                var hits = Physics.RaycastAll(new Vector3(xz.x, 30, xz.y), Vector3.down, 60, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance)
                    .Select(x => $"{PathOf(x.collider.transform)}[{x.collider.GetType().Name} L{x.collider.gameObject.layer} ign {Physics.GetIgnoreLayerCollision(cc.gameObject.layer, x.collider.gameObject.layer)}] y {x.point.y:F2}");
                sb.AppendLine($"debug ray {xz}: {string.Join(" | ", hits)}");
            }
        }
        int ok = 0, ng = 0;
        foreach (var (name, points, shouldPass) in Routes)
        {
            Teleport(cc, new Vector3(points[0].x, Ground(points[0]) + cc.height / 2 + .05f, points[0].y));
            Vector3 last = cc.transform.position; string blocked = null;
            for (int i = 1; i < points.Length && blocked == null; i++)
            {
                var target = new Vector3(points[i].x, 0, points[i].y);
                int steps = Mathf.CeilToInt(Vector2.Distance(points[i - 1], points[i]) / .08f) * 3;
                float vy = 0;
                for (int s = 0; s < steps; s++)
                {
                    var p = cc.transform.position; var d = target - new Vector3(p.x, 0, p.z);
                    if (d.magnitude < .1f) break;
                    vy = cc.isGrounded ? -1f : Mathf.Max(-6f, vy - 9.81f * .02f);
                    var flags = cc.Move(Vector3.ClampMagnitude(d, .08f) + Vector3.up * vy * .02f);
                    Physics.SyncTransforms();
                }
                var q = cc.transform.position;
                if (Vector2.Distance(new Vector2(q.x, q.z), points[i]) > .6f)
                {
                    var hits = Physics.OverlapCapsule(q + Vector3.up * (cc.height / 2 - cc.radius), q - Vector3.up * (cc.height / 2 - cc.radius), cc.radius + .25f, ~0, QueryTriggerInteraction.Ignore)
                        .Where(h => h.transform != cc.transform && !h.transform.IsChildOf(cc.transform)).Select(h => PathOf(h.transform)).Distinct().Take(4);
                    blocked = $"stopped at ({q.x:F1}, {q.y:F1}, {q.z:F1}) near: {string.Join(" | ", hits)}";
                }
                last = q;
            }
            bool pass = blocked == null;
            bool good = pass == shouldPass;
            if (good) ok++; else ng++;
            sb.AppendLine($"{(good ? "OK " : "NG ")} {name}: {(pass ? $"reached ({last.x:F1}, {last.y:F1}, {last.z:F1})" : blocked)}{(shouldPass ? "" : "  [expected to stop]")}");
        }
        // 配達ゾーンに入れたか
        var zone = GameObject.Find("DeliveryZone");
        if (zone != null) sb.AppendLine($"delivery zone trigger: {(zone.GetComponent<Collider>()?.isTrigger == true ? "trigger ok" : "missing")} at {zone.GetComponent<Collider>()?.bounds.center}");
        // スポーン地点に当たり判定が重なっていないか
        foreach (var sp in Object.FindObjectsByType<Transform>().Where(t => t.name.StartsWith("PlayerSpawn") || t.name == "SpawnPoint"))
        {
            // プレイヤーの CharacterController は中心が足元から1m上（center 0・高さ2）で、スポーン地点に中心が来る
            var p = sp.position + cc.center;
            float half = cc.height / 2 - cc.radius;
            var over = Physics.OverlapCapsule(p + Vector3.up * half, p - Vector3.up * (half - .02f), cc.radius - .02f, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => !h.transform.IsChildOf(cc.transform)).Select(h => PathOf(h.transform)).ToArray();
            sb.AppendLine($"spawn {sp.name} ({sp.position.x:F1}, {sp.position.y:F1}, {sp.position.z:F1}): {(over.Length == 0 ? "clear" : "overlaps " + string.Join(" | ", over))}");
        }
        Teleport(cc, home);
        sb.Insert(0, $"routes ok {ok}, ng {ng}\n");
        System.IO.Directory.CreateDirectory("Temp/RoomiesTown");
        System.IO.File.WriteAllText("Temp/RoomiesTown/walk.txt", sb.ToString());
        Debug.Log("[Town] walk: " + sb.ToString().Split('\n')[0]);
    }

    static float Ground(Vector2 xz)
    {
        // 屋根の下から探す（屋根の上に置かないように）。家の2階などの高い所から始める道すじはない
        return Physics.Raycast(new Vector3(xz.x, 2.5f, xz.y), Vector3.down, out var hit, 10, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y : 0;
    }

    static void Teleport(CharacterController cc, Vector3 pos)
    {
        cc.enabled = false; cc.transform.position = pos; Physics.SyncTransforms(); cc.enabled = true;
    }

    static string PathOf(Transform t) { var s = t.name; for (var x = t.parent; x != null; x = x.parent) s = x.name + "/" + s; return s; }
}
#endif
