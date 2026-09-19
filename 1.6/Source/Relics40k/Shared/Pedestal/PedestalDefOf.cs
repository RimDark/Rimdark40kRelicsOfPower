using RimWorld;
using Verse;

namespace Relics40k;

[DefOf]
public static class PedestalDefOf
{
    public static JobDef BEWH_TakeFromPedestal;

    static PedestalDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(PedestalDefOf));
    }
}
