using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// Bleeds every humanlike without a hemogen gene, and feeds hemogen to those with one. Hangs a red
/// mist over the map while it lasts.
/// </summary>
public class GameCondition_ExsanguinatingAir : GameCondition
{
    private static readonly SkyColorSet SkyColors = new SkyColorSet(
        new ColorInt(255, 205, 200).ToColor,
        new ColorInt(110, 35, 45).ToColor,
        new Color(0.75f, 0.55f, 0.55f),
        0.8f);

    private List<SkyOverlay> overlays;

    public override int TransitionTicks => 1200;

    private DefModExtension_ExsanguinatingAir Extension => def.GetModExtension<DefModExtension_ExsanguinatingAir>();

    public override void GameConditionTick()
    {
        base.GameConditionTick();

        var extension = Extension;

        if (extension == null || extension.intervalTicks <= 0 || Find.TickManager.TicksGame % extension.intervalTicks != 0)
        {
            return;
        }

        var hours = extension.intervalTicks / (float)GenDate.TicksPerHour;
        var maps = AffectedMaps;

        for (var i = 0; i < maps.Count; i++)
        {
            var pawns = maps[i].mapPawns.AllPawnsSpawned;

            for (var j = 0; j < pawns.Count; j++)
            {
                Affect(pawns[j], extension, hours);
            }
        }
    }

    private static void Affect(Pawn pawn, DefModExtension_ExsanguinatingAir extension, float hours)
    {
        if (pawn.Dead || !pawn.RaceProps.Humanlike || !pawn.RaceProps.IsFlesh || pawn.kindDef.immuneToGameConditionEffects)
        {
            return;
        }

        if (pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>() != null)
        {
            if (extension.hemogenPerHour > 0f)
            {
                GeneUtility.OffsetHemogen(pawn, extension.hemogenPerHour * hours, false);
            }

            return;
        }

        if (extension.bloodLossPerHour <= 0f || IsImmune(pawn, extension))
        {
            return;
        }

        HealthUtility.AdjustSeverity(pawn, HediffDefOf.BloodLoss, extension.bloodLossPerHour * hours);
    }

    private static bool IsImmune(Pawn pawn, DefModExtension_ExsanguinatingAir extension)
    {
        if (extension.immuneHediffs.NullOrEmpty())
        {
            return false;
        }

        foreach (var hediffDef in extension.immuneHediffs)
        {
            if (pawn.health.hediffSet.HasHediff(hediffDef))
            {
                return true;
            }
        }

        return false;
    }

    public override List<SkyOverlay> SkyOverlays(Map map)
    {
        var extension = Extension;

        if (extension == null || !extension.drawMist)
        {
            return null;
        }

        return overlays ??= new List<SkyOverlay> { new SkyOverlay_TintedMist(extension.mistColor) };
    }

    public override float SkyTargetLerpFactor(Map map)
    {
        var intensity = Extension?.mistIntensity ?? 0.4f;

        return GameConditionUtility.LerpInOutValue(this, TransitionTicks, intensity);
    }

    public override SkyTarget? SkyTarget(Map map)
    {
        return new SkyTarget(0.85f, SkyColors, 1f, 1f);
    }
}
