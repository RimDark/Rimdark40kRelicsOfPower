using Verse;

namespace Relics40k;

/// <summary>
/// Shape of the dead-end natural caves that fork off a buried site's entrance passage. Set
/// countRange to 0~0 to turn them off for a site.
/// </summary>
public class SideCaveSettings
{
    public IntRange countRange = new IntRange(2, 4);

    public IntRange lengthRange = new IntRange(6, 16);

    public IntRange widthRange = new IntRange(1, 3);

    public float wanderChance = 0.35f;

    public int chamberRadius = 3;

    // Chance an end chamber loses its roof, letting daylight down into the passage.
    public float openChamberChance = 0.25f;

    public IntRange chunkCountRange = new IntRange(1, 3);

    // Rock that stood over a cell before anything was carved. Keeps a cave from breaking the surface.
    public int minRockCover = 5;

    public int minDistanceFromEdge = 8;

    public int minSpacing = 4;

    public int minLengthForChamber = 3;
}
