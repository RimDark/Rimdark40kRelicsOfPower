using RimWorld;
using Verse;

namespace Relics40k;

public class WorkGiver_EnterAscensionEngine : WorkGiver_EnterBuilding
{
    public override ThingRequest PotentialWorkThingRequest =>
        AscensionEngineDefOf.Engine != null
            ? ThingRequest.ForDef(AscensionEngineDefOf.Engine)
            : ThingRequest.ForGroup(ThingRequestGroup.Everything);

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        return AscensionEngineDefOf.Engine == null;
    }
}
