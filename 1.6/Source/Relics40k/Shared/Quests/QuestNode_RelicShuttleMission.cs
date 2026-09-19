using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// A chartered shuttle that lands at the colony, carries a boarding party to the site, waits there,
/// and brings the survivors home. Built on Core types only - QuestGen_Shuttle.GenerateShuttle and
/// QuestNode_GenerateShuttle both gate on Royalty or Ideology, so the shuttle is made here instead.
/// </summary>
public class QuestNode_RelicShuttleMission : QuestNode
{
    public SlateRef<Site> site;

    public SlateRef<TransportShipDef> shipDef;

    public SlateRef<int> requiredPawnCount;

    public SlateRef<int> passageFee;

    public SlateRef<int> colonyBoardingDelayTicks;

    public SlateRef<int> sitePatienceTicks;

    [NoTranslate]
    public SlateRef<string> storeShuttleAs = "shuttle";

    protected override bool TestRunInt(Slate slate)
    {
        return shipDef.GetValue(slate) != null;
    }

    protected override void RunInt()
    {
        var quest = QuestGen.quest;
        var slate = QuestGen.slate;

        var targetSite = site.GetValue(slate);
        var shipDefValue = shipDef.GetValue(slate);
        var colonyMap = slate.Get<Map>("map");

        if (targetSite == null || shipDefValue == null || colonyMap == null)
        {
            Log.Error("[Relics of Power] Relic shuttle mission is missing its site, ship def or colony map.");
            return;
        }

        var pawnCount = Mathf.Max(1, requiredPawnCount.GetValue(slate));
        var fee = Mathf.Max(0, passageFee.GetValue(slate));
        var boardingDelay = Mathf.Max(2500, colonyBoardingDelayTicks.GetValue(slate));
        var patience = Mathf.Max(2500, sitePatienceTicks.GetValue(slate));

        slate.Set("requiredPawnCount", pawnCount);
        slate.Set("passageFee", fee);

        if (fee > 0)
        {
            quest.AddPart(new QuestPart_RelicShuttleFee
            {
                amount = fee,
                mapParent = colonyMap.Parent
            });
        }

        var questTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("Relics40kRelicShuttle");
        var shuttle = MakeShuttle(pawnCount);

        slate.Set(storeShuttleAs.GetValue(slate) ?? "shuttle", shuttle);
        QuestUtility.AddQuestTag(ref shuttle.questTags, questTag);

        var transportShip = quest.GenerateTransportShip(shipDefValue, null, shuttle).transportShip;
        slate.Set("transportShip", transportShip);
        QuestUtility.AddQuestTag(ref transportShip.questTags, questTag);
        quest.SendTransportShipAwayOnCleanup(transportShip, unloadContents: true, TransportShipDropMode.None);

        quest.AddShipJob_Arrive(transportShip, colonyMap.Parent, null, null, ShipJobStartMode.Instant);
        quest.AddShipJob_WaitSendable(transportShip, targetSite, leaveImmeiatelyWhenSatisfied: true);
        quest.AddShipJob(transportShip, ShipJobDefOf.Unload);
        quest.AddShipJob_WaitSendable(transportShip, colonyMap.Parent, leaveImmeiatelyWhenSatisfied: true, targetPlayerSettlement: true);
        quest.AddShipJob(transportShip, ShipJobDefOf.Unload);
        quest.AddShipJob_FlyAway(transportShip, null, null, TransportShipDropMode.None);

        var sentSatisfied = QuestGenUtility.HardcodedSignalWithQuestID("shuttle.SentSatisfied");
        var flewAway = QuestGenUtility.HardcodedSignalWithQuestID("transportShip.FlewAway");
        var mapGenerated = QuestGenUtility.HardcodedSignalWithQuestID("site.MapGenerated");

        quest.TendPawns(null, shuttle, sentSatisfied);
        quest.FeedPawns(null, shuttle, sentSatisfied);

        // Once the lighter has left the colony it waits for whoever is still alive at the tomb
        // rather than an exact count, so a casualty cannot strand the rest.
        quest.RequiredShuttleThings(shuttle, targetSite, flewAway, requireAllColonistsOnMap: true);

        quest.ShuttleLeaveDelay(shuttle, boardingDelay, null, Gen.YieldSingle(sentSatisfied), null, delegate
        {
            quest.Letter(LetterDefOf.NegativeEvent, null,
                label: "Relics.Shared.LighterLeftLabel".Translate(),
                text: "Relics.Shared.LighterLeftColonyText".Translate());
            quest.End(QuestEndOutcome.Fail, 0, null, null, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: false);
        });

        quest.Delay(patience, delegate
        {
            quest.Letter(LetterDefOf.NegativeEvent, null,
                label: "Relics.Shared.LighterLeftLabel".Translate(),
                text: "Relics.Shared.LighterLeftTombText".Translate());
            quest.End(QuestEndOutcome.Fail, 0, null, null, QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: false);
        }, mapGenerated, sentSatisfied, null, false, Gen.YieldSingle(targetSite),
            "Relics.Shared.LighterWaiting".Translate());

        quest.End(QuestEndOutcome.Fail, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("shuttle.Killed"),
            QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
        quest.End(QuestEndOutcome.Fail, 0, null, QuestGenUtility.HardcodedSignalWithQuestID("shuttle.LeftBehind"),
            QuestPart.SignalListenMode.OngoingOnly, sendStandardLetter: true);
    }

    private static Thing MakeShuttle(int pawnCount)
    {
        var shuttle = ThingMaker.MakeThing(ThingDefOf.Shuttle);
        var comp = shuttle.TryGetComp<CompShuttle>();

        if (comp != null)
        {
            comp.acceptColonists = true;
            comp.onlyAcceptColonists = true;
            comp.onlyAcceptHealthy = false;
            comp.requiredColonistCount = pawnCount;
            comp.maxColonistCount = pawnCount;
            comp.permitShuttle = false;
        }

        return shuttle;
    }
}
