using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Relics40k;

/// <summary>
/// Everything the Red Cellar site places and how it behaves, read by both the site part worker
/// and the gen step so the whole site is tunable from the SitePartDef.
/// </summary>
public class DefModExtension_RedCellar : DefModExtension
{
    public GameConditionDef airCondition = null;

    public ThingDef pedestalDef = null;

    public ThingDef chaliceDef = null;

    public ThingDef casketDef = null;

    public ThingDef wallStuff = null;

    public TerrainDef floorTerrain = null;

    public ThingDef filthDef = null;

    public int filthCount = 12;

    public PawnKindDef guardianKind = null;

    public int guardianCount = 4;

    // Extra sarcophagi placed already open and empty, for flavour.
    public int emptyCasketCount = 1;

    public HediffDef guardianHediff = null;

    public List<GeneDef> guardianXenogenes = [];

    public int roomSize = 13;

    // Optional hand-made crypt. When set it replaces the generated room; the pedestal inside it
    // (or the rect centre if it has none) is where the chalice goes.
    public PrefabDef roomPrefab = null;

    // Hand-made crypt as a token grid. Takes precedence over roomPrefab and the generated room.
    public StructureLayoutDef roomLayout = null;

    // Share of its tank every lamp and torch in the layout starts with. 1 is lit on arrival and
    // burning down; -1 leaves whatever the def's own initialFuelPercent gave it.
    public float layoutLampFuelFraction = 1f;

    // Spread across the layout's storage, at most one entry per shelf.
    public List<LayoutLootEntry> layoutLoot = [];

    // Cave hollowed around a hand-made layout. The boundary is the radius plus smooth noise, so it
    // bulges and pinches instead of following the layout's bounding box.
    public float hollowRadius = 3f;

    public float hollowNoiseAmplitude = 2.5f;

    public float hollowNoiseFrequency = 0.06f;

    // Unset uses the stone each cell was cut from.
    public TerrainDef hollowTerrain = null;

    public bool roofRoom = true;

    public int minRockDepth = 20;

    public int cavityPadding = 3;

    public IntRange tunnelWidthRange = new IntRange(2, 3);

    public float tunnelJitterChance = 0.3f;

    public bool roofTunnel = true;

    // Cells around the tunnel mouth reserved from later scatter gen steps, so nothing is dropped
    // over the way in.
    public int entranceClearRadius = 6;

    // Dead-end caves forking off the entrance passage.
    public SideCaveSettings sideCaves = new SideCaveSettings();

    public int landingPadSize = 9;

    public int landingPadMinDistanceFromCrypt = 35;

    public int landingPadMinDistanceFromEdge = 25;
}
