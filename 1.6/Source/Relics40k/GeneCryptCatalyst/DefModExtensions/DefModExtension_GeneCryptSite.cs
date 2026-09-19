using System.Collections.Generic;
using Verse;

namespace Relics40k;

public class DefModExtension_GeneCryptSite : DefModExtension
{
    public ThingDef sealDef = null;

    public StructureLayoutDef layoutDef = null;

    public ThingDef wallStuff = null;

    public List<LayoutLootEntry> layoutLoot = [];

    public float garrisonPointsFactor = 1f;

    public int garrisonRadius = 8;

    public List<ThingDef> decorationDefs = [];

    public IntRange decorationCountRange = new IntRange(12, 20);

    public int decorationRadius = 14;
}
