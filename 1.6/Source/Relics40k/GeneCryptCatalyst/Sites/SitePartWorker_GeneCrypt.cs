using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Relics40k;

/// <summary>
/// Dresses the crypt site: the gene-crypt seal, scattered ruin decoration, and a dormant
/// mechanoid garrison bedded down around it.
/// </summary>
public class SitePartWorker_GeneCrypt : SitePartWorker
{
    public override void PostMapGenerate(Map map)
    {
        base.PostMapGenerate(map);

        var extension = def.GetModExtension<DefModExtension_GeneCryptSite>();

        if (extension?.sealDef == null)
        {
            return;
        }

        var seal = extension.layoutDef != null
            ? SpawnVault(map, extension, out var vault)
            : SpawnLooseSeal(map, extension, out vault);

        if (seal == null)
        {
            Log.Warning("[Relics of Power] Could not place the gene-crypt on " + map);
            return;
        }

        StockShelves(map, vault, extension);
        SpawnDecoration(map, seal, extension);
        SpawnGarrison(map, seal, ThreatPointsFor(map) * extension.garrisonPointsFactor, extension.garrisonRadius);
    }

    /// <summary>Lays the authored vault down on the middle of the map and hands back its seal.</summary>
    private static Thing SpawnVault(Map map, DefModExtension_GeneCryptSite extension,
        out StructureLayoutUtility.Spawned spawned)
    {
        var size = StructureLayoutUtility.RotatedSize(extension.layoutDef, Rot4.North);
        var rect = CellRect.CenteredOn(map.Center, size).ClipInsideMap(map);

        spawned = StructureLayoutUtility.Spawn(map, extension.layoutDef, rect, Rot4.North,
            extension.wallStuff, RoofDefOf.RoofConstructed);

        RefogInterior(map, spawned);

        foreach (var thing in spawned.things)
        {
            if (thing.def == extension.sealDef)
            {
                return thing;
            }
        }

        return null;
    }

    /// <summary>
    /// The initial fog was computed before this site part ran, on open ground. Fog the vault's
    /// enclosed cells now; its walls stay lit from outside like any other blocker.
    /// </summary>
    private static void RefogInterior(Map map, StructureLayoutUtility.Spawned spawned)
    {
        foreach (var cell in spawned.structureCells)
        {
            var edifice = cell.GetEdifice(map);

            if (edifice != null && edifice.def.MakeFog)
            {
                continue;
            }

            map.fogGrid.Refog(CellRect.SingleCell(cell));
        }
    }

    private static Thing SpawnLooseSeal(Map map, DefModExtension_GeneCryptSite extension,
        out StructureLayoutUtility.Spawned spawned)
    {
        spawned = null;

        if (!TryFindSealCell(map, extension.sealDef, out var cell))
        {
            return null;
        }

        return GenSpawn.Spawn(ThingMaker.MakeThing(extension.sealDef), cell, map, Rot4.North);
    }

    /// <summary>Puts the site's loot list on the layout's shelves, one entry per shelf.</summary>
    private static void StockShelves(Map map, StructureLayoutUtility.Spawned spawned, DefModExtension_GeneCryptSite extension)
    {
        if (spawned == null || extension.layoutLoot.NullOrEmpty())
        {
            return;
        }

        var shelves = new List<Thing>();

        foreach (var thing in spawned.things)
        {
            if (thing is Building_Storage && !thing.Destroyed)
            {
                shelves.Add(thing);
            }
        }

        if (shelves.Count == 0)
        {
            return;
        }

        shelves.Shuffle();

        var next = 0;

        foreach (var entry in extension.layoutLoot)
        {
            if (next >= shelves.Count || entry.thingDef == null || !Rand.Chance(entry.chance))
            {
                continue;
            }

            var stuff = entry.thingDef.MadeFromStuff
                ? entry.stuff ?? GenStuff.DefaultStuffFor(entry.thingDef)
                : null;

            var loot = ThingMaker.MakeThing(entry.thingDef, stuff);
            loot.stackCount = Mathf.Clamp(entry.countRange.RandomInRange, 1, entry.thingDef.stackLimit);

            if (entry.quality.HasValue)
            {
                loot.TryGetComp<CompQuality>()?.SetQuality(entry.quality.Value, ArtGenerationContext.Outsider);
            }

            if (GenPlace.TryPlaceThing(loot, shelves[next].Position, map, ThingPlaceMode.Near))
            {
                next++;
            }
        }
    }

    private float ThreatPointsFor(Map map)
    {
        if (map.Parent is not Site site)
        {
            return 0f;
        }

        foreach (var part in site.parts)
        {
            if (part.def == def)
            {
                return part.parms.threatPoints;
            }
        }

        return 0f;
    }

    private static bool TryFindSealCell(Map map, ThingDef sealDef, out IntVec3 cell)
    {
        bool Validator(IntVec3 candidate)
        {
            foreach (var occupied in GenAdj.OccupiedRect(candidate, Rot4.North, sealDef.size).ExpandedBy(2))
            {
                if (!occupied.InBounds(map) || !occupied.Standable(map))
                {
                    return false;
                }

                if (occupied.GetEdifice(map) != null)
                {
                    return false;
                }
            }

            return true;
        }

        if (CellFinder.TryFindRandomCellNear(map.Center, map, 30, Validator, out cell))
        {
            return true;
        }

        return CellFinderLoose.TryFindRandomNotEdgeCellWith(12, Validator, map, out cell);
    }

    private static void SpawnDecoration(Map map, Thing seal, DefModExtension_GeneCryptSite extension)
    {
        if (extension.decorationDefs.NullOrEmpty())
        {
            return;
        }

        var count = extension.decorationCountRange.RandomInRange;

        for (var i = 0; i < count; i++)
        {
            var decorationDef = extension.decorationDefs.RandomElement();

            bool Validator(IntVec3 candidate)
            {
                return candidate.InBounds(map)
                       && candidate.Standable(map)
                       && candidate.GetEdifice(map) == null
                       && candidate.GetFirstItem(map) == null;
            }

            if (!CellFinder.TryFindRandomCellNear(seal.Position, map, extension.decorationRadius, Validator, out var cell))
            {
                continue;
            }

            var stuff = decorationDef.MadeFromStuff ? GenStuff.DefaultStuffFor(decorationDef) : null;

            GenSpawn.Spawn(ThingMaker.MakeThing(decorationDef, stuff), cell, map, Rot4.Random);
        }
    }

    private static void SpawnGarrison(Map map, Thing seal, float points, int radius)
    {
        if (points <= 0f)
        {
            return;
        }

        var mechanoids = Faction.OfMechanoids;

        if (mechanoids == null)
        {
            return;
        }

        var pawns = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
        {
            groupKind = PawnGroupKindDefOf.Combat,
            tile = map.Tile,
            faction = mechanoids,
            points = points
        }).ToList();

        if (pawns.Count == 0)
        {
            return;
        }

        var lord = LordMaker.MakeNewLord(
            mechanoids,
            new LordJob_SleepThenMechanoidsDefend([seal], mechanoids, 40f, seal.Position, canAssaultColony: false, isMechCluster: false),
            map);

        foreach (var pawn in pawns)
        {
            GenSpawn.Spawn(pawn, CellFinder.RandomClosewalkCellNear(seal.Position, map, radius), map);
            lord.AddPawn(pawn);
            pawn.TryGetComp<CompCanBeDormant>()?.ToSleep();
        }

        if (seal is Building building)
        {
            lord.AddBuilding(building);
        }
    }
}
