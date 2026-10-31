using System.Reflection;

namespace SomethingNeedDoing.Documentation;
/// <summary>
/// Provides documentation for Lua modules and functions.
/// </summary>
[RegisterSingleton]
public class LuaDocumentation
{
    private readonly Dictionary<string, List<LuaFunctionDoc>> _documentation = [];

    public void RegisterModule(string moduleName, List<LuaFunctionDoc> docs) => _documentation[moduleName] = docs;

    public Dictionary<string, List<LuaFunctionDoc>> GetModules() => _documentation;

    /// <summary>
    /// Documents the <see cref="LuaFunctionAttribute"/> members of a module, in the order they appear in the class.
    /// </summary>
    public static List<LuaFunctionDoc> DescribeMembers(object module, string modulePath)
    {
        var docs = new List<LuaFunctionDoc>();
        foreach (var member in module.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance))
        {
            if (member.GetCustomAttribute<LuaFunctionAttribute>() is not { } attr) continue;

            var description = attr.Description ?? member.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;
            switch (member)
            {
                case MethodInfo method:
                    var parameters = method.GetParameters().Select((param, i) => (
                        param.Name ?? $"param{i}",
                        LuaTypeConverter.GetLuaType(param.ParameterType),
                        attr.ParameterDescriptions?.Length > i ? attr.ParameterDescriptions[i] : null,
                        param.HasDefaultValue ? param.DefaultValue : null)).ToList();
                    docs.Add(new LuaFunctionDoc(modulePath, attr.Name ?? method.Name, description, LuaTypeConverter.GetLuaType(method.ReturnType), parameters, attr.Examples, true));
                    break;

                case PropertyInfo property:
                    docs.Add(new LuaFunctionDoc(modulePath, attr.Name ?? property.Name, description, LuaTypeConverter.GetLuaType(property.PropertyType), [], attr.Examples, false));
                    break;

                case FieldInfo field:
                    docs.Add(new LuaFunctionDoc(modulePath, attr.Name ?? field.Name, description, LuaTypeConverter.GetLuaType(field.FieldType), [], attr.Examples, false));
                    break;
            }
        }
        return docs;
    }
}
