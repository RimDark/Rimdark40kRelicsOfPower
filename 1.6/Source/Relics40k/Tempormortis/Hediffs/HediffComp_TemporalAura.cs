using RimWorld;
using Verse;

namespace Relics40k;

public class HediffComp_TemporalAura : HediffComp
{
    public HediffCompProperties_TemporalAura Props => (HediffCompProperties_TemporalAura)props;

    /// <summary>
    /// While the carrier hediff lasts, stamps or refreshes a short-lived slow hediff on every eligible pawn
    /// within radius. Pawns that leave the radius simply let their stamp expire.
    /// </summary>
    public override void CompPostTickInterval(ref float severityAdjustment, int delta)
    {
        base.CompPostTickInterval(ref severityAdjustment, delta);

        var pawn = Pawn;
        if (Props.slowHediff == null || !pawn.Spawned || !pawn.IsHashIntervalTick(Props.pulseIntervalTicks, delta))
        {
            return;
        }

        foreach (var thing in GenRadial.RadialDistinctThingsAround(pawn.Position, pawn.Map, Props.radius, true))
        {
            if (thing is not Pawn target || target == pawn || target.Dead)
            {
                continue;
            }
            if (Props.affectHostileOnly && !target.HostileTo(pawn))
            {
                continue;
            }
            if (!Props.affectDowned && target.Downed)
            {
                continue;
            }

            var existing = target.health.hediffSet.GetFirstHediffOfDef(Props.slowHediff);
            if (existing != null)
            {
                var existingDisappears = existing.TryGetComp<HediffComp_Disappears>();
                if (existingDisappears != null)
                {
                    existingDisappears.ticksToDisappear = Props.slowLingerTicks;
                    continue;
                }
                target.health.RemoveHediff(existing);
            }

            var slow = HediffMaker.MakeHediff(Props.slowHediff, target);
            var disappears = slow.TryGetComp<HediffComp_Disappears>();
            if (disappears != null)
            {
                disappears.ticksToDisappear = Props.slowLingerTicks;
            }
            target.health.AddHediff(slow);
        }
    }
}
