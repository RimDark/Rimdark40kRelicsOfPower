using RimWorld;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// The vigil. Runs while the keeper wears the dormant hourglass, stays awake, undrafted and
/// unarmed; the enemy comes at a fixed interval throughout. Completing it wakes the relic and
/// takes its price out of everyone in the colony who still ages.
/// </summary>
public class HediffComp_TempormortisRite : HediffComp
{
    private int ticksElapsed;

    private int raidsSent;

    public HediffCompProperties_TempormortisRite Props => (HediffCompProperties_TempormortisRite)props;

    public int TicksRemaining => Mathf.Max(0, Props.durationTicks - ticksElapsed);

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref ticksElapsed, "ticksElapsed");
        Scribe_Values.Look(ref raidsSent, "raidsSent");
    }

    private Apparel FindDormantRelic()
    {
        if (Props.dormantDef == null || Pawn?.apparel == null)
        {
            return null;
        }

        var worn = Pawn.apparel.WornApparel;

        for (var i = 0; i < worn.Count; i++)
        {
            if (worn[i].def == Props.dormantDef)
            {
                return worn[i];
            }
        }

        return null;
    }

    private string BreakReason()
    {
        var pawn = Pawn;

        if (pawn == null || pawn.Dead || !pawn.Spawned)
        {
            return "Relics.Tempormortis.BrokeLost".Translate();
        }

        if (FindDormantRelic() == null)
        {
            return "Relics.Tempormortis.BrokeUnworn".Translate();
        }

        if (pawn.Downed || !pawn.Awake())
        {
            return "Relics.Tempormortis.BrokeUnconscious".Translate();
        }

        if (pawn.Drafted)
        {
            return "Relics.Tempormortis.BrokeDrafted".Translate();
        }

        if (pawn.equipment?.Primary != null)
        {
            return "Relics.Tempormortis.BrokeArmed".Translate();
        }

        return null;
    }

    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        base.CompPostTickInterval(ref severityAdjustment, delta);

        var reason = BreakReason();

        if (reason != null)
        {
            BreakRite(reason);
            return;
        }

        ticksElapsed += delta;

        if (Props.raidIntervalTicks > 0 && ticksElapsed < Props.durationTicks
            && ticksElapsed / Props.raidIntervalTicks > raidsSent)
        {
            raidsSent++;
            SendRaid();
        }

        if (ticksElapsed >= Props.durationTicks)
        {
            CompleteRite();
        }
    }

    private void SendRaid()
    {
        var map = Pawn?.MapHeld;

        if (map == null)
        {
            return;
        }

        var parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
        parms.forced = true;
        parms.points = Mathf.Max(parms.points * Props.raidPointsFactor, 250f);

        if (!IncidentDefOf.RaidEnemy.Worker.TryExecute(parms))
        {
            Messages.Message("Relics.Tempormortis.RaidFailed".Translate(), Pawn, MessageTypeDefOf.SilentInput, false);
        }
    }

    private void BreakRite(string reason)
    {
        var pawn = Pawn;

        if (Props.brokenThought != null && pawn?.needs?.mood != null)
        {
            pawn.needs.mood.thoughts.memories.TryGainMemory(Props.brokenThought);
        }

        Find.LetterStack.ReceiveLetter(
            "Relics.Tempormortis.RiteBrokenLetterLabel".Translate(),
            "Relics.Tempormortis.RiteBrokenLetterText".Translate(pawn.Named("PAWN"), reason.Named("REASON")),
            LetterDefOf.NegativeEvent,
            pawn);

        pawn?.health.RemoveHediff(parent);
    }

    private void CompleteRite()
    {
        var pawn = Pawn;
        var dormant = FindDormantRelic();

        if (pawn == null || dormant == null)
        {
            BreakRite("Relics.Tempormortis.BrokeLost".Translate());
            return;
        }

        AwakenRelic(pawn, dormant);

        if (Props.keptThought != null && pawn.needs?.mood != null)
        {
            pawn.needs.mood.thoughts.memories.TryGainMemory(Props.keptThought);
        }

        var payers = TempormortisUtility.PawnsThatCanPayYears(pawn.MapHeld);
        payers.Remove(pawn);
        var charged = TempormortisUtility.ChargeYears(payers, Props.totalYears, Props.minYearsEach);

        var text = charged > 0
            ? "Relics.Tempormortis.RiteKeptLetterText".Translate(pawn.Named("PAWN"), charged.Named("YEARS"), payers.Count.Named("COUNT"))
            : "Relics.Tempormortis.RiteKeptNoPayersLetterText".Translate(pawn.Named("PAWN"));

        Find.LetterStack.ReceiveLetter(
            "Relics.Tempormortis.RiteKeptLetterLabel".Translate(),
            text,
            LetterDefOf.PositiveEvent,
            pawn);

        pawn.health.RemoveHediff(parent);
    }

    private void AwakenRelic(Pawn pawn, Apparel dormant)
    {
        if (Props.awakenedDef == null)
        {
            return;
        }

        pawn.apparel.Remove(dormant);
        dormant.Destroy();

        if (ThingMaker.MakeThing(Props.awakenedDef) is not Apparel awakened)
        {
            return;
        }

        pawn.apparel.Wear(awakened, false);
    }

    public override string CompTipStringExtra => "Relics.Tempormortis.RiteRemaining".Translate(TicksRemaining.ToStringTicksToPeriod());
}
