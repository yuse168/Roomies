using UnityEngine;

/// <summary>PlayerInteract から利用できる運び屋専用のインタラクト対象。</summary>
public abstract class SmugglingInteractable : MonoBehaviour
{
    public abstract bool CanInteract(SmugglingPlayer player);
    public abstract string GetInteractionLabel(SmugglingPlayer player);
    public abstract void Interact(SmugglingPlayer player);
}
