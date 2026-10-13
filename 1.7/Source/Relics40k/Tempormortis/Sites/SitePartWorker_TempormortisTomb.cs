using RimWorld;
using Verse;

namespace Relics40k;

public class SitePartWorker_TempormortisTomb : SitePartWorker
{
    public override void PostMapGenerate(Map map)
    {
        base.PostMapGenerate(map);

        var extension = def.GetModExtension<DefModExtension_TempormortisTomb>();

        if (extension?.dilationCondition == null || map.gameConditionManager.ConditionIsActive(extension.dilationCondition))
        {
            return;
        }

        map.gameConditionManager.RegisterCondition(GameConditionMaker.MakeConditionPermanent(extension.dilationCondition));
    }
}
