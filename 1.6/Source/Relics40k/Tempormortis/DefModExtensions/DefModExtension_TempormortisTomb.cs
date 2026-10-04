using System.Collections.Generic;
using Core40k;
using RimWorld;
using Verse;
using Verse.AI;

namespace Relics40k;

/// <summary>
/// Everything the tempormortis tomb places and how it behaves, read by the site part worker, the
/// gen step and the plinth's action, so the whole site is tunable from the SitePartDef.
/// </summary>
public class DefModExtension_TempormortisTomb : DefModExtension
{
    public GameConditionDef dilationCondition = null;

    public ThingDef reliquaryDef = null;

    public ThingDef dormantRelicDef = null;

    public ThingDef wallStuff = null;

    public TerrainDef floorTerrain = null;

    public ThingDef filthDef = null;

    public int filthCount = 10;

    public ThingDef corpseCasketDef = null;

    // Guardians are drawn from a hostile faction's own combat groups so loaded 40k faction mods
    // supply them without ever being referenced. Names are resolved at runtime and may be absent.
    public List<string> preferredFactionDefNames = [];

    public PawnKindDef guardianKind = null;

    // Once-per-game quest tag consumed when the relic is lifted from the plinth.
    public string oncePerGameTag = null;

    public int guardianCount = 4;

    public float guardianMinPoints = 800f;

    // When set, this KCSG structure replaces the generated room; roomSize, wallStuff and
    // floorTerrain are then unused.
    public KCSG.StructureLayoutDef roomLayout = null;

    public ThingSetMakerDef lootThingSetMaker = null;

    public FloatRange lootMarketValueRange = new FloatRange(200f, 450f);

    public int roomSize = 15;

    public bool roofRoom = true;

    // Duty the guardians hold while the field has them. Null falls back to lying asleep.
    public DutyDef frozenDuty = null;

    public int minRockDepth = 20;

    public int cavityPadding = 3;

    public IntRange tunnelWidthRange = new IntRange(2, 3);

    public float tunnelJitterChance = 0.3f;

    public bool roofTunnel = true;

    // Cells around the tunnel mouth reserved from later scatter gen steps, so nothing is
    // dropped over the way in.
    public int entranceClearRadius = 6;

    // Dead-end caves forking off the entrance passage.
    public SideCaveSettings sideCaves = new SideCaveSettings();

    public List<RankDef> requiredRanksOneAmong = [];
}
