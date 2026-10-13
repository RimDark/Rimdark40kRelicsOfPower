using System.Collections.Generic;
using Verse;

namespace Relics40k;

/// <summary>
/// Remembers which once-per-colony quests have already been offered in this game.
/// </summary>
public class GameComponent_Relics40kQuests : GameComponent
{
    private HashSet<string> firedQuestTags = new HashSet<string>();

    public GameComponent_Relics40kQuests(Game game)
    {
    }

    public static bool HasFired(string tag)
    {
        var component = Current.Game?.GetComponent<GameComponent_Relics40kQuests>();

        return component != null && component.firedQuestTags.Contains(tag);
    }

    public static void MarkFired(string tag)
    {
        Current.Game?.GetComponent<GameComponent_Relics40kQuests>()?.firedQuestTags.Add(tag);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref firedQuestTags, "firedQuestTags", LookMode.Value);

        if (Scribe.mode == LoadSaveMode.PostLoadInit && firedQuestTags == null)
        {
            firedQuestTags = new HashSet<string>();
        }
    }
}
