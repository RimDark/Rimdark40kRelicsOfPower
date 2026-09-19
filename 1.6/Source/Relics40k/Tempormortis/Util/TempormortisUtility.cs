using System.Collections.Generic;
using Core40k;
using RimWorld;
using UnityEngine;
using Verse;

namespace Relics40k;

public static class TempormortisUtility
{
    private const long TicksPerYear = 3600000L;

    /// <summary>Pawns whose genes slow their ageing below this rate never pay the sands.</summary>
    public const float LongLivedAgeFactorThreshold = 0.5f;

    /// <summary>True when the pawn holds at least one of the given ranks. An empty list passes.</summary>
    public static bool HasAnyRank(Pawn pawn, List<RankDef> ranks)
    {
        if (ranks.NullOrEmpty())
        {
            return true;
        }

        var rankInfo = pawn?.GetComp<CompRankInfo>();

        if (rankInfo == null)
        {
            return false;
        }

        for (var i = 0; i < ranks.Count; i++)
        {
            if (rankInfo.HasRank(ranks[i]))
            {
                return true;
            }
        }

        return false;
    }

    public static Pawn FindRankedColonist(List<RankDef> ranks)
    {
        foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
        {
            if (HasAnyRank(pawn, ranks))
            {
                return pawn;
            }
        }

        return null;
    }

    /// <summary>
    /// Free colonists on the map whose ageing has not been slowed by their genes. Space marines,
    /// perpetuals and any other long-lived pawn fall out here without naming another mod.
    /// </summary>
    public static List<Pawn> PawnsThatCanPayYears(Map map)
    {
        var payers = new List<Pawn>();

        if (map == null)
        {
            return payers;
        }

        foreach (var pawn in map.mapPawns.FreeColonistsSpawned)
        {
            if (pawn == null || pawn.Dead)
            {
                continue;
            }

            var ageFactor = pawn.genes?.BiologicalAgeTickFactor ?? 1f;

            if (ageFactor >= LongLivedAgeFactorThreshold)
            {
                payers.Add(pawn);
            }
        }

        return payers;
    }

    /// <summary>
    /// Splits totalYears as evenly as possible across the payers, never giving anyone less than
    /// minYearsEach, and returns the years actually taken.
    /// </summary>
    public static int ChargeYears(List<Pawn> payers, int totalYears, int minYearsEach)
    {
        if (payers.NullOrEmpty() || totalYears <= 0)
        {
            return 0;
        }

        var count = payers.Count;
        var baseYears = totalYears / count;
        var remainder = totalYears % count;
        var charged = 0;

        for (var i = 0; i < count; i++)
        {
            var years = Mathf.Max(baseYears + (i < remainder ? 1 : 0), minYearsEach);
            AgePawn(payers[i], years);
            charged += years;
        }

        return charged;
    }

    /// <summary>
    /// Advances biological age one birthday at a time so the age-related health rolls that hang off
    /// birthdays still happen, the way the game's own age jump does.
    /// </summary>
    public static void AgePawn(Pawn pawn, int years)
    {
        if (pawn?.ageTracker == null || years <= 0)
        {
            return;
        }

        var remaining = years * TicksPerYear;

        while (remaining > 0)
        {
            var ageBefore = pawn.ageTracker.AgeBiologicalYears;
            var toNextBirthday = NextBirthdayTick(pawn.ageTracker.AgeBiologicalTicks) - pawn.ageTracker.AgeBiologicalTicks;
            var step = toNextBirthday > remaining ? remaining : toNextBirthday;

            pawn.ageTracker.AgeBiologicalTicks += step;
            remaining -= step;

            if (pawn.ageTracker.AgeBiologicalYears > ageBefore)
            {
                pawn.ageTracker.DebugForceBirthdayBiological();
            }
        }
    }

    private static long NextBirthdayTick(long ageTicks)
    {
        return (ageTicks / TicksPerYear + 1) * TicksPerYear;
    }
}
