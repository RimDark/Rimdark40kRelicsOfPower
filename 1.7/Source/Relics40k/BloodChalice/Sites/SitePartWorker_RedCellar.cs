using RimWorld;
using Verse;

namespace Relics40k;

public class SitePartWorker_RedCellar : SitePartWorker
{
    public override void PostMapGenerate(Map map)
    {
        base.PostMapGenerate(map);

        var extension = def.GetModExtension<DefModExtension_RedCellar>();

        if (extension?.airCondition == null || map.gameConditionManager.ConditionIsActive(extension.airCondition))
        {
            return;
        }

        map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeConditionPermanent(extension.airCondition));
    }
}
