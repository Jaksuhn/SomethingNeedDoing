using SomethingNeedDoing.Documentation.StubGenerators.Builders;

namespace SomethingNeedDoing.Documentation.StubGenerators;

internal sealed class IpcStubGenerator(LuaDocumentation luaDocs) : StubGenerator
{
    private readonly LuaDocumentation _luaDocs = luaDocs ?? throw new ArgumentNullException(nameof(luaDocs));

    protected override StubFile GenerateStub()
    {
        var file = new StubFile("modules", "ipc.lua");
        var ipcModules = _luaDocs.GetModules()
            .Where(m => m.Key == "IPC")
            .ToList();

        if (ipcModules.Count == 0)
            return file;

        var ipcEntries = ipcModules[0].Value;

        var grouped = ipcEntries
            .GroupBy(f => f.ModuleName.Contains('.') ? f.ModuleName.Split('.')[1] : "Root")
            .ToList();

        var groupNames = new List<string>();

        // Generate class stubs for each IPC sub-group
        foreach (var group in grouped)
        {
            var className = group.Key;
            groupNames.Add(className);

            var classBuilder = new Builder();
            classBuilder.AddLine($"--- @class {className}");

            foreach (var doc in group)
            {
                var type = doc.IsMethod ? GetLuaFunctionSignature(doc) : doc.ReturnType.ToString();
                classBuilder.AddLine($"--- @field {doc.FunctionName} {type}");
            }

            file.AddBuilder(classBuilder);
        }

        // Generate root IPC class
        var ipcBuilder = new Builder();
        ipcBuilder.AddLine($"--- @class IPC");

        foreach (var name in groupNames)
            ipcBuilder.AddLine($"--- @field {name} {name}");

        ipcBuilder.AddLine("");
        ipcBuilder.AddLine($"--- @type IPC");
        ipcBuilder.AddLine($"--- @as IPC");
        ipcBuilder.AddLine("IPC = {}");

        file.AddBuilder(ipcBuilder);

        return file;
    }

    private static string GetLuaFunctionSignature(LuaFunctionDoc doc)
    {
        var parameters = doc.Parameters != null && doc.Parameters.Any()
            ? string.Join(", ", doc.Parameters.Select(p => $"{p.Name}: {p.Type}"))
            : "";
        return $"fun({parameters}): {doc.ReturnType}";
    }
}
