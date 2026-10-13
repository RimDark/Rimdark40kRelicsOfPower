using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Relics40k;

public class JobDriver_BreakGeneSeal : JobDriver
{
    private Building_GeneCryptSeal Seal => job.targetA.Thing as Building_GeneCryptSeal;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
        this.FailOn(() => Seal == null || Seal.Opened || !Seal.CanBeOpenedBy(pawn).Accepted);

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        var duration = Seal?.Extension?.breakSealTicks ?? 5000;

        var work = Toils_General.Wait(duration, TargetIndex.A);
        work.WithProgressBarToilDelay(TargetIndex.A);
        work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        work.handlingFacing = true;
        yield return work;

        var breakSeal = ToilMaker.MakeToil("BreakSeal");
        breakSeal.initAction = delegate
        {
            Seal?.BreakSeal(pawn);
        };
        breakSeal.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return breakSeal;
    }
}
