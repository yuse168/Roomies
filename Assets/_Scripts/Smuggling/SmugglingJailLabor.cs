using UnityEngine;

/// <summary>牢屋内の仮労働。翌朝になってからEで作業し、規定回数で解放される。</summary>
public class SmugglingJailLabor : SmugglingInteractable
{
    public override bool CanInteract(SmugglingPlayer player)
    {
        return player != null && player.CanDoJailLabor;
    }

    public override string GetInteractionLabel(SmugglingPlayer player)
    {
        if (player == null) return "労働する";
        return $"労働する ({player.JailLaborProgress}/{SmugglingConfig.JailLaborCount})";
    }

    public override void Interact(SmugglingPlayer player)
    {
        if (player != null) player.RequestJailLabor();
    }
}
