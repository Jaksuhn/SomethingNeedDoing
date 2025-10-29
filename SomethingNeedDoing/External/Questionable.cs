using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;
#nullable disable
public class Questionable : IPC
{
    public override string Name => "Questionable";
    public override string Repo => Repos.Punish;

    [EzIPC]
    [LuaFunction(description: "Checks if the plugin is running")]
    public Func<bool> IsRunning;

    [EzIPC]
    [LuaFunction(description: "Gets the current quest ID")]
    public Func<string> GetCurrentQuestId;

    [EzIPC]
    [LuaFunction(description: "Gets the current step data")]
    public Func<StepData> GetCurrentStepData;

    [EzIPC]
    [LuaFunction(
        description: "Checks if a quest is locked",
        parameterDescriptions: ["questId"])]
    public Func<string, bool> IsQuestLocked;

    [Changelog("13.54")]
    [EzIPC]
    [LuaFunction(
        description: "Checks if a quest has been completed",
        parameterDescriptions: ["questId"])]
    public Func<string, bool> IsQuestComplete;

    [Changelog("13.54")]
    [EzIPC]
    [LuaFunction(
        description: "Checks if a character can accept a quest",
        parameterDescriptions: ["questId"])]
    public Func<string, bool> IsReadyToAcceptQuest;

    [Changelog("13.54")]
    [EzIPC]
    [LuaFunction(
        description: "Checks if a quest is currently accepted",
        parameterDescriptions: ["questId"])]
    public Func<string, bool> IsQuestAccepted;

    [Changelog("13.54")]
    [EzIPC]
    [LuaFunction(
        description: "Checks if a quest is not obtainable on this character",
        parameterDescriptions: ["questId"])]
    public Func<string, bool> IsQuestUnobtainable;

    [EzIPC]
    [LuaFunction(
        description: "Imports quest priority",
        parameterDescriptions: ["data"])]
    public Func<string, bool> ImportQuestPriority;

    [EzIPC]
    [LuaFunction(description: "Clears quest priority")]
    public Func<bool> ClearQuestPriority;

    [EzIPC]
    [LuaFunction(
        description: "Adds a quest to priority",
        parameterDescriptions: ["questId"])]
    public Func<string, bool> AddQuestPriority;

    [EzIPC]
    [LuaFunction(
        description: "Inserts a quest into priority at a specific position",
        parameterDescriptions: ["position", "questId"])]
    public Func<int, string, bool> InsertQuestPriority;

    [EzIPC]
    [LuaFunction(description: "Exports quest priority")]
    public Func<string> ExportQuestPriority;

    public sealed class StepData
    {
        public required string QuestId { get; init; }
        public required byte Sequence { get; init; }
        public required int Step { get; init; }
        public required string InteractionType { get; init; }
        public required Vector3? Position { get; init; }
        public required ushort TerritoryId { get; init; }
    }
}
