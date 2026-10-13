using System.Collections.Generic;
using RimWorld.QuestGen;
using Verse;

namespace Relics40k;

/// <summary>
/// Only allows the quest to be offered while a free colonist has every listed gene active.
/// </summary>
public class QuestNode_ColonyHasGenes : QuestNode
{
    public List<GeneDef> genes = [];

    protected override bool TestRunInt(Slate slate)
    {
        if (genes.NullOrEmpty())
        {
            return true;
        }

        foreach (var map in Find.Maps)
        {
            if (!map.IsPlayerHome)
            {
                continue;
            }

            foreach (var pawn in map.mapPawns.FreeColonists)
            {
                if (HasAllGenes(pawn))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasAllGenes(Pawn pawn)
    {
        if (pawn?.genes == null)
        {
            return false;
        }

        foreach (var gene in genes)
        {
            if (!pawn.genes.HasActiveGene(gene))
            {
                return false;
            }
        }

        return true;
    }

    protected override void RunInt()
    {
    }
}
