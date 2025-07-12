using NLua;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Documentation;
using System.Reflection;
using System.Linq.Expressions;
using SomethingNeedDoing.External;

namespace SomethingNeedDoing.LuaMacro.Modules;

/// <summary>
/// Lua module that exposes IPC functions from various plugins to Lua macros.
/// </summary>
public class IPCModule : LuaModuleBase
{
    public override string ModuleName => "IPC";
    private readonly Dictionary<string, IPC> _ipcInstances = [];

    public IPCModule()
    {
        var ipcTypes = Assembly.GetExecutingAssembly().GetTypes().Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPC).IsAssignableFrom(t));
        foreach (var type in ipcTypes)
        {
            try
            {
                if (Activator.CreateInstance(type) is IPC ipc)
                    _ipcInstances[ipc.Name] = ipc;
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Failed to create instance of {type.Name}: {ex.Message}");
            }
        }
    }

    public override void Register(Lua lua)
    {
        lua.RegisterEnum<Wrath.AutoRotationConfigOption>();
        lua.DoString($"{ModuleName} = {{}}");

        RegisterHelperFunctions(lua);

        // Register each IPC class as a submodule
        foreach (var (name, instance) in _ipcInstances)
        {
            lua.DoString($"{ModuleName}.{name} = {{}}");

            // Register all fields marked with LuaFunction attribute
            var fields = instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<LuaFunctionAttribute>() != null);

            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<LuaFunctionAttribute>();
                var fieldName = attr?.Name ?? field.Name;

                if (field.GetValue(instance) is { } fieldValue)
                    // Register the field value as a Lua function
                    lua[$"{ModuleName}.{name}.{fieldName}"] = fieldValue;
            }

            // Register all methods marked with LuaFunction attribute
            var methods = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .Where(m => m.GetCustomAttribute<LuaFunctionAttribute>() != null);

            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<LuaFunctionAttribute>();
                var methodName = attr?.Name ?? method.Name;

                var delegateType = GetDelegateType(method);
                var methodDelegate = Delegate.CreateDelegate(delegateType, instance, method);
                lua[$"{ModuleName}.{name}.{methodName}"] = methodDelegate;
            }
        }
    }

    public void RegisterDocumentation(LuaDocumentation docs)
    {
        var moduleDocs = new List<LuaFunctionDoc>();

        // Register helper functions
        var isInstalledMethod = GetType().GetMethod("IsIPCInstalled", BindingFlags.NonPublic | BindingFlags.Instance);
        if (isInstalledMethod != null)
        {
            moduleDocs.Add(new LuaFunctionDoc(
                ModuleName,
                "IsInstalled",
                "Checks if an IPC plugin is installed",
                LuaTypeConverter.GetLuaType(isInstalledMethod.ReturnType),
                [(isInstalledMethod.GetParameters()[0].Name ?? "name", LuaTypeConverter.GetLuaType(isInstalledMethod.GetParameters()[0].ParameterType), "The name of the plugin to check")],
                null,
                true
            ));
        }

        var getAvailablePluginsMethod = GetType().GetMethod("GetAvailablePlugins", BindingFlags.NonPublic | BindingFlags.Instance);
        if (getAvailablePluginsMethod != null)
        {
            moduleDocs.Add(new LuaFunctionDoc(
                ModuleName,
                "GetAvailablePlugins",
                "Gets all available IPC plugins",
                LuaTypeConverter.GetLuaType(getAvailablePluginsMethod.ReturnType),
                [],
                null,
                true
            ));
        }

        // Register each IPC instance's functions
        foreach (var (name, instance) in _ipcInstances)
        {
            var fields = instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => f.GetCustomAttribute<LuaFunctionAttribute>() != null);

            foreach (var field in fields)
            {
                var attr = field.GetCustomAttribute<LuaFunctionAttribute>();
                if (attr == null) continue;

                var fieldName = attr.Name ?? field.Name;
                var fieldType = field.FieldType;
                var parameters = new List<(string Name, LuaTypeInfo Type, string? Description)>();
                var returnType = LuaTypeConverter.GetLuaType(typeof(void));

                if (fieldType.IsGenericType)
                {
                    var genericType = fieldType.GetGenericTypeDefinition();
                    var genericArgs = fieldType.GetGenericArguments();

                    if (genericType == typeof(Func<>))
                    {
                        // Func<T> - no parameters, one return type
                        returnType = LuaTypeConverter.GetLuaType(genericArgs[0]);
                    }
                    else if (genericType == typeof(Action))
                    {
                        // Action - no parameters, no return type
                        returnType = LuaTypeConverter.GetLuaType(typeof(void));
                    }
                    else if (genericType == typeof(Func<,>))
                    {
                        // Func<T, TResult> - one parameter, one return type
                        var paramDesc = attr.ParameterDescriptions?.FirstOrDefault();
                        parameters.Add((paramDesc ?? "param0", LuaTypeConverter.GetLuaType(genericArgs[0]), paramDesc));
                        returnType = LuaTypeConverter.GetLuaType(genericArgs[1]);
                    }
                    else if (genericType == typeof(Action<>))
                    {
                        // Action<T> - one parameter, no return type
                        var paramDesc = attr.ParameterDescriptions?.FirstOrDefault();
                        parameters.Add((paramDesc ?? "param0", LuaTypeConverter.GetLuaType(genericArgs[0]), paramDesc));
                        returnType = LuaTypeConverter.GetLuaType(typeof(void));
                    }
                    else if (genericType == typeof(Func<,,>))
                    {
                        // Func<T1, T2, TResult> - two parameters, one return type
                        var paramDesc0 = attr.ParameterDescriptions?.ElementAtOrDefault(0);
                        var paramDesc1 = attr.ParameterDescriptions?.ElementAtOrDefault(1);
                        parameters.Add((paramDesc0 ?? "param0", LuaTypeConverter.GetLuaType(genericArgs[0]), paramDesc0));
                        parameters.Add((paramDesc1 ?? "param1", LuaTypeConverter.GetLuaType(genericArgs[1]), paramDesc1));
                        returnType = LuaTypeConverter.GetLuaType(genericArgs[2]);
                    }
                    else if (genericType == typeof(Action<,>))
                    {
                        // Action<T1, T2> - two parameters, no return type
                        var paramDesc0 = attr.ParameterDescriptions?.ElementAtOrDefault(0);
                        var paramDesc1 = attr.ParameterDescriptions?.ElementAtOrDefault(1);
                        parameters.Add((paramDesc0 ?? "param0", LuaTypeConverter.GetLuaType(genericArgs[0]), paramDesc0));
                        parameters.Add((paramDesc1 ?? "param1", LuaTypeConverter.GetLuaType(genericArgs[1]), paramDesc1));
                        returnType = LuaTypeConverter.GetLuaType(typeof(void));
                    }
                    else if (genericType == typeof(Func<,,,>))
                    {
                        // Func<T1, T2, T3, TResult> - three parameters, one return type
                        var paramDesc0 = attr.ParameterDescriptions?.ElementAtOrDefault(0);
                        var paramDesc1 = attr.ParameterDescriptions?.ElementAtOrDefault(1);
                        var paramDesc2 = attr.ParameterDescriptions?.ElementAtOrDefault(2);
                        parameters.Add((paramDesc0 ?? "param0", LuaTypeConverter.GetLuaType(genericArgs[0]), paramDesc0));
                        parameters.Add((paramDesc1 ?? "param1", LuaTypeConverter.GetLuaType(genericArgs[1]), paramDesc1));
                        parameters.Add((paramDesc2 ?? "param2", LuaTypeConverter.GetLuaType(genericArgs[2]), paramDesc2));
                        returnType = LuaTypeConverter.GetLuaType(genericArgs[3]);
                    }
                    else if (genericType == typeof(Action<,,>))
                    {
                        // Action<T1, T2, T3> - three parameters, no return type
                        var paramDesc0 = attr.ParameterDescriptions?.ElementAtOrDefault(0);
                        var paramDesc1 = attr.ParameterDescriptions?.ElementAtOrDefault(1);
                        var paramDesc2 = attr.ParameterDescriptions?.ElementAtOrDefault(2);
                        parameters.Add((paramDesc0 ?? "param0", LuaTypeConverter.GetLuaType(genericArgs[0]), paramDesc0));
                        parameters.Add((paramDesc1 ?? "param1", LuaTypeConverter.GetLuaType(genericArgs[1]), paramDesc1));
                        parameters.Add((paramDesc2 ?? "param2", LuaTypeConverter.GetLuaType(genericArgs[2]), paramDesc2));
                        returnType = LuaTypeConverter.GetLuaType(typeof(void));
                    }
                }

                moduleDocs.Add(new LuaFunctionDoc(
                    $"{ModuleName}.{name}",
                    fieldName,
                    attr.Description,
                    returnType,
                    parameters,
                    attr.Examples,
                    true
                ));
            }

            // Register all methods marked with LuaFunction attribute
            var methods = instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance).Where(m => m.GetCustomAttribute<LuaFunctionAttribute>() != null);
            foreach (var method in methods)
            {
                var attr = method.GetCustomAttribute<LuaFunctionAttribute>();
                if (attr == null) continue;

                var methodName = attr.Name ?? method.Name;
                var parameters = method.GetParameters().Select((p, i) =>
                {
                    var paramDesc = attr.ParameterDescriptions?.ElementAtOrDefault(i);
                    return (paramDesc ?? p.Name ?? $"param{i}", LuaTypeConverter.GetLuaType(p.ParameterType), paramDesc);
                }).ToList();
                var returnType = LuaTypeConverter.GetLuaType(method.ReturnType);

                moduleDocs.Add(new LuaFunctionDoc(
                    $"{ModuleName}.{name}",
                    methodName,
                    attr.Description,
                    returnType,
                    parameters,
                    attr.Examples,
                    true
                ));
            }
        }

        docs.RegisterModule(this, moduleDocs);
    }

    /// <summary>
    /// Registers helper functions in the Lua environment
    /// </summary>
    private void RegisterHelperFunctions(Lua lua)
    {
        lua[$"{ModuleName}.IsInstalled"] = new Func<string, bool>(IsIPCInstalled);
        lua[$"{ModuleName}.GetAvailablePlugins"] = new Func<string[]>(GetAvailablePlugins);
    }

    /// <summary>
    /// Checks if an IPC plugin is installed
    /// </summary>
    private bool IsIPCInstalled(string name) => GetIPC<IPC>(name)?.IsInstalled ?? false;

    /// <summary>
    /// Gets all available IPC plugins
    /// </summary>
    private string[] GetAvailablePlugins() => [.. _ipcInstances.Values.Where(ipc => ipc.IsInstalled).Select(ipc => ipc.Name)];

    /// <summary>
    /// Gets an IPC instance by name
    /// </summary>
    private T? GetIPC<T>(string name) where T : IPC => _ipcInstances.TryGetValue(name, out var instance) ? instance as T : null;

    /// <summary>
    /// Gets the delegate type for a method
    /// </summary>
    private Type GetDelegateType(MethodInfo method)
    {
        var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
        return method.ReturnType == typeof(void) ? Expression.GetActionType(parameters) : Expression.GetFuncType([.. parameters, method.ReturnType]);
    }
}
