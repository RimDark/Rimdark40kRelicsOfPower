using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// Watches the Red Cellar once its map exists: every guardian killed refills part of the chalice's
/// cooldown, and the quest resolves on map removal by whether the chalice left with the player.
/// </summary>
public class QuestPart_RedCellar : QuestPart
{
    public string inSignalMapGenerated;

    public string inSignalMapRemoved;

    public Site site;

    public PawnKindDef guardianKind;

    public ThingDef chaliceDef;

    public float cooldownReductionPerKill = 0.25f;

    private List<Pawn> guardians = new List<Pawn>();

    private Thing chalice;

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
            CaptureCellar();
        }
        else if (signal.tag == inSignalMapRemoved)
        {
            quest.End(ChaliceIsWithPlayer() ? QuestEndOutcome.Success : QuestEndOutcome.Fail);
        }
    }

    public override void Notify_PawnKilled(Pawn pawn, DamageInfo? dinfo)
    {
        base.Notify_PawnKilled(pawn, dinfo);

        if (!guardians.Remove(pawn))
        {
            return;
        }

        if (chalice == null || chalice.Destroyed)
        {
            return;
        }

        var comp = chalice.TryGetComp<CompUseEffect_BloodChalice>();

        if (comp == null)
        {
            return;
        }

        comp.ReduceCooldown(Mathf.RoundToInt(comp.CooldownTicks * cooldownReductionPerKill));

        var remaining = comp.RemainingCooldownTicks;
        var text = remaining > 0
            ? "Relics.BloodChalice.GuardianFellRefilling".Translate(remaining.ToStringTicksToPeriod())
            : "Relics.BloodChalice.GuardianFellFull".Translate();

        Messages.Message(text, new LookTargets(chalice), MessageTypeDefOf.NeutralEvent);
    }

    private void CaptureCellar()
    {
        var map = site?.Map;

        if (map == null)
        {
            return;
        }

        guardians.Clear();

        foreach (var pawn in map.mapPawns.AllPawns)
        {
            if (pawn.kindDef == guardianKind)
            {
                guardians.Add(pawn);
            }
        }

        chalice = map.listerThings.ThingsOfDef(chaliceDef).FirstOrDefault();
    }

    private bool ChaliceIsWithPlayer()
    {
        if (chalice == null || chalice.Destroyed)
        {
            return false;
        }

        var holder = ThingOwnerUtility.GetAnyParent<Pawn>(chalice);

        if (holder != null)
        {
            return holder.Faction == Faction.OfPlayer;
        }

        var map = chalice.MapHeld;

        return map != null && map.IsPlayerHome;
    }

    public override void Cleanup()
    {
        base.Cleanup();
        guardians.Clear();
        chalice = null;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignalMapGenerated, "inSignalMapGenerated");
        Scribe_Values.Look(ref inSignalMapRemoved, "inSignalMapRemoved");
        Scribe_References.Look(ref site, "site");
        Scribe_Defs.Look(ref guardianKind, "guardianKind");
        Scribe_Defs.Look(ref chaliceDef, "chaliceDef");
        Scribe_Values.Look(ref cooldownReductionPerKill, "cooldownReductionPerKill", 0.25f);
        Scribe_Collections.Look(ref guardians, "guardians", LookMode.Reference);
        Scribe_References.Look(ref chalice, "chalice");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            guardians ??= new List<Pawn>();
            guardians.RemoveAll(p => p == null);
        }
    }
}
