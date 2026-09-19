using System.Collections.Generic;
using Core40k;
using RimWorld.QuestGen;
using Verse;

namespace Relics40k;

/// <summary>
/// Fails the quest's test run unless the colony has a free colonist holding one of the given ranks.
/// Optionally stores that colonist on the slate for quest text.
/// </summary>
public class QuestNode_RequireRankedColonist : QuestNode
{
    public List<RankDef> requiredRanksOneAmong = [];

    [NoTranslate]
    public SlateRef<string> storeAs;

    protected override bool TestRunInt(Slate slate)
    {
        var pawn = TempormortisUtility.FindRankedColonist(requiredRanksOneAmong);

        if (pawn == null)
        {
            return false;
        }

        Store(slate, pawn);
        return true;
    }

    protected override void RunInt()
    {
        var pawn = TempormortisUtility.FindRankedColonist(requiredRanksOneAmong);

        if (pawn != null)
        {
            Store(QuestGen.slate, pawn);
        }
    }

    private void Store(Slate slate, Pawn pawn)
    {
        var name = storeAs.GetValue(slate);

        if (!name.NullOrEmpty())
        {
            slate.Set(name, pawn);
        }
    }
}
