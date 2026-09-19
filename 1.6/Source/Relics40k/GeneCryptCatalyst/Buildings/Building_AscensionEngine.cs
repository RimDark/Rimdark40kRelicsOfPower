using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Relics40k;

/// <summary>
/// The crypt's ascension engine. Accepts only a thunder warrior, runs a timed procedure under
/// attack, remakes the occupant, then burns itself out for good.
/// </summary>
public class Building_AscensionEngine : Building_Enterable
{
    private bool ascended;

    private bool burnedOut;

    private int wavesSent;

    private int nextWaveTick = -1;

    public Pawn ascendedPawn;

    public string completedSignal;

    public string destroyedSignal;

    public override Vector3 PawnDrawOffset => Vector3.zero;

    public override bool IsContentsSuspended => false;

    public DefModExtension_AscensionEngine Extension => def.GetModExtension<DefModExtension_AscensionEngine>();

    public Pawn Occupant
    {
        get
        {
            for (var i = 0; i < innerContainer.Count; i++)
            {
                if (innerContainer[i] is Pawn pawn)
                {
                    return pawn;
                }
            }

            return null;
        }
    }

    private int TicksLeft
    {
        get
        {
            var extension = Extension;
            var total = extension?.procedureTicks ?? 20000;
            return startTick + total - Find.TickManager.TicksGame;
        }
    }

    public override AcceptanceReport CanAcceptPawn(Pawn selPawn)
    {
        if (!selPawn.IsColonist && !selPawn.IsSlaveOfColony && !selPawn.IsPrisonerOfColony)
        {
            return false;
        }

        if (ascended || burnedOut)
        {
            return "Relics.GeneCryptCatalyst.Spent".Translate();
        }

        if (Working || Occupant != null)
        {
            return "Relics.GeneCryptCatalyst.Busy".Translate();
        }

        if (selectedPawn != null && selectedPawn != selPawn)
        {
            return false;
        }

        if (selPawn.DevelopmentalStage.Baby())
        {
            return "Relics.GeneCryptCatalyst.EngineNotThunderWarrior".Translate();
        }

        var extension = Extension;

        if (extension == null || selPawn.genes == null)
        {
            return "Relics.GeneCryptCatalyst.EngineNotThunderWarrior".Translate();
        }

        foreach (var gene in extension.requiredGenes)
        {
            if (!selPawn.genes.HasActiveGene(gene))
            {
                return "Relics.GeneCryptCatalyst.EngineNotThunderWarrior".Translate();
            }
        }

        return true;
    }

    public override void TryAcceptPawn(Pawn pawn)
    {
        if (!CanAcceptPawn(pawn).Accepted)
        {
            return;
        }

        var wasSelected = pawn.DeSpawnOrDeselect();

        if (pawn.holdingOwner != null)
        {
            pawn.holdingOwner.TryTransferToContainer(pawn, innerContainer);
        }
        else
        {
            innerContainer.TryAdd(pawn);
        }

        if (wasSelected)
        {
            Find.Selector.Select(pawn, playSound: false, forceDesignatorDeselect: false);
        }

        BeginProcedure();
    }

    /// <summary>Starts the timer, wakes the dormant garrison and schedules the first wave.</summary>
    private void BeginProcedure()
    {
        var extension = Extension;

        startTick = Find.TickManager.TicksGame;
        wavesSent = 0;
        nextWaveTick = startTick + (extension?.firstWaveDelayTicks ?? 2500);

        WakeGarrison();

        Find.LetterStack.ReceiveLetter(
            "Relics.GeneCryptCatalyst.StartedLetterLabel".Translate(),
            "Relics.GeneCryptCatalyst.StartedLetterText".Translate(Occupant.Named("PAWN")),
            LetterDefOf.ThreatBig,
            this);
    }

    protected override void Tick()
    {
        base.Tick();

        if (!Working || ascended || burnedOut)
        {
            return;
        }

        if (Occupant == null)
        {
            startTick = -1;
            return;
        }

        var extension = Extension;

        if (nextWaveTick > 0 && Find.TickManager.TicksGame >= nextWaveTick)
        {
            SendWave(extension);
        }

        if (TicksLeft <= 0)
        {
            Ascend(extension);
        }
    }

    private void SendWave(DefModExtension_AscensionEngine extension)
    {
        var maxWaves = extension?.maxWaves ?? 4;

        if (wavesSent >= maxWaves)
        {
            nextWaveTick = -1;
            return;
        }

        wavesSent++;
        nextWaveTick = Find.TickManager.TicksGame + (extension?.waveIntervalTicks ?? 6000);

        var map = Map;
        var mechanoids = Faction.OfMechanoids;

        if (map == null || mechanoids == null)
        {
            return;
        }

        var factor = (extension?.waveThreatPointsFactor ?? 0.5f) * (1f + (extension?.waveEscalation ?? 0.25f) * (wavesSent - 1));
        var points = BaseThreatPoints(map) * factor;

        // A site map has almost no wealth, so the storyteller's own number can fall below the
        // cheapest mechanoid; never ask for a group the faction cannot build.
        points = Mathf.Max(points, mechanoids.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat));

        var parms = new IncidentParms
        {
            target = map,
            faction = mechanoids,
            points = points,
            raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn
        };

        var pawns = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
        {
            groupKind = PawnGroupKindDefOf.Combat,
            tile = map.Tile,
            faction = mechanoids,
            points = points
        }).ToList();

        if (pawns.Count == 0)
        {
            return;
        }

        parms.raidArrivalMode.Worker.Arrive(pawns, parms);

        LordMaker.MakeNewLord(mechanoids, new LordJob_AssaultThings(mechanoids, [this]), map, pawns);

        Messages.Message("Relics.GeneCryptCatalyst.WaveIncoming".Translate(wavesSent, maxWaves), this, MessageTypeDefOf.ThreatBig);
    }

    /// <summary>The site's own threat budget when there is one; the storyteller's otherwise.</summary>
    private static float BaseThreatPoints(Map map)
    {
        var best = 0f;

        if (map.Parent is Site site)
        {
            foreach (var part in site.parts)
            {
                best = Mathf.Max(best, part.parms.threatPoints);
            }
        }

        return best > 0f ? best : StorytellerUtility.DefaultThreatPointsNow(map);
    }

    private void Ascend(DefModExtension_AscensionEngine extension)
    {
        var occupant = Occupant;

        if (occupant == null || extension == null)
        {
            return;
        }

        RemoveGenes(occupant, extension.genesToRemove);
        AddGenes(occupant, extension.genesToAdd);
        ApplyXenotype(occupant, extension);

        if (extension.comaHediff != null)
        {
            occupant.health.AddHediff(extension.comaHediff);
        }

        ascended = true;
        ascendedPawn = occupant;
        startTick = -1;
        nextWaveTick = -1;

        EjectContents();

        Find.LetterStack.ReceiveLetter(
            "Relics.GeneCryptCatalyst.AscendedLetterLabel".Translate(occupant.Named("PAWN")),
            "Relics.GeneCryptCatalyst.AscendedLetterText".Translate(occupant.Named("PAWN")),
            LetterDefOf.PositiveEvent,
            occupant);

        if (!completedSignal.NullOrEmpty())
        {
            Find.SignalManager.SendSignal(new Signal(completedSignal));
        }

        BurnOut(extension);
    }

    /// <summary>Cooks the engine to slag so it can never be used again.</summary>
    private void BurnOut(DefModExtension_AscensionEngine extension)
    {
        burnedOut = true;

        var map = Map;
        var position = Position;

        if (map != null)
        {
            foreach (var cell in this.OccupiedRect())
            {
                FilthMaker.TryMakeFilth(cell, map, ThingDefOf.Filth_Ash, Rand.Range(1, 3));
            }

            if (!extension.burnoutLeavings.NullOrEmpty())
            {
                foreach (var leaving in extension.burnoutLeavings)
                {
                    GenPlace.TryPlaceThing(ThingMaker.MakeThing(leaving), position, map, ThingPlaceMode.Near);
                }
            }
        }

        Destroy();
    }

    private static void RemoveGenes(Pawn pawn, List<GeneDef> geneDefs)
    {
        if (geneDefs.NullOrEmpty() || pawn.genes == null)
        {
            return;
        }

        var toRemove = new List<Gene>();

        foreach (var gene in pawn.genes.GenesListForReading)
        {
            if (gene != null && geneDefs.Contains(gene.def))
            {
                toRemove.Add(gene);
            }
        }

        foreach (var gene in toRemove)
        {
            pawn.genes.RemoveGene(gene);
        }
    }

    private static void AddGenes(Pawn pawn, List<GeneDef> geneDefs)
    {
        if (geneDefs.NullOrEmpty() || pawn.genes == null)
        {
            return;
        }

        foreach (var geneDef in geneDefs)
        {
            if (!pawn.genes.HasActiveGene(geneDef))
            {
                pawn.genes.AddGene(geneDef, true);
            }
        }
    }

    private static void ApplyXenotype(Pawn pawn, DefModExtension_AscensionEngine extension)
    {
        if (pawn.genes == null)
        {
            return;
        }

        if (extension.newXenotype != null)
        {
            pawn.genes.SetXenotypeDirect(extension.newXenotype);
        }

        if (!extension.newXenotypeName.NullOrEmpty())
        {
            pawn.genes.xenotypeName = extension.newXenotypeName;
        }

        if (extension.newXenotypeIcon != null)
        {
            pawn.genes.iconDef = extension.newXenotypeIcon;
        }
    }

    private void WakeGarrison()
    {
        var map = Map;

        if (map == null)
        {
            return;
        }

        foreach (var pawn in map.mapPawns.AllPawnsSpawned.ToList())
        {
            if (pawn.Faction != Faction.OfMechanoids)
            {
                continue;
            }

            pawn.TryGetComp<CompCanBeDormant>()?.WakeUp();
        }
    }

    public void EjectContents()
    {
        var map = Map;

        if (map == null)
        {
            return;
        }

        innerContainer.TryDropAll(InteractionCell, map, ThingPlaceMode.Near);
        selectedPawn = null;
    }

    public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
    {
        var failed = mode == DestroyMode.KillFinalize && !ascended;

        if (failed)
        {
            EjectContents();
            startTick = -1;

            if (!destroyedSignal.NullOrEmpty())
            {
                Find.SignalManager.SendSignal(new Signal(destroyedSignal));
            }
        }

        base.Destroy(mode);
    }

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
    {
        foreach (var option in base.GetFloatMenuOptions(selPawn))
        {
            yield return option;
        }

        var label = (string)"Relics.GeneCryptCatalyst.Enter".Translate();

        if (!selPawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
        {
            yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
            yield break;
        }

        var report = CanAcceptPawn(selPawn);

        if (report.Accepted)
        {
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(label, delegate
                {
                    Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                        "Relics.GeneCryptCatalyst.Confirm".Translate(selPawn.Named("PAWN")),
                        delegate { SelectPawn(selPawn); }));
                }), selPawn, this);
        }
        else if (!report.Reason.NullOrEmpty())
        {
            yield return new FloatMenuOption(label + " (" + report.Reason + ")", null);
        }
    }

    public override string GetInspectString()
    {
        var text = base.GetInspectString();

        if (!text.NullOrEmpty())
        {
            text += "\n";
        }

        if (burnedOut || ascended)
        {
            return text + "Relics.GeneCryptCatalyst.InspectSpent".Translate();
        }

        if (Working)
        {
            return text + "Relics.GeneCryptCatalyst.InspectRunning".Translate(TicksLeft.ToStringTicksToPeriod());
        }

        return text + "Relics.GeneCryptCatalyst.InspectIdle".Translate();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref ascended, "ascended");
        Scribe_Values.Look(ref burnedOut, "burnedOut");
        Scribe_Values.Look(ref wavesSent, "wavesSent");
        Scribe_Values.Look(ref nextWaveTick, "nextWaveTick", -1);
        Scribe_References.Look(ref ascendedPawn, "ascendedPawn");
        Scribe_Values.Look(ref completedSignal, "completedSignal");
        Scribe_Values.Look(ref destroyedSignal, "destroyedSignal");
    }
}
