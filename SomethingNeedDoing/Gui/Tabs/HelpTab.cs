using ECommons.ImGuiMethods;

namespace SomethingNeedDoing.Gui.Tabs;

[RegisterSingleton, AutoConstruct]
public partial class HelpTab
{
    private readonly HelpLuaTab _luaTab;
    private readonly HelpCliTab _cliTab;
    private readonly HelpCommandsTab _commandsTab;

    public void Draw()
    {
        ImGuiEx.EzTabBar("Tabs",
            ("General", HelpGeneralTab.DrawTab, null, false),
            ("Commands", _commandsTab.DrawTab, null, false),
            ("Lua", _luaTab.DrawTab, null, false),
            ("Cli", _cliTab.DrawTab, null, false),
            ("Clicks", HelpClicksTab.DrawTab, null, false),
            ("Keys & Sends", HelpKeysTab.DrawTab, null, false));
    }
}
