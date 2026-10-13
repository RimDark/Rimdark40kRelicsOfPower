using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace Relics40k;

/// <summary>
/// A sealed sarcophagus holding one dormant relic guardian. Lifting the relic, damaging the casket
/// or opening it deliberately bursts every sealed sarcophagus on the map at once and throws whoever
/// comes out at the colony.
/// </summary>
public class Building_GuardianSarcophagus : Building_Casket
{
    private static List<Pawn> releaseBuffer;

    public string wakeMessageKey;

    public override int OpenTicks => 500;

    /// <summary>Marks an empty sarcophagus as already opened so it does not read as sealed.</summary>
    public void MarkOpened()
    {
        contentsKnown = true;
    }

    /// <summary>Bursts every sealed guardian sarcophagus on the map.</summary>
    public static void ReleaseAll(Map map)
    {
        Release(map, null, null);
    }

    public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
    {
        base.PreApplyDamage(ref dinfo, out absorbed);

        if (absorbed)
        {
            return;
        }

        if (HasAnyContents && dinfo.Def.harmsHealth && dinfo.Instigator != null && dinfo.Instigator.Faction != null)
        {
            EjectContents();
        }

        absorbed = false;
    }

    public override void EjectContents()
    {
        var map = Map;
        var mine = new List<Pawn>();

        foreach (Thing thing in (IEnumerable<Thing>)innerContainer)
        {
            if (thing is Pawn pawn)
            {
                PawnComponentsUtility.AddComponentsForSpawn(pawn);
                mine.Add(pawn);
            }
        }

        base.EjectContents();

        if (releaseBuffer != null)
        {
            releaseBuffer.AddRange(mine);
            return;
        }

        Release(map, mine, wakeMessageKey);
    }

    private static void Release(Map map, List<Pawn> seed, string messageKey)
    {
        if (map == null || releaseBuffer != null)
        {
            return;
        }

        releaseBuffer = new List<Pawn>();

        if (seed != null)
        {
            releaseBuffer.AddRange(seed);
        }

        try
        {
            foreach (var casket in SealedOn(map))
            {
                messageKey ??= casket.wakeMessageKey;
                casket.EjectContents();
            }

            MakeAssaultLord(map, releaseBuffer, messageKey);
        }
        finally
        {
            releaseBuffer = null;
        }
    }

    private static List<Building_GuardianSarcophagus> SealedOn(Map map)
    {
        var result = new List<Building_GuardianSarcophagus>();
        var buildings = map.listerThings.ThingsInGroup(ThingRequestGroup.BuildingArtificial);

        for (var i = 0; i < buildings.Count; i++)
        {
            if (buildings[i] is Building_GuardianSarcophagus casket && casket.HasAnyContents)
            {
                result.Add(casket);
            }
        }

        return result;
    }

    private static void MakeAssaultLord(Map map, List<Pawn> pawns, string messageKey)
    {
        pawns.RemoveAll(p => p == null || p.Dead || !p.Spawned || p.Faction == null || p.GetLord() != null);

        if (pawns.Count == 0)
        {
            return;
        }

        var faction = pawns[0].Faction;
        LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, false, false), map, pawns);

        if (!messageKey.NullOrEmpty())
        {
            Messages.Message(messageKey.Translate(), new LookTargets(pawns[0]), MessageTypeDefOf.ThreatBig);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref wakeMessageKey, "wakeMessageKey");
    }
}
