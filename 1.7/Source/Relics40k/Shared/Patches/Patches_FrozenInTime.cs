using HarmonyLib;
using RimWorld;
using Verse;

namespace Relics40k;

[HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
internal static class Patch_Pawn_PreApplyDamage_FrozenInTime
{
    private static bool Prefix(Pawn __instance, ref bool absorbed)
    {
        if (!FrozenInTimeUtility.IsFrozen(__instance))
        {
            return true;
        }

        absorbed = true;
        FrozenInTimeUtility.ThrowAbsorbMote(__instance);
        return false;
    }
}

[HarmonyPatch(typeof(FireUtility), nameof(FireUtility.CanEverAttachFire))]
internal static class Patch_FireUtility_CanEverAttachFire_FrozenInTime
{
    private static void Postfix(Thing t, ref bool __result)
    {
        if (__result && t is Pawn pawn && FrozenInTimeUtility.IsFrozen(pawn))
        {
            __result = false;
        }
    }
}
