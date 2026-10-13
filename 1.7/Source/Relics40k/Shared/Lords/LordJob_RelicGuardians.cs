using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Relics40k;

/// <summary>
/// Sleep until the relic is lifted, a sleeper is harmed by the player, or a dormancy wake-up arrives,
/// then assault without fleeing. Used by every relic site; the memo and wake message are per site.
/// </summary>
public class LordJob_RelicGuardians : LordJob
{
    public const string MemoRelicTaken = "BEWH_RelicTaken";

    private Faction faction;

    private string wakeMessageKey;

    // When set, the guardians stand frozen under this duty instead of lying asleep.
    private DutyDef frozenDuty;

    public override bool GuiltyOnDowned => true;

    public LordJob_RelicGuardians()
    {
    }

    public LordJob_RelicGuardians(Faction faction, string wakeMessageKey, DutyDef frozenDuty = null)
    {
        this.faction = faction;
        this.wakeMessageKey = wakeMessageKey;
        this.frozenDuty = frozenDuty;
    }

    public override StateGraph CreateGraph()
    {
        var graph = new StateGraph();
        LordToil sleep = frozenDuty != null ? new LordToil_FrozenInTime(frozenDuty) : new LordToil_Sleep();
        graph.StartingToil = sleep;

        var assault = graph.AttachSubgraph(new LordJob_AssaultColony(faction, false, false).CreateGraph()).StartingToil;

        var wake = new Transition(sleep, assault);
        wake.AddTrigger(new Trigger_Memo(MemoRelicTaken));
        wake.AddTrigger(new Trigger_PawnHarmed(1f, false, Faction.OfPlayer, frozenDuty));
        wake.AddTrigger(new Trigger_Custom(signal => signal.type == TriggerSignalType.DormancyWakeup));

        if (!wakeMessageKey.NullOrEmpty())
        {
            wake.AddPreAction(new TransitionAction_Message(wakeMessageKey.Translate(), MessageTypeDefOf.ThreatBig));
        }

        wake.AddPostAction(new TransitionAction_WakeAll());
        graph.AddTransition(wake);

        return graph;
    }

    /// <summary>Sends the relic-taken memo to every lord on the map and opens sealed sarcophagi.</summary>
    public static void WakeAll(Map map)
    {
        if (map == null)
        {
            return;
        }

        foreach (var lord in map.lordManager.lords)
        {
            lord.ReceiveMemo(MemoRelicTaken);
        }

        Building_GuardianSarcophagus.ReleaseAll(map);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref faction, "faction");
        Scribe_Values.Look(ref wakeMessageKey, "wakeMessageKey");
        Scribe_Defs.Look(ref frozenDuty, "frozenDuty");
    }
}
