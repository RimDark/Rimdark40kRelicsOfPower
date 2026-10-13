using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Relics40k;

public class DefModExtension_AscensionEngine : DefModExtension
{
    public List<GeneDef> requiredGenes = [];

    public List<GeneDef> genesToRemove = [];

    public List<GeneDef> genesToAdd = [];

    public XenotypeDef newXenotype = null;

    public string newXenotypeName = null;

    public XenotypeIconDef newXenotypeIcon = null;

    public HediffDef comaHediff = null;

    public int procedureTicks = 20000;

    public int firstWaveDelayTicks = 2500;

    public int waveIntervalTicks = 6000;

    public int maxWaves = 4;

    public float waveThreatPointsFactor = 0.5f;

    public float waveEscalation = 0.25f;

    public List<ThingDef> burnoutLeavings = [];
}
