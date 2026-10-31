
using SomethingNeedDoing.Documentation.StubGenerators.Builders;
using System.Reflection;

namespace SomethingNeedDoing.Documentation.StubGenerators;

internal sealed class ClassStubGenerator(Type type) : StubGenerator
{
    private readonly Type _type = type ?? throw new ArgumentNullException(nameof(type));

    protected override StubFile GenerateStub()
    {
        var filename = ToSnakeCase(_type.Name);
        var file = new StubFile("classes", $"{filename}.lua");
        var builder = new Builder();

        builder.AddLine($"--- @class {_type.Name}");

        // Properties
        var properties = _type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0); // Skip indexers

        foreach (var prop in properties)
        {
            var luaType = LuaTypeConverter.GetLuaType(prop.PropertyType);
            builder.AddLine($"--- @field {prop.Name} {luaType}");
        }

        // Fields
        var fields = _type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        foreach (var field in fields)
        {
            var luaType = LuaTypeConverter.GetLuaType(field.FieldType);
            builder.AddLine($"--- @field {field.Name} {luaType}");
        }

        // Methods
        var methods = _type.GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(IsLuaRelevantMethod)
            .Where(m => !m.IsSpecialName && m.DeclaringType == _type); // Exclude property accessors and inherited .NET object methods

        foreach (var method in methods)
        {
            var parameters = method.GetParameters()
                .Select(p => $"{p.Name}: {LuaTypeConverter.GetLuaType(p.ParameterType)}");

            var returnType = LuaTypeConverter.GetLuaType(method.ReturnType);
            builder.AddLine($"--- @field {method.Name} fun({string.Join(", ", parameters)}): {returnType}");
        }

        var ctor = _type.GetConstructors(BindingFlags.Public | BindingFlags.Instance)
        .OrderByDescending(c => c.GetParameters().Length)
        .FirstOrDefault();

        if (ctor != null)
        {
            builder.AddLine("");

            var parameters = ctor.GetParameters()
                .Select(p => $"{p.Name} {LuaTypeConverter.GetLuaType(p.ParameterType)}");

            var args = ctor.GetParameters()
                .Select(p => $"{p.Name}");

            foreach (var param in parameters)
            {
                builder.AddLine($"--- @param {param}");
            }

            builder.AddLine($"--- @return {_type.Name}");

            builder.AddLine($"function {_type.Name}({string.Join(", ", args)}) end");
        }

        file.AddBuilder(builder);
        return file;
    }

    private static bool IsLuaRelevantMethod(MethodInfo method)
    {
        var name = method.Name;
        if (name is "GetHashCode" or "Equals" or "CopyTo" or "TryCopyTo")
            return false;

        // Remove ToString overloads with format/provider
        if (name == "ToString" && method.GetParameters().Length > 0)
            return false;

        return true;
    }
}
