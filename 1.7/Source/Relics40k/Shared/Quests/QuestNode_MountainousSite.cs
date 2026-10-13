using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace Relics40k;

/// <summary>
/// Places the site on a mountainous tile so there is a mountain to bury it in.
/// QuestNode_Root_Site only exposes maxHilliness, so the tile search is done here.
/// </summary>
public class QuestNode_MountainousSite : QuestNode
{
    public SlateRef<SitePartDef> sitePartDef;

    public SlateRef<FactionDef> factionDef;

    public SlateRef<IntRange> distanceFromColonyRange;

    [NoTranslate]
    public SlateRef<string> storeAs = "site";

    protected override bool TestRunInt(Slate slate)
    {
        if (sitePartDef.GetValue(slate) == null)
        {
            return false;
        }

        return TryFindTile(slate, out _);
    }

    protected override void RunInt()
    {
        var slate = QuestGen.slate;
        var quest = QuestGen.quest;
        var partDef = sitePartDef.GetValue(slate);

        if (partDef == null || !TryFindTile(slate, out var tile))
        {
            Log.Error("[Relics of Power] Could not find a mountainous tile for the site.");
            return;
        }

        var factionDefValue = factionDef.GetValue(slate);
        var faction = factionDefValue != null
            ? Find.FactionManager.FirstFactionOfDef(factionDefValue)
            : Faction.OfAncientsHostile;

        var parms = new SitePartParams
        {
            points = slate.Get("points", 0f),
            threatPoints = slate.Get("points", 0f)
        };

        var site = QuestGen_Sites.GenerateSite(
            Gen.YieldSingle(new SitePartDefWithParams(partDef, parms)),
            tile,
            faction);

        slate.Set(storeAs.GetValue(slate) ?? "site", site);
        quest.SpawnWorldObject(site);
    }

    private bool TryFindTile(Slate slate, out PlanetTile tile)
    {
        var range = distanceFromColonyRange.GetValue(slate);
        var min = range.TrueMin > 0 ? range.TrueMin : 4;
        var max = range.TrueMax > min ? range.TrueMax : min + 8;

        if (TryFindTileWith(min, max, Hilliness.Mountainous, out tile))
        {
            return true;
        }

        if (TryFindTileWith(min, max * 3, Hilliness.Mountainous, out tile))
        {
            return true;
        }

        return TryFindTileWith(min, max * 3, Hilliness.LargeHills, out tile);
    }

    private static bool TryFindTileWith(int min, int max, Hilliness minHilliness, out PlanetTile tile)
    {
        return TryFindTileWith(min, max, minHilliness, true, out tile)
            || TryFindTileWith(min, max, minHilliness, false, out tile);
    }

    private static bool TryFindTileWith(int min, int max, Hilliness minHilliness, bool avoidRivers, out PlanetTile tile)
    {
        return TileFinder.TryFindNewSiteTile(
            out tile,
            min,
            max,
            allowCaravans: false,
            allowedLandmarks: null,
            selectLandmarkChance: 0f,
            canSelectComboLandmarks: true,
            TileFinderMode.Near,
            exitOnFirstTileFound: false,
            canBeSpace: false,
            layer: null,
            validator: IsHillyEnough);

        bool IsHillyEnough(PlanetTile candidate)
        {
            var worldTile = Find.WorldGrid[candidate];
            var hilliness = worldTile.hilliness;

            if (hilliness < minHilliness || hilliness == Hilliness.Impassable)
            {
                return false;
            }

            return !avoidRivers || worldTile is not SurfaceTile surface || surface.Rivers.NullOrEmpty();
        }
    }
}
