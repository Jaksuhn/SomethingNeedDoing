using NLua;
using SomethingNeedDoing.Framework.Interfaces;
using System.Linq.Expressions;
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

    public virtual void Register(NLua.Lua lua)
    {
        // Create module table
        var modulePath = GetModulePath();
        lua.DoString($"{modulePath} = {{}}");

        // Register all methods marked with LuaFunction attribute
        foreach (var method in GetType().GetMethods(BindingFlags.Public | BindingFlags.Instance))
        {
            if (method.GetCustomAttribute<LuaFunctionAttribute>() is not { } attr) continue;
            lua[$"{modulePath}.{attr.Name ?? method.Name}"] = CreateDelegate(method);
        }

        // Register all properties marked with LuaFunction attribute
        foreach (var property in GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetCustomAttribute<LuaFunctionAttribute>() is not { } attr) continue;
            if (property.GetValue(this) is { } propertyValue)
                lua[$"{modulePath}.{attr.Name ?? property.Name}"] = propertyValue;
        }
    }

    public string GetModulePath()
    {
        if (ParentModule == null)
            return ModuleName;

        if (ParentModule is LuaModuleBase parent)
            return $"{parent.GetModulePath()}.{ModuleName}";

        return $"{ParentModule.ModuleName}.{ModuleName}";
    }

    private Delegate CreateDelegate(MethodInfo method)
    {
        var delegateType = GetDelegateType(method);
        return Delegate.CreateDelegate(delegateType, this, method);
    }

    private Type GetDelegateType(MethodInfo method)
    {
        var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
        return method.ReturnType == typeof(void) ? Expression.GetActionType(parameters) : Expression.GetFuncType([.. parameters, method.ReturnType]);
    }
}
