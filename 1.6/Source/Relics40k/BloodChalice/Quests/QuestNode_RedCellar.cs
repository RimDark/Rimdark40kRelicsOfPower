using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace Relics40k;

public class QuestNode_RedCellar : QuestNode
{
    public SlateRef<Site> site;

    public SlateRef<PawnKindDef> guardianKind;

    public SlateRef<ThingDef> chaliceDef;

    public SlateRef<float> cooldownReductionPerKill;

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

        var part = new QuestPart_RedCellar
        {
            site = site.GetValue(slate),
            guardianKind = guardianKind.GetValue(slate),
            chaliceDef = chaliceDef.GetValue(slate),
            cooldownReductionPerKill = cooldownReductionPerKill.GetValue(slate),
            inSignalMapGenerated = QuestGenUtility.HardcodedSignalWithQuestID(inSignalMapGenerated.GetValue(slate)),
            inSignalMapRemoved = QuestGenUtility.HardcodedSignalWithQuestID(inSignalMapRemoved.GetValue(slate))
        };

        QuestGen.quest.AddPart(part);
    }
}
