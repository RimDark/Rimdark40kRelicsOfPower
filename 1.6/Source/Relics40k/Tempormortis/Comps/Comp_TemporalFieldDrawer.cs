using RimWorld;
using Verse;

namespace Relics40k;

public class Comp_TemporalFieldDrawer : ThingComp
{
    private CompProperties_TemporalFieldDrawer Props => (CompProperties_TemporalFieldDrawer)props;

    /// <summary>
    /// Draws the aura edge around the wearer for as long as the active hediff is present on them.
    /// </summary>
    public override void CompDrawWornExtras()
    {
        base.CompDrawWornExtras();

        if (Props.activeHediff == null || (parent as Apparel)?.Wearer is not { Spawned: true } wearer)
        {
            return;
        }

        var aura = wearer.health.hediffSet.GetFirstHediffOfDef(Props.activeHediff)?.TryGetComp<HediffComp_TemporalAura>();
        if (aura == null)
        {
            return;
        }

        GenDraw.DrawRadiusRing(wearer.Position, aura.Props.radius, Props.ringColor);
    }
}
