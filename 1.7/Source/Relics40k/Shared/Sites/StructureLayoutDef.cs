using System.Collections.Generic;
using Verse;

namespace Relics40k;

/// <summary>
/// A hand-made building described as a grid of comma-separated tokens, one row per line. Row 0 is
/// the southern edge and rows run north; "." is an empty cell. A token is Def, Def_Stuff,
/// Def_Rotation or Def_Stuff_Rotation. Field names match KCSG's StructureLayoutDef so a structure
/// exported by that framework pastes in with only the outer tag changed.
/// </summary>
public class StructureLayoutDef : Def
{
    public List<List<string>> layouts = [];

    public List<string> terrainGrid = [];

    public bool allowRotation = true;

    public bool forceGenerateRoof = true;

    // True puts walls and doors in the site's own wall stuff instead of the stuff named in the grid.
    public bool randomizeWallStuffAtGen = false;

    // Accepted so a KCSG export needs no editing. Not used.
    public bool isStorage = false;

    public bool spawnConduits = true;
}
