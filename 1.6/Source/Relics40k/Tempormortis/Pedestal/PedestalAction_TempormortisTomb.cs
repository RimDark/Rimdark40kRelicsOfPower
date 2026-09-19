using RimWorld;
using Verse;

namespace Relics40k;

/// <summary>
/// Only the Chaplaincy may lift the hourglass, and lifting it lets time back into the tomb: the
/// dilation field dies and the besiegers resume. Rules come from the site part's extension.
/// </summary>
public class PedestalAction_TempormortisTomb : PedestalAction
{
    private SitePartDef sitePartDef;

    private DefModExtension_TempormortisTomb Extension => sitePartDef?.GetModExtension<DefModExtension_TempormortisTomb>();

    public PedestalAction_TempormortisTomb()
    {
    }

    public PedestalAction_TempormortisTomb(SitePartDef sitePartDef)
    {
        this.sitePartDef = sitePartDef;
    }

    public override AcceptanceReport CanTake(CompPedestal pedestal, Pawn pawn)
    {
        var extension = Extension;

        if (extension == null || !TempormortisUtility.HasAnyRank(pawn, extension.requiredRanksOneAmong))
        {
            return "Relics.Tempormortis.NotChaplaincy".Translate();
        }

        return true;
    }

    public override string TakeLabel(CompPedestal pedestal)
    {
        return "Relics.Tempormortis.TakeRelic".Translate();
    }

    public override void Notify_Taken(CompPedestal pedestal, Thing item, Pawn taker, Map map)
    {
        var conditionDef = Extension?.dilationCondition;

        if (conditionDef != null && map != null)
        {
            map.gameConditionManager.GetActiveCondition(conditionDef)?.End();
        }

        LordJob_RelicGuardians.WakeAll(map);

        if (taker == null)
        {
            return;
        }

        Find.LetterStack.ReceiveLetter(
            "Relics.Tempormortis.TakenLetterLabel".Translate(),
            "Relics.Tempormortis.TakenLetterText".Translate(taker.Named("PAWN")),
            LetterDefOf.ThreatBig,
            taker);
    }

    public override void ExposeData()
    {
        Scribe_Defs.Look(ref sitePartDef, "sitePartDef");
    }
}
