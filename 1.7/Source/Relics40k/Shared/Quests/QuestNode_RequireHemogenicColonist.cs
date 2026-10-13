using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace Relics40k;

/// <summary>
/// Fails the quest's test run unless the colony has a free colonist carrying a hemogen resource
/// gene. Optionally stores that colonist on the slate for quest text.
/// </summary>
public class QuestNode_RequireHemogenicColonist : QuestNode
{
    [NoTranslate]
    public SlateRef<string> storeAs;

    protected override bool TestRunInt(Slate slate)
    {
        var pawn = FindHemogenicColonist();

        if (pawn == null)
        {
            return false;
        }

        Store(slate, pawn);
        return true;
    }

    protected override void RunInt()
    {
        var pawn = FindHemogenicColonist();

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

    public static Pawn FindHemogenicColonist()
    {
        foreach (var pawn in PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists)
        {
            if (pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>() != null)
            {
                return pawn;
            }
        }

        return null;
    }
}
