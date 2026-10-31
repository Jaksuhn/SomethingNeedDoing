using NLua;
using SomethingNeedDoing.Documentation;
using SomethingNeedDoing.LuaMacro.Modules.Engines;
using System.Threading.Tasks;

namespace SomethingNeedDoing.LuaMacro.Modules;

/// <summary>
/// Lua module that provides access to various execution engines.
/// </summary>
public class EnginesModule : LuaModuleBase
{
    private readonly Dictionary<string, IEngine> _engines = [];

    public override string ModuleName => "Engines";

    public EnginesModule(IEnumerable<IEngine> engines)
    {
        foreach (var engine in engines)
            _engines[engine.Name] = engine;
    }

    public override void Register(Lua lua)
    {
        base.Register(lua);
        RegisterEngineFunctions(lua);
    }

    /// <summary>
    /// Registers documentation for the Engines module at startup (without requiring actual engine instances).
    /// </summary>
    /// <param name="docs">The documentation system to register with.</param>
    public static void RegisterDocumentationStatic(LuaDocumentation docs)
    {
        var moduleDocs = new List<LuaFunctionDoc>
        {
            new(
            "Engines",
            "Run",
            "Executes content using the relevant engine",
            LuaTypeConverter.GetLuaType(typeof(void)),
            [("content", LuaTypeConverter.GetLuaType(typeof(string)), "The content to execute", null)],
            null,
            true
        )
        };

        var knownEngines = new[] { "Native", "NLua" };
        foreach (var engineName in knownEngines)
        {
            moduleDocs.Add(new LuaFunctionDoc(
                $"Engines.{engineName}",
                "Run",
                $"Executes content using the {engineName} engine",
                LuaTypeConverter.GetLuaType(typeof(void)),
                [("content", LuaTypeConverter.GetLuaType(typeof(string)), "The content to execute", null)],
                null,
                true
            ));
        }

        docs.RegisterModule("Engines", moduleDocs);
    }

    private void RegisterEngineFunctions(Lua lua)
    {
        foreach (var (name, engine) in _engines)
            lua[$"{ModuleName}.{name}"] = new { Run = new Action<string>(content => ExecuteEngine(engine, content)) };
        lua[$"{ModuleName}.Run"] = new Action<string>(ExecuteBestEngine);
    }

    /// <summary>
    /// Executes content using the specified engine synchronously (fire and forget).
    /// </summary>
    /// <param name="engine">The engine to use.</param>
    /// <param name="content">The content to execute.</param>
    private void ExecuteEngine(IEngine engine, string content)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await engine.ExecuteAsync(content);
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Error executing {engine.Name} content: {ex}");
            }
        });
    }

    /// <summary>
    /// Executes content using the best available engine.
    /// </summary>
    /// <param name="content">The content to execute.</param>
    private void ExecuteBestEngine(string content)
    {
        if (FindBestEngine(content) is { } engine)
            ExecuteEngine(engine, content);
        else
            FrameworkLogger.Warning($"No suitable engine found for content: {content}");
    }

    /// <summary>
    /// Finds the best engine for executing the given content.
    /// </summary>
    /// <param name="content">The content to find an engine for.</param>
    /// <returns>The best engine, or null if none found.</returns>
    private IEngine? FindBestEngine(string content) => _engines.Values.FirstOrDefault(engine => engine.CanExecute(content));
}
