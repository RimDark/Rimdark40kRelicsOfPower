using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Relics40k;

public class DefModExtension_GeneCryptSeal : DefModExtension
{
    public List<GeneDef> requiredGenes = [];

    public ThingDef engineDef = null;

    public JobDef breakSealJob = null;

    public int breakSealTicks = 5000;

    public ThingSetMakerDef treasureSetMaker = null;

    public int treasureRadius = 7;
}
