using Verse;

namespace Relics40k;

public class DefModExtension_TemporalDilation : DefModExtension
{
    public HediffDef dilationHediff = null;

    // While set, the field dies as soon as this thing is no longer spawned on an affected map,
    // whichever way it left.
    public ThingDef anchorDef = null;

    public int pulseIntervalTicks = 30;

    public int lingerTicks = 90;

    public bool affectDowned = true;
}
