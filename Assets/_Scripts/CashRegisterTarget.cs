using UnityEngine;

public sealed class CashRegisterTarget : MonoBehaviour
{
    public CashRegister register;
    [Tooltip("-1: scanner; 0..2: change choices")]
    public int action = -1;
    public string Label => register == null ? "" : register.ActionLabel(action);
    public void Use(PlayerInteract player) => register.UseRpc(player.NetworkObjectId, action, register.OrderNumber);
}
