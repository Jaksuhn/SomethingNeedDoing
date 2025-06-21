using SomethingNeedDoing.Documentation.StubGenerators.Builders;

namespace SomethingNeedDoing.Documentation.StubGenerators;

internal sealed class ModuleStubGenerator(string moduleName, IReadOnlyList<LuaFunctionDoc> entries) : StubGenerator
{
    private readonly string _moduleName = moduleName ?? throw new ArgumentNullException(nameof(moduleName));

    private readonly IReadOnlyList<LuaFunctionDoc> _entries = entries ?? throw new ArgumentNullException(nameof(entries));

    protected override StubFile GenerateStub()
    {
        var filename = ToSnakeCase(_moduleName);
        var file = new StubFile("modules", $"{filename}.lua");
        var builder = new Builder();

        builder.AddLine($"--- @class {_moduleName}");

        foreach (var doc in _entries)
        {
            var type = doc.IsMethod
                ? GetLuaFunctionSignature(doc)
                : doc.ReturnType.ToString();

            builder.AddLine($"--- @field {doc.FunctionName} {type}");
        }

        builder.AddLine("");
        builder.AddLine($"--- @type {_moduleName}");
        builder.AddLine($"--- @as {_moduleName}");
        builder.AddLine($"{_moduleName} = {{}}");

        file.AddBuilder(builder);
        return file;
    }

    private string GetLuaFunctionSignature(LuaFunctionDoc doc)
    {
        var parameters = doc.Parameters != null && doc.Parameters.Any() ? string.Join(", ", doc.Parameters.Select(p => $"{p.Name}: {p.Type}")) : "";
        return $"fun({parameters}): {doc.ReturnType}";
    }
}
