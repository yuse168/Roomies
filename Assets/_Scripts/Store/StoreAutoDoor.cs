using Unity.Netcode;
using UnityEngine;

/// <summary>
/// コンビニの自動ドア（2026/11/24）。人が近づくと 2 枚のとびらが左右に開き、チャイムが鳴る。
///
/// 開いているかどうかはサーバーが決めて全員に配る（全員の画面で同じときに開く）。サーバーは 0.12 秒おきに、
/// つながっている人の位置だけを見る（物理の判定は使わない）。とびらに当たり判定はない（通れなくなることがないように）。
/// 外（とびらの +Z の側）から入ってきた人がいると、店の人が「いらっしゃいませ」を言う（StoreShop.ServerGreet）。
///
/// プレハブ：Assets/_Prefabs/Store/StoreDoor.prefab
///  ・ルート：NetworkObject・このスクリプト（足元の真ん中が原点、+Z が通り）
///  ・Pivot_DoorL / Pivot_DoorR：左右のとびらの軸（中の Visual がとびら）。これだけを横へ動かす
///  ・Anchor_Sensor：人を見る所（とびらの上の感知器の位置）
/// </summary>
public class StoreAutoDoor : NetworkBehaviour
{
    [Header("とびら（Pivot_〇）")]
    [SerializeField] private Transform leftDoor;
    [SerializeField] private Transform rightDoor;
    [Tooltip("開いたときに、とびらが横へ動く長さ（m）")]
    [SerializeField, Min(0.1f)] private float slideDistance = 0.74f;
    [SerializeField, Min(0.05f)] private float openSeconds = 0.38f;
    [SerializeField, Min(0.05f)] private float closeSeconds = 0.55f;

    [Header("感知（サーバー）")]
    [SerializeField] private Transform sensor;
    [Tooltip("感知する範囲：とびらの面からの前後（m）と、横の半分（m）")]
    [SerializeField, Min(0.3f)] private float senseDepth = 2.3f;
    [SerializeField, Min(0.3f)] private float senseHalfWidth = 1.5f;
    [Tooltip("人がいなくなってから閉まるまでの秒数")]
    [SerializeField, Min(0f)] private float holdSeconds = 0.8f;
    [SerializeField, Min(0.05f)] private float checkInterval = 0.12f;

    [Header("いらっしゃいませ")]
    [SerializeField] private StoreShop shop;

    private readonly NetworkVariable<bool> open = new(false);
    private float openAmount;
    private Vector3 leftHome, rightHome;
    private bool homesTaken;
    private float nextCheck, lastSeen;

    public bool IsOpen => open.Value;
    public float OpenAmount => openAmount;

    private void TakeHomes()
    {
        if (homesTaken) return;
        homesTaken = true;
        if (leftDoor != null) leftHome = leftDoor.localPosition;
        if (rightDoor != null) rightHome = rightDoor.localPosition;
    }

    private void Awake() => TakeHomes();

    public override void OnNetworkSpawn()
    {
        open.OnValueChanged += OnOpenChanged;
        openAmount = open.Value ? 1f : 0f;
        Apply();
    }

    public override void OnNetworkDespawn() => open.OnValueChanged -= OnOpenChanged;

    private void OnOpenChanged(bool was, bool now)
    {
        Vector3 at = sensor != null ? sensor.position : transform.position;
        GameAudio.PlayAt("store/door_slide", at, now ? 1f : 0.8f, 0.04f, 12f);
        if (now) GameAudio.PlayAt("store/door_chime", at, 1f, 0f, 14f);
    }

    private void Update()
    {
        if (IsSpawned && IsServer && Time.time >= nextCheck)
        {
            nextCheck = Time.time + checkInterval;
            ServerSense();
        }
        float target = open.Value ? 1f : 0f;
        if (!Mathf.Approximately(openAmount, target))
        {
            float speed = 1f / (target > openAmount ? openSeconds : closeSeconds);
            openAmount = Mathf.MoveTowards(openAmount, target, speed * Time.deltaTime);
            Apply();
        }
    }

    private void ServerSense()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        Transform s = sensor != null ? sensor : transform;
        bool near = false, fromOutside = false;
        foreach (var client in nm.ConnectedClientsList)
        {
            if (client.PlayerObject == null) continue;
            Vector3 p = transform.InverseTransformPoint(client.PlayerObject.transform.position);
            if (Mathf.Abs(p.x) > senseHalfWidth || Mathf.Abs(p.z) > senseDepth || p.y < -1.5f || p.y > 3f) continue;
            near = true;
            if (p.z > 0.25f) fromOutside = true;
        }
        if (near) lastSeen = Time.time;
        bool shouldOpen = near || Time.time - lastSeen < holdSeconds;
        if (shouldOpen != open.Value)
        {
            open.Value = shouldOpen;
            if (shouldOpen && fromOutside && shop != null) shop.ServerGreet();
        }
    }

    /// <summary>とびらを、開いた量（0〜1）の位置へ。ゆっくり動きはじめ、ゆっくり止まる。</summary>
    private void Apply()
    {
        TakeHomes();
        float e = Mathf.SmoothStep(0f, 1f, openAmount);
        if (leftDoor != null) leftDoor.localPosition = leftHome + Vector3.left * (slideDistance * e);
        if (rightDoor != null) rightDoor.localPosition = rightHome + Vector3.right * (slideDistance * e);
    }
}
