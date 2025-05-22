using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.Utils;
using System;
using System.Numerics;
using System.Linq;

namespace SomethingNeedDoing.Gui;

/// <summary>
/// A compact status indicator that can be embedded in the titlebar or other small spaces
/// to show macro execution status
/// </summary>
public class MacroStatusIndicator
{
    private readonly IMacroScheduler _scheduler;
    private readonly MacroStatusWindow? _statusWindow;

    public MacroStatusIndicator(IMacroScheduler scheduler, MacroStatusWindow? statusWindow = null)
    {
        _scheduler = scheduler;
        _statusWindow = statusWindow;
    }

    public void Draw(float width, float height)
    {
        var runningMacros = _scheduler.GetMacros();
        int macroCount = runningMacros.Count();
        int runningCount = 0;
        int pausedCount = 0;
        int completedCount = 0;
        int errorCount = 0;

        foreach (var macro in runningMacros)
        {
            switch (macro.State)
            {
                case MacroState.Running:
                    runningCount++;
                    break;
                case MacroState.Paused:
                    pausedCount++;
                    break;
                case MacroState.Completed:
                    completedCount++;
                    break;
                case MacroState.Error:
                    errorCount++;
                    break;
            }
        }

        // Draw the background
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos();
        var statusColor = GetStatusColor(runningCount, pausedCount, errorCount);
        
        // Store original cursor position
        var originalPos = ImGui.GetCursorPos();
        
        // Draw clickable indicator with hover effect
        bool isHovered = false;
        
        // Make a invisible button over the area for interaction
        ImGui.InvisibleButton("##StatusIndicatorButton", new Vector2(width, height));
        isHovered = ImGui.IsItemHovered();
        
        // If clicked and status window exists, toggle it
        if (ImGui.IsItemClicked() && _statusWindow != null)
        {
            _statusWindow.IsOpen = !_statusWindow.IsOpen;
            if (_statusWindow.IsOpen)
            {
                _statusWindow.BringToFront();
            }
        }
        
        // Change color on hover
        if (isHovered)
        {
            statusColor = new Vector4(statusColor.X + 0.1f, statusColor.Y + 0.1f, statusColor.Z + 0.1f, statusColor.W);
        }
        
        // Draw the background with the calculated color
        drawList.AddRectFilled(pos, pos + new Vector2(width, height), ImGui.ColorConvertFloat4ToU32(statusColor), 4);
        
        // Get appropriate status icon
        FontAwesomeIcon statusIcon = GetStatusIcon(runningCount, pausedCount, completedCount, errorCount);
        string statusText = GetStatusText(macroCount, runningCount, pausedCount, completedCount, errorCount);
        
        // Restore the original cursor position for drawing the content
        ImGui.SetCursorPos(originalPos);
        
        // Draw the icon and text with proper font handling
        var padding = 5f;
        float originalCursorPosX = ImGui.GetCursorPosX();
        float originalCursorPosY = ImGui.GetCursorPosY();
        
        // Create a temporary position for positioning the icon and text
        ImGui.SetCursorScreenPos(new Vector2(pos.X + padding, pos.Y + (height - ImGui.GetTextLineHeight()) / 2));
        
        // Draw the icon with the proper font
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextColored(new Vector4(1, 1, 1, 1), statusIcon.ToIconString());
        ImGui.PopFont();
        
        // Get the width of the icon to position the text
        float iconWidth = ImGui.GetItemRectSize().X;
        
        // Draw the text right after the icon
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(1, 1, 1, 1), statusText);
        
        // Add a tooltip on hover
        if (isHovered)
        {
            ImGui.BeginTooltip();
            ImGui.Text("Click to toggle Status Window");
            if (macroCount > 0)
            {
                ImGui.Separator();
                if (runningCount > 0)
                    ImGui.Text($"Running: {runningCount}");
                if (pausedCount > 0)
                    ImGui.Text($"Paused: {pausedCount}");
                if (completedCount > 0)
                    ImGui.Text($"Completed: {completedCount}");
                if (errorCount > 0)
                    ImGui.Text($"Errors: {errorCount}");
            }
            ImGui.EndTooltip();
        }
        
        // Restore the original cursor position
        ImGui.SetCursorPos(new Vector2(originalCursorPosX, originalCursorPosY));
    }

    private Vector4 GetStatusColor(int runningCount, int pausedCount, int errorCount)
    {
        if (errorCount > 0)
            return ImGuiColors.DalamudRed;
        if (pausedCount > 0)
            return ImGuiColors.DalamudOrange;
        if (runningCount > 0)
            return ImGuiColors.HealerGreen;
        
        return ImGuiColors.DalamudGrey;
    }
    
    private FontAwesomeIcon GetStatusIcon(int runningCount, int pausedCount, int completedCount, int errorCount)
    {
        if (errorCount > 0)
            return FontAwesomeHelper.IconErrorStatus;
        if (pausedCount > 0)
            return FontAwesomeHelper.IconPausedStatus;
        if (runningCount > 0)
            return FontAwesomeHelper.IconRunning;
        if (completedCount > 0)
            return FontAwesomeHelper.IconCompletedStatus;
            
        return FontAwesomeHelper.IconMacros;
    }

    private string GetStatusText(int macroCount, int runningCount, int pausedCount, int completedCount, int errorCount)
    {
        if (macroCount == 0)
            return "No macros running";
        
        if (errorCount > 0)
            return $"{errorCount} error(s)";
        
        if (pausedCount > 0)
            return $"{pausedCount} paused";
        
        if (runningCount > 0)
            return $"{runningCount} running";
        
        if (completedCount > 0)
            return $"{completedCount} completed";
        
        return $"{macroCount} macros";
    }
}
