#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 作り直し2の仕組みの確認（Play Mode・ホストで GameRoom に入ってから）。結果は Temp/RoomiesTown/gimmick.txt。ゲームの値は変えない（お金は使う）。
/// ・スロット3台・ブラックジャック台：正面に立って視線の先の当たり判定で台が選ばれるか（PlayerInteract と同じ Raycast）、
///   サーバーの距離判定を通るか、実際に Interact を呼んでスロットが回る／ブラックジャックの手が配られるか。
/// ・牢屋：ServerSendToJail（逮捕の移送と同じ処理）で JailPoint へ移り、格子の外へ歩いて出られないか。作業台に視線が届くか。
/// ・メインの家：スポーン地点から階段を下りて通りへ出られるか（歩行確認の道すじ）。
/// </summary>
internal static partial class RoomiesTownRebuild
{
    static readonly StringBuilder gimmickLog = new StringBuilder();

    [MenuItem("Roomies/Art/Town/Gimmick Check (Play Mode)")]
    internal static void GimmickCheck()
    {
        if (!EditorApplication.isPlaying) { Debug.LogWarning("[Town] gimmick check works only in Play Mode"); return; }
        var cc = Object.FindObjectsByType<CharacterController>().FirstOrDefault(c => c.GetComponentInParent<PlayerMovement>() != null);
        if (cc == null) { Debug.LogWarning("[Town] no player"); return; }
        gimmickLog.Clear();
        var runner = new GameObject("Gimmick Check Runner").AddComponent<GimmickCheckRunner>();
        runner.StartCoroutine(Run(cc, runner));
    }

    class GimmickCheckRunner : MonoBehaviour { }

    static void Log(string s) { gimmickLog.AppendLine(s); }

    static IEnumerator Run(CharacterController cc, MonoBehaviour runner)
    {
        var earning = cc.GetComponentInParent<PlayerEarning>();
        Vector3 home = cc.transform.position;
        var money = SharedMoneyManager.Instance;
        Log($"money at start {(money != null ? money.CurrentMoney : -1)}");

        // ---- スロット
        foreach (var slot in Object.FindObjectsByType<SlotMachine>().OrderBy(s => s.transform.position.x))
        {
            var canvas = slot.GetComponentInChildren<Canvas>(true);
            var corners = new Vector3[4]; ((RectTransform)canvas.transform).GetWorldCorners(corners);
            Vector3 screen = (corners[0] + corners[2]) / 2;
            Vector3 stand = new Vector3(screen.x, 0, screen.z + 1.1f);
            Teleport(cc, stand + Vector3.up * (cc.height / 2 + .12f));
            yield return null;
            var eye = stand + Vector3.up * 1.6f;
            bool hitSlot = Physics.Raycast(eye, (screen - eye).normalized, out var hit, 3f) && hit.collider.GetComponentInParent<SlotMachine>() == slot;
            bool inRange = slot.IsWithinInteractionRange(cc.transform.position);
            int before = money != null ? money.CurrentMoney : 0;
            string text0 = ReelText(slot);
            slot.Interact(earning);
            yield return new WaitForSeconds(2.6f);
            int after = money != null ? money.CurrentMoney : 0;
            Log($"SLOT {slot.transform.parent?.name}/{slot.name}: ray hits slot {hitSlot} ({(hit.collider ? PathOf(hit.collider.transform) : "none")}), server range {inRange}, money {before} -> {after}, reels '{text0}' -> '{ReelText(slot)}'");
        }

        // ---- ブラックジャック
        var bj = Object.FindAnyObjectByType<BlackjackTable>();
        if (bj != null)
        {
            Vector3 stand = bj.transform.position + bj.transform.forward * 1.5f;
            stand.y = 0;
            Teleport(cc, stand + Vector3.up * (cc.height / 2 + .12f));
            yield return null;
            var eye = stand + Vector3.up * 1.6f;
            var target = bj.transform.position + Vector3.up * .95f;
            bool hitBj = Physics.Raycast(eye, (target - eye).normalized, out var hit, 3f) && hit.collider.GetComponentInParent<BlackjackTable>() == bj;
            Log($"BLACKJACK: ray hits table {hitBj} ({(hit.collider ? PathOf(hit.collider.transform) : "none")})");
            ulong me = cc.GetComponentInParent<Unity.Netcode.NetworkObject>().OwnerClientId;
            // 3 ラウンド遊び切る（2 ラウンド目はヒットしてからスタンド）。結果を見せる時間のあと待機に戻るまで待つ
            for (int round = 0; round < 3; round++)
            {
                for (int w = 0; w < 100 && !bj.IsIdle; w++) yield return new WaitForSeconds(.1f);
                int before = money != null ? money.CurrentMoney : 0;
                bj.Interact(earning);
                yield return new WaitForSeconds(1.2f);
                bool started = !bj.IsIdle;
                int afterBet = money != null ? money.CurrentMoney : 0;
                string hitInfo = "";
                if (round == 1 && bj.CanLocalPlayerStand(me)) { bj.Interact(earning); yield return new WaitForSeconds(1.2f); hitInfo = $" hit -> label '{bj.GetInteractionLabel(me)}',"; }
                bool stood = false;
                if (bj.CanLocalPlayerStand(me)) { bj.Stand(); stood = true; }
                float t = 0; while (!bj.IsIdle && t < 8f) { yield return new WaitForSeconds(.1f); t += .1f; }
                int after = money != null ? money.CurrentMoney : 0;
                Log($"BLACKJACK round {round + 1}: started {started}, money {before} -> {afterBet} (bet) ->{hitInfo} stood {stood}, back to idle {bj.IsIdle} after {t:F1}s, money after {after} (diff {after - before:+0;-0;0}), label '{bj.GetInteractionLabel(me)}'");
            }
        }

        // ---- 牢屋
        var smug = cc.GetComponentInParent<SmugglingPlayer>();
        var jailPoint = SmugglingJailPoint.Find();
        if (smug != null && jailPoint != null)
        {
            smug.ServerSendToJail();
            yield return new WaitForSeconds(.5f);
            var p = cc.transform.position;
            bool atJail = new Vector2(p.x - jailPoint.transform.position.x, p.z - jailPoint.transform.position.z).magnitude < .6f;
            Log($"JAIL: sent to JailPoint {atJail} (player {p}, point {jailPoint.transform.position})");
            // 作業台に視線が届くか
            var labor = Object.FindAnyObjectByType<SmugglingJailLabor>();
            if (labor != null)
            {
                var eye = cc.transform.position + Vector3.up * .6f;
                var t = labor.transform.position + Vector3.up * .8f;
                bool hitLabor = Physics.Raycast(eye, (t - eye).normalized, out var lh, 4f) && lh.collider.GetComponentInParent<SmugglingJailLabor>() == labor;
                Log($"JAIL: workbench reachable by look {hitLabor} ({(lh.collider ? PathOf(lh.collider.transform) : "none")})");
            }
            // 4方向へ歩いて外へ出られないか
            foreach (var dir in new[] { Vector3.back, Vector3.forward, Vector3.left, Vector3.right })
            {
                Teleport(cc, new Vector3(jailPoint.transform.position.x, p.y, jailPoint.transform.position.z));
                yield return null;
                for (int i = 0; i < 160; i++) { cc.Move(dir * .06f + Vector3.down * .05f); Physics.SyncTransforms(); }
                var q = cc.transform.position;
                bool inside = q.x > 4.9f && q.x < 11.9f && q.z > 15.05f && q.z < 22.25f;
                Log($"JAIL: walk {dir} -> stays inside {inside} at ({q.x:F2}, {q.y:F2}, {q.z:F2})");
            }
        }

        Teleport(cc, home);
        System.IO.Directory.CreateDirectory("Temp/RoomiesTown");
        System.IO.File.WriteAllText("Temp/RoomiesTown/gimmick.txt", gimmickLog.ToString());
        Debug.Log("[Town] gimmick check written");
        Object.Destroy(runner.gameObject);
    }

    static string ReelText(SlotMachine slot)
    {
        var t = slot.GetComponentsInChildren<TMPro.TextMeshProUGUI>(true).FirstOrDefault(x => x.name.StartsWith("'ResultText") || x.name.StartsWith("ResultText"));
        return t != null ? t.text : "?";
    }
}
#endif
