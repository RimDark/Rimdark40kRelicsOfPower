using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Relics40k;

/// <summary>
/// The dormant hourglass. Offers the rite to a wearer of the Chaplaincy; the vigil itself runs on
/// the hediff the rite adds, not here, so it keeps ticking however the relic is carried.
/// </summary>
public class Comp_TempormortisRite : ThingComp
{
    private CompProperties_TempormortisRite Props => (CompProperties_TempormortisRite)props;

    private Pawn Wearer => (parent as Apparel)?.Wearer;

    public AcceptanceReport CanBeginRite(Pawn pawn)
    {
        if (pawn == null)
        {
            return false;
        }

        if (!TempormortisUtility.HasAnyRank(pawn, Props.requiredRanksOneAmong))
        {
            return "Relics.Tempormortis.NotChaplaincy".Translate();
        }

        if (Props.riteHediff != null && pawn.health.hediffSet.HasHediff(Props.riteHediff))
        {
            return "Relics.Tempormortis.RiteUnderway".Translate();
        }

        if (pawn.equipment?.Primary != null)
        {
            return "Relics.Tempormortis.BrokeArmed".Translate();
        }

        if (pawn.Drafted)
        {
            return "Relics.Tempormortis.BrokeDrafted".Translate();
        }

        return true;
    }

    public override IEnumerable<Gizmo> CompGetWornGizmosExtra()
    {
        foreach (var gizmo in base.CompGetWornGizmosExtra())
        {
            yield return gizmo;
        }

        var wearer = Wearer;

        if (Props.riteHediff == null || wearer == null || wearer.Faction != Faction.OfPlayer)
        {
            yield break;
        }

        var command = new Command_Action
        {
            defaultLabel = "Relics.Tempormortis.BeginRite".Translate(),
            defaultDesc = "Relics.Tempormortis.BeginRiteDesc".Translate(),
            icon = parent.def.uiIcon,
            action = delegate { BeginRite(wearer); }
        };

        var report = CanBeginRite(wearer);

        if (!report.Accepted)
        {
            command.Disable(report.Reason);
        }

        yield return command;
    }

    private void BeginRite(Pawn pawn)
    {
        if (Props.riteHediff == null || !CanBeginRite(pawn).Accepted)
        {
            return;
        }

        pawn.health.AddHediff(Props.riteHediff);

        Find.LetterStack.ReceiveLetter(
            "Relics.Tempormortis.RiteBegunLetterLabel".Translate(),
            "Relics.Tempormortis.RiteBegunLetterText".Translate(pawn.Named("PAWN")),
            LetterDefOf.NeutralEvent,
            pawn);
    }

    public override string CompInspectStringExtra()
    {
        return "Relics.Tempormortis.InspectDormant".Translate();
    }
}
