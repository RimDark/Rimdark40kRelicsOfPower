using RimWorld;
using Verse;

namespace Relics40k;

public class HediffCompProperties_TempormortisRite : HediffCompProperties
{
    public ThingDef dormantDef = null;

    public ThingDef awakenedDef = null;

    public int durationTicks = 60000;

    public int raidIntervalTicks = 15000;

    public float raidPointsFactor = 1f;

    public int totalYears = 10;

    public int minYearsEach = 1;

    public ThoughtDef keptThought = null;

    public ThoughtDef brokenThought = null;

    public HediffCompProperties_TempormortisRite()
    {
        compClass = typeof(HediffComp_TempormortisRite);
    }
}
