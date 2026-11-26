using FFXIVClientStructs.FFXIV.Client.Game.UI;
using SomethingNeedDoing.Core.Interfaces;
using static FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement;

namespace SomethingNeedDoing.LuaMacro.Wrappers;

/// <summary>
/// Your achievements. The game only has them after asking the server: the completed list (as when the Achievements window is first opened) and the progress of one achievement at a time (as the window does for a progress bar). Request, then wait until IsLoaded (or Progress.IsLoaded) is true.
/// </summary>
public unsafe class AchievementsWrapper : IWrapper
{
    private static Achievement* A => Instance();
    private static DateTime _progressRequestedAt = DateTime.MinValue;

    [LuaDocs(description: "Whether the completed-achievements list has arrived (after RequestCompleted, or once the Achievements window has been opened).")][Changelog("15.16")] public bool IsLoaded => A->IsLoaded();
    [LuaDocs(description: "Ask the server for the completed-achievements list. Does nothing if it's already loaded or on its way.")][Changelog("15.16")] public void RequestCompleted() { if (A->State == AchievementState.Invalid) A->RequestCompletedAchievements(); }
    [LuaDocs(description: "Whether an achievement (Achievement sheet row id) is complete. False until IsLoaded is true.")][Changelog("15.16")] public bool IsComplete(int id) => A->IsLoaded() && A->IsComplete(id);

    [LuaDocs(description: "Ask the server for one achievement's progress (Achievement sheet row id). The answer replaces Progress; check Progress.Id and Progress.IsLoaded. Returns false, sending nothing, while an earlier request is still waiting (for up to 5 seconds).")][Changelog("15.16")]
    public bool RequestProgress(uint id)
    {
        if (A->ProgressRequestState == AchievementState.Requested && DateTime.Now - _progressRequestedAt < TimeSpan.FromSeconds(5))
            return false;
        _progressRequestedAt = DateTime.Now;
        A->RequestAchievementProgress(id);
        return true;
    }

    [LuaDocs(description: "The latest progress answer (see RequestProgress).")][Changelog("15.16")] public AchievementProgressWrapper Progress => new();
}

public unsafe class AchievementProgressWrapper : IWrapper
{
    private static Achievement* A => Instance();

    [LuaDocs(description: "The achievement this answer is for (0 before any request).")][Changelog("15.16")] public uint Id => A->ProgressAchievementId;
    [LuaDocs][Changelog("15.16")] public uint Current => A->ProgressCurrent;
    [LuaDocs][Changelog("15.16")] public uint Max => A->ProgressMax;
    [LuaDocs(description: "Whether the answer has arrived (false while a request is waiting for it).")][Changelog("15.16")] public bool IsLoaded => A->ProgressRequestState == AchievementState.Loaded;
}
