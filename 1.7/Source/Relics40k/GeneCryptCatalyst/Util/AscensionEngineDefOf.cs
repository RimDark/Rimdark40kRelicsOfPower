using Verse;

namespace Relics40k;

/// <summary>
/// Resolved by name rather than through DefOf, because the engine's def only exists while
/// Mankind's Finest is loaded.
/// </summary>
public static class AscensionEngineDefOf
{
    private const string EngineDefName = "BEWH_AscensionEngine";

    private static bool resolved;

    private static ThingDef engine;

    public static ThingDef Engine
    {
        get
        {
            if (!resolved)
            {
                resolved = true;
                engine = DefDatabase<ThingDef>.GetNamedSilentFail(EngineDefName);
            }

            return engine;
        }
    }
}
