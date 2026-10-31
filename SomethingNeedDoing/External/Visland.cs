using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;

public class Visland : IPC
{
    public override string Name => "visland";
    public override string Repo => Repos.Veyn;

    [EzIPC]
    [LuaFunction(description: "Checks if a route is currently running")]
    public Func<bool> IsRouteRunning = null!;

    [EzIPC]
    [LuaFunction(description: "Checks if a route is currently paused")]
    public Func<bool> IsRoutePaused = null!;

    [EzIPC]
    [LuaFunction(
        description: "Sets whether a route is paused",
        parameterDescriptions: ["paused"])]
    public Action<bool> SetRoutePaused = null!;

    [EzIPC]
    [LuaFunction(description: "Stops the current route")]
    public Action StopRoute = null!;

    [EzIPC]
    [LuaFunction(
        description: "Starts a route",
        parameterDescriptions: ["routeName", "loop"])]
    public Action<string, bool> StartRoute = null!;
}
