using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;

public class RotationSolverReborn : IPC
{
    public override string Name => "RotationSolverReborn";
    public override string Repo => Repos.CombatReborn;

    [EzIPC]
    [LuaFunction(
        description: "Tests the IPC connection with a parameter.",
        parameterDescriptions: ["param"])]
    public readonly Action<string> Test = null!;

    [EzIPC]
    [LuaFunction(
        description: "Adds a name ID to the priority list.",
        parameterDescriptions: ["nameId"])]
    public readonly Action<uint> AddPriorityNameID = null!;

    [EzIPC]
    [LuaFunction(
        description: "Removes a name ID from the priority list.",
        parameterDescriptions: ["nameId"])]
    public readonly Action<uint> RemovePriorityNameID = null!;

    [EzIPC]
    [LuaFunction(
        description: "Adds a name ID to the blacklist.",
        parameterDescriptions: ["nameId"])]
    public readonly Action<uint> AddBlacklistNameID = null!;

    [EzIPC]
    [LuaFunction(
        description: "Removes a name ID from the blacklist.",
        parameterDescriptions: ["nameId"])]
    public readonly Action<uint> RemoveBlacklistNameID = null!;

    [EzIPC]
    [LuaFunction(
        description: "Changes the operating mode of RotationSolverReborn.",
        parameterDescriptions: ["stateCommand"])]
    public readonly Action<StateCommandType> ChangeOperatingMode = null!;

    [EzIPC]
    [LuaFunction(
        description: "Triggers a special state in RotationSolverReborn.",
        parameterDescriptions: ["specialCommand"])]
    public readonly Action<SpecialCommandType> TriggerSpecialState = null!;

    [EzIPC]
    [LuaFunction(
        description: "Changes a plugin setting or triggers an auxiliary command.",
        parameterDescriptions: ["otherType", "str"])]
    public readonly Action<OtherCommandType, string> OtherCommand = null!;

    [EzIPC]
    [LuaFunction(
        description: "Executes a specific action command with a timing parameter.",
        parameterDescriptions: ["action", "time"])]
    public readonly Action<string, float> ActionCommand = null!;

    public enum StateCommandType
    {
        Off,
        Auto,
        Manual,
    }

    public enum SpecialCommandType
    {
        EndSpecial,
        HealArea,
        HealSingle,
        DefenseArea,
        DefenseSingle,
        MoveForward,
        MoveBack,
        Speed,
        DispelStancePositional,
        RaiseShirk,
        AntiKnockback,
        Burst,
        NoCasting,
    }

    public enum OtherCommandType
    {
        Settings,
        Rotations,
        DutyRotations,
        DoActions,
        ToggleActions,
        NextAction,
    }
}
