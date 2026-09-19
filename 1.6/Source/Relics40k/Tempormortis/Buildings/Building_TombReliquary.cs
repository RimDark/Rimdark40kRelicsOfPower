using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Relics40k;

/// <summary>
/// The plinth the dormant tempormortis rests on. Only a member of the Chaplaincy may lift it, and
/// lifting it lets time back into the tomb: the dilation field dies and the besiegers resume.
/// </summary>
public class Building_TombReliquary : Building_Storage
{
    private bool taken;

    public DefModExtension_TempormortisTomb Extension => def.GetModExtension<DefModExtension_TempormortisTomb>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref taken, "taken");
    }

    public AcceptanceReport CanBeTakenBy(Pawn pawn)
    {
        if (taken || HeldRelic == null)
        {
            return "Relics.Tempormortis.AlreadyTaken".Translate();
        }

        var extension = Extension;

        if (extension == null || !TempormortisUtility.HasAnyRank(pawn, extension.requiredRanksOneAmong))
        {
            return "Relics.Tempormortis.NotChaplaincy".Translate();
        }

        return true;
    }

    public Thing HeldRelic
    {
        get
        {
            var extension = Extension;
            var map = Map;

            if (extension?.dormantRelicDef == null || map == null)
            {
                return null;
            }

            foreach (var thing in Position.GetThingList(map))
            {
                if (thing.def == extension.dormantRelicDef)
                {
                    return thing;
                }
            }

            return null;
        }
    }

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
    {
        foreach (var option in base.GetFloatMenuOptions(selPawn))
        {
            yield return option;
        }

        var extension = Extension;

        if (extension?.takeRelicJob == null || HeldRelic == null)
        {
            yield break;
        }

        var label = (string)"Relics.Tempormortis.TakeRelic".Translate();
        var report = CanBeTakenBy(selPawn);

        if (!report.Accepted)
        {
            yield return new FloatMenuOption(label + " (" + report.Reason + ")", null);
            yield break;
        }

        if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
        {
            yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
            yield break;
        }

        if (!selPawn.CanReserve(this))
        {
            yield return new FloatMenuOption(label + " (" + "Reserved".Translate() + ")", null);
            yield break;
        }

        yield return new FloatMenuOption(label, delegate
        {
            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(extension.takeRelicJob, this), JobTag.Misc);
        });
    }

    /// <summary>Hands the relic to the taker; despawning it off the plinth is what kills the field and wakes the tomb.</summary>
    public void TakeRelic(Pawn taker)
    {
        if (taken)
        {
            return;
        }

        var relic = HeldRelic;

        if (relic == null || taker == null)
        {
            return;
        }

        var map = Map;

        relic.DeSpawn();

        if (!taker.inventory.innerContainer.TryAdd(relic))
        {
            GenPlace.TryPlaceThing(relic, taker.Position, map, ThingPlaceMode.Near);
        }

        Find.LetterStack.ReceiveLetter(
            "Relics.Tempormortis.TakenLetterLabel".Translate(),
            "Relics.Tempormortis.TakenLetterText".Translate(taker.Named("PAWN")),
            LetterDefOf.ThreatBig,
            taker);
    }

    public override void Notify_LostThing(Thing newItem)
    {
        base.Notify_LostThing(newItem);

        if (taken || newItem == null || newItem.def != Extension?.dormantRelicDef)
        {
            return;
        }

        ReleaseRelic(Map);
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        var map = Map;
        base.Destroy(mode);

        if (!taken)
        {
            ReleaseRelic(map);
        }
    }

    /// <summary>Marks the plinth empty, ends the dilation field and wakes the guardians, however the relic left.</summary>
    private void ReleaseRelic(Map map)
    {
        taken = true;
        EndDilation(map);
        LordJob_RelicGuardians.WakeAll(map);
    }

    private void EndDilation(Map map)
    {
        var conditionDef = Extension?.dilationCondition;

        if (conditionDef == null || map == null)
        {
            return;
        }

        map.gameConditionManager.GetActiveCondition(conditionDef)?.End();
    }

    public override string GetInspectString()
    {
        var text = base.GetInspectString();

        if (!text.NullOrEmpty())
        {
            text += "\n";
        }

        return text + (taken || HeldRelic == null
            ? "Relics.Tempormortis.InspectEmpty".Translate()
            : "Relics.Tempormortis.InspectHolding".Translate());
    }
}
