using NLua;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.LuaMacro;
using System.Reflection;

namespace SomethingNeedDoing.Utils;

public static class LuaExtensions
{
    public static object[] LoadEntryPointWrappedScript(this Lua lua, string script) => lua.DoString(string.Format(LuaCodeSnippets.EntrypointTemplate, script));
    public static void LoadFStrings(this Lua lua) => lua.DoString(LuaCodeSnippets.FStringSnippet);

    /// <summary>
    /// Loads the enhanced error handler for better Lua error reporting.
    /// </summary>
    /// <param name="lua">The Lua state.</param>
    public static void LoadErrorHandler(this Lua lua) => lua.DoString(LuaCodeSnippets.ErrorHandlerSnippet);

    /// <summary>
    /// Registers a class as a dynamically accessible object. Must be called after <see cref="Lua.LoadCLRPackage"/>
    /// </summary>
    public static void RegisterClass<T>(this Lua lua) => lua.RegisterClass(typeof(T));

    /// <inheritdoc cref="RegisterClass{T}(Lua)"/>
    public static void RegisterClass(this Lua lua, Type type)
    {
        var assembly = type.Assembly.GetName().Name ?? throw new ArgumentException($"Assembly name for type {type.FullName} is null.");
        lua.LoadAssembly(assembly);
        lua.DoString(@$"{type.Name} = luanet.import_type('{type.FullName}')()");
    }

    /// <summary>
    /// Registers an enum as a dynamically accessible object. Must be called after <see cref="Lua.LoadCLRPackage"/>
    /// This funcion just forwards to RegisterClass and exists only for clarity.
    /// </summary>
    public static void RegisterEnum<T>(this Lua lua) where T : Enum => lua.RegisterClass<T>();

    // <summary>
    /// Loads a .NET assembly into the Lua state.
    /// </summary>
    public static void LoadAssembly(this Lua lua, string assembly) => lua.DoString($"luanet.load_assembly('{assembly}')");

    public static void LoadPackageSearcherSnippet(this Lua lua) => lua.DoString(LuaCodeSnippets.PackageSearchersSnippet);

    public static void LoadRequirePaths(this Lua lua)
    {
        foreach (var path in C.LuaRequirePaths)
        {
            if (!PathHelper.ValidatePath(path))
                continue;

            lua.DoString($"table.insert(snd.require.paths, '{path}')");
        }
    }

    public static void ApplyPrintOverride(this Lua lua)
        => lua.RegisterFunction("print", typeof(LuaExtensions).GetMethod(nameof(PrintFunction), BindingFlags.NonPublic | BindingFlags.Static));

    public static void RegisterInternalFunctions(this Lua lua)
        => lua.RegisterFunction("InternalGetMacroText", typeof(LuaExtensions).GetMethod(nameof(InternalGetMacroText), BindingFlags.NonPublic | BindingFlags.Static));

    private static void PrintFunction(params object[] args)
        => Svc.Log.Information($"{(args.Length == 0 ? string.Empty : string.Join("\t", args))}");

    private static string? InternalGetMacroText(string macroName)
        => C.GetMacroByName(macroName)?.ContentSansMetadata();

    public static void SetTriggerEventData(this Lua lua, TriggerEventArgs? args)
    {
        if (args is null) return;
        lua.NewTable("TriggerData");
        var table = lua.GetTable("TriggerData");

        table["eventType"] = args.EventType;
        table["timestamp"] = args.Timestamp;

        if (args.Data is Dictionary<string, object> data)
        {
            // If it's a dictionary, add each key-value pair to the table
            foreach (var kvp in data)
                table[kvp.Key] = kvp.Value;
        }
        else if (args.Data != null)
        {
            // If it's not a dictionary but not null, add it as a single value
            table["data"] = args.Data;
        }
    }
}
