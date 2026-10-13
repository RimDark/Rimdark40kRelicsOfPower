using Verse;

namespace Relics40k;

/// <summary>
/// What a pedestal does when its item leaves it. Set by whoever stocks the pedestal and scribed with
/// it, so a quest can decide the rules at map generation without the pedestal def knowing the quest.
/// </summary>
public abstract class PedestalAction : IExposable
{
    public virtual AcceptanceReport CanTake(CompPedestal pedestal, Pawn pawn)
    {
        return true;
    }

    /// <summary>Right-click label; null falls back to "Take {item}".</summary>
    public virtual string TakeLabel(CompPedestal pedestal)
    {
        return null;
    }

    /// <summary>Fires once for every removal. taker is null when the item simply fell off, e.g. the pedestal was destroyed.</summary>
    public abstract void Notify_Taken(CompPedestal pedestal, Thing item, Pawn taker, Map map);

    public virtual void ExposeData()
    {
    }
}
