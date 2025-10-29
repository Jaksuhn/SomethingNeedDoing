using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Gui.Modals;
using SomethingNeedDoing.Gui.Tabs;
using SomethingNeedDoing.Managers;
using System.Diagnostics;

namespace SomethingNeedDoing.Gui;

public class MainWindow : Window
{
    private readonly HelpTab _helpTab;
    private readonly MacrosTab _macrosTab;
    private readonly VersionHistoryModal _versionHistoryModal;
    private readonly CreateMacroModal _createMacroModal;
    private bool ClickedHeaderLastFrame;
    private bool ClickedHeaderCurrentFrame;

    public MainWindow(IMacroScheduler scheduler, MacroEditor macroEditor, HelpTab helpTab, VersionHistoryModal versionHistoryModal, GitMacroManager gitManager)
        : base($"{P.Name} v{P.Version}###{P.Name}_{nameof(MainWindow)}", ImGuiWindowFlags.NoScrollbar)
    {
        _helpTab = helpTab;
        _macrosTab = new MacrosTab(scheduler, macroEditor, gitManager);
        _versionHistoryModal = versionHistoryModal;
        _createMacroModal = new CreateMacroModal(gitManager);

        Size = new Vector2(1000, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        TitleBarButtons.Add(new TitleBarButton
        {
            Icon = FontAwesomeIcon.Heart,
            ShowTooltip = () =>
            {
                using (ImRaii.Tooltip())
                    ImGuiEx.IconWithText(FontAwesomeIcon.Coffee, "Ko-fi");
            },
            Priority = int.MinValue,
            IconOffset = new Vector2(1.5f, 1),
            Click = _ =>
            {
                ClickedHeaderCurrentFrame = true;
                if (ClickedHeaderLastFrame)
                    return;

                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "https://ko-fi.com/croizat",
                        UseShellExecute = true,
                        Verb = string.Empty,
                    });
                }
                catch { }
            },
            AvailableClickthrough = true,
        });

    }

    public override void PreDraw()
    {
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
        ClickedHeaderLastFrame = ClickedHeaderCurrentFrame;
        ClickedHeaderCurrentFrame = false;
    }

    public override void PostDraw() => ImGui.PopStyleVar();

    public override void Draw()
    {
        _createMacroModal.DrawModal();
        CreateFolderModal.DrawModal();
        RenameModal.DrawModal();
        RenameFolderModal.DrawModal();
        MigrationModal.DrawModal();
        _versionHistoryModal.Draw();

        using var _ = ImRaii.TabBar("Tabs");
        using (var tab = ImRaii.TabItem("MacrosLibrary"))
            if (tab)
                _macrosTab.Draw();

        using (var tab = ImRaii.TabItem("Help"))
            if (tab)
                _helpTab.Draw();

        using (var tab = ImRaii.TabItem("Settings"))
            if (tab)
                SettingsTab.DrawTab();
    }
}
