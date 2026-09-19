using HarmonyLib;
using Verse;

namespace Relics40k;

public class Relics40kMod : Mod
{
    public Relics40kMod(ModContentPack content) : base(content)
    {
        new Harmony("Phonicmas.Relics40k").PatchAll();
    }
}
