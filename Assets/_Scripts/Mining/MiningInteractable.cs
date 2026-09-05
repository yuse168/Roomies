using UnityEngine;

public enum MiningAction { Travel, Pickaxe, Sell, Rock }

/// <summary>Authored targets dispatched through the existing PlayerInteract raycast.</summary>
public class MiningInteractable : MonoBehaviour
{
    public MiningSite site;
    public int index;
    public MiningAction action;
    [Range(0, 3)] public int level;
    public Transform destination;
    public Collider targetCollider;
    [Range(3, 8)] public int maxHits = 4;
    public Transform rockVisual;
    public Transform dropPoint;
    public string displayName;

    public string Label
    {
        get
        {
            if (action == MiningAction.Rock)
                return $"{displayName}  [{site.Health(index)}/{maxHits}]  左クリックで採掘";
            if (action == MiningAction.Pickaxe) return "ツルハシを借りる（無料）";
            if (action == MiningAction.Sell) return "持っている鉱石を換金";
            return level == 0 ? "地上へ戻る" : $"地下{level}層へ  /  日没で閉鎖";
        }
    }

    public void Use(PlayerInteract player) => site.RequestUseServerRpc(index, player.NetworkObjectId);

    public void Refresh(int hp)
    {
        if (action != MiningAction.Rock) return;
        if (rockVisual != null) rockVisual.gameObject.SetActive(hp > 0);
        if (targetCollider != null) targetCollider.enabled = hp > 0;
    }
}
