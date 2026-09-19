using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace Relics40k;

/// <summary>
/// Holds pawns standing exactly where they are, facing wherever they were facing, doing nothing.
/// The stun is re-applied on a short cycle so it never lapses, and released the moment the toil
/// ends - so whatever stopped them is what is holding them, and losing it lets them move again.
/// </summary>
public class LordToil_FrozenInTime : LordToil
{
    private const int StunTicks = 600;

    private const int RefreshIntervalTicks = 120;

    private readonly DutyDef dutyDef;

    public override bool AllowSatisfyLongNeeds => false;

    public override bool AllowRestingInBed => false;

    public LordToil_FrozenInTime(DutyDef dutyDef = null)
    {
        this.dutyDef = dutyDef;
    }

    public override void UpdateAllDuties()
    {
        var duty = dutyDef ?? DutyDefOf.IdleNoInteraction;

        for (var i = 0; i < lord.ownedPawns.Count; i++)
        {
            lord.ownedPawns[i].mindState.duty = new PawnDuty(duty);
        }
    }

    public override void Init()
    {
        base.Init();
        FreezeAll();
    }

    public override void LordToilTick()
    {
        base.LordToilTick();

        if (Find.TickManager.TicksGame % RefreshIntervalTicks == 0)
        {
            FreezeAll();
        }
    }

    private void FreezeAll()
    {
        for (var i = 0; i < lord.ownedPawns.Count; i++)
        {
            var pawn = lord.ownedPawns[i];

            if (pawn == null || pawn.Dead || !pawn.Spawned || pawn.Downed)
            {
                continue;
            }

            pawn.pather?.StopDead();
            pawn.stances?.stunner?.StunFor(StunTicks, null, false, false, true);
        }
    }

    public override void Cleanup()
    {
        base.Cleanup();

        for (var i = 0; i < lord.ownedPawns.Count; i++)
        {
            lord.ownedPawns[i]?.stances?.stunner?.StopStun();
        }
    }
}
