using NLua;

namespace SomethingNeedDoing.Framework.Interfaces;
/// <summary>
/// Base interface for all Lua API modules.
/// </summary>
public interface ILuaModule
{
    /// <summary>
    /// Gets the name of the module as it will appear in Lua.
    /// </summary>
    string ModuleName { get; }

    /// <summary>
    /// Registers this module's functions with the Lua environment.
    /// </summary>
    void Register(NLua.Lua lua);
}
