using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace Relics40k;

/// <summary>
/// Shared map-gen helpers for sites that sit buried in rock: finding the deepest spot, making up
/// any shortfall in cover, hollowing a ragged cavity and cutting a passage back out to open air.
/// </summary>
public static class BuriedSiteUtility
{
    /// <summary>
    /// Distance from every cell to the nearest open cell that connects to the map edge. Open,
    /// edge-connected cells are 0 and the value climbs with depth into rock.
    /// </summary>
    public static int[] BuildDepthGrid(Map map)
    {
        var indices = map.cellIndices;
        var depth = new int[indices.NumGridCells];

        for (var i = 0; i < depth.Length; i++)
        {
            depth[i] = int.MaxValue;
        }

        var queue = new List<IntVec3>();

        foreach (var cell in FindOutsideCells(map))
        {
            depth[indices.CellToIndex(cell)] = 0;
            queue.Add(cell);
        }

        for (var head = 0; head < queue.Count; head++)
        {
            var cell = queue[head];
            var next = depth[indices.CellToIndex(cell)] + 1;

            for (var i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                var adjacent = cell + GenAdj.AdjacentCells[i];

                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                var index = indices.CellToIndex(adjacent);

                if (depth[index] > next)
                {
                    depth[index] = next;
                    queue.Add(adjacent);
                }
            }
        }

        return depth;
    }

    /// <summary>Open cells reachable on foot from the map edge.</summary>
    public static HashSet<IntVec3> FindOutsideCells(Map map)
    {
        var outside = new HashSet<IntVec3>();
        var frontier = new List<IntVec3>();

        foreach (var cell in CellRect.WholeMap(map).EdgeCells)
        {
            if (IsWalkableForEntry(map, cell) && outside.Add(cell))
            {
                frontier.Add(cell);
            }
        }

        for (var head = 0; head < frontier.Count; head++)
        {
            var cell = frontier[head];

            for (var i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                var adjacent = cell + GenAdj.AdjacentCells[i];

                if (adjacent.InBounds(map) && IsWalkableForEntry(map, adjacent) && outside.Add(adjacent))
                {
                    frontier.Add(adjacent);
                }
            }
        }

        return outside;
    }

    public static bool IsWalkableForEntry(Map map, IntVec3 cell)
    {
        var edifice = cell.GetEdifice(map);

        return edifice == null || edifice is Building_Door;
    }

    /// <summary>Removes natural rock only, so constructed walls and other site content survive.</summary>
    public static void CarveCell(Map map, IntVec3 cell, RoofDef roof)
    {
        if (!cell.InBounds(map))
        {
            return;
        }

        var edifice = cell.GetEdifice(map);

        if (edifice == null || edifice.def.building is not { isNaturalRock: true })
        {
            return;
        }

        edifice.Destroy();

        if (roof != null)
        {
            map.roofGrid.SetRoof(cell, roof);
        }
    }

    /// <summary>Picks the spot with the most rock over it that a rect of this size will fit.</summary>
    public static bool TryFindBuriedCenter(Map map, IntVec2 size, int cavityPadding, int minRockDepth, out IntVec3 center)
    {
        var depth = BuildDepthGrid(map);
        var indices = map.cellIndices;
        var usedRects = MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects");
        var margin = cavityPadding + Mathf.Max(minRockDepth, 0) + 2;

        center = IntVec3.Invalid;
        var bestScore = -1;

        for (var x = margin; x < map.Size.x - margin; x += 3)
        {
            for (var z = margin; z < map.Size.z - margin; z += 3)
            {
                var candidate = new IntVec3(x, 0, z);
                var rect = GenAdj.OccupiedRect(candidate, Rot4.North, size);

                if (!rect.ExpandedBy(margin).InBounds(map) || usedRects.Any(r => r.Overlaps(rect.ExpandedBy(2))))
                {
                    continue;
                }

                var score = int.MaxValue;

                foreach (var cell in rect)
                {
                    if (!cell.SupportsStructureType(map, TerrainAffordanceDefOf.Heavy))
                    {
                        score = -1;
                        break;
                    }

                    score = Mathf.Min(score, depth[indices.CellToIndex(cell)]);
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    center = candidate;
                }
            }
        }

        return center.IsValid;
    }

    /// <summary>
    /// Spawns rock to make up any shortfall between the hollow around the rect and open air, so
    /// the site is buried minRockDepth deep however the map happened to generate.
    /// </summary>
    public static void BuryRect(Map map, CellRect rect, int cavityPadding, int minRockDepth)
    {
        if (minRockDepth <= 0)
        {
            return;
        }

        var rockDef = Find.World.NaturalRockTypesIn(map.Tile).RandomElementWithFallback(ThingDefOf.Sandstone);

        if (rockDef == null)
        {
            return;
        }

        var hollow = rect.ExpandedBy(cavityPadding);
        var shell = rect.ExpandedBy(cavityPadding + minRockDepth).ClipInsideMap(map);
        var usedRects = MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects");

        foreach (var cell in shell)
        {
            if (hollow.Contains(cell) || cell.OnEdge(map) || cell.GetEdifice(map) != null)
            {
                continue;
            }

            if (usedRects.Any(r => r.Contains(cell)))
            {
                continue;
            }

            GenSpawn.Spawn(rockDef, cell, map);
            map.roofGrid.SetRoof(cell, RoofDefOf.RoofRockThick);
        }
    }

    /// <summary>Hollows out a ragged space around the rect so it does not read as a boxed room.</summary>
    public static void CarveCavity(Map map, CellRect rect, int cavityPadding)
    {
        if (cavityPadding <= 0)
        {
            return;
        }

        var hollow = rect.ExpandedBy(cavityPadding).ClipInsideMap(map);

        foreach (var cell in hollow)
        {
            if (rect.Contains(cell))
            {
                continue;
            }

            if (RingDistance(cell, rect) >= cavityPadding && Rand.Chance(0.45f))
            {
                continue;
            }

            CarveCell(map, cell, RoofDefOf.RoofRockThick);
        }
    }

    private static int RingDistance(IntVec3 cell, CellRect rect)
    {
        var x = Mathf.Max(rect.minX - cell.x, cell.x - rect.maxX, 0);
        var z = Mathf.Max(rect.minZ - cell.z, cell.z - rect.maxZ, 0);

        return Mathf.Max(x, z);
    }

    /// <summary>
    /// Hollows a cave-shaped space around a hand-made structure. Distance from the structure sets how
    /// far the rock is taken back and smooth noise on that boundary keeps it from reading as a box, so
    /// it bulges into alcoves in places and pinches back against the walls in others. Each cell's floor
    /// becomes the stone it was cut from. Returns the cells it opened.
    /// </summary>
    public static HashSet<IntVec3> CarveHollowAround(Map map, List<IntVec3> structureCells, CellRect limit,
        float radius, float amplitude, float frequency, RoofDef roof, TerrainDef floorOverride)
    {
        var carved = new HashSet<IntVec3>();

        if (structureCells.NullOrEmpty() || radius <= 0f)
        {
            return carved;
        }

        var indices = map.cellIndices;
        var distance = new int[indices.NumGridCells];

        for (var i = 0; i < distance.Length; i++)
        {
            distance[i] = int.MaxValue;
        }

        var queue = new List<IntVec3>();

        foreach (var cell in structureCells)
        {
            if (!cell.InBounds(map))
            {
                continue;
            }

            distance[indices.CellToIndex(cell)] = 0;
            queue.Add(cell);
        }

        var reach = Mathf.CeilToInt(radius + Mathf.Abs(amplitude)) + 1;

        for (var head = 0; head < queue.Count; head++)
        {
            var cell = queue[head];
            var next = distance[indices.CellToIndex(cell)] + 1;

            if (next > reach)
            {
                continue;
            }

            for (var i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                var adjacent = cell + GenAdj.AdjacentCells[i];

                if (!adjacent.InBounds(map) || !limit.Contains(adjacent))
                {
                    continue;
                }

                var index = indices.CellToIndex(adjacent);

                if (distance[index] > next)
                {
                    distance[index] = next;
                    queue.Add(adjacent);
                }
            }
        }

        var noise = new Perlin(frequency, 2.0, 0.5, 4, Rand.Range(0, int.MaxValue), QualityMode.Medium);

        foreach (var cell in limit)
        {
            var depth = distance[indices.CellToIndex(cell)];

            if (depth <= 0 || depth == int.MaxValue)
            {
                continue;
            }

            if (depth > radius + (amplitude * noise.GetValue(cell)))
            {
                continue;
            }

            var floor = floorOverride ?? StoneFloorAt(cell);

            CarveCell(map, cell, roof);

            if (floor != null)
            {
                map.terrainGrid.SetTerrain(cell, floor);
            }

            carved.Add(cell);
        }

        return carved;
    }

    /// <summary>Rough floor of whatever stone the map's own rock noise puts at this cell.</summary>
    public static TerrainDef StoneFloorAt(IntVec3 cell)
    {
        if (RockNoises.rockNoises.NullOrEmpty())
        {
            return null;
        }

        return GenStep_RocksFromGrid.RockDefAt(cell)?.building?.naturalTerrain;
    }

    /// <summary>
    /// Cuts a passage from the door out to open ground by walking downhill on the depth field, then
    /// verifies the door really is connected and forces a straight cut if it is not. Hangs dead-end
    /// side caves off the finished passage. Returns the cell the passage surfaces at.
    /// </summary>
    public static IntVec3 CarveEntrance(Map map, IntVec3 doorCell, IntRange tunnelWidthRange,
        float tunnelJitterChance, bool roofTunnel, SideCaveSettings sideCaves, CellRect avoid, int avoidPadding)
    {
        var depth = BuildDepthGrid(map);
        var roof = roofTunnel ? RoofDefOf.RoofRockThick : null;
        var path = new List<IntVec3>();
        var mouth = CarveDownhill(map, doorCell, tunnelWidthRange, tunnelJitterChance, roof, depth, path);

        if (!FindOutsideCells(map).Contains(doorCell))
        {
            var edge = IntVec3.Invalid;
            var bestDistance = float.MaxValue;

            foreach (var candidate in CellRect.WholeMap(map).EdgeCells)
            {
                var distance = candidate.DistanceToSquared(doorCell);

                if (distance < bestDistance && IsWalkableForEntry(map, candidate))
                {
                    bestDistance = distance;
                    edge = candidate;
                }
            }

            if (edge.IsValid)
            {
                foreach (var cell in GenSight.PointsOnLineOfSight(doorCell, edge))
                {
                    foreach (var wide in GenRadial.RadialCellsAround(cell, 1f, true))
                    {
                        CarveCell(map, wide, roof);
                    }

                    path.Add(cell);
                    mouth = cell;
                }
            }
        }

        CarveSideCaves(map, path, depth, roof, sideCaves, avoid, avoidPadding);

        return mouth;
    }

    /// <summary>
    /// Reserves the ground around the passage mouth. Later scatter gen steps - ruins at order 750,
    /// geysers at 950 - run long after this one and test UsedRects before placing, so without this
    /// the way in can end up buried under something.
    /// </summary>
    public static void ReserveEntrance(Map map, IntVec3 mouth, int radius)
    {
        if (!mouth.IsValid || radius <= 0)
        {
            return;
        }

        var span = (radius * 2) + 1;

        MapGenerator.GetOrGenerateVar<List<CellRect>>("UsedRects")
            .Add(CellRect.CenteredOn(mouth, span, span).ClipInsideMap(map));
    }

    private static IntVec3 CarveDownhill(Map map, IntVec3 doorCell, IntRange tunnelWidthRange,
        float tunnelJitterChance, RoofDef roof, int[] depth, List<IntVec3> path)
    {
        var indices = map.cellIndices;
        var cell = doorCell;
        var width = tunnelWidthRange.RandomInRange;
        var steps = 0;
        var limit = map.Size.x + map.Size.z;

        while (depth[indices.CellToIndex(cell)] > 0 && steps++ < limit)
        {
            if (Rand.Chance(tunnelJitterChance))
            {
                width = tunnelWidthRange.RandomInRange;
            }

            foreach (var wide in GenRadial.RadialCellsAround(cell, width / 2f, true))
            {
                CarveCell(map, wide, roof);
            }

            path.Add(cell);

            var best = cell;
            var bestDepth = depth[indices.CellToIndex(cell)];

            for (var i = 0; i < GenAdj.AdjacentCells.Length; i++)
            {
                var adjacent = cell + GenAdj.AdjacentCells[i];

                if (!adjacent.InBounds(map))
                {
                    continue;
                }

                var adjacentDepth = depth[indices.CellToIndex(adjacent)];

                if (adjacentDepth < bestDepth)
                {
                    bestDepth = adjacentDepth;
                    best = adjacent;
                }
            }

            if (best == cell)
            {
                break;
            }

            cell = best;
        }

        return cell;
    }

    /// <summary>
    /// Hangs a few dead-end caves off the entrance passage, spread along it and each ending in a
    /// small chamber. Cells are tested against the pre-carve depth field, so no cave can break the
    /// surface however far it wanders.
    /// </summary>
    private static void CarveSideCaves(Map map, List<IntVec3> path, int[] depth, RoofDef roof,
        SideCaveSettings settings, CellRect avoid, int avoidPadding)
    {
        if (settings == null || path.Count < 10)
        {
            return;
        }

        var count = settings.countRange.RandomInRange;

        if (count <= 0)
        {
            return;
        }

        var first = 3;
        var last = path.Count - 4;

        if (last <= first)
        {
            return;
        }

        var rockDef = settings.chunkCountRange.max > 0
            ? Find.World.NaturalRockTypesIn(map.Tile).RandomElementWithFallback(ThingDefOf.Sandstone)
            : null;

        var taken = new List<int>();

        for (var attempt = 0; attempt < count * 8 && taken.Count < count; attempt++)
        {
            var index = Rand.RangeInclusive(first, last);

            if (taken.Any(i => Mathf.Abs(i - index) < settings.minSpacing))
            {
                continue;
            }

            if (depth[map.cellIndices.CellToIndex(path[index])] < settings.minRockCover + 2)
            {
                continue;
            }

            taken.Add(index);
            CarveBranch(map, path, index, depth, roof, settings, avoid, avoidPadding, rockDef);
        }
    }

    private static void CarveBranch(Map map, List<IntVec3> path, int index, int[] depth, RoofDef roof,
        SideCaveSettings settings, CellRect avoid, int avoidPadding, ThingDef rockDef)
    {
        var before = path[Mathf.Max(index - 1, 0)];
        var after = path[Mathf.Min(index + 1, path.Count - 1)];
        var along = new Vector2(after.x - before.x, after.z - before.z);

        if (along.sqrMagnitude < 0.01f)
        {
            along = new Vector2(1f, 0f);
        }

        along.Normalize();

        var side = Rand.Bool ? 1f : -1f;
        var direction = new Vector2(-along.y * side, along.x * side);
        var cell = path[index];
        var position = new Vector2(cell.x + 0.5f, cell.z + 0.5f);
        var length = settings.lengthRange.RandomInRange;
        var width = settings.widthRange.RandomInRange;
        var carved = 0;

        for (var step = 0; step < length; step++)
        {
            if (Rand.Chance(settings.wanderChance))
            {
                direction = Rotate(direction, Rand.Range(-40f, 40f));
                width = settings.widthRange.RandomInRange;
            }

            position += direction;

            var next = new IntVec3(Mathf.RoundToInt(position.x), 0, Mathf.RoundToInt(position.y));

            if (next == cell)
            {
                continue;
            }

            if (!IsCaveCell(map, next, depth, settings, avoid, avoidPadding))
            {
                break;
            }

            foreach (var wide in GenRadial.RadialCellsAround(next, width / 2f, true))
            {
                if (IsCaveCell(map, wide, depth, settings, avoid, avoidPadding))
                {
                    CarveCell(map, wide, roof);
                }
            }

            cell = next;
            carved++;
        }

        if (carved < settings.minLengthForChamber)
        {
            return;
        }

        CarveChamber(map, cell, depth, roof, settings, avoid, avoidPadding, rockDef);
    }

    private static void CarveChamber(Map map, IntVec3 center, int[] depth, RoofDef roof,
        SideCaveSettings settings, CellRect avoid, int avoidPadding, ThingDef rockDef)
    {
        var radius = Mathf.Max(settings.chamberRadius, 1);
        var cells = new List<IntVec3>();

        foreach (var cell in GenRadial.RadialCellsAround(center, radius, true))
        {
            if (!IsCaveCell(map, cell, depth, settings, avoid, avoidPadding))
            {
                continue;
            }

            CarveCell(map, cell, roof);
            cells.Add(cell);
        }

        if (cells.Count == 0)
        {
            return;
        }

        if (roof != null && Rand.Chance(settings.openChamberChance))
        {
            foreach (var cell in GenRadial.RadialCellsAround(center, Mathf.Max(radius - 1, 1), true))
            {
                if (cells.Contains(cell))
                {
                    map.roofGrid.SetRoof(cell, null);
                }
            }
        }

        if (rockDef?.building?.mineableThing == null)
        {
            return;
        }

        var chunks = settings.chunkCountRange.RandomInRange;

        for (var i = 0; i < chunks; i++)
        {
            var cell = cells.RandomElement();

            if (cell.Standable(map) && cell.GetFirstItem(map) == null)
            {
                GenSpawn.Spawn(rockDef.building.mineableThing, cell, map);
            }
        }
    }

    private static bool IsCaveCell(Map map, IntVec3 cell, int[] depth, SideCaveSettings settings,
        CellRect avoid, int avoidPadding)
    {
        if (!cell.InBounds(map) || cell.CloseToEdge(map, settings.minDistanceFromEdge))
        {
            return false;
        }

        if (depth[map.cellIndices.CellToIndex(cell)] < settings.minRockCover)
        {
            return false;
        }

        return !avoid.ExpandedBy(avoidPadding).Contains(cell);
    }

    private static Vector2 Rotate(Vector2 vector, float degrees)
    {
        var radians = degrees * Mathf.Deg2Rad;
        var sin = Mathf.Sin(radians);
        var cos = Mathf.Cos(radians);

        return new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos).normalized;
    }
}
