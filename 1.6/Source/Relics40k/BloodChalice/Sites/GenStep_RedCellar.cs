using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Relics40k;

/// <summary>
/// Buries the crypt in the mountain, cuts a passage out to open ground, clears a landing pad at the
/// mouth, then puts the chalice on its pedestal and the guardians asleep around it.
/// </summary>
public class GenStep_RedCellar : GenStep
{
    private const string GuardianWakeMessageKey = "Relics.BloodChalice.GuardiansWake";

    public override int SeedPart => 1181749253;

    public override void Generate(Map map, GenStepParams parms)
    {
        var extension = parms.sitePart?.def.GetModExtension<DefModExtension_RedCellar>();

        if (extension == null)
        {
            return;
        }

        if (extension.roomLayout != null)
        {
            GenerateFromLayout(map, extension);
            return;
        }

        var prefab = extension.roomPrefab;
        var size = prefab != null
            ? new IntVec2(Mathf.Max(prefab.size.x, 3), Mathf.Max(prefab.size.z, 3))
            : new IntVec2(Mathf.Max(extension.roomSize, 9), Mathf.Max(extension.roomSize, 9));

        if (!BuriedSiteUtility.TryFindBuriedCenter(map, size, extension.cavityPadding, extension.minRockDepth, out var center))
        {
            Log.Warning("[Relics of Power] Could not find a place for the red cellar.");
            return;
        }

        var rect = GenAdj.OccupiedRect(center, Rot4.North, size).ClipInsideMap(map);

        BuriedSiteUtility.SolidifyTerrain(map, rect.ExpandedBy(extension.cavityPadding));
        BuriedSiteUtility.BuryRect(map, rect, extension.cavityPadding, extension.minRockDepth);
        BuriedSiteUtility.CarveCavity(map, rect, extension.cavityPadding);
        ClearRect(map, rect);

        IntVec3 pedestalCell;
        IntVec3 doorCell;

        if (prefab != null)
        {
            SpawnPrefabRoom(map, rect, center, prefab, extension, out pedestalCell, out doorCell);
        }
        else
        {
            doorCell = BuildRoom(map, rect, extension);
            pedestalCell = rect.CenterCell;
            SpawnPedestal(map, pedestalCell, extension);
        }

        if (extension.roofRoom)
        {
            foreach (var cell in rect)
            {
                map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
            }
        }

        SpawnFilth(map, rect, extension);
        SpawnChalice(map, pedestalCell, extension);
        SpawnGuardians(map, rect, pedestalCell, extension, prefab != null);

        var mouth = BuriedSiteUtility.CarveEntrance(map, doorCell, extension.tunnelWidthRange,
            extension.tunnelJitterChance, extension.roofTunnel, extension.sideCaves, rect, extension.cavityPadding + 2);
        PlaceLandingPad(map, rect, mouth, extension);

        MapGenerator.SetVar("RectOfInterest", rect);
        MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects").Add(rect);
        BuriedSiteUtility.ReserveEntrance(map, mouth, extension.entranceClearRadius);
    }

    /// <summary>
    /// Builds the site around a hand-made layout: bury it, read the grid onto the map, then stock
    /// the crypt from what the layout put there rather than placing anything ourselves.
    /// </summary>
    private static void GenerateFromLayout(Map map, DefModExtension_RedCellar extension)
    {
        var layout = extension.roomLayout;
        var rotation = layout.allowRotation ? Rot4.Random : Rot4.North;
        var size = StructureLayoutUtility.RotatedSize(layout, rotation);

        if (size.x <= 0 || size.z <= 0)
        {
            Log.Warning("[Relics of Power] Red cellar layout " + layout.defName + " is empty.");
            return;
        }

        var reach = Mathf.CeilToInt(extension.hollowRadius + Mathf.Abs(extension.hollowNoiseAmplitude)) + 1;
        var claimed = new IntVec2(size.x + (reach * 2), size.z + (reach * 2));

        if (!BuriedSiteUtility.TryFindBuriedCenter(map, claimed, 0, extension.minRockDepth, out var center))
        {
            Log.Warning("[Relics of Power] Could not find a place for the red cellar.");
            return;
        }

        var rect = GenAdj.OccupiedRect(center, Rot4.North, size).ClipInsideMap(map);
        var limit = rect.ExpandedBy(reach).ClipInsideMap(map);

        BuriedSiteUtility.SolidifyTerrain(map, limit);
        BuriedSiteUtility.BuryRect(map, limit, 0, extension.minRockDepth);

        var spawned = StructureLayoutUtility.Spawn(map, layout, rect, rotation, extension.wallStuff,
            RoofDefOf.RoofConstructed);

        var hollow = BuriedSiteUtility.CarveHollowAround(map, spawned.structureCells, limit,
            extension.hollowRadius, extension.hollowNoiseAmplitude, extension.hollowNoiseFrequency,
            RoofDefOf.RoofRockThick, extension.hollowTerrain);

        SealAroundLayout(map, limit, spawned, hollow);

        var pedestalCell = FindPedestalCell(map, spawned, rect, extension);

        SpawnChalice(map, pedestalCell, extension);
        FillLayoutCaskets(map, spawned, extension);
        FuelLayoutLamps(spawned, extension);
        StockLayoutStorage(map, spawned, extension);
        SpawnFilthIn(map, spawned.structureCells, extension);

        var doorCell = spawned.entranceCell.IsValid
            ? spawned.entranceCell
            : new IntVec3(rect.CenterCell.x, 0, rect.minZ);

        var mouth = BuriedSiteUtility.CarveEntrance(map, doorCell, extension.tunnelWidthRange,
            extension.tunnelJitterChance, extension.roofTunnel, extension.sideCaves, limit, 2);

        PlaceLandingPad(map, limit, mouth, extension);

        MapGenerator.SetVar("RectOfInterest", limit);
        MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects").Add(limit);
        BuriedSiteUtility.ReserveEntrance(map, mouth, extension.entranceClearRadius);
    }

    /// <summary>
    /// Fills every cell the site claimed that is neither structure nor hollow, so the space around the
    /// temple cannot end up with a hole in it that bypasses the entrance passage.
    /// </summary>
    private static void SealAroundLayout(Map map, CellRect rect, StructureLayoutUtility.Spawned spawned,
        HashSet<IntVec3> hollow)
    {
        var rockDef = Find.World.NaturalRockTypesIn(map.Tile).RandomElementWithFallback(ThingDefOf.Sandstone);

        if (rockDef == null)
        {
            return;
        }

        var used = new HashSet<IntVec3>(spawned.structureCells);
        used.UnionWith(hollow);

        foreach (var cell in rect)
        {
            if (used.Contains(cell) || cell.OnEdge(map) || cell.GetEdifice(map) != null)
            {
                continue;
            }

            GenSpawn.Spawn(rockDef, cell, map);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
        }
    }

    private static IntVec3 FindPedestalCell(Map map, StructureLayoutUtility.Spawned spawned, CellRect rect,
        DefModExtension_RedCellar extension)
    {
        foreach (var thing in spawned.things)
        {
            if (!thing.Destroyed && thing.TryGetComp<CompPedestal>() != null)
            {
                return thing.Position;
            }
        }

        var fallback = spawned.structureCells.Count > 0 ? spawned.structureCells.RandomElement() : rect.CenterCell;
        SpawnPedestal(map, fallback, extension);

        return fallback;
    }

    private static void FillLayoutCaskets(Map map, StructureLayoutUtility.Spawned spawned,
        DefModExtension_RedCellar extension)
    {
        var caskets = new List<Building_GuardianSarcophagus>();

        foreach (var thing in spawned.things)
        {
            if (thing is Building_GuardianSarcophagus casket && !casket.Destroyed)
            {
                caskets.Add(casket);
            }
        }

        if (caskets.Count == 0 || extension.guardianKind == null)
        {
            return;
        }

        caskets.Shuffle();

        var faction = Faction.OfAncientsHostile;
        var filled = 0;

        foreach (var casket in caskets)
        {
            casket.wakeMessageKey = GuardianWakeMessageKey;

            if (filled >= extension.guardianCount)
            {
                casket.MarkOpened();
                continue;
            }

            var pawn = MakeGuardian(map, faction, extension);

            if (casket.TryAcceptThing(pawn, false))
            {
                filled++;
            }
            else
            {
                casket.MarkOpened();
                Find.WorldPawns.PassToWorld(pawn, RimWorld.Planet.PawnDiscardDecideMode.Discard);
            }
        }
    }

    /// <summary>Sets every lamp and torch in the layout to the same share of its tank.</summary>
    private static void FuelLayoutLamps(StructureLayoutUtility.Spawned spawned, DefModExtension_RedCellar extension)
    {
        if (extension.layoutLampFuelFraction < 0f)
        {
            return;
        }

        var fraction = Mathf.Clamp01(extension.layoutLampFuelFraction);

        foreach (var thing in spawned.things)
        {
            var comp = thing.TryGetComp<CompRefuelable>();

            if (comp == null)
            {
                continue;
            }

            comp.ConsumeFuel(comp.Fuel);
            comp.Refuel(comp.Props.fuelCapacity * fraction);
        }
    }

    private static void StockLayoutStorage(Map map, StructureLayoutUtility.Spawned spawned,
        DefModExtension_RedCellar extension)
    {
        if (extension.layoutLoot.NullOrEmpty())
        {
            return;
        }

        var shelves = new List<Thing>();

        foreach (var thing in spawned.things)
        {
            if (thing is Building_Storage && thing.def != extension.pedestalDef && !thing.Destroyed)
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

    private static void SpawnFilthIn(Map map, List<IntVec3> cells, DefModExtension_RedCellar extension)
    {
        if (extension.filthDef == null || extension.filthCount <= 0 || cells.Count == 0)
        {
            return;
        }

        for (var i = 0; i < extension.filthCount; i++)
        {
            var cell = cells.RandomElement();

            if (cell.Standable(map))
            {
                FilthMaker.TryMakeFilth(cell, map, extension.filthDef);
            }
        }
    }

    private static void PlaceLandingPad(Map map, CellRect cryptRect, IntVec3 mouth, DefModExtension_RedCellar extension)
    {
        var size = Mathf.Max(extension.landingPadSize, 7);

        if (!TryFindPad(map, cryptRect, mouth, size, extension.landingPadMinDistanceFromCrypt, extension, out var pad)
            && !TryFindPad(map, cryptRect, mouth, size, 0, extension, out pad))
        {
            return;
        }

        foreach (var cell in pad)
        {
            map.roofGrid.SetRoof(cell, null);

            foreach (var thing in cell.GetThingList(map).ToList())
            {
                if (thing.def.category == ThingCategory.Plant || thing.def.category == ThingCategory.Item)
                {
                    thing.Destroy();
                }
            }
        }

        MapGenerator.PlayerStartSpot = pad.CenterCell;
        MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects").Add(pad);
    }

    private static bool TryFindPad(Map map, CellRect cryptRect, IntVec3 mouth, int size, int minDistanceFromCrypt,
        DefModExtension_RedCellar extension, out CellRect pad)
    {
        var usedRects = MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects");
        var cryptCenter = cryptRect.CenterCell;

        foreach (var candidate in GenRadial.RadialCellsAround(mouth, 60f, true))
        {
            if (!candidate.InBounds(map) || candidate.CloseToEdge(map, extension.landingPadMinDistanceFromEdge))
            {
                continue;
            }

            if (minDistanceFromCrypt > 0 && candidate.DistanceTo(cryptCenter) < minDistanceFromCrypt)
            {
                continue;
            }

            var rect = CellRect.CenteredOn(candidate, size, size);

            if (!rect.InBounds(map) || usedRects.Any(r => r.Overlaps(rect)) || rect.Overlaps(cryptRect.ExpandedBy(2)))
            {
                continue;
            }

            if (rect.Cells.All(c => IsPadCell(map, c)))
            {
                pad = rect;
                return true;
            }
        }

        pad = CellRect.Empty;
        return false;
    }

    private static bool IsPadCell(Map map, IntVec3 cell)
    {
        if (cell.GetEdifice(map) != null || cell.Roofed(map))
        {
            return false;
        }

        if (cell.GetTerrain(map).IsWater)
        {
            return false;
        }

        return cell.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy);
    }

    private static void ClearRect(Map map, CellRect rect)
    {
        var toDestroy = new List<Thing>();

        foreach (var cell in rect)
        {
            toDestroy.AddRange(cell.GetThingList(map));
        }

        foreach (var thing in toDestroy)
        {
            if (!thing.Destroyed && thing.def.destroyable)
            {
                thing.Destroy();
            }
        }
    }

    private static void SpawnPrefabRoom(Map map, CellRect rect, IntVec3 center, PrefabDef prefab,
        DefModExtension_RedCellar extension, out IntVec3 pedestalCell, out IntVec3 doorCell)
    {
        var spawned = new List<Thing>();
        PrefabUtility.SpawnPrefab(prefab, map, center, Rot4.North, null, spawned);

        pedestalCell = IntVec3.Invalid;
        doorCell = IntVec3.Invalid;

        foreach (var thing in spawned)
        {
            if (thing.TryGetComp<CompPedestal>() != null)
            {
                pedestalCell = thing.Position;
            }
            else if (thing.def.IsDoor)
            {
                doorCell = thing.Position;
            }
        }

        if (!pedestalCell.IsValid)
        {
            pedestalCell = rect.CenterCell;
            SpawnPedestal(map, pedestalCell, extension);
        }

        if (!doorCell.IsValid)
        {
            doorCell = new IntVec3(rect.CenterCell.x, 0, rect.minZ);
        }
    }

    private static IntVec3 BuildRoom(Map map, CellRect rect, DefModExtension_RedCellar extension)
    {
        var wallStuff = extension.wallStuff ?? ThingDefOf.BlocksGranite;
        var floor = extension.floorTerrain ?? TerrainDefOf.FlagstoneSandstone;
        var doorCell = new IntVec3(rect.CenterCell.x, 0, rect.minZ);

        foreach (var cell in rect)
        {
            map.terrainGrid.SetTerrain(cell, floor);
        }

        foreach (var cell in rect.EdgeCells)
        {
            var def = cell == doorCell ? ThingDefOf.Door : ThingDefOf.Wall;
            GenSpawn.Spawn(ThingMaker.MakeThing(def, wallStuff), cell, map);
        }

        return doorCell;
    }

    private static void SpawnFilth(Map map, CellRect rect, DefModExtension_RedCellar extension)
    {
        if (extension.filthDef == null || extension.filthCount <= 0)
        {
            return;
        }

        var interior = rect.ContractedBy(1);

        for (var i = 0; i < extension.filthCount; i++)
        {
            var cell = interior.RandomCell;

            if (cell.Standable(map))
            {
                FilthMaker.TryMakeFilth(cell, map, extension.filthDef);
            }
        }
    }

    private static void SpawnPedestal(Map map, IntVec3 cell, DefModExtension_RedCellar extension)
    {
        if (extension.pedestalDef != null)
        {
            GenSpawn.Spawn(ThingMaker.MakeThing(extension.pedestalDef), cell, map);
        }
    }

    private static void SpawnChalice(Map map, IntVec3 cell, DefModExtension_RedCellar extension)
    {
        if (extension.chaliceDef == null)
        {
            return;
        }

        var existing = map.listerThings.ThingsOfDef(extension.chaliceDef);
        var chalice = existing.Count > 0 ? existing[0] : ThingMaker.MakeThing(extension.chaliceDef);

        chalice.TryGetComp<CompUseEffect_BloodChalice>()?.StartCooldown();

        var pedestal = cell.GetFirstThingWithComp<CompPedestal>(map)?.GetComp<CompPedestal>();

        if (pedestal != null)
        {
            pedestal.action = new PedestalAction_WakeGuardians();
        }

        if ((pedestal == null || !pedestal.TryPlace(chalice)) && !chalice.Spawned)
        {
            GenSpawn.Spawn(chalice, cell, map);
        }
    }

    private static void SpawnGuardians(Map map, CellRect rect, IntVec3 pedestalCell,
        DefModExtension_RedCellar extension, bool fromPrefab)
    {
        if (extension.guardianKind == null || extension.guardianCount <= 0)
        {
            return;
        }

        var faction = Faction.OfAncientsHostile;
        var casketDef = extension.casketDef;
        var sealable = casketDef?.thingClass != null && typeof(Building_GuardianSarcophagus).IsAssignableFrom(casketDef.thingClass);
        var stuff = casketDef != null && casketDef.MadeFromStuff ? extension.wallStuff ?? ThingDefOf.BlocksGranite : null;
        var size = casketDef?.size ?? IntVec2.One;
        var interior = rect.ContractedBy(1);
        var used = new HashSet<IntVec3> { pedestalCell };
        var corners = fromPrefab ? null : CornerCells(rect, size);
        var total = sealable ? extension.guardianCount + Mathf.Max(extension.emptyCasketCount, 0) : extension.guardianCount;
        var loose = new List<Pawn>();

        for (var i = 0; i < total; i++)
        {
            var preferred = corners != null && i < corners.Count ? corners[i] : IntVec3.Invalid;
            var cellSize = sealable ? size : IntVec2.One;

            if (!TryFindGuardianCell(map, interior, pedestalCell, used, preferred, cellSize, out var cell))
            {
                continue;
            }

            foreach (var occupied in GenAdj.OccupiedRect(cell, Rot4.North, cellSize))
            {
                used.Add(occupied);
            }

            var pawn = i < extension.guardianCount ? MakeGuardian(map, faction, extension) : null;

            if (!sealable)
            {
                if (pawn != null)
                {
                    GenSpawn.Spawn(pawn, cell, map);
                    loose.Add(pawn);
                }

                continue;
            }

            var casket = (Building_GuardianSarcophagus)ThingMaker.MakeThing(casketDef, stuff);
            casket.wakeMessageKey = GuardianWakeMessageKey;

            if (pawn == null || !casket.TryAcceptThing(pawn, false))
            {
                casket.MarkOpened();

                if (pawn != null)
                {
                    Find.WorldPawns.PassToWorld(pawn, RimWorld.Planet.PawnDiscardDecideMode.Discard);
                }
            }

            GenSpawn.Spawn(casket, cell, map, Rot4.North);
        }

        if (loose.Count > 0)
        {
            LordMaker.MakeNewLord(faction, new LordJob_RelicGuardians(faction, GuardianWakeMessageKey), map, loose);
        }
    }

    /// <summary>The four inner corners of the crypt, then the middle of its far wall.</summary>
    private static List<IntVec3> CornerCells(CellRect rect, IntVec2 size)
    {
        var inner = rect.ContractedBy(2);
        var farZ = inner.maxZ - Mathf.Max(size.z - 1, 0);

        return
        [
            new IntVec3(inner.minX, 0, farZ),
            new IntVec3(inner.maxX, 0, farZ),
            new IntVec3(inner.minX, 0, inner.minZ),
            new IntVec3(inner.maxX, 0, inner.minZ),
            new IntVec3(rect.CenterCell.x, 0, farZ)
        ];
    }

    private static Pawn MakeGuardian(Map map, Faction faction, DefModExtension_RedCellar extension)
    {
        var request = new PawnGenerationRequest(
            extension.guardianKind,
            faction,
            PawnGenerationContext.NonPlayer,
            map.Tile,
            forceGenerateNewPawn: true,
            canGeneratePawnRelations: false,
            mustBeCapableOfViolence: true);

        var pawn = PawnGenerator.GeneratePawn(request);
        ApplyGuardianState(pawn, extension);

        return pawn;
    }

    private static bool TryFindGuardianCell(Map map, CellRect interior, IntVec3 pedestalCell, HashSet<IntVec3> used,
        IntVec3 preferred, IntVec2 size, out IntVec3 cell)
    {
        if (preferred.IsValid && Fits(map, interior, used, preferred, size))
        {
            cell = preferred;
            return true;
        }

        return CellFinder.TryFindRandomCellNear(pedestalCell, map, Mathf.Max(interior.Width, interior.Height),
            c => Fits(map, interior, used, c, size) && !c.AdjacentToCardinal(pedestalCell),
            out cell);
    }

    private static bool Fits(Map map, CellRect interior, HashSet<IntVec3> used, IntVec3 cell, IntVec2 size)
    {
        var occupied = GenAdj.OccupiedRect(cell, Rot4.North, size);

        if (!interior.Contains(occupied.Min) || !interior.Contains(occupied.Max))
        {
            return false;
        }

        foreach (var c in occupied)
        {
            if (used.Contains(c) || !c.Standable(map))
            {
                return false;
            }
        }

        return true;
    }

    private static void ApplyGuardianState(Pawn pawn, DefModExtension_RedCellar extension)
    {
        if (extension.guardianHediff != null && !pawn.health.hediffSet.HasHediff(extension.guardianHediff))
        {
            pawn.health.AddHediff(extension.guardianHediff);
        }

        if (pawn.genes == null || extension.guardianXenogenes.NullOrEmpty())
        {
            return;
        }

        foreach (var geneDef in extension.guardianXenogenes)
        {
            if (!pawn.genes.HasActiveGene(geneDef))
            {
                pawn.genes.AddGene(geneDef, true);
            }
        }
    }
}
