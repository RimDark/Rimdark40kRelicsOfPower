using System.Collections.Generic;
using RimWorld;
using Verse;

namespace Relics40k;

/// <summary>
/// Everything the blood chalice does is tuned from here, so retuning it is an XML edit
/// rather than a rebuild.
/// </summary>
public class DefModExtension_BloodChalice : DefModExtension
{
    // 900000 ticks = 15 in-game days.
    public int cooldownTicks = 900000;

    // Removes every Hediff_Injury, permanent scars included.
    public bool healInjuries = true;

    // Removes any other hediff that is both isBad and everCurableByItem: diseases,
    // infections, chronic conditions, addictions, tolerances, toxic buildup, blood loss.
    public bool cureBadHediffs = true;

    // Lost limbs are never regrown. This only clears IsFresh on the stump so it stops
    // bleeding - otherwise a freshly-amputated drinker bleeds out with a healed body.
    public bool sealStumps = true;

    public bool fillHemogen = true;

    public bool fillDeathrestNeed = true;

    public ThoughtDef moodThought = null;

    // Never removed, whatever the flags above say.
    public List<HediffDef> excludedHediffs = [];
}
