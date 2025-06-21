using SomethingNeedDoing.Documentation;
using SomethingNeedDoing.Documentation.StubGenerators;
using SomethingNeedDoing.Documentation.StubGenerators.Builders;
using System.Reflection;

internal sealed class WrapperStubGenerator(Type wrapperType) : StubGenerator
{
    private readonly Type _wrapperType = wrapperType ?? throw new ArgumentNullException(nameof(wrapperType));

    protected override StubFile GenerateStub()
    {
        var filename = ToSnakeCase(_wrapperType.Name);
        var file = new StubFile("wrappers", $"{filename}.lua");
        var builder = new Builder();

        builder.AddLine($"--- @class {_wrapperType.Name}");

        var properties = _wrapperType
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetCustomAttributes(typeof(LuaDocsAttribute), true).Length != 0);

        foreach (var prop in properties)
        {
            if (prop.Name == "Item" && prop.GetIndexParameters().Length > 0) continue;

            var typeName = LuaTypeConverter.GetLuaType(prop.PropertyType);
            builder.AddLine($"--- @field {prop.Name} {typeName}");
        }

        var methods = _wrapperType
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.GetCustomAttributes(typeof(LuaDocsAttribute), true).Length != 0);

        foreach (var method in methods)
        {
            var parameters = method.GetParameters()
                .Select(p => $"{p.Name}: {LuaTypeConverter.GetLuaType(p.ParameterType)}");
            var returnType = LuaTypeConverter.GetLuaType(method.ReturnType);

            builder.AddLine($"--- @field {method.Name} fun({string.Join(", ", parameters)}): {returnType}");
        }

        file.AddBuilder(builder);
        return file;
    }
}
