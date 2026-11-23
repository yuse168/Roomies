using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// カジノのエレベーター。箱は本当には上下しない。
/// 扉を閉めている間に、箱の中にいる人を、行き先の階の箱の同じ位置へ移す
/// （プレイヤーの位置は各自の端末が決めているので、動く床に乗せると揺れる。鉱山の移動と同じ仕組みを使う）。
///
/// プレハブ：Assets/_Prefabs/Casino/CasinoElevator.prefab（配置ツール Roomies/Casino/2 Build Gimmick Prefabs）
///  ・ルート：NetworkObject・このスクリプト
///  ・Floor_1F / 2F / 3F：その階の箱。それぞれに
///      Visual（壁と箱の見た目・ぶつかるだけの当たり判定）、Anchor_Cabin（箱の床の真ん中）、
///      Pivot_DoorL / Pivot_DoorR（左右に引く扉の軸。当たり判定つき）、ボタン（CasinoElevatorButton）、階数のランプ
///
/// 流れ（サーバー）：ボタン → 扉が閉まる → 少し待つ（ランプが動く）→ 中の人を移す → 扉が開く。
/// 行ける階は CasinoAccess（鍵）が決める。降りるのはいつでも自由。
/// </summary>
public class CasinoElevator : NetworkBehaviour
{
    [System.Serializable]
    public class FloorParts
    {
        [Tooltip("箱の床の真ん中（Anchor_Cabin）。移すときの基準")]
        public Transform cabinAnchor;
        [Tooltip("左右の扉の軸（Pivot_DoorL / Pivot_DoorR）。ローカルの x へ引く")]
        public Transform doorLeft;
        public Transform doorRight;
        [Tooltip("階数のランプの「点いた」見た目。1階・2階・3階の順に並べ、何組でも続けて入れてよい（外の上と箱の中など）")]
        public GameObject[] lampsLit = new GameObject[0];
    }

    private enum Phase { Open, Closing, Moving, Opening }

    [Header("階ごとの部品（下の階から）")]
    [SerializeField] private FloorParts[] floors = new FloorParts[0];

    [Header("箱の中と見なす範囲（Anchor_Cabin から見た大きさ）")]
    [SerializeField] private Vector3 cabinSize = new Vector3(2.4f, 2.6f, 2.05f);

    [Header("動き（秒・m）")]
    [Tooltip("閉じたときの扉の軸の位置（箱の真ん中からの距離＝扉の幅の半分）")]
    [SerializeField, Min(0f)] private float doorClosedOffset = 0.35f;
    [Tooltip("扉を引く幅（片側）")]
    [SerializeField, Min(0.1f)] private float doorSlide = 0.68f;
    [SerializeField, Min(0.1f)] private float doorSeconds = 0.6f;
    [Tooltip("人を乗せて動くあいだ、扉を閉めておく秒数")]
    [SerializeField, Min(0.2f)] private float rideSeconds = 2.5f;
    [Tooltip("扉が開ききってから、次のボタンを受け付けるまでの秒数")]
    [SerializeField, Min(0f)] private float settleSeconds = 0.2f;

    [Header("揺れ（乗っている人の画面だけ。見た目）")]
    [Tooltip("動いているあいだ、箱の明かりをこの幅で明滅させる（0 で止める）")]
    [SerializeField, Range(0f, 1f)] private float lightFlicker = 0.25f;
    [SerializeField] private Light[] cabinLights = new Light[0];

    private readonly NetworkVariable<int> carFloor = new(0);          // 箱がある階（0 始まり）
    private readonly NetworkVariable<int> targetFloor = new(0);
    private readonly NetworkVariable<int> phase = new((int)Phase.Open);

    private float[] doorOpen;          // 階ごとの扉の開き具合（0 閉〜1 開）
    private float[] lightBase;
    private float moveStartedAt;
    private int shownLamp = -1;

    public int FloorCount => floors.Length;
    public int CarFloor => carFloor.Value;
    public bool IsIdle => phase.Value == (int)Phase.Open;

    public override void OnNetworkSpawn()
    {
        phase.OnValueChanged += OnPhaseChanged;
        SnapDoors();
    }

    public override void OnNetworkDespawn()
    {
        phase.OnValueChanged -= OnPhaseChanged;
    }

    private void Awake()
    {
        lightBase = new float[cabinLights.Length];
        for (int i = 0; i < cabinLights.Length; i++)
            lightBase[i] = cabinLights[i] != null ? cabinLights[i].intensity : 0f;
    }

    // ================================================================ サーバー

    /// <summary>その階へ行く／その階へ呼ぶ（サーバーのみ）。動けないときは理由を返す。</summary>
    public string ServerRequest(int floor)
    {
        if (!IsServer) return "";
        if (floor < 0 || floor >= floors.Length) return "";
        if (phase.Value != (int)Phase.Open) return "動いている";
        if (floor == carFloor.Value) return "";

        CasinoAccess access = CasinoAccess.Instance;
        int unlocked = access != null ? access.UnlockedFloor : 1;
        if (floor + 1 > unlocked) return $"{floor + 1}階へは鍵が必要";

        StartCoroutine(ServerRide(floor));
        return "";
    }

    private IEnumerator ServerRide(int to)
    {
        int from = carFloor.Value;
        targetFloor.Value = to;
        phase.Value = (int)Phase.Closing;
        yield return new WaitForSeconds(doorSeconds);

        phase.Value = (int)Phase.Moving;
        yield return new WaitForSeconds(rideSeconds);

        ServerMovePassengers(from, to);
        carFloor.Value = to;
        phase.Value = (int)Phase.Opening;
        yield return new WaitForSeconds(doorSeconds + settleSeconds);

        phase.Value = (int)Phase.Open;
    }

    /// <summary>from の階の箱の中にいる人を、to の階の箱の同じ位置へ移す。持っている荷物も一緒に移す。</summary>
    private void ServerMovePassengers(int from, int to)
    {
        Transform a = floors[from].cabinAnchor, b = floors[to].cabinAnchor;
        if (a == null || b == null) return;

        foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
        {
            NetworkObject player = client.PlayerObject;
            if (player == null || !IsInsideCabin(from, player.transform.position)) continue;

            // 箱から見た位置と向きを保ったまま、行き先の箱へ
            Vector3 local = a.InverseTransformPoint(player.transform.position);
            float yaw = player.transform.eulerAngles.y + (b.eulerAngles.y - a.eulerAngles.y);
            Vector3 destination = b.TransformPoint(local);

            foreach (NetworkObject obj in NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (!obj.TryGetComponent(out CarryableObject carry) || !carry.IsHeldBy(client.ClientId)) continue;
                carry.ServerRelocate(b.TransformPoint(a.InverseTransformPoint(obj.transform.position)));
            }

            var sync = player.GetComponent<PlayerSpawnSync>();
            if (sync != null) sync.ServerTeleport(destination, yaw);
        }
    }

    /// <summary>その位置が、その階の箱の中か（サーバーの判定にも、確認の道具にも使う）。</summary>
    public bool IsInsideCabin(int floor, Vector3 worldPosition)
    {
        if (floor < 0 || floor >= floors.Length || floors[floor].cabinAnchor == null) return false;
        Vector3 p = floors[floor].cabinAnchor.InverseTransformPoint(worldPosition);
        return Mathf.Abs(p.x) <= cabinSize.x * 0.5f &&
               Mathf.Abs(p.z) <= cabinSize.z * 0.5f &&
               p.y >= -0.3f && p.y <= cabinSize.y;
    }

    // ================================================================ 見た目（各クライアント）

    private void OnPhaseChanged(int oldValue, int newValue)
    {
        Vector3 at = CabinPosition(carFloor.Value);
        switch ((Phase)newValue)
        {
            case Phase.Closing:
                GameAudio.PlayAt("casino/elevator_close", at);
                break;
            case Phase.Moving:
                moveStartedAt = Time.time;
                GameAudio.PlayAt("casino/elevator_move", at, 1f, 0f, 12f);
                break;
            case Phase.Opening:
                GameAudio.PlayAt("casino/elevator_ding", at, 1f, 0f, 14f);
                GameAudio.PlayAt("casino/elevator_open", at);
                break;
        }
    }

    private Vector3 CabinPosition(int floor)
    {
        if (floor < 0 || floor >= floors.Length || floors[floor].cabinAnchor == null) return transform.position;
        return floors[floor].cabinAnchor.position + Vector3.up * 1.2f;
    }

    private bool DoorShouldBeOpen(int floor)
    {
        var p = (Phase)phase.Value;
        return floor == carFloor.Value && (p == Phase.Open || p == Phase.Opening);
    }

    private void SnapDoors()
    {
        doorOpen = new float[floors.Length];
        for (int i = 0; i < floors.Length; i++)
        {
            doorOpen[i] = DoorShouldBeOpen(i) ? 1f : 0f;
            ApplyDoor(i);
        }
        ApplyLamps(carFloor.Value);
    }

    private void Update()
    {
        if (!IsSpawned) return;
        if (doorOpen == null || doorOpen.Length != floors.Length) SnapDoors();

        for (int i = 0; i < floors.Length; i++)
        {
            float goal = DoorShouldBeOpen(i) ? 1f : 0f;
            if (Mathf.Approximately(doorOpen[i], goal)) continue;
            doorOpen[i] = Mathf.MoveTowards(doorOpen[i], goal, Time.deltaTime / doorSeconds);
            ApplyDoor(i);
        }

        // 階数のランプ：動いているあいだは、出発の階から行き先の階へ順に移る
        bool moving = phase.Value == (int)Phase.Moving;
        int lamp = carFloor.Value;
        if (moving)
        {
            float t = Mathf.Clamp01((Time.time - moveStartedAt) / rideSeconds);
            lamp = Mathf.RoundToInt(Mathf.Lerp(carFloor.Value, targetFloor.Value, t));
        }
        if (lamp != shownLamp) ApplyLamps(lamp);

        // 箱の明かり：動いているあいだだけ少し明滅する
        for (int i = 0; i < cabinLights.Length; i++)
        {
            if (cabinLights[i] == null) continue;
            float k = moving && lightFlicker > 0f
                ? 1f - lightFlicker * Mathf.PerlinNoise(Time.time * 9f, i * 3.1f)
                : 1f;
            cabinLights[i].intensity = lightBase[i] * k;
        }
    }

    private void ApplyDoor(int floor)
    {
        FloorParts f = floors[floor];
        float x = doorSlide * Mathf.SmoothStep(0f, 1f, doorOpen[floor]);
        if (f.doorLeft != null) { Vector3 p = f.doorLeft.localPosition; p.x = -doorClosedOffset - x; f.doorLeft.localPosition = p; }
        if (f.doorRight != null) { Vector3 p = f.doorRight.localPosition; p.x = doorClosedOffset + x; f.doorRight.localPosition = p; }
    }

    private void ApplyLamps(int lit)
    {
        shownLamp = lit;
        foreach (FloorParts f in floors)
        {
            if (f.lampsLit == null) continue;
            for (int i = 0; i < f.lampsLit.Length; i++)
            {
                bool on = floors.Length > 0 && i % floors.Length == lit;
                if (f.lampsLit[i] != null && f.lampsLit[i].activeSelf != on) f.lampsLit[i].SetActive(on);
            }
        }
    }
}
