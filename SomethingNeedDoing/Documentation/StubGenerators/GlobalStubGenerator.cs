using SomethingNeedDoing.Documentation.StubGenerators.Builders;

namespace SomethingNeedDoing.Documentation.StubGenerators;

internal sealed class GlobalStubGenerator : StubGenerator
{
    protected override StubFile GenerateStub()
    {
        var file = new StubFile("global.lua");

        file.AddBuilder(new Builder().AddAlias("object", "any"));

        file.AddBuilder(new Builder().AddFunction("yield", [("value", "string")], "nil"));

        // NLua
        file.AddBuilder(new Builder().AddFunction("import", [("assemblyName", "string"), ("packageName", "string?")], "table"));
        file.AddBuilder(new Builder().AddFunction("CLRPackage", [("assemblyName", "string"), ("packageName", "string?")], "table"));

        // Luanet
        file.AddBuilder(
            new Builder()
                .AddClass("Luanet")
                .AddField("load_assembly", "fun(assemblyName: string): boolean")
                .AddField("import_type", "fun(typename: string): boolean")
                .AddField("namespace", "fun(ns: string|string[]|table<string>): boolean")
                .AddField("make_array", "fun(type: object, from: table): object[]")
                .AddField("each", "fun(source: object[]|table): fun(): object")
                .AddField("enum", "fun(enumType: string): table<string, integer>")
                .AddField("enum", "fun(enumType: object, value: string|integer): object")
                .AddField("ctype", "fun(proxy: object): object")
                .AddField("make_object", "fun(methodTable: table, className: string): object")
                .AddField("free_object", "fun(obj: object): nil")
                .AddField("get_object_member", "fun(obj: object, member: string|integer): object")
                .AddLine("")
                .AddLine("--- @type Luanet")
                .AddLine("--- @as Luanet")
                .AddLine("luanet = {}")
        );

        return file;
    }
}
