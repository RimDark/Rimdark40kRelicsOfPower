using RimWorld;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// Holds an entire map inside the tempormortis field: every pawn on it, the player's included, is
/// kept under the same dilation hediff the relic itself inflicts.
/// </summary>
public class GameCondition_TemporalDilation : GameCondition
{
    private static readonly SkyColorSet SkyColors = new SkyColorSet(
        new ColorInt(200, 215, 255).ToColor,
        new ColorInt(60, 80, 130).ToColor,
        new Color(0.6f, 0.65f, 0.8f),
        0.85f);

    public override int TransitionTicks => 600;

    private DefModExtension_TemporalDilation Extension => def.GetModExtension<DefModExtension_TemporalDilation>();

    public override void GameConditionTick()
    {
        base.GameConditionTick();

        var extension = Extension;

        if (extension?.dilationHediff == null || extension.pulseIntervalTicks <= 0)
        {
            return;
        }

        if (Find.TickManager.TicksGame % extension.pulseIntervalTicks != 0)
        {
            return;
        }

        var maps = AffectedMaps;

        if (extension.anchorDef != null && !AnchorPresent(maps, extension.anchorDef))
        {
            End();
            return;
        }

        for (var i = 0; i < maps.Count; i++)
        {
            var pawns = maps[i].mapPawns.AllPawnsSpawned;

            for (var j = 0; j < pawns.Count; j++)
            {
                Affect(pawns[j], extension);
            }
        }
    }

    private static bool AnchorPresent(System.Collections.Generic.List<Map> maps, ThingDef anchorDef)
    {
        for (var i = 0; i < maps.Count; i++)
        {
            if (maps[i].listerThings.ThingsOfDef(anchorDef).Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void Affect(Pawn pawn, DefModExtension_TemporalDilation extension)
    {
        if (pawn.Dead || pawn.kindDef.immuneToGameConditionEffects)
        {
            return;
        }

        if (!extension.affectDowned && pawn.Downed)
        {
            return;
        }

        var existing = pawn.health.hediffSet.GetFirstHediffOfDef(extension.dilationHediff);

        if (existing != null)
        {
            var existingDisappears = existing.TryGetComp<HediffComp_Disappears>();

            if (existingDisappears != null)
            {
                existingDisappears.ticksToDisappear = extension.lingerTicks;
                return;
            }

            pawn.health.RemoveHediff(existing);
        }

        var hediff = HediffMaker.MakeHediff(extension.dilationHediff, pawn);
        var disappears = hediff.TryGetComp<HediffComp_Disappears>();

        if (disappears != null)
        {
            disappears.ticksToDisappear = extension.lingerTicks;
        }

        pawn.health.AddHediff(hediff);
    }

    public override float SkyTargetLerpFactor(Map map)
    {
        return GameConditionUtility.LerpInOutValue(this, TransitionTicks, 0.3f);
    }

    public override SkyTarget? SkyTarget(Map map)
    {
        return new SkyTarget(0.9f, SkyColors, 1f, 1f);
    }
}
