using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.LuaMacro;
using System.Collections.Generic;
using System.Reflection;

namespace SomethingNeedDoing.Documentation;
/// <summary>
/// Provides documentation for Lua modules and functions.
/// </summary>
public class LuaDocumentation
{
    private readonly Dictionary<string, ModuleDocumentation> _modules = new();

    public LuaDocumentation()
    {
        // Initialize with built-in modules and their documentation
        InitializeBuiltInModules();
    }

    private void InitializeBuiltInModules()
    {
        // Add core module documentation
        var coreModule = new ModuleDocumentation(
            "Core",
            "Core functionality for all Lua scripts",
            new List<FunctionDocumentation>
            {
                new(
                    "yield",
                    "Passes a command back to the macro engine for execution",
                    new List<ParameterDocumentation>
                    {
                        new("command", "The command to execute (e.g., a chat command)")
                    },
                    "yield(\"/ac Muscle Memory <wait.3>\")\nyield(\"/echo Hello world!\")"
                ),
                new(
                    "sleep",
                    "Pauses the script for the specified number of seconds",
                    new List<ParameterDocumentation>
                    {
                        new("seconds", "Time to wait in seconds")
                    },
                    "sleep(3.5) -- Wait for 3.5 seconds"
                ),
                new(
                    "echo",
                    "Prints a message to the chat log",
                    new List<ParameterDocumentation>
                    {
                        new("message", "The message to display")
                    },
                    "echo(\"Craft complete!\")"
                )
            }
        );
        _modules["Core"] = coreModule;

        // Add game module documentation
        var gameModule = new ModuleDocumentation(
            "Game",
            "Functions for interacting with the game state",
            new List<FunctionDocumentation>
            {
                new(
                    "isInCombat",
                    "Checks if the player is currently in combat",
                    new List<ParameterDocumentation>(),
                    "if Game.isInCombat() then\n    echo(\"Can't do this in combat!\")\n    return\nend"
                ),
                new(
                    "isCrafting",
                    "Checks if the player is currently crafting",
                    new List<ParameterDocumentation>(),
                    "while Game.isCrafting() do\n    yield(\"/ac Observe <wait.3>\")\nend"
                ),
                new(
                    "getTarget",
                    "Gets information about the current target",
                    new List<ParameterDocumentation>(),
                    "local target = Game.getTarget()\nif target then\n    echo(\"Current target: \" .. target.name)\nend"
                )
            }
        );
        _modules["Game"] = gameModule;

        // Add UI module documentation
        var uiModule = new ModuleDocumentation(
            "UI",
            "Functions for interacting with the game's user interface",
            new List<FunctionDocumentation>
            {
                new(
                    "click",
                    "Clicks on a UI element",
                    new List<ParameterDocumentation>
                    {
                        new("addonName", "Name of the addon/window"),
                        new("element", "Name or index of the element to click")
                    },
                    "UI.click(\"SelectYesno\", \"Yes\") -- Click Yes on a confirmation dialog"
                ),
                new(
                    "waitForAddon",
                    "Waits for a specific addon/window to appear",
                    new List<ParameterDocumentation>
                    {
                        new("addonName", "Name of the addon/window to wait for"),
                        new("timeout", "Maximum time to wait in seconds (optional)")
                    },
                    "UI.waitForAddon(\"SelectYesno\", 5) -- Wait up to 5 seconds for confirmation dialog"
                ),
                new(
                    "sendKey",
                    "Sends a keyboard input to the game",
                    new List<ParameterDocumentation>
                    {
                        new("key", "Key to send (e.g., 'ESCAPE', 'RETURN')")
                    },
                    "UI.sendKey(\"ESCAPE\") -- Press the Escape key"
                )
            }
        );
        _modules["UI"] = uiModule;
    }

    public void RegisterModule(ILuaModule module)
    {
        var functions = new List<FunctionDocumentation>();

        foreach (var method in module.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.GetCustomAttribute<LuaFunctionAttribute>() is not { } attr) continue;

            var methodParams = method.GetParameters();
            var parameters = new List<ParameterDocumentation>();

            for (var i = 0; i < methodParams.Length; i++)
            {
                var param = methodParams[i];
                var paramDesc = attr.ParameterDescriptions?.Length > i ? attr.ParameterDescriptions[i] : null;
                parameters.Add(new ParameterDocumentation(
                    param.Name ?? $"param{i}",
                    paramDesc ?? $"Parameter {i+1} of {attr.Name ?? method.Name}"
                ));
            }

            var description = attr.Description ?? method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description ?? $"Function {attr.Name ?? method.Name}";

            var function = new FunctionDocumentation(
                attr.Name ?? method.Name,
                description,
                parameters,
                attr.Examples != null ? string.Join("\n", attr.Examples) : null
            );

            functions.Add(function);

            // If the return type is a wrapper class, register its properties and methods
            if (method.ReturnType.IsClass && method.ReturnType.IsNested)
            {
                // Register properties
                var wrapperProperties = method.ReturnType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in wrapperProperties)
                {
                    if (prop.GetCustomAttribute<LuaDocsAttribute>() is not { } wrapperAttr) continue;

                    var propFunction = new FunctionDocumentation(
                        $"{attr.Name ?? method.Name}.{wrapperAttr.Name ?? prop.Name}",
                        wrapperAttr.Description ?? $"Property of {method.ReturnType.Name}",
                        new List<ParameterDocumentation>(),
                        wrapperAttr.Examples != null ? string.Join("\n", wrapperAttr.Examples) : null
                    );
                    functions.Add(propFunction);
                }

                // Register methods
                var wrapperMethods = method.ReturnType.GetMethods(BindingFlags.Public | BindingFlags.Instance);
                foreach (var wrapperMethod in wrapperMethods)
                {
                    if (wrapperMethod.GetCustomAttribute<LuaDocsAttribute>() is not { } wrapperAttr) continue;

                    var wrapperMethodParams = wrapperMethod.GetParameters();
                    var wrapperParameters = new List<ParameterDocumentation>();

                    for (var i = 0; i < wrapperMethodParams.Length; i++)
                    {
                        var param = wrapperMethodParams[i];
                        var paramDesc = wrapperAttr.ParameterDescriptions?.Length > i ? wrapperAttr.ParameterDescriptions[i] : null;

                        wrapperParameters.Add(new ParameterDocumentation(
                            param.Name ?? $"param{i}",
                            paramDesc ?? $"Parameter {i+1} of {wrapperAttr.Name ?? wrapperMethod.Name}"
                        ));
                    }

                    var wrapperFunction = new FunctionDocumentation(
                        $"{attr.Name ?? method.Name}.{wrapperAttr.Name ?? wrapperMethod.Name}",
                        wrapperAttr.Description ?? $"Method of {method.ReturnType.Name}",
                        wrapperParameters,
                        wrapperAttr.Examples != null ? string.Join("\n", wrapperAttr.Examples) : null
                    );
                    functions.Add(wrapperFunction);
                }
            }
        }

        _modules[module.ModuleName] = new ModuleDocumentation(
            module.ModuleName,
            $"Module containing {module.ModuleName} functionality",
            functions
        );
    }

    /// <summary>
    /// Gets all documented modules.
    /// </summary>
    public List<ModuleDocumentation> GetModules()
    {
        return _modules.Values.ToList();
    }

    /// <summary>
    /// Gets documentation for a specific module.
    /// </summary>
    public ModuleDocumentation GetModule(string moduleName)
    {
        return _modules.TryGetValue(moduleName, out var module)
            ? module
            : new ModuleDocumentation(moduleName, "No documentation available", []);
    }
}

/// <summary>
/// Documentation for a Lua module.
/// </summary>
public class ModuleDocumentation
{
    /// <summary>
    /// Name of the module.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Description of the module.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Functions contained in the module.
    /// </summary>
    public List<FunctionDocumentation> Functions { get; }

    public ModuleDocumentation(string name, string description, List<FunctionDocumentation> functions)
    {
        Name = name;
        Description = description;
        Functions = functions;
    }
}

/// <summary>
/// Documentation for a Lua function.
/// </summary>
public class FunctionDocumentation
{
    /// <summary>
    /// Name of the function.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Description of the function.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Parameters accepted by the function.
    /// </summary>
    public List<ParameterDocumentation> Parameters { get; }

    /// <summary>
    /// Example usage of the function.
    /// </summary>
    public string Example { get; }

    public FunctionDocumentation(string name, string description, List<ParameterDocumentation> parameters, string? example = null)
    {
        Name = name;
        Description = description;
        Parameters = parameters;
        Example = example ?? string.Empty;
    }
}

/// <summary>
/// Documentation for a function parameter.
/// </summary>
public class ParameterDocumentation
{
    /// <summary>
    /// Name of the parameter.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Description of the parameter.
    /// </summary>
    public string Description { get; }

    public ParameterDocumentation(string name, string description)
    {
        Name = name;
        Description = description;
    }
}
