using Verse;

namespace Relics40k;

/// <summary>Lifting the item wakes every relic guardian on the map.</summary>
public class PedestalAction_WakeGuardians : PedestalAction
{
    public override void Notify_Taken(CompPedestal pedestal, Thing item, Pawn taker, Map map)
    {
        LordJob_RelicGuardians.WakeAll(map);
    }
}
