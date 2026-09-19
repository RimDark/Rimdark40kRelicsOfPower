using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// Reads a StructureLayoutDef onto the map: clears the footprint its terrain grid names, lays that
/// terrain, spawns things in dependency order and reports back what the site needs to finish up.
/// </summary>
public static class StructureLayoutUtility
{
    private const string EmptyToken = ".";

    /// <summary>What a layout left behind, for the gen step to build the rest of the site on.</summary>
    public class Spawned
    {
        public List<Thing> things = new List<Thing>();

        // Walls, doors and enclosed floor. The space around the structure is not the layout's to own.
        public List<IntVec3> structureCells = new List<IntVec3>();

        public IntVec3 entranceCell = IntVec3.Invalid;
    }

    /// <summary>Grid size as authored, in columns by rows.</summary>
    public static IntVec2 SizeOf(StructureLayoutDef def)
    {
        var rows = def.terrainGrid.Count;
        var cols = 0;

        foreach (var row in def.terrainGrid)
        {
            cols = Mathf.Max(cols, CellCount(row));
        }

        foreach (var layer in def.layouts)
        {
            rows = Mathf.Max(rows, layer.Count);

            foreach (var row in layer)
            {
                cols = Mathf.Max(cols, CellCount(row));
            }
        }

        return new IntVec2(cols, rows);
    }

    /// <summary>Footprint the layout needs on the map once placed at this rotation.</summary>
    public static IntVec2 RotatedSize(StructureLayoutDef def, Rot4 rotation)
    {
        var size = SizeOf(def);

        return rotation.IsHorizontal ? new IntVec2(size.z, size.x) : size;
    }

    public static Spawned Spawn(Map map, StructureLayoutDef def, CellRect rect, Rot4 rotation,
        ThingDef wallStuff, RoofDef roof)
    {
        var result = new Spawned();
        var size = SizeOf(def);

        if (size.x <= 0 || size.z <= 0)
        {
            return result;
        }

        var terrain = Split(def.terrainGrid, size);
        var layers = new List<string[][]>();

        foreach (var layer in def.layouts)
        {
            layers.Add(Split(layer, size));
        }

        var occupied = FindOccupiedCells(layers, size);
        var outside = FindOutsideCells(occupied, size);

        for (var row = 0; row < size.z; row++)
        {
            for (var col = 0; col < size.x; col++)
            {
                if (outside.Contains(Index(size, col, row)))
                {
                    continue;
                }

                if (terrain[row][col] == EmptyToken && !occupied.Contains(Index(size, col, row)))
                {
                    continue;
                }

                var cell = GridToMap(rect, rotation, size, col, row);

                if (cell.InBounds(map))
                {
                    result.structureCells.Add(cell);
                }
            }
        }

        ClearCells(map, result.structureCells);

        for (var row = 0; row < size.z; row++)
        {
            for (var col = 0; col < size.x; col++)
            {
                var token = terrain[row][col];

                if (token == EmptyToken || outside.Contains(Index(size, col, row)))
                {
                    continue;
                }

                var terrainDef = DefDatabase<TerrainDef>.GetNamedSilentFail(token);
                var cell = GridToMap(rect, rotation, size, col, row);

                if (terrainDef != null && cell.InBounds(map))
                {
                    map.terrainGrid.SetTerrain(cell, terrainDef);
                }
            }
        }

        SpawnThings(map, def, layers, size, rect, rotation, wallStuff, true, result);
        SpawnThings(map, def, layers, size, rect, rotation, wallStuff, false, result);

        if (def.forceGenerateRoof)
        {
            foreach (var cell in result.structureCells)
            {
                map.roofGrid.SetRoof(cell, roof);
            }
        }

        result.entranceCell = FindEntrance(map, layers, size, rect, rotation, outside);

        return result;
    }

    private static void SpawnThings(Map map, StructureLayoutDef def, List<string[][]> layers, IntVec2 size,
        CellRect rect, Rot4 rotation, ThingDef wallStuff, bool edifices, Spawned result)
    {
        foreach (var layer in layers)
        {
            for (var row = 0; row < size.z; row++)
            {
                for (var col = 0; col < size.x; col++)
                {
                    if (!TryParseToken(layer[row][col], out var thingDef, out var stuff, out var thingRotation))
                    {
                        continue;
                    }

                    if (thingDef.IsEdifice() != edifices)
                    {
                        continue;
                    }

                    var cell = GridToMap(rect, rotation, size, col, row);

                    if (!cell.InBounds(map))
                    {
                        continue;
                    }

                    if (def.randomizeWallStuffAtGen && wallStuff != null
                        && (thingDef.IsDoor || thingDef == ThingDefOf.Wall))
                    {
                        stuff = wallStuff;
                    }

                    if (thingDef.MadeFromStuff)
                    {
                        stuff ??= GenStuff.DefaultStuffFor(thingDef);
                    }
                    else
                    {
                        stuff = null;
                    }

                    var placed = Rotate(thingRotation, rotation);
                    var thing = ThingMaker.MakeThing(thingDef, stuff);

                    GenSpawn.Spawn(thing, cell, map, thingDef.rotatable ? placed : Rot4.North);
                    result.things.Add(thing);
                }
            }
        }
    }

    /// <summary>The door that opens onto outside air, so the entrance passage has somewhere to go.</summary>
    private static IntVec3 FindEntrance(Map map, List<string[][]> layers, IntVec2 size, CellRect rect,
        Rot4 rotation, HashSet<int> outside)
    {
        var candidates = new List<IntVec3>();

        for (var row = 0; row < size.z; row++)
        {
            for (var col = 0; col < size.x; col++)
            {
                if (!IsDoorCell(layers, col, row))
                {
                    continue;
                }

                if (!TouchesOutside(size, outside, col, row))
                {
                    continue;
                }

                var cell = GridToMap(rect, rotation, size, col, row);

                if (cell.InBounds(map))
                {
                    candidates.Add(cell);
                }
            }
        }

        return candidates.Count > 0 ? candidates.RandomElement() : IntVec3.Invalid;
    }

    private static bool IsDoorCell(List<string[][]> layers, int col, int row)
    {
        foreach (var layer in layers)
        {
            if (TryParseToken(layer[row][col], out var thingDef, out _, out _) && thingDef.IsDoor)
            {
                return true;
            }
        }

        return false;
    }

    private static bool TouchesOutside(IntVec2 size, HashSet<int> outside, int col, int row)
    {
        for (var i = 0; i < 4; i++)
        {
            var c = col + (i == 2 ? -1 : i == 3 ? 1 : 0);
            var r = row + (i == 0 ? -1 : i == 1 ? 1 : 0);

            if (c < 0 || r < 0 || c >= size.x || r >= size.z || outside.Contains(Index(size, c, r)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Grid cells reachable from the grid border without crossing anything the layout places.</summary>
    /// <summary>
    /// Every grid cell a placed thing covers, footprints included, so a 3x2 door written as one
    /// token still seals the wall it sits in.
    /// </summary>
    private static HashSet<int> FindOccupiedCells(List<string[][]> layers, IntVec2 size)
    {
        var occupied = new HashSet<int>();

        foreach (var layer in layers)
        {
            for (var row = 0; row < size.z; row++)
            {
                for (var col = 0; col < size.x; col++)
                {
                    if (!TryParseToken(layer[row][col], out var thingDef, out _, out var thingRotation))
                    {
                        continue;
                    }

                    var footprintRotation = thingDef.rotatable ? thingRotation : Rot4.North;

                    foreach (var cell in GenAdj.OccupiedRect(new IntVec3(col, 0, row), footprintRotation, thingDef.size))
                    {
                        if (cell.x >= 0 && cell.z >= 0 && cell.x < size.x && cell.z < size.z)
                        {
                            occupied.Add(Index(size, cell.x, cell.z));
                        }
                    }
                }
            }
        }

        return occupied;
    }

    private static HashSet<int> FindOutsideCells(HashSet<int> occupied, IntVec2 size)
    {
        var outside = new HashSet<int>();
        var frontier = new List<int>();

        for (var row = 0; row < size.z; row++)
        {
            for (var col = 0; col < size.x; col++)
            {
                var onBorder = row == 0 || col == 0 || row == size.z - 1 || col == size.x - 1;

                if (onBorder && !occupied.Contains(Index(size, col, row)) && outside.Add(Index(size, col, row)))
                {
                    frontier.Add(Index(size, col, row));
                }
            }
        }

        for (var head = 0; head < frontier.Count; head++)
        {
            var col = frontier[head] % size.x;
            var row = frontier[head] / size.x;

            for (var i = 0; i < 4; i++)
            {
                var c = col + (i == 2 ? -1 : i == 3 ? 1 : 0);
                var r = row + (i == 0 ? -1 : i == 1 ? 1 : 0);

                if (c < 0 || r < 0 || c >= size.x || r >= size.z)
                {
                    continue;
                }

                if (!occupied.Contains(Index(size, c, r)) && outside.Add(Index(size, c, r)))
                {
                    frontier.Add(Index(size, c, r));
                }
            }
        }

        return outside;
    }

    private static void ClearCells(Map map, List<IntVec3> cells)
    {
        var toDestroy = new List<Thing>();

        foreach (var cell in cells)
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

    private static IntVec3 GridToMap(CellRect rect, Rot4 rotation, IntVec2 size, int col, int row)
    {
        int x;
        int z;

        switch (rotation.AsInt)
        {
            case 1:
                x = row;
                z = size.x - 1 - col;
                break;
            case 2:
                x = size.x - 1 - col;
                z = size.z - 1 - row;
                break;
            case 3:
                x = size.z - 1 - row;
                z = col;
                break;
            default:
                x = col;
                z = row;
                break;
        }

        return new IntVec3(rect.minX + x, 0, rect.minZ + z);
    }

    private static Rot4 Rotate(Rot4 thingRotation, Rot4 structureRotation)
    {
        return new Rot4((thingRotation.AsInt + structureRotation.AsInt) % 4);
    }

    // A trailing North/East/South/West is a rotation. The rest resolves as a def name first and only
    // splits stuff off the right if that fails, so a def whose own name has an underscore still works.
    private static bool TryParseToken(string token, out ThingDef thingDef, out ThingDef stuff, out Rot4 rotation)
    {
        thingDef = null;
        stuff = null;
        rotation = Rot4.North;

        if (token.NullOrEmpty() || token == EmptyToken)
        {
            return false;
        }

        var parts = new List<string>(token.Split('_'));

        if (parts.Count > 1 && TryParseRotation(parts[parts.Count - 1], out rotation))
        {
            parts.RemoveAt(parts.Count - 1);
        }

        for (var split = parts.Count; split >= 1; split--)
        {
            var name = string.Join("_", parts.GetRange(0, split).ToArray());
            var candidate = DefDatabase<ThingDef>.GetNamedSilentFail(name);

            if (candidate == null)
            {
                continue;
            }

            thingDef = candidate;

            if (split < parts.Count)
            {
                var stuffName = string.Join("_", parts.GetRange(split, parts.Count - split).ToArray());
                stuff = DefDatabase<ThingDef>.GetNamedSilentFail(stuffName);
            }

            return true;
        }

        return false;
    }

    private static bool TryParseRotation(string value, out Rot4 rotation)
    {
        switch (value)
        {
            case "North":
                rotation = Rot4.North;
                return true;
            case "East":
                rotation = Rot4.East;
                return true;
            case "South":
                rotation = Rot4.South;
                return true;
            case "West":
                rotation = Rot4.West;
                return true;
            default:
                rotation = Rot4.North;
                return false;
        }
    }

    private static string[][] Split(List<string> grid, IntVec2 size)
    {
        var rows = new string[size.z][];

        for (var row = 0; row < size.z; row++)
        {
            rows[row] = new string[size.x];

            var parts = row < grid.Count && !grid[row].NullOrEmpty() ? grid[row].Split(',') : null;

            for (var col = 0; col < size.x; col++)
            {
                rows[row][col] = parts != null && col < parts.Length ? parts[col].Trim() : EmptyToken;
            }
        }

        return rows;
    }

    private static int Index(IntVec2 size, int col, int row)
    {
        return (row * size.x) + col;
    }

    private static int CellCount(string row)
    {
        return row.NullOrEmpty() ? 0 : row.Split(',').Length;
    }
}
