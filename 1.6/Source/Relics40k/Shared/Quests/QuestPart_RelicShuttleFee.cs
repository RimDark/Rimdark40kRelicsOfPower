using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// Gates acceptance on the colony having the passage fee, and takes the silver when the quest is
/// accepted. Silver is counted and consumed on one map only, so the cost is predictable.
/// </summary>
public class QuestPart_RelicShuttleFee : QuestPart_RequirementsToAccept
{
    public int amount;

    public MapParent mapParent;

    public override IEnumerable<GlobalTargetInfo> Culprits
    {
        get
        {
            if (mapParent != null)
            {
                yield return mapParent;
            }
        }
    }

    public override AcceptanceReport CanAccept()
    {
        if (amount <= 0)
        {
            return true;
        }

        if (AvailableSilver() < amount)
        {
            return new AcceptanceReport("Relics.Shared.LighterNeedSilver".Translate(amount));
        }

        return true;
    }

    public override void PreQuestAccept()
    {
        base.PreQuestAccept();

        if (amount <= 0)
        {
            return;
        }

        var remaining = amount;

        foreach (var silver in SilverOnMap())
        {
            if (remaining <= 0)
            {
                break;
            }

            var taken = Mathf.Min(remaining, silver.stackCount);
            silver.SplitOff(taken).Destroy();
            remaining -= taken;
        }
    }

    private int AvailableSilver()
    {
        var total = 0;

        foreach (var silver in SilverOnMap())
        {
            total += silver.stackCount;
        }

        return total;
    }

    private List<Thing> SilverOnMap()
    {
        var result = new List<Thing>();
        var map = mapParent?.Map;

        if (map == null)
        {
            return result;
        }

        foreach (var thing in map.listerThings.ThingsOfDef(ThingDefOf.Silver))
        {
            if (thing.Spawned && !thing.IsForbidden(Faction.OfPlayer))
            {
                result.Add(thing);
            }
        }

        return result;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref amount, "amount", 0);
        Scribe_References.Look(ref mapParent, "mapParent");
    }
}
