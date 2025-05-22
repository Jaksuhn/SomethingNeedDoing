using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.Scheduler;
using SomethingNeedDoing.Utils;
using System.Numerics;
using System.Linq;

namespace SomethingNeedDoing.Gui;

public class RunningMacrosPanel
{
    private readonly IMacroScheduler _scheduler;
    private readonly MacroHierarchyManager _hierarchyManager;
    private static bool _isCollapsed = false;

    public RunningMacrosPanel(IMacroScheduler scheduler, MacroHierarchyManager hierarchyManager)
    {
        _scheduler = scheduler;
        _hierarchyManager = hierarchyManager;
    }

    public void Draw()
    {
        using var panel = ImRaii.Child("RunningMacrosPanel", new Vector2(-1, _isCollapsed ? 30 : 150));
        if (!panel) return;

        // Header with collapse button
        if (ImGuiX.IconTextButton(_isCollapsed ? FontAwesomeIcon.AngleDown : FontAwesomeIcon.AngleUp, 
                                 _isCollapsed ? "Running Macros" : "Running Macros"))
            _isCollapsed = !_isCollapsed;

        if (_isCollapsed) return;

        ImGui.Separator();

        // Get all running macros
        var runningMacros = _scheduler.GetMacros().ToList();
        if (runningMacros.Any())
        {
            // Filter out temporary macros that have parents in the list
            // We'll draw those as child elements
            var topLevelMacros = runningMacros
                .Where(m => !m.Id.Contains("_") || !runningMacros.Any(p => m.Id.StartsWith(p.Id + "_")))
                .ToList();

            foreach (var macro in topLevelMacros)
            {
                DrawMacroWithChildren(macro, runningMacros, 0);
            }
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudGrey, "No running macros");
        }
    }

    private void DrawMacroWithChildren(IMacro macro, List<IMacro> allMacros, int depth)
    {
        // Get indent based on depth
        float indentSize = 20.0f * depth;
        if (depth > 0)
            ImGui.Indent(indentSize);

        // Draw the macro itself
        DrawMacroControl(macro);

        // Get children for this macro
        var children = _hierarchyManager.GetChildMacros(macro.Id);
        if (children.Count > 0)
        {
            foreach (var child in children)
            {
                // Only draw children that are in the running macros list
                if (allMacros.Any(m => m.Id == child.Id))
                {
                    DrawMacroWithChildren(child, allMacros, depth + 1);
                }
            }
        }

        if (depth > 0)
            ImGui.Unindent(indentSize);
    }

    private void DrawMacroControl(IMacro macro)
    {
        using var _ = ImRaii.PushId(macro.Id);

        // Macro name and status
        ImGui.Text($"{macro.Name} [{macro.State}]");
        ImGui.SameLine(ImGui.GetWindowWidth() - 200);
        
        // Control buttons
        var state = _scheduler.GetMacroState(macro.Id);
        if (ImGuiX.IconTextButton(
            state == MacroState.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause, 
            state == MacroState.Paused ? "Resume" : "Pause"))
        {
            if (state == MacroState.Paused)
                _scheduler.ResumeMacro(macro.Id);
            else
                _scheduler.PauseMacro(macro.Id);
        }
        
        ImGui.SameLine();
        if (ImGuiX.IconTextButton(FontAwesomeIcon.Stop, "Stop"))
            _scheduler.StopMacro(macro.Id);
            
        ImGui.SameLine();
        if (ImGuiX.IconTextButton(FontAwesomeIcon.Ban, "Disable"))
        {
            macro.Metadata.TriggerEvents.Clear();
            C.Save();
        }
    }

    public void DrawDetailed()
    {
        // Get all running macros
        var runningMacros = _scheduler.GetMacros().ToList();
        if (!runningMacros.Any())
        {
            var center = ImGui.GetContentRegionAvail() / 2;

            // Display the desktop icon properly
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(center.X, center.Y - 30)); 
            ImGui.PushFont(UiBuilder.IconFont);
            var iconText = FontAwesomeIcon.Desktop.ToIconString();
            var iconSize = ImGui.CalcTextSize(iconText);
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(-iconSize.X / 2, 0));
            ImGui.TextColored(ImGuiColors.DalamudGrey, iconText);
            ImGui.PopFont();

            var text = "No running macros";
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(center.X - textSize.X / 2, center.Y));
            ImGui.TextColored(ImGuiColors.DalamudGrey, text);
            return;
        }

        // Filter out temporary macros that have parents in the list
        var topLevelMacros = runningMacros
            .Where(m => !m.Id.Contains("_") || !runningMacros.Any(p => m.Id.StartsWith(p.Id + "_")))
            .ToList();
            
        // Draw detailed info for each top-level macro and its children
        foreach (var macro in topLevelMacros)
        {
            string statusText = GetStatusText(macro.State);
            bool isOpen = ImGui.CollapsingHeader($"{statusText} {macro.Name}");

            if (isOpen)
            {
                ImGui.Indent(20.0f);
                
                // Draw details for this macro
                DrawDetailedMacroInfo(macro);
                
                // Draw child macros
                var children = _hierarchyManager.GetChildMacros(macro.Id);
                if (children.Count > 0)
                {
                    ImGui.Separator();
                    ImGui.TextColored(ImGuiColors.DalamudViolet, "Child Macros:");
                    
                    foreach (var child in children)
                    {
                        // Only show children that are currently running
                        if (runningMacros.Any(m => m.Id == child.Id))
                        {
                            string childStatusText = GetStatusText(child.State);
                            bool childOpen = ImGui.CollapsingHeader($"{childStatusText} {child.Name}##child_{child.Id}");
                            
                            if (childOpen)
                            {
                                ImGui.Indent(20.0f);
                                DrawDetailedMacroInfo(child);
                                ImGui.Unindent(20.0f);
                            }
                        }
                    }
                }
                
                ImGui.Unindent(20.0f);
            }
        }
    }

    private void DrawDetailedMacroInfo(IMacro macro)
    {
        // Draw more detailed information about the macro
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Status:");
        ImGui.SameLine();
        ImGui.Text(macro.State.ToString());

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Type:");
        ImGui.SameLine();
        ImGui.Text(macro.Type.ToString());
        
        if (macro.Metadata.CraftingLoop)
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Loop:");
            ImGui.SameLine();
            ImGui.Text(macro.Metadata.CraftLoopCount < 0 ? "Infinite" : macro.Metadata.CraftLoopCount.ToString());
        }

        ImGui.Spacing();
        ImGui.Separator();

        // Control buttons with proper FontAwesome icons
        var state = _scheduler.GetMacroState(macro.Id);
        
        // Define fixed button sizes to prevent expansion
        float buttonWidth = 110;
        
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(5, 0));
        
        if (ImGuiX.IconTextButton(
            state == MacroState.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause, 
            state == MacroState.Paused ? "Resume" : "Pause", 
            new Vector2(buttonWidth, 0)))
        {
            if (state == MacroState.Paused)
                _scheduler.ResumeMacro(macro.Id);
            else
                _scheduler.PauseMacro(macro.Id);
        }

        ImGui.SameLine();
        if (ImGuiX.IconTextButton(FontAwesomeIcon.Stop, "Stop", new Vector2(80, 0)))
        {
            _scheduler.StopMacro(macro.Id);
        }
        
        ImGui.SameLine();
        if (ImGuiX.IconTextButton(FontAwesomeIcon.Ban, "Disable", new Vector2(80, 0)))
        {
            macro.Metadata.TriggerEvents.Clear();
            C.Save();
        }
        
        ImGui.PopStyleVar();
    }

    private string GetStatusText(MacroState state)
    {
        FontAwesomeIcon icon = state switch
        {
            MacroState.Completed => FontAwesomeIcon.CheckCircle,
            MacroState.Running => FontAwesomeIcon.Play,
            MacroState.Paused => FontAwesomeIcon.Pause,
            MacroState.Error => FontAwesomeIcon.ExclamationTriangle,
            _ => FontAwesomeIcon.Circle
        };
        
        ImGui.PushFont(UiBuilder.IconFont);
        string iconText = icon.ToIconString();
        ImGui.PopFont();
        return iconText;
    }
}
