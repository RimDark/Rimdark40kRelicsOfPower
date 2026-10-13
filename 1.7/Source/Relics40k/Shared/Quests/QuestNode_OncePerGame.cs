using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace Relics40k;

/// <summary>
/// Like QuestNode_OncePerColony, but consumes the tag when the quest ends in success rather than
/// when it is generated, so a declined or failed run does not burn the quest for the save.
/// </summary>
public class QuestNode_OncePerGame : QuestNode
{
    [NoTranslate]
    public SlateRef<string> tag;

    protected override bool TestRunInt(Slate slate)
    {
        var value = tag.GetValue(slate);

        return !value.NullOrEmpty() && !GameComponent_Relics40kQuests.HasFired(value);
    }

    protected override void RunInt()
    {
        var slate = QuestGen.slate;
        var value = tag.GetValue(slate);

        if (value.NullOrEmpty())
        {
            return;
        }

        QuestGen.quest.AddPart(new QuestPart_MarkOncePerGame
        {
            tag = value
        });
    }
}
