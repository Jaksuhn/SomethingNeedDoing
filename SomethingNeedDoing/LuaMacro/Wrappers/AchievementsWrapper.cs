using FFXIVClientStructs.FFXIV.Client.Game.UI;
using SomethingNeedDoing.Core.Interfaces;
using static FFXIVClientStructs.FFXIV.Client.Game.UI.Achievement;

namespace SomethingNeedDoing.LuaMacro.Wrappers;

public unsafe class AchievementsWrapper : IWrapper
{
    private static Achievement* A => Instance();
    private static DateTime _progressRequestedAt = DateTime.MinValue;

    [LuaDocs(description: "Whether the completed-achievements list has arrived.")]
    [Changelog("15.16")]
    public bool IsLoaded => A->IsLoaded();

    [LuaDocs(description: "Ask the server for the completed-achievements list.")]
    [Changelog("15.16")]
    public void RequestCompleted()
    {
        if (A->State == AchievementState.Invalid)
            A->RequestCompletedAchievements();
    }

    [LuaDocs(description: "Whether an achievement (Achievement row id) is complete. False until IsLoaded is true.")]
    [Changelog("15.16")]
    public bool IsComplete(int id) => A->IsLoaded() && A->IsComplete(id);

    [LuaDocs(description: "Requests progress for an achievement with a five second cooldown on requests.")]
    [Changelog("15.16")]
    public bool RequestProgress(uint id)
    {
        if (A->ProgressRequestState == AchievementState.Requested && DateTime.Now - _progressRequestedAt < TimeSpan.FromSeconds(5))
            return false;
        _progressRequestedAt = DateTime.Now;
        A->RequestAchievementProgress(id);
        return true;
    }

    [LuaDocs(description: "Last requested achievement progress")]
    [Changelog("15.16")]
    public AchievementProgressWrapper Progress => new();
}

public unsafe class AchievementProgressWrapper : IWrapper
{
    private static Achievement* A => Instance();

    [LuaDocs(description: "Achievement row id")]
    [Changelog("15.16")]
    public uint Id => A->ProgressAchievementId;

    [LuaDocs][Changelog("15.16")] public uint Current => A->ProgressCurrent;
    [LuaDocs][Changelog("15.16")] public uint Max => A->ProgressMax;
    [LuaDocs][Changelog("15.16")] public bool IsLoaded => A->ProgressRequestState == AchievementState.Loaded;
}
