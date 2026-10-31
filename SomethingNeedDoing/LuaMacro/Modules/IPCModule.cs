using NLua;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Documentation;
using System.Reflection;

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
        LuaReflection.RegisterEnums(lua, _ipcInstances.Values.SelectMany(ipc => LuaReflection.GetMembers(ipc.GetType()).Enums).Distinct());

        lua.DoString($"{ModuleName} = {{}}");
        lua[$"{ModuleName}.IsInstalled"] = new Func<string, bool>(IsIPCInstalled);
        lua[$"{ModuleName}.GetAvailablePlugins"] = new Func<string[]>(GetAvailablePlugins);

        // Register each IPC class as a submodule
        foreach (var (name, instance) in _ipcInstances)
        {
            lua.DoString($"{ModuleName}.{name} = {{}}");
            var members = LuaReflection.GetMembers(instance.GetType());

            foreach (var (fieldName, field) in members.Fields)
                if (field.GetValue(instance) is { } fieldValue)
                    lua[$"{ModuleName}.{name}.{fieldName}"] = fieldValue;

            foreach (var (methodName, method) in members.Methods)
                lua[$"{ModuleName}.{name}.{methodName}"] = LuaReflection.CreateDelegate(instance, method);
        }
    }

    public override void RegisterDocumentation(LuaDocumentation docs)
    {
        var isInstalledParam = ((Func<string, bool>)IsIPCInstalled).Method.GetParameters()[0];
        var moduleDocs = new List<LuaFunctionDoc>
        {
            new(ModuleName, "IsInstalled", "Checks if an IPC plugin is installed", LuaTypeConverter.GetLuaType(typeof(bool)),
                [(isInstalledParam.Name ?? "name", LuaTypeConverter.GetLuaType(isInstalledParam.ParameterType), "The name of the plugin to check", null)], null, true),
            new(ModuleName, "GetAvailablePlugins", "Gets all available IPC plugins", LuaTypeConverter.GetLuaType(typeof(string[])), [], null, true),
        };

        foreach (var (name, instance) in _ipcInstances)
        {
            var modulePath = $"{ModuleName}.{name}";

            // fields hold delegates, so document their Invoke signature
            foreach (var field in instance.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.GetCustomAttribute<LuaFunctionAttribute>() is not { } attr) continue;

                var invoke = typeof(Delegate).IsAssignableFrom(field.FieldType) ? field.FieldType.GetMethod("Invoke") : null;
                var parameters = (invoke?.GetParameters() ?? []).Select((p, i) =>
                {
                    var paramDesc = attr.ParameterDescriptions?.ElementAtOrDefault(i);
                    return (paramDesc ?? $"param{i}", LuaTypeConverter.GetLuaType(p.ParameterType), paramDesc, (object?)null);
                }).ToList();

                moduleDocs.Add(new LuaFunctionDoc(modulePath, attr.Name ?? field.Name, attr.Description, LuaTypeConverter.GetLuaType(invoke?.ReturnType ?? typeof(void)), parameters, attr.Examples, true));
            }

            foreach (var method in instance.GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.GetCustomAttribute<LuaFunctionAttribute>() is not { } attr) continue;

                var parameters = method.GetParameters().Select((p, i) =>
                {
                    var paramDesc = attr.ParameterDescriptions?.ElementAtOrDefault(i);
                    return (paramDesc ?? p.Name ?? $"param{i}", LuaTypeConverter.GetLuaType(p.ParameterType), paramDesc, p.HasDefaultValue ? p.DefaultValue : null);
                }).ToList();

                moduleDocs.Add(new LuaFunctionDoc(modulePath, attr.Name ?? method.Name, attr.Description, LuaTypeConverter.GetLuaType(method.ReturnType), parameters, attr.Examples, true));
            }
        }

        docs.RegisterModule(ModuleName, moduleDocs);
    }

    private bool IsIPCInstalled(string name) => _ipcInstances.TryGetValue(name, out var instance) && instance.IsInstalled;
    private string[] GetAvailablePlugins() => [.. _ipcInstances.Values.Where(ipc => ipc.IsInstalled).Select(ipc => ipc.Name)];
}
