using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace Relics40k;

public class DefModExtension_ExsanguinatingAir : DefModExtension
{
    public float bloodLossPerHour = 0.06f;

    public float hemogenPerHour = 0.02f;

    public int intervalTicks = 250;

    // Pawns carrying any of these are never bled by the air.
    public List<HediffDef> immuneHediffs = [];

    public Color mistColor = new Color(0.45f, 0.02f, 0.03f);

    // Peak alpha of the mist, and the strength of the sky tint. The mist draws above the lighting
    // pass, so this is not darkened by an unlit roof and wants to stay low.
    public float mistIntensity = 0.4f;

    public bool drawMist = true;
}
