using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Managers;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Gui;

/// <summary>
/// Macro editor with IDE-like features
/// </summary>
public class MacroEditor(IMacroScheduler scheduler, GitMacroManager gitManager, WindowSystem ws)
{
    private readonly IMacroScheduler _scheduler = scheduler;
    private readonly GitMacroManager _gitManager = gitManager;
    private bool _showLineNumbers = true;
    private bool _highlightSyntax = true;
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
        ImGui.SameLine(ImGui.GetWindowWidth() - (macro is ConfigMacro { IsGitMacro: true } ? 145 : 120));

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
        if (ImGuiUtils.IconButton(
            _showLineNumbers ? FontAwesomeHelper.IconSortAsc : FontAwesomeHelper.IconSortDesc,
            "Toggle Line Numbers"))
            _showLineNumbers = !_showLineNumbers;

        ImGui.SameLine();
        if (ImGuiUtils.IconButton(
            _highlightSyntax ? FontAwesomeHelper.IconCheck : FontAwesomeHelper.IconXmark,
            "Syntax Highlighting (not currently available)"))
            _highlightSyntax = !_highlightSyntax;

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
        var lineNumberWidth = CalculateLineNumberWidth(macro.Content);
        var lineHeight = ImGui.GetTextLineHeight();
        var editorPadding = 5.0f;

        // Use a single scrollable child window that contains both line numbers and text editor
        using var scrollableChild = ImRaii.Child("CodeEditorScrollable", new Vector2(0, height), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!scrollableChild) return;

        if (_showLineNumbers)
        {
            DrawLineNumbers(macro.Content, lineNumberWidth, lineHeight, editorPadding);
            ImGui.SameLine(0, 0);
        }

        DrawTextEditor(macro, lineHeight, editorPadding);
    }

    private float CalculateLineNumberWidth(string content) => content.Split('\n').Length switch
    {
        > 999 => 60,
        > 99 => 50,
        _ => 40
    };

    private void DrawLineNumbers(string content, float width, float height, float padding)
    {
        using var _ = ImRaii.PushColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.1f, 1.0f)).Push(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        using var __ = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0)).Push(ImGuiStyleVar.WindowPadding, new Vector2(padding, padding));

        var lines = content.Split('\n');
        var calculatedHeight = lines.Length * height + padding * 2;
        var availableHeight = ImGui.GetContentRegionAvail().Y;
        var totalHeight = Math.Max(calculatedHeight, availableHeight);

        // Create a child window for line numbers that doesn't scroll independently
        using var child = ImRaii.Child("LineNumbers", new Vector2(width, totalHeight), true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        if (!child) return;

        for (var i = 0; i < lines.Length; i++)
        {
            var textWidth = ImGui.CalcTextSize($"{i + 1}").X;
            ImGui.SetCursorPosX(width - textWidth - 6);
            ImGui.Text($"{i + 1}");
        }
    }

    private void DrawTextEditor(IMacro macro, float height, float padding)
    {
        using var _ = ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0.15f, 0.15f, 0.15f, 1.0f));
        using var __ = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(5, padding));

        var lines = macro.Content.Split('\n');
        var totalHeight = Math.Max(lines.Length * height + padding * 2, ImGui.GetContentRegionAvail().Y);
        var editorWidth = ImGui.GetContentRegionAvail().X;

        if (macro is ConfigMacro configMacro)
        {
            var contents = configMacro.Content;
            if (ImGui.InputTextMultiline("##MacroEditor", ref contents, 1_000_000, new Vector2(editorWidth, totalHeight), ImGuiInputTextFlags.AllowTabInput))
            {
                configMacro.Content = contents;
                C.Save();
            }
        }
    }

    private void DrawStatusBar(IMacro macro)
    {
        using var _ = ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));

        var lines = macro.Content.Split('\n').Length;
        var chars = macro.Content.Length;
        ImGuiEx.Text(ImGuiColors.DalamudGrey, $"Name: {macro.Name}  |  Lines: {lines}  |  Chars: {chars}  |  Type: {macro.Type}");

        if (macro is ConfigMacro { IsGitMacro: true } configMacro)
        {
            ImGui.SameLine(0, 0);
            ImGuiEx.Text(ImGuiColors.DalamudGrey, " | ");
            ImGui.SameLine(0, 0);
            ImGuiUtils.DrawLink(ImGuiColors.DalamudGrey, $"Git: {configMacro.GitInfo}", configMacro.GitInfo.RepositoryUrl);
        }
    }
}
