using RimWorld;
using Verse;

namespace Relics40k;

/// <summary>
/// Storage for a single relic. Losing what it holds, or being destroyed, wakes every sleeping
/// lord on the map that listens for the relic-taken memo.
/// </summary>
public class Building_ReliquaryPedestal : Building_Storage
{
    public override void Notify_LostThing(Thing newItem)
    {
        base.Notify_LostThing(newItem);
        LordJob_RelicGuardians.WakeAll(Map);
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        var map = Map;
        base.Destroy(mode);
        LordJob_RelicGuardians.WakeAll(map);
    }
}
