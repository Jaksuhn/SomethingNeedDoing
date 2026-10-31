using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;
using System.Threading.Tasks;

namespace SomethingNeedDoing.External;

public class Navmesh : IPC
{
    public override string Name => "vnavmesh";
    public override string Repo => Repos.Veyn;

    [EzIPC("Nav.%m")]
    [LuaFunction(description: "Checks if the navmesh is ready")]
    public readonly Func<bool> IsReady = null!;

    [EzIPC("Nav.%m")]
    [LuaFunction(description: "Gets the build progress")]
    public readonly Func<float> BuildProgress = null!;

    [EzIPC("Nav.%m")]
    [LuaFunction(description: "Reloads the navmesh")]
    public readonly Func<bool> Reload = null!;

    [EzIPC("Nav.%m")]
    [LuaFunction(description: "Rebuilds the navmesh")]
    public readonly Func<bool> Rebuild = null!;

    /// <summary> Vector3 from, Vector3 to, bool fly </summary>
    [EzIPC("Nav.%m")]
    [LuaFunction(
        description: "Finds a path between two points",
        parameterDescriptions: ["from", "to", "fly"])]
    public readonly Func<Vector3, Vector3, bool, Task<List<Vector3>>> Pathfind = null!;

    /// <summary> Vector3 dest, bool fly </summary>
    [EzIPC("SimpleMove.%m")]
    [LuaFunction(
        description: "Pathfinds and moves to a destination",
        parameterDescriptions: ["dest", "fly"])]
    public readonly Func<Vector3, bool, bool> PathfindAndMoveTo = null!;

    [EzIPC("SimpleMove.%m")]
    [LuaFunction(description: "Checks if pathfinding is in progress")]
    public readonly Func<bool> PathfindInProgress = null!;

    [EzIPC("Path.%m")]
    [LuaFunction(
        description: "Move through a predetermined set of waypoints",
        parameterDescriptions: ["waypoints", "fly"])]
    [Changelog("12.67")]
    public readonly Action<List<Vector3>, bool> MoveTo = null!;

    [EzIPC("Path.%m")]
    [LuaFunction(description: "Stops the current path")]
    public readonly Action Stop = null!;

    [EzIPC("Path.%m")]
    [LuaFunction(description: "Checks if a path is running")]
    public readonly Func<bool> IsRunning = null!;

    /// <summary> Vector3 p, float halfExtentXZ, float halfExtentY </summary>
    [EzIPC("Query.Mesh.%m")]
    [LuaFunction(
        description: "Finds the nearest point on the mesh",
        parameterDescriptions: ["p", "halfExtentXZ", "halfExtentY"])]
    public readonly Func<Vector3, float, float, Vector3?> NearestPoint = null!;

    /// <summary> Vector3 p, bool allowUnlandable, float halfExtentXZ (default 5) </summary>
    [EzIPC("Query.Mesh.%m")]
    [LuaFunction(
        description: "Finds a point on the floor",
        parameterDescriptions: ["p", "allowUnlandable", "halfExtentXZ"])]
    public readonly Func<Vector3, bool, float, Vector3?> PointOnFloor = null!;
}
