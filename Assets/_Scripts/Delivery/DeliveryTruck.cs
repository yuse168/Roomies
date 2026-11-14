using System.Collections.Generic;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 配達トラック。荷台に積んだ荷物は、朝の終わりの「発車」ではじめてお金になる。
///
/// ・荷台はルートの BoxCollider（Is Trigger）。中にある箱をサーバーが数える。
///   クライアントは「積んだ」と申告できない（数えるのはサーバーの当たり判定だけ）。
/// ・発車は朝→夜に切り替わる瞬間（DayManager の残り時間が 0）。そのとき
///   ・荷台にあって誰も持っていない箱の、いまの値段を共同口座へ入れる（1箱ずつ DeliveryReward）
///   ・トラックの近く（GameBalance.truckBoardRadius）にいる人は「乗った」：家へ帰る（朝と同じリスポーン）
///   ・乗れなかった人は町に置いていかれる。その人が積んだ箱の稼ぎは個人の記録に入らない
///     （お金そのものは共同口座に入る）。手に持っていた箱は数えない
/// ・残り時間の知らせは GameBalance.truckWarnSeconds の秒数で全員へ出す。
///
/// 規約（AGENTS.md）：見た目は Visual の下、看板の文字は Anchor_Sign の下の TMP を参照する。
/// 置き直しはメニュー Roomies/Delivery/Build Truck Prefab と Place Truck In Open Scene。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class DeliveryTruck : NetworkBehaviour
{
    [Header("参照（プレハブ内）")]
    [Tooltip("乗った・乗らないを測る中心。荷台の後ろあたり")]
    [SerializeField] private Transform boardAnchor;
    [Tooltip("発車までの時間と積み荷の合計を出す看板の文字")]
    [SerializeField] private TMP_Text signLabel;

    [Header("参照（シーン）")]
    [Tooltip("荷物を出す集荷場。積んだら補充してもらう")]
    [SerializeField] private DeliveryZone deliveryZone;

    [Header("走り去る（見た目だけ）")]
    [Tooltip("動かす軸（Pivot_Drive）。見た目と看板はこの下。荷台の判定（ルート）は動かさない")]
    [SerializeField] private Transform drivePivot;
    [Tooltip("発車してから動き出すまでの秒数")]
    [SerializeField, Min(0f)] private float driveStartDelay = 0.8f;
    [Tooltip("曲がり終えてから消えるまでに、まっすぐ走る距離（m）")]
    [SerializeField, Min(0f)] private float driveStraightDistance = 20f;
    [Tooltip("曲がる半径（m）。正の値で左へ、負の値で右へ曲がる")]
    [SerializeField] private float driveTurnRadius = 3f;
    [Tooltip("動き出してから消えるまでの秒数（だんだん速くなる）")]
    [SerializeField, Min(0.5f)] private float driveSeconds = 5f;

    [Header("音")]
    [SerializeField] private string warnSoundKey = "mining/warning";
    [SerializeField] private string departSoundKey = "delivery/complete";
    [SerializeField] private string loadSoundKey = "money/gain";

    // サーバー：荷台に入っている箱と、その箱の当たり判定
    private readonly Dictionary<NetworkObject, HashSet<Collider>> inBed = new();
    private readonly List<NetworkObject> scratch = new();

    // 全員：看板に出す積み荷の合計（サーバーだけが書く）
    private readonly NetworkVariable<int> bedValue = new(0);
    private readonly NetworkVariable<int> bedCount = new(0);

    // 全員：発車して出かけているか（サーバーだけが書く）。見た目の動きは各自の画面で動かす
    private readonly NetworkVariable<bool> away = new(false);
    private float driveTime;
    public bool IsAway => away.Value;

    private int nextWarnIndex;
    private bool sawMorning;        // サーバー：朝を見たか（朝→夜に変わった瞬間に発車する）
    private float nextTallyTime;

    public int BedValue => bedValue.Value;
    public int BedCount => bedCount.Value;

    public override void OnNetworkSpawn()
    {
        // 出かけている途中で入った人は、もう走り去ったあとの状態から
        driveTime = away.Value ? driveStartDelay + driveSeconds : 0f;
        ApplyDrivePose();
    }

    public override void OnNetworkDespawn()
    {
        inBed.Clear();
    }

    // ---- サーバー：荷台に入った・出た ----

    private void OnTriggerEnter(Collider other)
    {
        if (!IsServer || away.Value) return;    // 出かけている間は積めない
        NetworkObject box = BoxOf(other);
        if (box == null) return;

        bool isNew = !inBed.TryGetValue(box, out var colliders);
        if (isNew)
        {
            colliders = new HashSet<Collider>();
            inBed.Add(box, colliders);
        }
        colliders.Add(other);

        if (isNew)
        {
            LoadedRpc(box.transform.position);
            if (deliveryZone != null) deliveryZone.ServerRefill();
            ServerTally();
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;
        NetworkObject box = BoxOf(other);
        if (box == null || !inBed.TryGetValue(box, out var colliders)) return;

        colliders.Remove(other);
        if (colliders.Count > 0) return;

        inBed.Remove(box);
        ServerTally();
    }

    private static NetworkObject BoxOf(Collider other)
    {
        var item = other.GetComponentInParent<DeliveryItem>();
        if (item == null || item.IsBroken) return null;
        var box = item.NetworkObject;
        return box != null && box.IsSpawned ? box : null;
    }

    /// <summary>この箱が荷台にあるか（サーバーのみ。集荷場の補充に使う）。</summary>
    public bool ServerContains(NetworkObject box)
    {
        return box != null && inBed.ContainsKey(box);
    }

    private void Prune()
    {
        scratch.Clear();
        foreach (var pair in inBed)
        {
            var box = pair.Key;
            pair.Value.RemoveWhere(c => c == null || !c.enabled || !c.gameObject.activeInHierarchy);
            if (box == null || !box.IsSpawned || pair.Value.Count == 0) scratch.Add(box);
        }
        foreach (var box in scratch) inBed.Remove(box);
    }

    /// <summary>荷台の合計を数え直して看板へ送る（サーバーのみ）。</summary>
    private void ServerTally()
    {
        Prune();
        int value = 0, count = 0;
        foreach (var box in inBed.Keys)
        {
            var item = box.GetComponent<DeliveryItem>();
            if (item == null || item.IsBroken) continue;
            var carry = box.GetComponent<CarryableObject>();
            if (carry != null && carry.IsHeld) continue;     // 手に持っているうちは積んだことにしない
            value += item.CurrentValue;
            count++;
        }
        bedValue.Value = value;
        bedCount.Value = count;
    }

    // ---- サーバー：残り時間の知らせ ----

    private void Update()
    {
        UpdateSign();
        UpdateDrive();

        if (!IsServer || !IsSpawned) return;

        // 荷台の中で値段が下がることもあるので、ときどき数え直す
        if (Time.time >= nextTallyTime)
        {
            nextTallyTime = Time.time + 0.5f;
            ServerTally();
        }

        DayManager dm = DayManager.Instance;
        if (dm == null || !dm.IsSpawned) return;

        // 朝→夜に変わったら発車。夜→朝で知らせの順番を戻す
        if (dm.IsNight)
        {
            if (sawMorning)
            {
                sawMorning = false;
                ServerDepart();
            }
            return;
        }
        if (!sawMorning)
        {
            sawMorning = true;
            nextWarnIndex = 0;
            if (away.Value)
            {
                // 朝になったら元の場所に戻る。出かけている間に荷台の場所へ置かれた箱も数える
                away.Value = false;
                ServerRescanBed();
            }
        }

        int[] warns = GameBalance.Current.truckWarnSeconds;
        if (warns == null || nextWarnIndex >= warns.Length) return;

        float remaining = dm.RemainingSeconds;
        if (remaining > warns[nextWarnIndex]) return;

        // 朝の途中から始めた・巻き戻したときに、まとめて鳴らない
        while (nextWarnIndex + 1 < warns.Length && remaining <= warns[nextWarnIndex + 1]) nextWarnIndex++;

        // 知らせるのは実際の残り秒数（途中から始めたときに「60秒」と言って10秒で出ないように）
        int seconds = Mathf.Max(1, Mathf.CeilToInt(remaining));
        nextWarnIndex++;
        WarnRpc(seconds, bedCount.Value, bedValue.Value);
    }

    /// <summary>発車：荷台の荷物をお金にして、乗った人を家へ帰す（サーバーのみ）。</summary>
    public void ServerDepart()
    {
        if (!IsServer) return;
        Prune();

        var nm = NetworkManager.Singleton;
        var money = SharedMoneyManager.Instance;

        // 1) 乗った人・置いていかれた人
        var aboard = new HashSet<ulong>();
        var leftBehind = new List<string>();
        float radius = GameBalance.Current.truckBoardRadius;
        Vector3 center = boardAnchor != null ? boardAnchor.position : transform.position;

        if (nm != null)
        {
            foreach (var client in nm.ConnectedClientsList)
            {
                var player = client.PlayerObject;
                if (player == null) continue;

                var smuggling = player.GetComponent<SmugglingPlayer>();
                bool jailed = smuggling != null && smuggling.IsJailed;

                Vector3 d = player.transform.position - center;
                d.y = 0f;
                if (!jailed && d.sqrMagnitude <= radius * radius)
                {
                    aboard.Add(client.ClientId);
                }
                else if (!jailed)
                {
                    var nameDisplay = player.GetComponent<PlayerNameDisplay>();
                    leftBehind.Add(nameDisplay != null ? nameDisplay.GetPlayerName() : "だれか");
                }
            }
        }

        // 2) 荷台の荷物をお金にする（持たれている箱・割れた箱は数えない）
        int total = 0, count = 0;
        scratch.Clear();
        scratch.AddRange(inBed.Keys);
        foreach (var box in scratch)
        {
            if (box == null || !box.IsSpawned) continue;
            var item = box.GetComponent<DeliveryItem>();
            var carry = box.GetComponent<CarryableObject>();
            if (item == null || item.IsBroken) continue;
            if (carry != null && carry.IsHeld) continue;

            int value = item.CurrentValue;
            if (value > 0 && money != null &&
                money.TryAdd(value, SharedMoneyReason.DeliveryReward, "Truck"))
            {
                total += value;
                count++;

                // 積んだ人が乗っていれば、その人の稼ぎとして記録する
                ulong loader = carry != null ? carry.ServerLastHolderClientId : ulong.MaxValue;
                if (aboard.Contains(loader) && nm != null &&
                    nm.ConnectedClients.TryGetValue(loader, out var loaderClient) &&
                    loaderClient.PlayerObject != null)
                {
                    var earning = loaderClient.PlayerObject.GetComponent<PlayerEarning>();
                    if (earning != null) earning.AddEarning(value);
                }
            }

            inBed.Remove(box);
            item.ServerRemove();
        }

        ServerTally();
        if (deliveryZone != null) deliveryZone.ServerRefill();

        // お金の確定はここまでで済んでいる。あとは見た目だけ走り去る（朝に戻る）
        away.Value = true;

        // 3) 乗った人は家へ帰る（朝と同じリスポーン。牢屋の人は牢屋のまま）
        if (nm != null)
        {
            foreach (ulong id in aboard)
            {
                if (!nm.ConnectedClients.TryGetValue(id, out var client) || client.PlayerObject == null) continue;
                var spawn = client.PlayerObject.GetComponent<PlayerSpawnSync>();
                if (spawn != null) spawn.ServerRespawn();
            }
        }

        // 4) 全員に知らせる
        var body = new StringBuilder();
        body.Append(count > 0 ? $"積み荷 {count}個　+¥{total:N0}　共同口座へ" : "荷台はからっぽ……");
        if (leftBehind.Count > 0)
            body.Append($"\n置いていかれた：{string.Join("、", leftBehind)}");

        DepartedRpc(total);
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounce(
                "トラック発車！", body.ToString(),
                count > 0 ? NightEventManager.StylePeace : NightEventManager.StyleInfo);
    }

    /// <summary>荷台の場所にある箱を数え直す（サーバーのみ。戻ってきた朝に1回）。</summary>
    private void ServerRescanBed()
    {
        var bed = GetComponent<BoxCollider>();
        Vector3 center = transform.TransformPoint(bed.center);
        Vector3 half = Vector3.Scale(bed.size, transform.lossyScale) * 0.5f;
        foreach (var c in Physics.OverlapBox(center, half, transform.rotation, ~0, QueryTriggerInteraction.Ignore))
        {
            NetworkObject box = BoxOf(c);
            if (box == null) continue;
            if (!inBed.TryGetValue(box, out var colliders))
            {
                colliders = new HashSet<Collider>();
                inBed.Add(box, colliders);
            }
            colliders.Add(c);
        }
        ServerTally();
    }

    // ---- 全員：走り去る見た目 ----

    private void UpdateDrive()
    {
        if (drivePivot == null) return;
        float target = away.Value ? driveTime + Time.deltaTime : 0f;
        if (Mathf.Approximately(target, driveTime) && !away.Value) return;
        driveTime = target;
        ApplyDrivePose();
    }

    /// <summary>
    /// 走り去る途中の位置と向き。左（または右）へ90度曲がってから、まっすぐ加速して消える。
    /// 見た目だけなので各自の画面で計算する（away が同期されるので、全員ほぼ同じ時に動く）。
    /// </summary>
    private void ApplyDrivePose()
    {
        if (drivePivot == null) return;

        float t = Mathf.Max(0f, driveTime - driveStartDelay);
        if (!away.Value || t <= 0f)
        {
            if (!drivePivot.gameObject.activeSelf) drivePivot.gameObject.SetActive(true);
            drivePivot.localPosition = Vector3.zero;
            drivePivot.localRotation = Quaternion.identity;
            return;
        }

        if (t >= driveSeconds)
        {
            if (drivePivot.gameObject.activeSelf) drivePivot.gameObject.SetActive(false);
            return;
        }

        float r = Mathf.Abs(driveTurnRadius);
        float side = driveTurnRadius >= 0f ? -1f : 1f;        // 左は -X
        float arc = r * Mathf.PI * 0.5f;
        float total = arc + driveStraightDistance;
        float u = t / driveSeconds;
        float s = total * u * u;                              // だんだん速くなる

        Vector3 pos;
        float yaw;
        if (s < arc && r > 0.01f)
        {
            float a = s / r;
            pos = new Vector3(side * (r - r * Mathf.Cos(a)), 0f, r * Mathf.Sin(a));
            yaw = side * a * Mathf.Rad2Deg;
        }
        else
        {
            float straight = s - arc;
            pos = new Vector3(side * (r + straight), 0f, r);
            yaw = side * 90f;
        }
        drivePivot.localPosition = pos;
        drivePivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
    }

    // ---- 全員：音と看板 ----

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void LoadedRpc(Vector3 position)
    {
        GameAudio.PlayAt(loadSoundKey, position, 0.7f, 0.05f, 16f);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void WarnRpc(int seconds, int count, int value)
    {
        GameAudio.Play(warnSoundKey, 0.9f);
        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ShowBanner(
                $"トラック発車まで {seconds}秒！",
                $"荷台 {count}個・¥{value:N0}　近くにいないと置いていかれる",
                NightEventManager.StyleDanger);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void DepartedRpc(int total)
    {
        GameAudio.PlayAt(departSoundKey, transform.position, 1f, 0f, 40f);
    }

    private void UpdateSign()
    {
        if (signLabel == null) return;
        DayManager dm = DayManager.Instance;
        string head;
        if (dm == null || !IsSpawned) head = "配達トラック";
        else if (dm.IsNight) head = "朝に出発";
        else
        {
            int s = Mathf.Max(0, Mathf.CeilToInt(dm.RemainingSeconds));
            head = $"発車まで {s / 60}:{s % 60:00}";
        }
        string text = $"{head}\n荷台 {bedCount.Value}個 ¥{bedValue.Value:N0}";
        if (signLabel.text != text) signLabel.text = text;
    }
}
