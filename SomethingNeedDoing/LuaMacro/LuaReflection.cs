using NLua;
using SomethingNeedDoing.Core.Interfaces;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace SomethingNeedDoing.LuaMacro;

/// <summary>
/// Reflection helpers shared by Lua modules. Per-type results are cached since modules are registered on every Lua macro start.
/// </summary>
public static class LuaReflection
{
    private const BindingFlags PublicInstance = BindingFlags.Public | BindingFlags.Instance;

    /// <summary>
    /// The <see cref="LuaFunctionAttribute"/> members of a type.
    /// </summary>
    public sealed record LuaMembers((string Name, MethodInfo Method)[] Methods, (string Name, MethodInfo Getter)[] Properties, (string Name, FieldInfo Field)[] Fields, Type[] Enums);

    private static readonly ConcurrentDictionary<Type, LuaMembers> MembersCache = [];

    public static LuaMembers GetMembers(Type type) => MembersCache.GetOrAdd(type, static t => new(
        [.. t.GetMethods(PublicInstance).Select(m => (m.GetCustomAttribute<LuaFunctionAttribute>(), m)).Where(x => x.Item1 != null).Select(x => (x.Item1!.Name ?? x.m.Name, x.m))],
        [.. t.GetProperties(PublicInstance).Select(p => (p.GetCustomAttribute<LuaFunctionAttribute>(), p)).Where(x => x.Item1 != null).Select(x => (x.Item1!.Name ?? x.p.Name, x.p.GetMethod!))],
        [.. t.GetFields(PublicInstance).Select(f => (f.GetCustomAttribute<LuaFunctionAttribute>(), f)).Where(x => x.Item1 != null).Select(x => (x.Item1!.Name ?? x.f.Name, x.f))],
        [.. FindEnums(t)]));

    public static Type GetDelegateType(MethodInfo method)
    {
        var parameters = method.GetParameters().Select(p => p.ParameterType).ToArray();
        return method.ReturnType == typeof(void) ? Expression.GetActionType(parameters) : Expression.GetFuncType([.. parameters, method.ReturnType]);
    }

    public static Delegate CreateDelegate(object target, MethodInfo method) => Delegate.CreateDelegate(GetDelegateType(method), target, method);

    public static void RegisterEnums(Lua lua, IEnumerable<Type> enumTypes)
    {
        foreach (var enumType in enumTypes)
        {
            try
            {
                lua.RegisterClass(enumType);
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error($"Failed to register enum {enumType.Name}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Finds the enums used by a type's Lua members (and the wrapper types they return), so they can be registered as Lua globals.
    /// </summary>
    private static HashSet<Type> FindEnums(Type type)
    {
        var enumTypes = new HashSet<Type>();
        var deep = typeof(IPC).IsAssignableFrom(type); // IPC signatures use nullables and generics more than modules do
        AddMemberEnums(type, enumTypes, deep);

        // wrapper types returned by Lua members
        foreach (var member in type.GetMembers(PublicInstance).Where(m => m.GetCustomAttribute<LuaFunctionAttribute>() != null))
            if (member switch { MethodInfo m => m.ReturnType, PropertyInfo p => p.PropertyType, _ => null } is { } returnType && IsWrapperType(returnType))
                AddMemberEnums(returnType, enumTypes, deep);

        // IPC delegates expose enums through their generic arguments
        if (deep)
            foreach (var field in type.GetFields(PublicInstance).Where(f => f.GetCustomAttribute<LuaFunctionAttribute>() != null))
                AddEnums(field.FieldType, enumTypes, deep);

        return enumTypes;
    }

    /// <summary>
    /// Adds the enums used by a type's <see cref="LuaFunctionAttribute"/>/<see cref="LuaDocsAttribute"/> methods and properties, including nested types.
    /// </summary>
    private static void AddMemberEnums(Type type, HashSet<Type> enumTypes, bool deep)
    {
        foreach (var method in type.GetMethods(PublicInstance).Where(IsLuaMember))
        {
            AddEnums(method.ReturnType, enumTypes, deep);
            foreach (var parameter in method.GetParameters())
                AddEnums(parameter.ParameterType, enumTypes, deep);
        }

        foreach (var property in type.GetProperties(PublicInstance).Where(IsLuaMember))
            AddEnums(property.PropertyType, enumTypes, deep);

        foreach (var nestedType in type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic))
            AddMemberEnums(nestedType, enumTypes, deep);
    }

    private static bool IsLuaMember(MemberInfo member) => member.GetCustomAttribute<LuaFunctionAttribute>() != null || member.GetCustomAttribute<LuaDocsAttribute>() != null;

    /// <summary>
    /// Adds <paramref name="type"/> if it's an enum. When <paramref name="deep"/>, also looks inside nullables, arrays and generic arguments.
    /// </summary>
    private static void AddEnums(Type? type, HashSet<Type> enumTypes, bool deep)
    {
        if (type == null) return;
        if (deep && Nullable.GetUnderlyingType(type) is { } underlying)
            type = underlying;
        if (type.IsEnum)
        {
            enumTypes.Add(type);
            return;
        }
        if (!deep) return;

        if (type.IsArray)
            AddEnums(type.GetElementType(), enumTypes, deep);
        else if (type.IsGenericType)
            foreach (var arg in type.GetGenericArguments())
                AddEnums(arg, enumTypes, deep);
    }

    private static bool IsWrapperType(Type type)
        => typeof(IWrapper).IsAssignableFrom(type) || (type.IsGenericType && type.GetGenericArguments().Any(IsWrapperType));
}
