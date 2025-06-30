using DalamudCodeEditor;
using SomethingNeedDoing.Documentation;

namespace SomethingNeedDoing.Gui.Editor;

public class LuaLanguageDefinition : DalamudCodeEditor.LuaLanguageDefinition
{
    public LuaLanguageDefinition(LuaDocumentation luaDocs)
    {
        var sndSpecificSymbols = new List<string>(["Svc", "luanet", "import", "CLRPackage", "yield"]);

        // Add module keys
        foreach (var module in luaDocs.GetModules())
        {
            sndSpecificSymbols.Add(module.Key);
        }

        foreach (var ident in sndSpecificSymbols)
        {
            Identifiers[ident] = new Identifier { Declaration = "SND" };
        }
    }
}
