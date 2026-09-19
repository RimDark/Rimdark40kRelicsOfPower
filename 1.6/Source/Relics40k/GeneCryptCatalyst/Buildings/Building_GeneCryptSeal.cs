using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Relics40k;

/// <summary>
/// The crypt's gene-lock. Only a pawn carrying the whole Unification-era gene set can break it,
/// and breaking it reveals the ascension engine, whatever was left stored beside it, and the
/// garrison that was sleeping around the vault.
/// </summary>
public class Building_GeneCryptSeal : Building
{
    private bool opened;

    public string engineCompletedSignal;

    public string engineDestroyedSignal;

    public bool Opened => opened;

    public DefModExtension_GeneCryptSeal Extension => def.GetModExtension<DefModExtension_GeneCryptSeal>();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref opened, "opened");
        Scribe_Values.Look(ref engineCompletedSignal, "engineCompletedSignal");
        Scribe_Values.Look(ref engineDestroyedSignal, "engineDestroyedSignal");
    }

    public AcceptanceReport CanBeOpenedBy(Pawn pawn)
    {
        if (opened)
        {
            return "Relics.GeneCryptCatalyst.AlreadyOpen".Translate();
        }

        var extension = Extension;

        if (extension == null || pawn?.genes == null)
        {
            return "Relics.GeneCryptCatalyst.SealNotThunderWarrior".Translate();
        }

        foreach (var gene in extension.requiredGenes)
        {
            if (!pawn.genes.HasActiveGene(gene))
            {
                return "Relics.GeneCryptCatalyst.SealNotThunderWarrior".Translate();
            }
        }

        return true;
    }

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
    {
        foreach (var option in base.GetFloatMenuOptions(selPawn))
        {
            yield return option;
        }

        var extension = Extension;

        if (extension?.breakSealJob == null)
        {
            yield break;
        }

        var label = (string)"Relics.GeneCryptCatalyst.BreakSeal".Translate();
        var report = CanBeOpenedBy(selPawn);

        if (!report.Accepted)
        {
            yield return new FloatMenuOption(label + " (" + report.Reason + ")", null);
            yield break;
        }

        if (!selPawn.CanReach(this, PathEndMode.Touch, Danger.Deadly))
        {
            yield return new FloatMenuOption(label + " (" + "NoPath".Translate() + ")", null);
            yield break;
        }

        if (!selPawn.CanReserve(this))
        {
            yield return new FloatMenuOption(label + " (" + "Reserved".Translate() + ")", null);
            yield break;
        }

        yield return new FloatMenuOption(label, delegate
        {
            selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(extension.breakSealJob, this), JobTag.Misc);
        });
    }

    /// <summary>Opens the vault: reveals the engine and its stores, then wakes the garrison.</summary>
    public void BreakSeal(Pawn opener)
    {
        if (opened)
        {
            return;
        }

        opened = true;

        var extension = Extension;

        SpawnContents(extension);
        WakeGarrison();

        Find.LetterStack.ReceiveLetter(
            "Relics.GeneCryptCatalyst.OpenedLetterLabel".Translate(),
            "Relics.GeneCryptCatalyst.OpenedLetterText".Translate(opener.Named("PAWN")),
            LetterDefOf.ThreatBig,
            new TargetInfo(Position, Map));

        // The seal is the vault's only door, so breaking it has to leave the way in open.
        var map = Map;
        var footprint = this.OccupiedRect();

        Destroy();

        foreach (var cell in footprint)
        {
            map.fogGrid.FloodUnfogAdjacent(cell, sendLetters: false);
        }
    }

    private void SpawnContents(DefModExtension_GeneCryptSeal extension)
    {
        var map = Map;

        if (extension == null || map == null)
        {
            return;
        }

        SpawnEngine(extension, map);
        SpawnTreasure(extension, map);
    }

    private void SpawnEngine(DefModExtension_GeneCryptSeal extension, Map map)
    {
        if (extension.engineDef == null)
        {
            return;
        }

        // A hand-authored vault already contains one.
        if (map.listerThings.ThingsOfDef(extension.engineDef).Any())
        {
            return;
        }

        bool Validator(IntVec3 candidate)
        {
            foreach (var occupied in GenAdj.OccupiedRect(candidate, Rot4.North, extension.engineDef.size).ExpandedBy(1))
            {
                if (!occupied.InBounds(map) || !occupied.Standable(map) || occupied.GetEdifice(map) != null)
                {
                    return false;
                }
            }

            return true;
        }

        if (!CellFinder.TryFindRandomCellNear(Position, map, 10, Validator, out var cell))
        {
            return;
        }

        var engine = GenSpawn.Spawn(ThingMaker.MakeThing(extension.engineDef), cell, map, Rot4.North) as Building_AscensionEngine;

        if (engine == null)
        {
            return;
        }

        engine.completedSignal = engineCompletedSignal;
        engine.destroyedSignal = engineDestroyedSignal;
    }

    private void SpawnTreasure(DefModExtension_GeneCryptSeal extension, Map map)
    {
        if (extension.treasureSetMaker?.root == null)
        {
            return;
        }

        var things = extension.treasureSetMaker.root.Generate();

        if (things.NullOrEmpty())
        {
            return;
        }

        foreach (var thing in things)
        {
            bool Validator(IntVec3 candidate)
            {
                return candidate.InBounds(map) && candidate.Standable(map) && candidate.GetEdifice(map) == null;
            }

            if (CellFinder.TryFindRandomCellNear(Position, map, extension.treasureRadius, Validator, out var cell))
            {
                GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near);
            }
            else
            {
                GenPlace.TryPlaceThing(thing, Position, map, ThingPlaceMode.Near);
            }
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

    public override string GetInspectString()
    {
        var text = base.GetInspectString();

        if (!text.NullOrEmpty())
        {
            text += "\n";
        }

        return text + (opened
            ? "Relics.GeneCryptCatalyst.InspectOpen".Translate()
            : "Relics.GeneCryptCatalyst.InspectSealed".Translate());
    }
}
