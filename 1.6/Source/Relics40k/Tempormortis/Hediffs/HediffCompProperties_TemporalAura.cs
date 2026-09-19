using Verse;

namespace Relics40k;

public class HediffCompProperties_TemporalAura : HediffCompProperties
{
    public float radius = 5.9f;
    public int pulseIntervalTicks = 30;
    public HediffDef slowHediff;
    public int slowLingerTicks = 90;
    public bool affectHostileOnly = true;
    public bool affectDowned = false;

    public HediffCompProperties_TemporalAura()
    {
        compClass = typeof(HediffComp_TemporalAura);
    }
}
