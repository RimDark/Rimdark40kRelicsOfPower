using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Relics40k;

/// <summary>
/// The frozen state is the lord toil itself, so nothing is stored and nothing has to be cleared:
/// once the relic is lifted the lord leaves the toil and the guardians are ordinary pawns again.
/// </summary>
public static class FrozenInTimeUtility
{
    private const int MoteIntervalTicks = 60;

    private const int MoteCacheCap = 64;

    private static readonly Dictionary<int, int> LastMoteTick = new Dictionary<int, int>();

    private static readonly List<int> StaleKeys = new List<int>();

    private static readonly Color MoteColor = new Color(0.55f, 0.7f, 1f);

    public static bool IsFrozen(Pawn pawn)
    {
        return pawn != null && pawn.GetLord()?.CurLordToil is LordToil_FrozenInTime;
    }

    /// <summary>Shows the player that the blow landed on something that is not moving.</summary>
    public static void ThrowAbsorbMote(Pawn pawn)
    {
        if (pawn == null || !pawn.Spawned || pawn.Map == null)
        {
            return;
        }

        var now = Find.TickManager.TicksGame;

        if (LastMoteTick.TryGetValue(pawn.thingIDNumber, out var last) && now - last < MoteIntervalTicks)
        {
            return;
        }

        Prune(now);
        LastMoteTick[pawn.thingIDNumber] = now;
        MoteMaker.ThrowText(pawn.DrawPos, pawn.Map, "Relics.Tempormortis.Unmoved".Translate(), MoteColor, 1.9f);
    }

    private static void Prune(int now)
    {
        if (LastMoteTick.Count < MoteCacheCap)
        {
            return;
        }

        StaleKeys.Clear();

        foreach (var pair in LastMoteTick)
        {
            if (now - pair.Value >= MoteIntervalTicks)
            {
                StaleKeys.Add(pair.Key);
            }
        }

        for (var i = 0; i < StaleKeys.Count; i++)
        {
            LastMoteTick.Remove(StaleKeys[i]);
        }

        StaleKeys.Clear();
    }
}
