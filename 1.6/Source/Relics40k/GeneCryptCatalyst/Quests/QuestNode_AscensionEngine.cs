using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace Relics40k;

public class QuestNode_AscensionEngine : QuestNode
{
    public SlateRef<Site> site;

    public SlateRef<ThingDef> sealDef;

    public SlateRef<ThingDef> engineDef;

    [NoTranslate]
    public SlateRef<string> inSignalMapGenerated;

    [NoTranslate]
    public SlateRef<string> inSignalMapRemoved;

    protected override bool TestRunInt(Slate slate)
    {
        return true;
    }

    protected override void RunInt()
    {
        var slate = QuestGen.slate;

        QuestGen.quest.AddPart(new QuestPart_AscensionEngine
        {
            site = site.GetValue(slate),
            sealDef = sealDef.GetValue(slate),
            engineDef = engineDef.GetValue(slate),
            inSignalMapGenerated = QuestGenUtility.HardcodedSignalWithQuestID(inSignalMapGenerated.GetValue(slate)),
            inSignalMapRemoved = QuestGenUtility.HardcodedSignalWithQuestID(inSignalMapRemoved.GetValue(slate)),
            inSignalEngineCompleted = QuestGen.GenerateNewSignal("EngineCompleted"),
            inSignalEngineDestroyed = QuestGen.GenerateNewSignal("EngineDestroyed")
        });
    }
}
