using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace Relics40k;

/// <summary>
/// The blood chalice. Drinkable only by a pawn carrying a hemogen resource gene; heals
/// everything except lost limbs, tops the pawn's hemogen and deathrest off, then goes on
/// cooldown. The chalice is never consumed, so there is deliberately no
/// CompUseEffect_DestroySelf on the def.
/// </summary>
public class CompUseEffect_BloodChalice : CompUseEffect
{
    private int lastUsedTick = -1;

    private DefModExtension_BloodChalice Extension =>
        parent.def.GetModExtension<DefModExtension_BloodChalice>();

    public int RemainingCooldownTicks
    {
        get
        {
            var extension = Extension;

            if (extension == null || lastUsedTick < 0)
            {
                return 0;
            }

            return Mathf.Max(0, lastUsedTick + extension.cooldownTicks - Find.TickManager.TicksGame);
        }
    }

    public int CooldownTicks => Extension?.cooldownTicks ?? 0;

    public void StartCooldown()
    {
        lastUsedTick = Find.TickManager.TicksGame;
    }

    public void ReduceCooldown(int ticks)
    {
        if (lastUsedTick >= 0 && ticks > 0)
        {
            lastUsedTick -= ticks;
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref lastUsedTick, "BEWH_bloodChaliceLastUsedTick", -1);
    }

    /// <summary>
    /// Gates the float menu option, the targeting gizmo and JobDriver_UseItem's FailOn all at
    /// once - CompUsable.CanBeUsedBy polls every CompUseEffect on the parent.
    /// </summary>
    public override AcceptanceReport CanBeUsedBy(Pawn p)
    {
        if (Extension == null || p?.genes == null)
        {
            return "Relics.BloodChalice.NotHemogenic".Translate();
        }

        // Class check, not a defName check: any mod whose hemogenic gene derives from
        // Gene_Hemogen qualifies, and no mod becomes a dependency.
        if (p.genes.GetFirstGeneOfType<Gene_Hemogen>() == null)
        {
            return "Relics.BloodChalice.NotHemogenic".Translate();
        }

        var remaining = RemainingCooldownTicks;

        if (remaining > 0)
        {
            return "Relics.BloodChalice.OnCooldown".Translate(remaining.ToStringTicksToPeriod());
        }

        return true;
    }

    public override TaggedString ConfirmMessage(Pawn p)
    {
        return "Relics.BloodChalice.Confirm".Translate(p.Named("PAWN"));
    }

    public override string CompInspectStringExtra()
    {
        var remaining = RemainingCooldownTicks;

        if (remaining > 0)
        {
            return "Relics.BloodChalice.Refilling".Translate(remaining.ToStringTicksToPeriod());
        }

        return "Relics.BloodChalice.Ready".Translate();
    }

    public override void DoEffect(Pawn usedBy)
    {
        base.DoEffect(usedBy);

        var extension = Extension;

        if (extension == null || usedBy == null || usedBy.Dead)
        {
            return;
        }

        HealBody(usedBy, extension);
        FillHemogen(usedBy, extension);
        FillDeathrest(usedBy, extension);
        GiveThought(usedBy, extension);

        // Set last, so an exception part-way through can never silently burn the charge.
        lastUsedTick = Find.TickManager.TicksGame;

        Messages.Message(
            "Relics.BloodChalice.Message".Translate(usedBy.Named("PAWN")),
            usedBy,
            MessageTypeDefOf.PositiveEvent);
    }

    private static void HealBody(Pawn pawn, DefModExtension_BloodChalice extension)
    {
        if (pawn.health?.hediffSet == null)
        {
            return;
        }

        // Copy first: RemoveHediff mutates the list we would otherwise be iterating.
        var hediffs = new List<Hediff>(pawn.health.hediffSet.hediffs);

        foreach (var hediff in hediffs)
        {
            if (hediff?.def == null)
            {
                continue;
            }

            if (!extension.excludedHediffs.NullOrEmpty() && extension.excludedHediffs.Contains(hediff.def))
            {
                continue;
            }

            // A limb the chalice cannot give back. Cauterise the stump so it stops bleeding
            // and stops hurting, but leave the part missing.
            if (hediff is Hediff_MissingPart missingPart)
            {
                if (extension.sealStumps && missingPart.IsFresh)
                {
                    missingPart.IsFresh = false;
                }

                continue;
            }

            // Bionics, prosthetics, implants. Not damage.
            if (hediff is Hediff_AddedPart || hediff is Hediff_Implant || hediff.def.countsAsAddedPartOrImplant)
            {
                continue;
            }

            // Another removal may already have taken this one with it.
            if (!pawn.health.hediffSet.hediffs.Contains(hediff))
            {
                continue;
            }

            if (hediff is Hediff_Injury)
            {
                if (extension.healInjuries)
                {
                    pawn.health.RemoveHediff(hediff);
                }

                continue;
            }

            // everCurableByItem is the same flag the healer mech serum respects, and vanilla
            // has already used it to protect what should not be trivially cured - luciferium
            // addiction, metalhorror implants, mutant hediffs.
            if (extension.cureBadHediffs && hediff.def.isBad && hediff.def.everCurableByItem)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }

    private static void FillHemogen(Pawn pawn, DefModExtension_BloodChalice extension)
    {
        if (!extension.fillHemogen)
        {
            return;
        }

        var hemogen = pawn.genes?.GetFirstGeneOfType<Gene_Hemogen>();

        if (hemogen == null)
        {
            return;
        }

        // Routes through Gene_HemogenDrain when the pawn has one, and clamps at Max either way.
        GeneUtility.OffsetHemogen(pawn, hemogen.Max, false);
    }

    private static void FillDeathrest(Pawn pawn, DefModExtension_BloodChalice extension)
    {
        if (!extension.fillDeathrestNeed)
        {
            return;
        }

        var need = pawn.needs?.TryGetNeed<Need_Deathrest>();

        if (need != null)
        {
            need.CurLevel = need.MaxLevel;
        }
    }

    private static void GiveThought(Pawn pawn, DefModExtension_BloodChalice extension)
    {
        if (extension.moodThought == null)
        {
            return;
        }

        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(extension.moodThought);
    }
}
