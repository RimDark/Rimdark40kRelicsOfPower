using RimWorld;
using Verse;

namespace Relics40k;

public class QuestPart_MarkOncePerGame : QuestPart
{
    public string tag;

    public override void Cleanup()
    {
        base.Cleanup();

        if (tag.NullOrEmpty() || quest.State != QuestState.EndedSuccess)
        {
            return;
        }

        GameComponent_Relics40kQuests.MarkFired(tag);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref tag, "tag");
    }
}
