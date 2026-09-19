using UnityEngine;
using Verse;

namespace Relics40k;

public class CompProperties_TemporalFieldDrawer : CompProperties
{
    public HediffDef activeHediff;
    public Color ringColor = new Color(0.55f, 0.7f, 1f, 0.6f);

    public CompProperties_TemporalFieldDrawer()
    {
        compClass = typeof(Comp_TemporalFieldDrawer);
    }
}
