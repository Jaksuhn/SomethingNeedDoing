using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using ECommons.MathHelpers;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Gui.Editor;
using SomethingNeedDoing.Managers;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Gui;

/// <summary>
/// Macro editor with IDE-like features
/// </summary>
public class MacroEditor(IMacroScheduler scheduler, GitMacroManager gitManager, WindowSystem ws, CodeEditor _editor)
{
    private readonly IMacroScheduler _scheduler = scheduler;
    private readonly GitMacroManager _gitManager = gitManager;
    private UpdateState _updateState = UpdateState.Unknown;

    private enum UpdateState
    {
        Unknown,
        None,
        Available
    }

    public void Draw(IMacro? macro)
    {
        using var child = ImRaii.Child("RightPanel", new Vector2(0, -1), false);
        if (!child) return;

        if (macro == null)
        {
            DrawEmptyState();
            return;
        }

        _editor.SetMacro(macro);
        _editor.ReadOnly = _scheduler.GetMacroState(macro.Id) is MacroState.Running;

        DrawEditorToolbar(macro);
        ImGui.Separator();

        var editorHeight = ImGui.GetContentRegionAvail().Y - ImGui.GetFrameHeightWithSpacing() * 2;
        DrawCodeEditor(macro, editorHeight);
        DrawStatusBar(macro);
    }

    private void DrawEmptyState()
    {
        var center = ImGui.GetContentRegionAvail() / 2;
        var text = "Select a macro or create a new one";
        var textSize = ImGui.CalcTextSize(text);
        ImGui.SetCursorPos(ImGui.GetCursorPos() + center - textSize / 2);
        ImGui.TextColored(ImGuiColors.DalamudGrey, text);
    }

    private void DrawEditorToolbar(IMacro macro)
    {
        using var toolbar = ImRaii.Child("ToolbarChild", new Vector2(-1, ImGui.GetFrameHeight() * 2f), false);
        if (!toolbar) return;

        ImGui.Spacing();
        ImGui.Spacing();
        DrawActionButtons(macro);
        DrawRightAlignedControls(macro);
    }

    private void DrawActionButtons(IMacro macro)
    {
        var group = new ImGuiEx.EzButtonGroup();
        var startBtn = GetStartOrResumeAction(macro);
        group.AddIconOnly(FontAwesomeIcon.PlayCircle, () => startBtn.action(), startBtn.tooltip);
        group.AddIconOnly(FontAwesomeIcon.PauseCircle, () => _scheduler.PauseMacro(macro.Id), "Pause", new() { Condition = () => _scheduler.GetMacroState(macro.Id) is MacroState.Running });
        group.AddIconOnly(FontAwesomeIcon.StopCircle, () => _scheduler.StopMacro(macro.Id), "Stop");
        group.AddIconOnly(FontAwesomeIcon.Clipboard, () => Copy(macro.Content), "Copy");
        group.Draw();
    }

    private (Action action, string tooltip) GetStartOrResumeAction(IMacro macro)
        => _scheduler.GetMacroState(macro.Id) switch
        {
            MacroState.Paused => (() => _scheduler.ResumeMacro(macro.Id), "Resume"),
            _ => (() => _scheduler.StartMacro(macro), "Start")
        };

    private void DrawRightAlignedControls(IMacro macro)
    {
        int buttonCount = 4;
        int offset = 40 * buttonCount;
        int gitMacroPadding = 25;

        ImGui.SameLine(ImGui.GetWindowWidth() - (macro is ConfigMacro { IsGitMacro: true } ? offset + gitMacroPadding : offset));

        using var _ = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);

        var runningMacros = _scheduler.GetMacros().ToList();
        var macroCount = runningMacros.Count;
        var (statusColor, statusIcon) = macroCount > 0
            ? (ImGuiColors.HealerGreen, FontAwesomeIcon.Play)
            : (ImGuiColors.DalamudGrey, FontAwesomeIcon.Desktop);

        using (ImRaii.PushColor(ImGuiCol.Text, statusColor))
        {
            if (ImGuiUtils.IconButton(statusIcon, macroCount > 0 ? $"{macroCount} running" : "No macros running"))
                ws.Toggle<StatusWindow>();
        }

        ImGui.SameLine();
        if (ImGuiUtils.IconButton(_editor.IsShowingLineNumbers ? FontAwesomeHelper.IconSortAsc : FontAwesomeHelper.IconSortDesc, "Toggle Line Numbers"))
            _editor.IsShowingLineNumbers ^= true;

        ImGui.SameLine();
        if (ImGuiUtils.IconButton(_editor.IsShowingWhitespace ? FontAwesomeHelper.IconInvisible : FontAwesomeHelper.IconVisible, "Toggle Line Numbers"))
            _editor.IsShowingWhitespace ^= true;

        ImGui.SameLine();
        if (ImGuiUtils.IconButton(_editor.IsHighlightingSyntax ? FontAwesomeHelper.IconCheck : FontAwesomeHelper.IconXmark, "Syntax Highlighting"))
            _editor.IsHighlightingSyntax ^= true;

        if (macro is ConfigMacro { IsGitMacro: true } configMacro)
        {
            ImGui.SameLine();
            var (updateIndicator, updateColor, tooltip) = _updateState switch
            {
                UpdateState.None => ("0", ImGuiColors.DalamudGrey, "No updates available"),
                UpdateState.Available => ("1", ImGuiColors.DPSRed, "Update available (click to update)"),
                _ => ("?", ImGuiColors.DalamudGrey, "Check for updates")
            };

            if (ImGuiUtils.IconButtonWithNotification(FontAwesomeIcon.Bell, updateIndicator, updateColor, tooltip))
            {
                if (_updateState == UpdateState.Available)
                    Task.Run(async () => await _gitManager.UpdateMacro(configMacro));
                else
                    Task.Run(async () =>
                    {
                        try
                        {
                            await _gitManager.CheckForUpdates(configMacro);
                            _updateState = configMacro.GitInfo.HasUpdate ? UpdateState.Available : UpdateState.None;
                        }
                        catch
                        {
                            _updateState = UpdateState.Unknown;
                        }
                    });
            }
        }
    }

    private void DrawCodeEditor(IMacro macro, float height)
    {
        using var editorWrapper = ImRaii.Child("CodeEditor", new Vector2(0, height), false);
        if (!editorWrapper) return;

        if (macro is ConfigMacro configMacro)
        {
            if (_editor.Draw())
            {
                configMacro.Content = _editor.GetContent();
                C.Save();
            }
        }
    }

    private bool _wantOpenSelector;
    private void DrawStatusBar(IMacro macro)
    {
        using var _ = ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));

        var chars = macro.Content.Length;
        ImGuiEx.Text(ImGuiColors.DalamudGrey, $"Name: {macro.Name}  |  Lines: {_editor.Lines}  |  Chars: {chars}  |  Column: {_editor.Column}  |  Readonly: {_editor.ReadOnly}  |");
        ImGui.SameLine(0, 5);
        ImGuiEx.Text(ImGuiColors.DalamudGrey, $"Type: {macro.Type}");
        if (ImGui.IsItemClicked())
            _wantOpenSelector = true;

        if (_wantOpenSelector)
        {
            ImGui.OpenPopup("type_selector");
            _wantOpenSelector = false;
        }

        if (macro is ConfigMacro cfgMacro)
        {
            using var popup = ImRaii.Popup("type_selector");
            if (popup)
                if (DrawTypeSelector(cfgMacro))
                    ImGui.CloseCurrentPopup();
        }

        if (macro is ConfigMacro { IsGitMacro: true } configMacro)
        {
            ImGui.SameLine(0, 0);
            ImGuiEx.Text(ImGuiColors.DalamudGrey, " | ");
            ImGui.SameLine(0, 0);
            ImGuiUtils.DrawLink(ImGuiColors.DalamudGrey, $"Git: {configMacro.GitInfo}", configMacro.GitInfo.RepositoryUrl);
        }
    }

    private bool DrawTypeSelector(ConfigMacro macro)
    {
        foreach (var type in Enum.GetValues<MacroType>())
        {
            var active = macro.Type == type;
            ImGui.SameLine();
            using var col = ImRaii.PushColor(ImGuiCol.Button, ImGuiColors.ParsedPurple, active)
                .Push(ImGuiCol.ButtonHovered, ImGuiColors.ParsedPurple.AddNoW(0.1f), active)
                .Push(ImGuiCol.ButtonActive, ImGuiColors.ParsedPurple.AddNoW(0.2f), active);
            if (ImGui.Button(type.ToString()))
            {
                macro.Type = type;
                C.Save();
                return true;
            }
        }

        return false;
    }
}
