using RimWorld;
using Verse;

namespace Relics40k;

public class QuestPart_MarkOncePerGame : QuestPart
{
    public string inSignal;

    public string tag;

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);

        if (signal.tag != inSignal || tag.NullOrEmpty())
        {
            return;
        }

        GameComponent_Relics40kQuests.MarkFired(tag);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
        Scribe_Values.Look(ref tag, "tag");
    }
}
