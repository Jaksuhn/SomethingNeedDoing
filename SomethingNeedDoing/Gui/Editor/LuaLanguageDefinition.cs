using DalamudCodeEditor;
using SomethingNeedDoing.Documentation;

namespace SomethingNeedDoing.Gui.Editor;

[RegisterSingleton, AutoConstruct]
public partial class LuaLanguageDefinition : DalamudCodeEditor.LuaLanguageDefinition
{
    private readonly LuaDocumentation _luaDocs;

    [AutoPostConstruct]
    private void Initialize()
    {
        var sndSpecificSymbols = new List<string>(["Svc", "luanet", "import", "CLRPackage", "yield"]);

        // Add module keys
        foreach (var module in _luaDocs.GetModules())
        {
            sndSpecificSymbols.Add(module.Key);
        }

        foreach (var ident in sndSpecificSymbols)
        {
            Identifiers[ident] = new Identifier { Declaration = "SND" };
        }
    }
}
