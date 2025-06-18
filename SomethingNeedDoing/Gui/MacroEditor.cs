using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Managers;
using System.Threading.Tasks;
using ImGuiNET;
using System.Numerics;

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
    private int _lastCursorPos = -1;
    private int _currentCursorLine = 0;
    private string _editorId = "##MacroEditor";

    // Constants
    private const float EditorPadding = 5.0f;
    private const int TopScrollMargin = 2;
    private const int BottomScrollMargin = 3;
    private const int MaxTextLength = 1_000_000;
    private const int LineNumberRightPadding = 6;
    
    // UI Layout constants
    private const int GitMacroToolbarWidth = 145;
    private const int RegularToolbarWidth = 120;
    private const int ToolbarHeightMultiplier = 2;

    private enum UpdateState
    {
        Unknown,
        None,
        Available
    }

    public void Draw(IMacro? macro)
    {
        using var rightPanel = ImRaii.Child("RightPanel", new Vector2(0, -1), false);
        if (!rightPanel) return;

        if (macro == null)
        {
            DrawEmptyState();
            return;
        }

        DrawEditorToolbar(macro);
        ImGui.Separator();

        var availableSpace = ImGui.GetContentRegionAvail().Y;
        var statusBarSpace = ImGui.GetFrameHeightWithSpacing() * ToolbarHeightMultiplier;
        var editorHeight = availableSpace - statusBarSpace;
        
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
        var toolbarHeight = ImGui.GetFrameHeight() * ToolbarHeightMultiplier;
        using var toolbar = ImRaii.Child("ToolbarChild", new Vector2(-1, toolbarHeight), false);
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
        var isGitMacro = macro is ConfigMacro { IsGitMacro: true };
        var toolbarWidth = isGitMacro ? GitMacroToolbarWidth : RegularToolbarWidth;
        var rightAlignment = ImGui.GetWindowWidth() - toolbarWidth;
        
        ImGui.SameLine(rightAlignment);

        using var greyText = ImRaii.PushColor(ImGuiCol.Text, ImGuiColors.DalamudGrey);

        DrawRunningMacrosButton();
        DrawLineNumberToggle();
        DrawSyntaxHighlightToggle();
        
        if (isGitMacro)
            DrawGitUpdateButton((ConfigMacro)macro);
    }

    private void DrawRunningMacrosButton()
    {
        var runningMacros = _scheduler.GetMacros().ToList();
        var macroCount = runningMacros.Count;
        var hasRunningMacros = macroCount > 0;
        
        var statusColor = hasRunningMacros ? ImGuiColors.HealerGreen : ImGuiColors.DalamudGrey;
        var statusIcon = hasRunningMacros ? FontAwesomeIcon.Play : FontAwesomeIcon.Desktop;
        var statusText = hasRunningMacros ? $"{macroCount} running" : "No macros running";

        using (ImRaii.PushColor(ImGuiCol.Text, statusColor))
        {
            if (ImGuiUtils.IconButton(statusIcon, statusText))
                ws.Toggle<StatusWindow>();
        }
    }

    private void DrawLineNumberToggle()
    {
        ImGui.SameLine();
        var icon = _showLineNumbers ? FontAwesomeHelper.IconSortAsc : FontAwesomeHelper.IconSortDesc;
        if (ImGuiUtils.IconButton(icon, "Toggle Line Numbers"))
            _showLineNumbers = !_showLineNumbers;
    }

    private void DrawSyntaxHighlightToggle()
    {
        ImGui.SameLine();
        var icon = _highlightSyntax ? FontAwesomeHelper.IconCheck : FontAwesomeHelper.IconXmark;
        if (ImGuiUtils.IconButton(icon, "Syntax Highlighting (not currently available)"))
            _highlightSyntax = !_highlightSyntax;
    }

    private void DrawGitUpdateButton(ConfigMacro configMacro)
    {
        ImGui.SameLine();
        var (indicator, color, tooltip) = GetUpdateIndicatorInfo();

        if (ImGuiUtils.IconButtonWithNotification(FontAwesomeIcon.Bell, indicator, color, tooltip))
        {
            if (_updateState == UpdateState.Available)
                Task.Run(async () => await _gitManager.UpdateMacro(configMacro));
            else
                Task.Run(async () => await CheckForUpdates(configMacro));
        }
    }

    private (string indicator, Vector4 color, string tooltip) GetUpdateIndicatorInfo()
    {
        return _updateState switch
        {
            UpdateState.None => ("0", ImGuiColors.DalamudGrey, "No updates available"),
            UpdateState.Available => ("1", ImGuiColors.DPSRed, "Update available (click to update)"),
            _ => ("?", ImGuiColors.DalamudGrey, "Check for updates")
        };
    }

    private async Task CheckForUpdates(ConfigMacro configMacro)
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
    }

    private void DrawCodeEditor(IMacro macro, float height)
    {
        var lineNumberWidth = CalculateLineNumberWidth(macro.Content);
        var lineHeight = ImGui.CalcTextSize("A").Y;

        using var scrollChild = ImRaii.Child("CodeEditorScrollable", new Vector2(0, height), false, ImGuiWindowFlags.HorizontalScrollbar);
        if (!scrollChild) return;

        if (_showLineNumbers)
        {
            DrawLineNumbers(macro.Content, lineNumberWidth, lineHeight, EditorPadding);
            ImGui.SameLine(0, 0);
        }

        DrawTextEditor(macro, lineHeight, EditorPadding);
        HandleCursorTracking(lineHeight, EditorPadding, height);
    }

    private float CalculateLineNumberWidth(string content)
    {
        var lineCount = content.Split('\n').Length;
        return lineCount switch
        {
            > 999 => 60,
            > 99 => 50,
            _ => 40
        };
    }

    private void DrawLineNumbers(string content, float width, float lineHeight, float padding)
    {
        var lineNumberBackground = new Vector4(0.1f, 0.1f, 0.1f, 1.0f);
        
        using var colors = ImRaii.PushColor(ImGuiCol.ChildBg, lineNumberBackground)
            .Push(ImGuiCol.Text, ImGuiColors.DalamudGrey);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(0, 0))
            .Push(ImGuiStyleVar.WindowPadding, new Vector2(padding, padding))
            .Push(ImGuiStyleVar.FramePadding, new Vector2(5, padding));

        var lines = content.Split('\n');
        var calculatedHeight = lines.Length * lineHeight + padding * 2;
        var availableHeight = ImGui.GetContentRegionAvail().Y;
        var totalHeight = Math.Max(calculatedHeight, availableHeight);

        var childFlags = ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse;
        using var child = ImRaii.Child("LineNumbers", new Vector2(width, totalHeight), true, childFlags);
        if (!child) return;

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = $"{i + 1}";
            var textWidth = ImGui.CalcTextSize(lineNumber).X;
            var rightAlignedX = width - textWidth - LineNumberRightPadding;
            
            ImGui.SetCursorPosX(rightAlignedX);
            ImGui.Text(lineNumber);
        }
    }

    private unsafe void DrawTextEditor(IMacro macro, float lineHeight, float padding)
    {
        var editorBackground = new Vector4(0.15f, 0.15f, 0.15f, 1.0f);
        var framePadding = new Vector2(5, padding);
        
        using var colors = ImRaii.PushColor(ImGuiCol.FrameBg, editorBackground);
        using var styles = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, framePadding);

        if (macro is not ConfigMacro configMacro) return;

        var contents = configMacro.Content;
        var lines = contents.Split('\n');
        var textHeight = lines.Length * lineHeight + padding * 2;
        var availableHeight = ImGui.GetContentRegionAvail().Y;
        var totalHeight = Math.Max(textHeight, availableHeight);
        var editorWidth = ImGui.GetContentRegionAvail().X;
        
        var inputFlags = ImGuiInputTextFlags.AllowTabInput | ImGuiInputTextFlags.CallbackAlways;
        var hasContentChanged = ImGui.InputTextMultiline(_editorId, ref contents, MaxTextLength, 
            new Vector2(editorWidth, totalHeight), inputFlags, UpdateCursorPosition);

        if (hasContentChanged)
        {
            configMacro.Content = contents;
            C.Save();
        }
    }

    private unsafe int UpdateCursorPosition(ImGuiInputTextCallbackData* data)
    {
        var ptr = new ImGuiInputTextCallbackDataPtr(data);
        
        if (ptr.CursorPos == _lastCursorPos) return 0;
        
        _lastCursorPos = ptr.CursorPos;
        
        // Count newlines to find current line
        _currentCursorLine = 0;
        for (int i = 0; i < Math.Min(ptr.CursorPos, ptr.BufTextLen); i++)
        {
            unsafe
            {
                if (((byte*)ptr.Buf)[i] == '\n')
                    _currentCursorLine++;
            }
        }
        
        return 0;
    }

    private void HandleCursorTracking(float lineHeight, float padding, float viewportHeight)
    {
        if (!ImGui.IsItemActive() && !ImGui.IsItemFocused()) return;
        
        var scrollInfo = GetScrollInfo();
        var visibleLines = CalculateVisibleLines(scrollInfo, lineHeight);
        
        if (ShouldScrollUp(visibleLines))
            ScrollUp(visibleLines, lineHeight);
        else if (ShouldScrollDown(visibleLines))
            ScrollDown(visibleLines, lineHeight, scrollInfo);
    }

    private (float scrollY, float maxScrollY, float windowHeight) GetScrollInfo()
    {
        return (ImGui.GetScrollY(), ImGui.GetScrollMaxY(), ImGui.GetWindowHeight());
    }

    private (int top, int bottom) CalculateVisibleLines((float scrollY, float maxScrollY, float windowHeight) scrollInfo, float lineHeight)
    {
        var topVisibleLine = (int)(scrollInfo.scrollY / lineHeight);
        var bottomVisiblePixel = scrollInfo.scrollY + scrollInfo.windowHeight;
        var bottomVisibleLine = (int)(bottomVisiblePixel / lineHeight);
        
        return (topVisibleLine, bottomVisibleLine);
    }

    private bool ShouldScrollUp((int top, int bottom) visibleLines)
    {
        return _currentCursorLine < visibleLines.top + TopScrollMargin;
    }

    private bool ShouldScrollDown((int top, int bottom) visibleLines)
    {
        return _currentCursorLine > visibleLines.bottom - BottomScrollMargin;
    }

    private void ScrollUp((int top, int bottom) visibleLines, float lineHeight)
    {
        var targetLine = Math.Max(0, _currentCursorLine - TopScrollMargin);
        var newScrollY = targetLine * lineHeight;
        ImGui.SetScrollY(Math.Max(0, newScrollY));
    }

    private void ScrollDown((int top, int bottom) visibleLines, float lineHeight, (float scrollY, float maxScrollY, float windowHeight) scrollInfo)
    {
        var targetVisibleLine = visibleLines.bottom - BottomScrollMargin;
        var scrollAdjustment = (_currentCursorLine - targetVisibleLine) * lineHeight;
        var newScrollY = scrollInfo.scrollY + scrollAdjustment;
        ImGui.SetScrollY(Math.Min(scrollInfo.maxScrollY, Math.Max(0, newScrollY)));
    }

    private void Copy(string content)
    {
        ImGui.SetClipboardText(content);
    }

    private void DrawStatusBar(IMacro macro)
    {
        using var _ = ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));

        var lines = macro.Content.Split('\n').Length;
        var chars = macro.Content.Length;
        ImGuiEx.Text(ImGuiColors.DalamudGrey, $"Name: {macro.Name}  |  Lines: {lines}  |  Chars: {chars}  |  Type: {macro.Type}  |  Cursor: {_currentCursorLine + 1}");

        if (macro is ConfigMacro { IsGitMacro: true } configMacro)
        {
            ImGui.SameLine(0, 0);
            ImGuiEx.Text(ImGuiColors.DalamudGrey, " | ");
            ImGui.SameLine(0, 0);
            ImGuiUtils.DrawLink(ImGuiColors.DalamudGrey, $"Git: {configMacro.GitInfo}", configMacro.GitInfo.RepositoryUrl);
        }
    }
}