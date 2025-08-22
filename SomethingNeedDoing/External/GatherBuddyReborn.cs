using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;
public class GatherBuddyReborn : IPC
{
    public override string Name => "GatherBuddyReborn";
    public override string Repo => Repos.GatherBuddyReborn;

    [EzIPC]
    [LuaFunction(description: "Gets the plugin's current version")]
    public Func<int> Version = null!;

    [EzIPC]
    [LuaFunction(
        description: "Identifies the item id by item name",
        parameterDescriptions: ["itemName"])]
    public Func<string, uint> Identify = null!;

    [EzIPC]
    [LuaFunction(description: "Checks if the plugin is enabled")]
    public Func<bool> IsAutoGatherEnabled = null!;

    [EzIPC]
    [LuaFunction(description: "Checks if the plugin is waiting")]
    public Func<bool> IsAutoGatherWaiting = null!;

    [EzIPC]
    [LuaFunction(description: "Prints out the status text")]
    public Func<string> GetAutoGatherStatusText = null!;

    [EzIPC]
    [LuaFunction(
        description: "Enables/Disables the plugin",
        parameterDescriptions: ["enabled"])]
    public Action<bool> SetAutoGatherEnabled = null!;

    [EzIPCEvent]
    [LuaFunction(description: "Event triggered when the plugin is waiting")]
    public Action AutoGatherWaiting = null!;

    [EzIPCEvent]
    [LuaFunction(
        description: "Event triggered when the plugin's enabled state changes",
        parameterDescriptions: ["enabled"])]
    public Action<bool> AutoGatherEnabledChanged = null!;
}
