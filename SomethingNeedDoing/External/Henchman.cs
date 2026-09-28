using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;

public class Henchman : IPC
{
    public override string Name => "Henchman";
    public override string Repo => Repos.Knightmore;

    /*
     * General
     */

    [EzIPC]
    [LuaFunction(description: "Check if a Henchman task is running")]
    public readonly Func<bool> IsBusy = null!;

    [EzIPC]
    [LuaFunction(description: "Cancel the currently running Henchman task")]
    public readonly Action CancelAllTasks = null!;

    /*
     * Features
     */

    [EzIPC]
    [LuaFunction(description: "Start the On A Boat task")]
    public readonly Action StartOnABoat = null!;

    [EzIPC]
    [LuaFunction(description: "Start the On Your Mark task")]
    public readonly Action StartOnYourMark = null!;

    /*
     * Tweaks
     */

    [EzIPC]
    [LuaFunction(
        description: "Toggle rendering",
        parameterDescriptions: ["enabled"])]
    public readonly Action<bool> SetRender = null!;
    [EzIPC]
    [LuaFunction(
        description: "Force rendering",
        parameterDescriptions: ["enabled"])]
    public readonly Action<bool> ForceRender = null!;
}
