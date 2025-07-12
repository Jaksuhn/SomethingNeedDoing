using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;
public class Wrath : IPC
{
    public override string Name => "WrathCombo";
    public override string Repo => Repos.Punish;
    private static string InternalName => Svc.PluginInterface.InternalName;

    [EzIPC("RegisterForLease")]
    private readonly Func<string, string, Guid?> _registerForLease = null!;

    [LuaFunction(
        description: "Registers for lease",
        parameterDescriptions: ["scriptName"])]
    public Guid? RegisterForLease(string scriptName) => _registerForLease(InternalName, scriptName);

    [EzIPC]
    [LuaFunction(
        description: "Gets the auto rotation state")]
    public readonly Func<bool> GetAutoRotationState = null!;

    [EzIPC]
    [LuaFunction(
        description: "Sets the auto rotation state",
        parameterDescriptions: ["leaseId", "enabled"])]
    public readonly Func<Guid, bool, SetResult> SetAutoRotationState = null!;

    [EzIPC]
    [LuaFunction(
        description: "Checks if the current job auto rotation is ready")]
    public readonly Func<bool> IsCurrentJobAutoRotationReady = null!;

    [EzIPC]
    [LuaFunction(
        description: "Sets the current job auto rotation ready",
        parameterDescriptions: ["leaseId"])]
    public readonly Func<Guid, SetResult> SetCurrentJobAutoRotationReady = null!;

    [EzIPC]
    [LuaFunction(
        description: "Releases control",
        parameterDescriptions: ["leaseId"])]
    public readonly Action<Guid> ReleaseControl = null!;

    [EzIPC]
    [LuaFunction(
        description: $"Gets the auto rotation config state for the given {nameof(AutoRotationConfigOption)}",
        parameterDescriptions: ["configOption"])]
    public readonly Func<AutoRotationConfigOption, object?> GetAutoRotationConfigState = null!;

    [EzIPC]
    [LuaFunction(
        description: $"Sets the auto rotation config state for the given {nameof(AutoRotationConfigOption)} to the given value (must be of the expected type)",
        parameterDescriptions: ["leaseId", "configOption", "configValue"])]
    public readonly Func<Guid, AutoRotationConfigOption, object, SetResult> SetAutoRotationConfigState = null!;

    public enum AutoRotationConfigOption
    {
        InCombatOnly = 0, //bool
        DPSRotationMode = 1, //DPSRotationMode Enum (or int of enum value)
        HealerRotationMode = 2, //HealerRotationMode Enum (or int of enum value)
        FATEPriority = 3, //bool
        QuestPriority = 4, //bool
        SingleTargetHPP = 5, //int
        AoETargetHPP = 6, //int
        SingleTargetRegenHPP = 7, //int
        ManageKardia = 8, //bool
        AutoRez = 9, //bool
        AutoRezDPSJobs = 10, //bool
        AutoCleanse = 11, //bool
        IncludeNPCs = 12, //bool
    }

    public enum SetResult
    {
        IGNORED = -1,
        Okay = 0,
        OkayWorking = 1,
        IPCDisabled = 10,
        InvalidLease = 11,
        BlacklistedLease = 12,
        Duplicate = 13,
        PlayerNotAvailable = 14,
        InvalidConfiguration = 15,
        InvalidValue = 16,
    }

    public enum DPSRotationMode
    {
        Manual = 0,
        Highest_Max = 1,
        Lowest_Max = 2,
        Highest_Current = 3,
        Lowest_Current = 4,
        Tank_Target = 5,
        Nearest = 6,
        Furthest = 7,
    }

    public enum HealerRotationMode
    {
        Manual = 0,
        Highest_Current = 1,
        Lowest_Current = 2,
    }
}
