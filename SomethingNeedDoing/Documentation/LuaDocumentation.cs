using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro;
using System.Reflection;

namespace SomethingNeedDoing.Documentation;
/// <summary>
/// Provides documentation for Lua modules and functions.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class LuaDocumentation
{
    private readonly Dictionary<string, List<LuaFunctionDoc>> _documentation = [];

    public void RegisterModule(ILuaModule module)
    {
        var docs = new List<LuaFunctionDoc>();
        var modulePath = module is LuaModuleBase baseModule ? baseModule.GetModulePath() : module.ModuleName;

        // Register all members in the order they appear in the class
        foreach (var member in module.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance))
        {
            if (member.GetCustomAttribute<LuaFunctionAttribute>() is not { } attr) continue;

            switch (member)
            {
                case MethodInfo method:
                    var methodParams = method.GetParameters();
                    var parameters = new List<(string Name, LuaTypeInfo Type, string? Description, object? DefaultValue)>();

                    for (var i = 0; i < methodParams.Length; i++)
                    {
                        var param = methodParams[i];
                        var paramType = LuaTypeConverter.GetLuaType(param.ParameterType);
                        var paramDesc = attr.ParameterDescriptions?.Length > i ? attr.ParameterDescriptions[i] : null;
                        var defaultValue = param.HasDefaultValue ? param.DefaultValue : null;

                        parameters.Add((param.Name ?? $"param{i}", paramType, paramDesc, defaultValue));
                    }

                    var returnType = LuaTypeConverter.GetLuaType(method.ReturnType);
                    var description = attr.Description ?? method.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

                    docs.Add(new LuaFunctionDoc(
                        modulePath,
                        attr.Name ?? method.Name,
                        description,
                        returnType,
                        parameters,
                        attr.Examples,
                        true
                    ));
                    break;

                case PropertyInfo property:
                    var propertyType = LuaTypeConverter.GetLuaType(property.PropertyType);
                    var propertyDescription = attr.Description ?? property.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

                    docs.Add(new LuaFunctionDoc(
                        modulePath,
                        attr.Name ?? property.Name,
                        propertyDescription,
                        propertyType,
                        [],
                        attr.Examples,
                        false
                    ));
                    break;

                case FieldInfo field:
                    var fieldType = LuaTypeConverter.GetLuaType(field.FieldType);
                    var fieldDescription = attr.Description ?? field.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>()?.Description;

                    docs.Add(new LuaFunctionDoc(
                        modulePath,
                        attr.Name ?? field.Name,
                        fieldDescription,
                        fieldType,
                        [],
                        attr.Examples,
                        false
                    ));
                    break;
            }
        }

        _documentation[module.ModuleName] = docs;
    }

    public void RegisterModule(ILuaModule module, List<LuaFunctionDoc> docs) => _documentation[module.ModuleName] = docs;

    public void RegisterModule(string moduleName, List<LuaFunctionDoc> docs) => _documentation[moduleName] = docs;

    public Dictionary<string, List<LuaFunctionDoc>> GetModules() => _documentation;
}
