using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Relics40k;

public class JobDriver_TakeTempormortis : JobDriver
{
    private Building_TombReliquary Reliquary => job.targetA.Thing as Building_TombReliquary;

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
        this.FailOn(() => Reliquary == null || !Reliquary.CanBeTakenBy(pawn).Accepted);

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        var work = Toils_General.Wait(240, TargetIndex.A);
        work.WithProgressBarToilDelay(TargetIndex.A);
        work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        work.handlingFacing = true;
        yield return work;

        var take = ToilMaker.MakeToil("TakeTempormortis");
        take.initAction = delegate
        {
            Reliquary?.TakeRelic(pawn);
        };
        take.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return take;
    }
}
