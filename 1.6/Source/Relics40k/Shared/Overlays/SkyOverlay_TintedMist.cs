using RimWorld;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// A slow tinted haze drawn over the whole map. Uses a private copy of the core fog material so
/// recolouring it never touches ordinary fog weather. Draws above the lighting pass by default, so
/// the tint still reads inside an unlit roofed site instead of being multiplied away.
/// </summary>
[StaticConstructorOnStartup]
public class SkyOverlay_TintedMist : WeatherOverlayDualPanner
{
    private static readonly Material MistMat = new Material(MatLoader.LoadMat("Weather/FogOverlayWorld"));

    private readonly float altitude;

    public SkyOverlay_TintedMist(Color color, AltitudeLayer layer = AltitudeLayer.VisEffects)
    {
        worldOverlayMat = MistMat;
        worldOverlayPanSpeed1 = 0.0004f;
        worldOverlayPanSpeed2 = 0.00028f;
        worldPanDir1 = new Vector2(1f, 0.4f).normalized;
        worldPanDir2 = new Vector2(-0.6f, 1f).normalized;
        altitude = layer.AltitudeFor();
        ForcedOverlayColor = color;
    }

    public override void DrawOverlay(Map map)
    {
        if (worldOverlayMat != null)
        {
            DrawWorldOverlay(map, worldOverlayMat, altitude, GetRenderLayer());
        }
    }
}
