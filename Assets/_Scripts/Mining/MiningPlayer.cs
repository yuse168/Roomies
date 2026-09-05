using Unity.Netcode;
using UnityEngine;

public class MiningPlayer : NetworkBehaviour
{
    private readonly NetworkVariable<bool> equipped = new(false);
    private readonly NetworkVariable<int> level = new(0);
    private GameObject pickaxe;
    private Light headlamp;
    private float swingUntil, noticeUntil;
    private string notice;
    public int Level => level.Value;
    public bool HasPickaxe => equipped.Value;
    public string CurrentNotice => Time.unscaledTime < noticeUntil ? notice : null;
    public void ServerEquip() { if (IsServer) equipped.Value = true; }
    public void ServerSetLevel(int value) { if (IsServer) level.Value = value; }

    public string HudLabel
    {
        get
        {
            if (CurrentNotice != null) return CurrentNotice;
            var held = GetComponent<PlayerInteract>().HeldObject;
            var ore = held != null ? held.GetComponent<MiningOre>() : null;
            string cargo = ore != null ? $"{ore.displayName}  {ore.ValueLabel}  重量{held.weightLevel}/5" : "手荷物なし / Fで鉱石を持つ";
            if (Level == 0) return ore != null ? cargo + "\n入口の買取機に E で売却" : null;
            int seconds = Mathf.CeilToInt(DayManager.Instance != null ? DayManager.Instance.RemainingSeconds : 0);
            string danger = MiningSite.Instance != null ? MiningSite.Instance.HazardLabel : "";
            return $"地下{Level}層  閉鎖まで {seconds / 60}:{seconds % 60:00}\n" + (danger.Length > 0 ? danger : cargo);
        }
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;
        var interact = GetComponent<PlayerInteract>();
        if (equipped.Value && pickaxe == null)
        {
            var prefab = Resources.Load<GameObject>("RoomiesArt/Mining/pickaxe");
            if (prefab != null)
            {
                Transform parent = IsOwner && interact.cameraTransform != null ? interact.cameraTransform : transform;
                pickaxe = Instantiate(prefab, parent);
                foreach (var collider in pickaxe.GetComponentsInChildren<Collider>()) Destroy(collider);
                pickaxe.transform.localScale = Vector3.one * (IsOwner ? .65f : .8f);
            }
        }
        if (pickaxe != null)
        {
            pickaxe.SetActive(equipped.Value && Level > 0 && (!IsOwner || interact.HeldObject == null && !interact.HasHeldFurniture));
            float swing = Mathf.Clamp01((swingUntil - Time.time) / .42f);
            float arc = Mathf.Sin(swing * Mathf.PI);
            pickaxe.transform.localPosition = IsOwner ? new Vector3(.43f, -.4f, .63f) : new Vector3(.55f, 0, .4f);
            pickaxe.transform.localRotation = Quaternion.Euler(-18 - arc * 65, -15, -18 + arc * 20);
        }
        if (IsOwner && Level > 0 && headlamp == null && interact.cameraTransform != null)
        {
            var go = new GameObject("Mining helmet light");
            go.transform.SetParent(interact.cameraTransform, false);
            headlamp = go.AddComponent<Light>(); headlamp.type = LightType.Spot;
            headlamp.range = 18; headlamp.spotAngle = 90; headlamp.intensity = 3;
            headlamp.color = new Color(1,.88f,.66f); headlamp.shadows = LightShadows.None;
        }
        if (headlamp != null) headlamp.enabled = Level > 0;
    }
    public override void OnNetworkDespawn()
    {
        if (pickaxe != null) Destroy(pickaxe);
        if (headlamp != null) Destroy(headlamp.gameObject);
    }
    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)] public void SwingRpc()
    {
        swingUntil = Time.time + .42f;
        if (IsOwner) GetComponent<PlayerMovement>()?.ApplyMiningImpact(Vector3.zero);
    }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)] public void ImpactRpc(Vector3 impulse) => GetComponent<PlayerMovement>()?.ApplyMiningImpact(impulse);
    public void ServerNotice(string text, bool sale) { if (IsServer) NoticeRpc(text, sale); }
    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)] private void NoticeRpc(string text, bool sale) => Notice(text, sale);
    public void Notice(string text, bool sale, bool warning = false)
    {
        notice = text; noticeUntil = Time.unscaledTime + 4;
        var site = MiningSite.Instance;
        if (site != null && (sale || warning))
            MiningFeedback.Sound(transform.position, sale ? site.saleSound : site.warningSound, .45f);
    }
}
