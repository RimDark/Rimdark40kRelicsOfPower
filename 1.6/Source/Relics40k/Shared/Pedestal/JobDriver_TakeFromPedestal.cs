using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Relics40k;

public class JobDriver_TakeFromPedestal : JobDriver
{
    private CompPedestal Pedestal => job.targetA.Thing?.TryGetComp<CompPedestal>();

    public override bool TryMakePreToilReservations(bool errorOnFailed)
    {
        return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
        this.FailOn(() => Pedestal == null || !Pedestal.CanTake(pawn).Accepted);

        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

        var work = Toils_General.Wait(Pedestal?.Props.takeDurationTicks ?? 240, TargetIndex.A);
        work.WithProgressBarToilDelay(TargetIndex.A);
        work.FailOnDespawnedNullOrForbidden(TargetIndex.A);
        work.FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch);
        work.handlingFacing = true;
        yield return work;

        var take = ToilMaker.MakeToil("TakeFromPedestal");
        take.initAction = delegate
        {
            Pedestal?.Take(pawn);
        };
        take.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return take;
    }
}
