using System.Collections.Generic;
using Core40k;
using Verse;

namespace Relics40k;

public class CompProperties_TempormortisRite : CompProperties
{
    public HediffDef riteHediff = null;

    public List<RankDef> requiredRanksOneAmong = [];

    public CompProperties_TempormortisRite()
    {
        compClass = typeof(Comp_TempormortisRite);
    }
}
