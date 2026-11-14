using TMPro;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 配達の荷物。値段を持ち、強くぶつけるたびに値段が下がる。0 になると割れて消える。
///
/// ・値段と「割れた」の判定はサーバーだけが決める（NetworkVariable はサーバーだけが書ける）。
///   クライアントは値段を表示するだけで、申告で値段や報酬は変わらない。
/// ・減る量は GameBalance（Resources/Balance/GameBalance）の「荷物の値段」で決める。
///   しきい値以下の速さ（そっと置く・歩いて運ぶ）では減らない。
/// ・お金になるのは、トラックの荷台に積んで発車したとき（<see cref="DeliveryTruck"/>）。
///
/// 規約（AGENTS.md）：値札はプレハブに置いてある TMP を参照するだけで、実行時に作らない。
/// </summary>
public class DeliveryItem : NetworkBehaviour
{
    [Header("値札（プレハブの Anchor_PriceTag の下）")]
    [Tooltip("頭の上に出す値段の文字。メニュー Roomies/Delivery/Build Price Tags が置く")]
    [SerializeField] private TMP_Text priceLabel;

    [Tooltip("値札の文字色（ふつう）")]
    [SerializeField] private Color normalColor = new Color(1f, 0.96f, 0.86f);
    [Tooltip("値札の文字色（レア）")]
    [SerializeField] private Color rareColor = new Color(1f, 0.82f, 0.3f);
    [Tooltip("最初の値段の半分を切ったときの文字色")]
    [SerializeField] private Color damagedColor = new Color(1f, 0.45f, 0.35f);

    [Tooltip("この距離（m）より遠いと値札を消す")]
    [SerializeField, Min(1f)] private float labelHideDistance = 18f;

    [Header("音")]
    [SerializeField] private string damageSoundKey = "money/spend";
    [SerializeField] private string breakSoundKey = "mining/break";

    [Header("出てすぐ")]
    [Tooltip("出てからこの秒数は値段が下がらない")]
    [SerializeField, Min(0f)] private float spawnGraceSeconds = 1.5f;

    [Tooltip("シーンに直接置いた箱の種類（集荷場から出た箱は集荷場が決める）。rare_Box はオン")]
    [SerializeField] private bool rareByDefault;

    private readonly NetworkVariable<bool> isRareItem = new(false);
    private readonly NetworkVariable<int> startValue = new(0);
    private readonly NetworkVariable<int> currentValue = new(0);

    private bool isBroken;
    private float noDamageUntil;
    private DeliveryZone ownerZone;
    private CarryableObject carry;
    private Camera cachedCamera;

    public bool IsRareItem() => isRareItem.Value;
    public int CurrentValue => currentValue.Value;
    public int StartValue => startValue.Value;
    public bool IsBroken => isBroken;

    public void SetOwnerZone(DeliveryZone zone) => ownerZone = zone;

    /// <summary>出したときにサーバーが呼ぶ。レアかどうかと最初の値段を決める。</summary>
    public void SetRare(bool rare)
    {
        if (!IsServer) return;

        GameBalance balance = GameBalance.Current;
        int value = rare ? balance.rareBoxValue : balance.normalBoxValue;

        isRareItem.Value = rare;
        startValue.Value = value;
        currentValue.Value = value;
        noDamageUntil = Time.time + spawnGraceSeconds;
    }

    public override void OnNetworkSpawn()
    {
        currentValue.OnValueChanged += OnValueChanged;
        isRareItem.OnValueChanged += OnRareChanged;
        carry = GetComponent<CarryableObject>();

        // シーンに直接置いた箱にも値段を付ける（集荷場から出た箱は、このあと SetRare で決め直す）
        if (IsServer && startValue.Value <= 0) SetRare(rareByDefault);
        if (IsServer) noDamageUntil = Time.time + spawnGraceSeconds;
        RefreshLabel();
    }

    public override void OnNetworkDespawn()
    {
        currentValue.OnValueChanged -= OnValueChanged;
        isRareItem.OnValueChanged -= OnRareChanged;

        // シーンに直接置いた箱は消さずに隠す（ServerRemove 参照）。遅れて入った人の画面でも
        // NGO がこの OnNetworkDespawn を呼ぶので、同じように隠れる
        if (NetworkObject != null && NetworkObject.IsSceneObject == true) gameObject.SetActive(false);
    }

    /// <summary>
    /// 箱を消す（サーバーのみ）。割れたとき・トラックで運び出したときに使う。
    /// 集荷場が出した箱は破棄する。シーンに直接置いてある箱は、破棄すると NGO が警告を出すので
    /// （遅れて入った人との食い違いのもと）、Despawn(false) でネットワークから外し、隠すだけにする。
    /// </summary>
    public void ServerRemove()
    {
        if (!IsServer || NetworkObject == null || !NetworkObject.IsSpawned) return;
        bool sceneObject = NetworkObject.IsSceneObject == true;
        NetworkObject.Despawn(!sceneObject);
    }

    // ---- サーバー：ぶつかったら値段を下げる ----

    private void OnCollisionEnter(Collision collision)
    {
        if (!IsServer || !IsSpawned || isBroken) return;
        if (Time.time < noDamageUntil) return;
        if (startValue.Value <= 0) return;

        // まだ誰も持っていない箱は減らさない（集荷場の高い所から落ちてくる分・転がっている分）。
        // 一度でも誰かが持てば、そのあとの落下・衝突・地震はすべて数える
        if (carry != null && carry.ServerLastHolderClientId == ulong.MaxValue) return;

        GameBalance balance = GameBalance.Current;
        float fragility = isRareItem.Value ? balance.rareBoxFragility : balance.normalBoxFragility;
        int damage = balance.BoxDamage(startValue.Value, fragility, collision.relativeVelocity.magnitude);
        if (damage <= 0) return;

        noDamageUntil = Time.time + balance.boxDamageCooldown;
        ServerDamage(damage);
    }

    /// <summary>値段を下げる（サーバーのみ）。0 になったら割る。</summary>
    public void ServerDamage(int amount)
    {
        if (!IsServer || isBroken || amount <= 0) return;

        int next = Mathf.Max(0, currentValue.Value - amount);
        currentValue.Value = next;

        if (next > 0)
        {
            DamagedRpc(transform.position, amount);
            return;
        }

        isBroken = true;
        BrokenRpc(transform.position, isRareItem.Value);

        var carry = GetComponent<CarryableObject>();
        if (carry != null) carry.ServerRelease();

        if (NightEventManager.Instance != null)
            NightEventManager.Instance.ServerAnnounce(
                "割れた！",
                $"¥{startValue.Value:N0} の荷物が ¥0 に……",
                NightEventManager.StyleDanger);

        DeliveryZone zone = ownerZone;
        ServerRemove();
        if (zone != null) zone.ServerRefill();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void DamagedRpc(Vector3 position, int amount)
    {
        GameAudio.PlayAt(damageSoundKey, position, 0.9f, 0.08f, 18f);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    private void BrokenRpc(Vector3 position, bool rare)
    {
        GameAudio.PlayAt(breakSoundKey, position, 1f, 0.05f, 26f);
    }

    // ---- 全員：値札の表示 ----

    private void OnValueChanged(int previous, int current) => RefreshLabel();
    private void OnRareChanged(bool previous, bool current) => RefreshLabel();

    private void RefreshLabel()
    {
        if (priceLabel == null) return;

        int value = currentValue.Value;
        priceLabel.text = $"¥{value:N0}";

        bool damaged = startValue.Value > 0 && value * 2 < startValue.Value;
        priceLabel.color = damaged ? damagedColor : isRareItem.Value ? rareColor : normalColor;
    }

    private void LateUpdate()
    {
        if (priceLabel == null) return;

        if (cachedCamera == null || !cachedCamera.isActiveAndEnabled) cachedCamera = Camera.main;
        if (cachedCamera == null) return;

        Transform label = priceLabel.transform;
        Vector3 toCamera = label.position - cachedCamera.transform.position;
        bool show = IsSpawned && toCamera.sqrMagnitude < labelHideDistance * labelHideDistance;
        if (priceLabel.enabled != show) priceLabel.enabled = show;
        if (!show) return;

        // 箱が回っても値札はいつも上を向き、カメラの方を向く
        label.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
    }
}
