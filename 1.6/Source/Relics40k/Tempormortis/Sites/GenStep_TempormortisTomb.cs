using System.Collections.Generic;
using System.Linq;
using KCSG;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace Relics40k;

/// <summary>
/// Buries the tomb in the mountain, cuts a passage in, stands the dormant tempormortis on its
/// plinth and leaves the besiegers frozen mid-assault around it.
/// </summary>
public class GenStep_TempormortisTomb : GenStep
{
    private const string GuardianWakeMessageKey = "Relics.Tempormortis.GuardiansWake";

    public override int SeedPart => 1774903112;

    public override void Generate(Map map, GenStepParams parms)
    {
        var extension = parms.sitePart?.def.GetModExtension<DefModExtension_TempormortisTomb>();

        if (extension == null)
        {
            return;
        }

        var layout = extension.roomLayout;
        var rotation = Rot4.North;
        IntVec2 size;

        if (layout != null)
        {
            if (layout.Sizes.x <= 0 || layout.Sizes.z <= 0)
            {
                layout.ResolveLayouts();
            }

            rotation = layout.randomRotation ? Rot4.Random : Rot4.North;
            size = rotation.IsHorizontal
                ? new IntVec2(layout.Sizes.z, layout.Sizes.x)
                : new IntVec2(layout.Sizes.x, layout.Sizes.z);
        }
        else
        {
            var side = Mathf.Max(extension.roomSize, 9);
            size = new IntVec2(side, side);
        }

        if (!BuriedSiteUtility.TryFindBuriedCenter(map, size, extension.cavityPadding, extension.minRockDepth, out var center))
        {
            Log.Warning("[Relics of Power] Could not find a place for the tempormortis tomb.");
            return;
        }

        var rect = GenAdj.OccupiedRect(center, Rot4.North, size).ClipInsideMap(map);

        BuriedSiteUtility.SolidifyTerrain(map, rect.ExpandedBy(extension.cavityPadding));
        BuriedSiteUtility.BuryRect(map, rect, extension.cavityPadding, extension.minRockDepth);
        BuriedSiteUtility.CarveCavity(map, rect, extension.cavityPadding);

        IntVec3 doorCell;
        IntVec3 plinthCell;
        List<Thing> spawned = null;

        if (layout != null)
        {
            spawned = new List<Thing>();

            // KCSG reads these statics while it places; its own gen steps prime them first.
            GenOption.structureLayout = layout;
            GenOption.GetAllMineableIn(rect, map);
            LayoutUtils.CleanRect(layout, map, rect, true, rotation);
            LayoutUtils.Generate(layout, rect, map, spawned, null, true, rotation);

            doorCell = FindOuterDoor(spawned, rect, rect.CenterCell);
            plinthCell = FindSpawnedPedestal(spawned);
        }
        else
        {
            ClearRect(map, rect);
            doorCell = BuildRoom(map, rect, extension);
            plinthCell = rect.CenterCell;
            SpawnReliquary(map, plinthCell, extension);
            SpawnCasket(map, rect, extension);

            if (extension.roofRoom)
            {
                foreach (var cell in rect)
                {
                    map.roofGrid.SetRoof(cell, RoofDefOf.RoofConstructed);
                }
            }
        }

        var interior = CollectInterior(map, rect, plinthCell.IsValid ? plinthCell : doorCell);

        if (!plinthCell.IsValid)
        {
            var deepest = FurthestFrom(map, rect, doorCell);
            plinthCell = deepest.IsValid ? deepest : rect.CenterCell;
            SpawnReliquary(map, plinthCell, extension);
        }

        SpawnRelic(map, plinthCell, parms.sitePart?.def, extension);
        SpawnFilth(map, interior, extension);
        SpawnLoot(map, spawned, interior, extension);
        SpawnGuardians(map, interior, plinthCell, extension, parms.sitePart?.parms?.threatPoints ?? 0f);

        var mouth = BuriedSiteUtility.CarveEntrance(map, doorCell, extension.tunnelWidthRange,
            extension.tunnelJitterChance, extension.roofTunnel, extension.sideCaves, rect, extension.cavityPadding + 2);

        MapGenerator.SetVar("RectOfInterest", rect);
        MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects").Add(rect);
        BuriedSiteUtility.ReserveEntrance(map, mouth, extension.entranceClearRadius);
    }

    /// <summary>The structure's way in: of every door it placed, the one closest to the rect edge.</summary>
    private static IntVec3 FindOuterDoor(List<Thing> spawned, CellRect rect, IntVec3 fallback)
    {
        var best = fallback;
        var bestDistance = int.MaxValue;

        foreach (var thing in spawned)
        {
            if (thing is not Building_Door)
            {
                continue;
            }

            var cell = thing.Position;
            var distance = Mathf.Min(cell.x - rect.minX, rect.maxX - cell.x, cell.z - rect.minZ, rect.maxZ - cell.z);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = cell;
            }
        }

        return best;
    }

    private static IntVec3 FindSpawnedPedestal(List<Thing> spawned)
    {
        foreach (var thing in spawned)
        {
            if (!thing.Destroyed && thing.TryGetComp<CompPedestal>() != null)
            {
                return thing.Position;
            }
        }

        return IntVec3.Invalid;
    }

    /// <summary>
    /// Cells inside the structure's walls, in breadth-first order from the seed. The structure does
    /// not fill its bounding box, so anything that places things has to work from this rather than
    /// from the rect - otherwise it puts them in the rock hollow outside.
    /// </summary>
    private static List<IntVec3> CollectInterior(Map map, CellRect rect, IntVec3 seed)
    {
        var result = new List<IntVec3>();

        if (!seed.IsValid || !rect.Contains(seed))
        {
            return result;
        }

        var seen = new HashSet<IntVec3> { seed };
        var queue = new Queue<IntVec3>();
        queue.Enqueue(seed);

        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            result.Add(cell);

            for (var i = 0; i < GenAdj.CardinalDirections.Length; i++)
            {
                var adjacent = cell + GenAdj.CardinalDirections[i];

                if (rect.Contains(adjacent) && adjacent.InBounds(map) && !seen.Contains(adjacent) && InteriorPassable(map, adjacent))
                {
                    seen.Add(adjacent);
                    queue.Enqueue(adjacent);
                }
            }
        }

        return result;
    }

    private static bool InteriorPassable(Map map, IntVec3 cell)
    {
        var edifice = cell.GetEdifice(map);

        return edifice == null || edifice is Building_Door || edifice.def.passability != Traversability.Impassable;
    }

    private static IntVec3 FurthestFrom(Map map, CellRect rect, IntVec3 from)
    {
        var cells = CollectInterior(map, rect, from);

        return cells.Count > 0 ? cells[cells.Count - 1] : IntVec3.Invalid;
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

    private static IntVec3 BuildRoom(Map map, CellRect rect, DefModExtension_TempormortisTomb extension)
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
            var thingDef = cell == doorCell ? ThingDefOf.Door : ThingDefOf.Wall;
            GenSpawn.Spawn(ThingMaker.MakeThing(thingDef, wallStuff), cell, map);
        }

        return doorCell;
    }

    private static void SpawnReliquary(Map map, IntVec3 cell, DefModExtension_TempormortisTomb extension)
    {
        if (extension.reliquaryDef != null && cell.IsValid && cell.GetFirstThing(map, extension.reliquaryDef) == null)
        {
            GenSpawn.Spawn(ThingMaker.MakeThing(extension.reliquaryDef), cell, map);
        }
    }

    private static void SpawnRelic(Map map, IntVec3 cell, SitePartDef sitePartDef, DefModExtension_TempormortisTomb extension)
    {
        if (extension.dormantRelicDef == null || !cell.IsValid)
        {
            return;
        }

        if (map.listerThings.ThingsOfDef(extension.dormantRelicDef).Count > 0)
        {
            return;
        }

        var relic = ThingMaker.MakeThing(extension.dormantRelicDef);
        var pedestal = cell.GetFirstThingWithComp<CompPedestal>(map)?.GetComp<CompPedestal>();

        if (pedestal != null)
        {
            pedestal.action = new PedestalAction_TempormortisTomb(sitePartDef);

            if (pedestal.TryPlace(relic))
            {
                return;
            }
        }

        GenSpawn.Spawn(relic, cell, map);
    }

    /// <summary>The Judiciar who turned the sands is still in here, at the foot of his own plinth.</summary>
    private static void SpawnCasket(Map map, CellRect rect, DefModExtension_TempormortisTomb extension)
    {
        if (extension.corpseCasketDef == null)
        {
            return;
        }

        var stuff = extension.corpseCasketDef.MadeFromStuff ? extension.wallStuff ?? ThingDefOf.BlocksGranite : null;
        var interior = rect.ContractedBy(1);
        var cell = rect.CenterCell + new IntVec3(0, 0, 3);
        var occupied = GenAdj.OccupiedRect(cell, Rot4.North, extension.corpseCasketDef.size);

        if (!interior.Contains(occupied.Min) || !interior.Contains(occupied.Max))
        {
            return;
        }

        GenSpawn.Spawn(ThingMaker.MakeThing(extension.corpseCasketDef, stuff), cell, map, Rot4.North);
    }

    private static void SpawnFilth(Map map, List<IntVec3> interior, DefModExtension_TempormortisTomb extension)
    {
        if (extension.filthDef == null || extension.filthCount <= 0 || interior.Count == 0)
        {
            return;
        }

        for (var i = 0; i < extension.filthCount; i++)
        {
            var cell = interior.RandomElement();

            if (cell.Standable(map))
            {
                FilthMaker.TryMakeFilth(cell, map, extension.filthDef);
            }
        }
    }

    /// <summary>Stocks whatever shelving the structure placed, so the tomb is worth searching.</summary>
    private static void SpawnLoot(Map map, List<Thing> spawned, List<IntVec3> interior, DefModExtension_TempormortisTomb extension)
    {
        if (extension.lootThingSetMaker?.root == null)
        {
            return;
        }

        var storage = spawned?
            .Where(t => t is Building_Storage && !t.Destroyed && t.def != extension.reliquaryDef)
            .ToList() ?? new List<Thing>();

        if (storage.Count == 0 && interior.Count == 0)
        {
            return;
        }

        var parms = default(ThingSetMakerParams);
        parms.totalMarketValueRange = extension.lootMarketValueRange;

        var things = extension.lootThingSetMaker.root.Generate(parms);

        for (var i = 0; i < things.Count; i++)
        {
            var thing = things[i];
            var target = storage.Count > 0
                ? storage[i % storage.Count].Position
                : interior.RandomElement();

            if (!GenPlace.TryPlaceThing(thing, target, map, ThingPlaceMode.Near))
            {
                thing.Destroy();
            }
        }
    }

    /// <summary>
    /// Picks the most advanced enemy available - spacer over industrial over tribal, mechanoids
    /// included - without ever referencing another mod. Preferred faction defNames are resolved by
    /// name and silently skipped when absent.
    /// </summary>
    private static Faction ResolveGuardianFaction(DefModExtension_TempormortisTomb extension)
    {
        foreach (var defName in extension.preferredFactionDefNames)
        {
            var factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(defName);

            if (factionDef == null)
            {
                continue;
            }

            var preferred = Find.FactionManager.FirstFactionOfDef(factionDef);

            if (preferred != null && !preferred.defeated && preferred.HostileTo(Faction.OfPlayer))
            {
                return preferred;
            }
        }

        var candidates = Find.FactionManager.AllFactions.Where(IsEligibleGuardianFaction).ToList();

        if (candidates.Count == 0)
        {
            return Faction.OfAncientsHostile;
        }

        var best = candidates.Max(TechRank);

        return candidates.Where(f => TechRank(f) == best).RandomElement();
    }

    private static bool IsEligibleGuardianFaction(Faction faction)
    {
        if (faction == null || faction.IsPlayer || faction.defeated || !faction.HostileTo(Faction.OfPlayer))
        {
            return false;
        }

        if (!HasCombatGroup(faction))
        {
            return false;
        }

        // Mechanoids are flagged Ultra but are wanted; every other Ultra-and-above faction is not.
        if (faction.def == FactionDefOf.Mechanoid)
        {
            return true;
        }

        return faction.def.humanlikeFaction && faction.def.techLevel <= TechLevel.Spacer;
    }

    private static int TechRank(Faction faction)
    {
        return faction.def == FactionDefOf.Mechanoid ? (int)TechLevel.Spacer : (int)faction.def.techLevel;
    }

    private static void SpawnGuardians(Map map, List<IntVec3> interior, IntVec3 plinthCell, DefModExtension_TempormortisTomb extension, float threatPoints)
    {
        var faction = ResolveGuardianFaction(extension);

        if (faction == null || interior.Count == 0)
        {
            return;
        }

        var pawns = GenerateGuardians(map, faction, extension, threatPoints);

        if (pawns.Count == 0)
        {
            return;
        }

        var open = interior
            .Where(c => c != plinthCell && !c.AdjacentToCardinal(plinthCell) && c.Standable(map))
            .ToList();

        var doorways = CollectDoorways(map, interior);
        var candidates = open.Where(c => !doorways.Contains(c)).InRandomOrder().ToList();

        if (candidates.Count == 0)
        {
            candidates = open.InRandomOrder().ToList();
        }

        var spawned = new List<Pawn>();
        var next = 0;

        foreach (var pawn in pawns)
        {
            if (next >= candidates.Count)
            {
                Find.WorldPawns.PassToWorld(pawn);
                continue;
            }

            GenSpawn.Spawn(pawn, candidates[next++], map, Rot4.Random);
            spawned.Add(pawn);
        }

        if (spawned.Count == 0)
        {
            return;
        }

        LordMaker.MakeNewLord(faction, new LordJob_RelicGuardians(faction, GuardianWakeMessageKey, extension.frozenDuty), map, spawned);
    }

    /// <summary>
    /// Every door in the structure and the ring of cells touching it. A frozen guardian cannot be
    /// pushed aside or killed, so one standing in a chokepoint would seal the tomb for good.
    /// </summary>
    private static HashSet<IntVec3> CollectDoorways(Map map, List<IntVec3> interior)
    {
        var result = new HashSet<IntVec3>();

        foreach (var cell in interior)
        {
            if (cell.GetEdifice(map) is not Building_Door)
            {
                continue;
            }

            result.Add(cell);

            for (var i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                result.Add(cell + GenAdj.AdjacentCells[i]);
            }
        }

        return result;
    }

    private static List<Pawn> GenerateGuardians(Map map, Faction faction, DefModExtension_TempormortisTomb extension, float threatPoints)
    {
        var pawns = new List<Pawn>();

        if (extension.guardianKind == null && HasCombatGroup(faction))
        {
            var parms = new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                tile = map.Tile,
                faction = faction,
                points = Mathf.Max(Mathf.Max(threatPoints, extension.guardianMinPoints), faction.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat))
            };

            pawns.AddRange(PawnGroupMakerUtility.GeneratePawns(parms));

            if (pawns.Count > 0)
            {
                return pawns;
            }
        }

        var kind = extension.guardianKind ?? faction.def.basicMemberKind;

        if (kind == null)
        {
            return pawns;
        }

        for (var i = 0; i < Mathf.Max(extension.guardianCount, 1); i++)
        {
            var request = new PawnGenerationRequest(
                kind,
                faction,
                PawnGenerationContext.NonPlayer,
                map.Tile,
                forceGenerateNewPawn: true,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true);

            pawns.Add(PawnGenerator.GeneratePawn(request));
        }

        return pawns;
    }

    private static bool HasCombatGroup(Faction faction)
    {
        var makers = faction.def.pawnGroupMakers;

        if (makers.NullOrEmpty())
        {
            return false;
        }

        for (var i = 0; i < makers.Count; i++)
        {
            if (makers[i].kindDef == PawnGroupKindDefOf.Combat)
            {
                return true;
            }
        }

        return false;
    }
}
