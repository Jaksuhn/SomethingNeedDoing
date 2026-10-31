using NLua;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Documentation;
using System.Reflection;

namespace SomethingNeedDoing.LuaMacro;
/// <summary>
/// Base class for Lua API modules
/// </summary>
public abstract class LuaModuleBase : ILuaModule
{
    public abstract string ModuleName { get; }
    public ILuaModule? ParentModule { get; set; }
    public virtual Type? ParentType => null;

    private LuaModuleManager? _moduleManager;
    internal void SetModuleManager(LuaModuleManager manager) => _moduleManager = manager;
    protected T? GetModule<T>() where T : class, ILuaModule => _moduleManager?.GetModule<T>();

    private readonly Dictionary<string, Delegate> _propertyDelegateMap = [];

    public virtual void Register(Lua lua)
    {
        var members = LuaReflection.GetMembers(GetType());
        LuaReflection.RegisterEnums(lua, members.Enums);

        // Create module table
        var modulePath = GetModulePath();
        lua.DoString($"{modulePath} = {{}}");
        RegisterMetaTable(lua, modulePath);

        foreach (var (name, method) in members.Methods)
            lua[$"{modulePath}.{name}"] = LuaReflection.CreateDelegate(this, method);

        // properties are exposed through the metatable as getters
        foreach (var (name, getter) in members.Properties)
            _propertyDelegateMap[name] = LuaReflection.CreateDelegate(this, getter);
    }

    /// <summary>
    /// Registers this module's documentation. By default, documents its <see cref="LuaFunctionAttribute"/> members.
    /// </summary>
    public virtual void RegisterDocumentation(LuaDocumentation docs) => docs.RegisterModule(ModuleName, LuaDocumentation.DescribeMembers(this, GetModulePath()));

    protected virtual object? MetaIndex(LuaTable table, object key) => null;

    private object? MetatableOnIndex(LuaTable table, object key) => key switch
    {
        string stringKey when _propertyDelegateMap.TryGetValue(stringKey, out var propertyFunc) => propertyFunc.DynamicInvoke(),
        _ => MetaIndex(table, key)
    };

    private void RegisterMetaTable(Lua lua, string modulePath)
    {
        var metaPath = $"{modulePath}.__mt";
        var metaTable = lua.GetTable(metaPath);
        if (metaTable == null)
        {
            lua.NewTable(metaPath);
            lua.DoString($"setmetatable({modulePath}, {metaPath})");
            metaTable = lua.GetTable(metaPath);
        }

        metaTable[nameof(MetatableOnIndex)] = LuaReflection.CreateDelegate(this, typeof(LuaModuleBase).GetMethod(nameof(MetatableOnIndex), BindingFlags.NonPublic | BindingFlags.Instance)!);
        lua.DoString($"{metaPath}.__index = function(...) return {metaPath}.{nameof(MetatableOnIndex)}(...) end");
    }

    public string GetModulePath()
    {
        if (ParentModule == null)
            return ModuleName;

        if (ParentModule is LuaModuleBase parent)
            return $"{parent.GetModulePath()}.{ModuleName}";

        return $"{ParentModule.ModuleName}.{ModuleName}";
    }
}
