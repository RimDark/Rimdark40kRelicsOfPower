using RimWorld;
using UnityEngine;

namespace Relics40k;

public class CompProperties_Pedestal : CompProperties_ThingContainer
{
    // Where the held item sits relative to the pedestal's centre, in cells. x is right, z is up; y is ignored.
    public Vector3 heldItemOffset = new Vector3(0f, 0f, 0.1f);

    // Multiplier on the held item's own drawSize.
    public float heldItemScale = 1f;

    public int takeDurationTicks = 240;

    public CompProperties_Pedestal()
    {
        compClass = typeof(CompPedestal);
        stackLimit = 1;
    }
}
