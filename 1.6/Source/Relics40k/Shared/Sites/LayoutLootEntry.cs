using RimWorld;
using Verse;

namespace Relics40k;

/// <summary>One possible item left in a hand-made layout's storage.</summary>
public class LayoutLootEntry
{
    public ThingDef thingDef = null;

    public ThingDef stuff = null;

    public IntRange countRange = IntRange.One;

    public float chance = 1f;

    public QualityCategory? quality = null;
}
