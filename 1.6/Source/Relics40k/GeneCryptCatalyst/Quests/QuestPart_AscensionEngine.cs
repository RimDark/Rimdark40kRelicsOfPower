using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace Relics40k;

/// <summary>
/// Wires the engine to the quest: fails the moment it is destroyed without an ascension, and
/// resolves on map removal by whether the ascended pawn actually got out alive.
/// </summary>
public class QuestPart_AscensionEngine : QuestPart
{
    public string inSignalMapGenerated;

    public string inSignalMapRemoved;

    public string inSignalEngineCompleted;

    public string inSignalEngineDestroyed;

    public Site site;

    public ThingDef sealDef;

    public ThingDef engineDef;

    private Building_GeneCryptSeal seal;

    private Pawn ascendedPawn;

    public override IEnumerable<GlobalTargetInfo> QuestLookTargets
    {
        get
        {
            if (site != null)
            {
                yield return site;
            }
        }
    }

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);

        if (signal.tag == inSignalMapGenerated)
        {
            CaptureSeal();
            BindExistingEngine();
        }
        else if (signal.tag == inSignalEngineCompleted)
        {
            ascendedPawn = FindEngine()?.ascendedPawn;
        }
        else if (signal.tag == inSignalEngineDestroyed)
        {
            quest.End(QuestEndOutcome.Fail);
        }
        else if (signal.tag == inSignalMapRemoved)
        {
            quest.End(AscendedPawnIsSafe() ? QuestEndOutcome.Success : QuestEndOutcome.Fail);
        }
    }

    /// <summary>
    /// The engine only exists once the seal is broken, so the signal tags are handed to the seal
    /// at map generation and it forwards them on when it spawns the engine.
    /// </summary>
    private void CaptureSeal()
    {
        var map = site?.Map;

        if (map == null || sealDef == null)
        {
            return;
        }

        seal = map.listerThings.ThingsOfDef(sealDef).FirstOrDefault() as Building_GeneCryptSeal;

        if (seal == null)
        {
            return;
        }

        seal.engineCompletedSignal = inSignalEngineCompleted;
        seal.engineDestroyedSignal = inSignalEngineDestroyed;
    }

    /// <summary>When the vault ships with its engine already placed there is no seal to forward through.</summary>
    private void BindExistingEngine()
    {
        var engine = FindEngine();

        if (engine == null)
        {
            return;
        }

        engine.completedSignal = inSignalEngineCompleted;
        engine.destroyedSignal = inSignalEngineDestroyed;
    }

    private Building_AscensionEngine FindEngine()
    {
        var map = site?.Map;

        if (map == null || engineDef == null)
        {
            return null;
        }

        return map.listerThings.ThingsOfDef(engineDef).FirstOrDefault() as Building_AscensionEngine;
    }

    private bool AscendedPawnIsSafe()
    {
        if (ascendedPawn == null || ascendedPawn.Destroyed || ascendedPawn.Dead)
        {
            return false;
        }

        return ascendedPawn.Faction == Faction.OfPlayer;
    }

    public override void Cleanup()
    {
        base.Cleanup();
        seal = null;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignalMapGenerated, "inSignalMapGenerated");
        Scribe_Values.Look(ref inSignalMapRemoved, "inSignalMapRemoved");
        Scribe_Values.Look(ref inSignalEngineCompleted, "inSignalEngineCompleted");
        Scribe_Values.Look(ref inSignalEngineDestroyed, "inSignalEngineDestroyed");
        Scribe_References.Look(ref site, "site");
        Scribe_Defs.Look(ref sealDef, "sealDef");
        Scribe_Defs.Look(ref engineDef, "engineDef");
        Scribe_References.Look(ref seal, "seal");
        Scribe_References.Look(ref ascendedPawn, "ascendedPawn");
    }
}
