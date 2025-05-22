using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.Scheduler;
using SomethingNeedDoing.Utils;
using System.Numerics;

namespace SomethingNeedDoing.Gui;

/// <summary>
/// A detachable window that shows the status of all running macros
/// </summary>
public class MacroStatusWindow : Window
{
    private readonly IMacroScheduler _scheduler;
    private readonly MacroHierarchyManager _hierarchyManager;
    private bool _compactMode = true;
    
    // Time tracking for animation
    private float _elapsedSeconds = 0;

    public MacroStatusWindow(IMacroScheduler scheduler, MacroHierarchyManager hierarchyManager) 
        : base("Macros Status###SND_MacroStatus", 
            ImGuiWindowFlags.AlwaysAutoResize | 
            ImGuiWindowFlags.NoScrollbar)
    {
        _scheduler = scheduler;
        _hierarchyManager = hierarchyManager;
        
        // Smaller default size
        Size = new Vector2(280, 150);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        // Force compact mode by default
        _compactMode = true;
        
        // Always allow resizing regardless of content
        RespectCloseHotkey = true;
    }
    
    public override void Update()
    {
        // Update animation timer
        _elapsedSeconds += ImGui.GetIO().DeltaTime;
    }

    public override void Draw()
    {
        // Draw mode toggle button in title bar (more compact)
        float windowWidth = ImGui.GetWindowWidth();
        
        // Position closer to the right edge
        ImGui.SetCursorPos(new Vector2(windowWidth - 40, 2));
        
        // Draw just the icon for toggle button - no text
        using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button(_compactMode 
                ? FontAwesomeIcon.ExpandAlt.ToIconString() 
                : FontAwesomeIcon.CompressAlt.ToIconString()))
            {
                _compactMode = !_compactMode;
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(_compactMode ? "Expand view" : "Compact view");
            }
        }
        
        ImGui.SetCursorPos(new Vector2(0, 26)); // Reset cursor below title bar
        
        // Draw different content based on mode
        if (_compactMode)
            DrawCompactMode();
        else
            DrawDetailedMode();
    }
    
    private void DrawCompactMode()
    {
        // Get all running macros
        var runningMacros = _scheduler.GetMacros().ToList();
        if (!runningMacros.Any())
        {
            // More subtle message when no macros are running
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 0.7f), "No active macros");
            return;
        }
        
        // Filter to show only top-level macros
        var topLevelMacros = runningMacros
            .Where(m => !m.Id.Contains("_") || !runningMacros.Any(p => m.Id.StartsWith(p.Id + "_")))
            .ToList();
            
        // Create a more compact table
        ImGuiTableFlags tableFlags = ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit;
        if (ImGui.BeginTable("CompactMacroStatus", 3, tableFlags))
        {
            // Setup columns with more compact widths
            ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 25); // Status icon only
            ImGui.TableSetupColumn("Macro", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Actions", ImGuiTableColumnFlags.WidthFixed, 70); // Smaller action buttons
            
            // Draw each macro in compact form
            foreach (var macro in topLevelMacros)
            {
                DrawCompactMacroRow(macro);
                
                // Draw children if any
                var children = _hierarchyManager.GetChildMacros(macro.Id);
                foreach (var child in children)
                {
                    if (runningMacros.Any(m => m.Id == child.Id))
                    {
                        DrawCompactMacroRow(child, true);
                    }
                }
            }
            
            ImGui.EndTable();
        }
    }
    
    private void DrawCompactMacroRow(IMacro macro, bool isChild = false)
    {
        ImGui.TableNextRow();
        
        // Status icon column
        ImGui.TableNextColumn();
        DrawAnimatedStatusIcon(macro.State);
        
        // Name and state column combined
        ImGui.TableNextColumn();
        if (isChild)
        {
            ImGui.Indent(10);
            ImGui.Text("↳");
            ImGui.SameLine(0, 2);
        }
        
        // Show name with state in brackets
        ImGui.Text(macro.Name);
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.DalamudGrey, $"[{macro.State}]");
        
        if (isChild)
            ImGui.Unindent(10);
            
        // Actions column with smaller buttons
        ImGui.TableNextColumn();
        using (var _ = ImRaii.PushId(macro.Id))
        {
            // Control buttons
            var state = _scheduler.GetMacroState(macro.Id);
            float smallButtonSize = ImGui.GetFrameHeight() * 0.8f;
            
            // Set a smaller button style
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(2, 2));
            
            // Draw pause/resume button (icon only)
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(
                $"{(state == MacroState.Paused ? FontAwesomeIcon.Play : FontAwesomeIcon.Pause).ToIconString()}##btn_{macro.Id}_pause", 
                new Vector2(smallButtonSize)))
            {
                if (state == MacroState.Paused)
                    _scheduler.ResumeMacro(macro.Id);
                else
                    _scheduler.PauseMacro(macro.Id);
            }
            ImGui.PopFont();
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip(state == MacroState.Paused ? "Resume" : "Pause");
            }
            
            // Draw stop button (icon only)
            ImGui.SameLine(0, 4);
            ImGui.PushFont(UiBuilder.IconFont);
            if (ImGui.Button(
                $"{FontAwesomeIcon.Stop.ToIconString()}##btn_{macro.Id}_stop", 
                new Vector2(smallButtonSize)))
            {
                _scheduler.StopMacro(macro.Id);
            }
            ImGui.PopFont();
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("Stop");
            }
            
            ImGui.PopStyleVar();
        }
    }
    
    private void DrawDetailedMode()
    {
        // Add a ScrollRegion to prevent the window from expanding indefinitely
        using var scrollRegion = ImRaii.Child("DetailedMacroScrollRegion", new Vector2(-1, -1), false, ImGuiWindowFlags.AlwaysVerticalScrollbar);
        
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
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 0.7f), iconText);
            ImGui.PopFont();

            var text = "No active macros";
            var textSize = ImGui.CalcTextSize(text);
            ImGui.SetCursorPos(ImGui.GetCursorPos() + new Vector2(center.X - textSize.X / 2, center.Y));
            ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 0.7f), text);
            return;
        }

        // Filter to show only top-level macros
        var topLevelMacros = runningMacros
            .Where(m => !m.Id.Contains("_") || !runningMacros.Any(p => m.Id.StartsWith(p.Id + "_")))
            .ToList();
            
        // Draw detailed info for each top-level macro
        foreach (var macro in topLevelMacros)
        {
            string statusText = GetStatusText(macro.State);
            bool isOpen = ImGui.CollapsingHeader($"{statusText} {macro.Name}", ImGuiTreeNodeFlags.DefaultOpen);

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
                            bool childOpen = ImGui.CollapsingHeader($"{childStatusText} {child.Name}##child_{child.Id}", 
                                ImGuiTreeNodeFlags.DefaultOpen);
                            
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
                ImGui.Separator();
            }
        }
    }
    
    private void DrawDetailedMacroInfo(IMacro macro)
    {
        // More compact layout with grid alignment
        float labelWidth = 50;
        float lineHeight = ImGui.GetTextLineHeight() + 2;
        
        // Use a single row for status info
        ImGui.BeginGroup();
        
        // Status info - put on the same line with colored text
        ImGui.Text("Status:"); 
        ImGui.SameLine(labelWidth);
        
        // Show status with appropriate color
        Vector4 statusColor = macro.State switch
        {
            MacroState.Running => ImGuiColors.HealerGreen,
            MacroState.Paused => ImGuiColors.DalamudOrange,
            MacroState.Error => ImGuiColors.DalamudRed,
            MacroState.Completed => ImGuiColors.ParsedBlue,
            _ => ImGuiColors.DalamudGrey
        };
        ImGui.TextColored(statusColor, macro.State.ToString());
        
        // Type row on the same line for compactness
        ImGui.SameLine(labelWidth + 80);
        ImGui.Text("Type:"); 
        ImGui.SameLine(labelWidth + 120);
        ImGui.Text(macro.Type.ToString());
        
        // Only show loop info if it's a crafting loop
        if (macro.Metadata.CraftingLoop)
        {
            ImGui.Text("Loop:"); 
            ImGui.SameLine(labelWidth);
            ImGui.Text(macro.Metadata.CraftLoopCount < 0 ? "Infinite" : macro.Metadata.CraftLoopCount.ToString());
        }
        
        ImGui.EndGroup();
        
        ImGui.Spacing();
        ImGui.Separator();

        // Control buttons with proper FontAwesome icons
        var state = _scheduler.GetMacroState(macro.Id);
        
        // Define fixed button sizes to prevent expansion
        float buttonWidth = 75;
        
        // Ensure consistent button spacing
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(5, 0));
        
        // Resume/Pause button with fixed width
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
        if (ImGuiX.IconTextButton(
            FontAwesomeIcon.Stop, 
            "Stop", 
            new Vector2(buttonWidth, 0)))
        {
            _scheduler.StopMacro(macro.Id);
        }
        
        ImGui.SameLine();
        if (ImGuiX.IconTextButton(
            FontAwesomeIcon.Ban, 
            "Disable", 
            new Vector2(buttonWidth, 0)))
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
    
    private void DrawAnimatedStatusIcon(MacroState state)
    {
        // Use animation for Running state
        bool shouldAnimate = state == MacroState.Running;
        float pulse = shouldAnimate ? 0.5f + 0.5f * MathF.Sin(_elapsedSeconds * 3.0f) : 1.0f;
        
        // Set color based on state
        Vector4 color = state switch
        {
            MacroState.Running => new Vector4(0.0f, 1.0f, 0.0f, pulse),
            MacroState.Paused => new Vector4(1.0f, 0.65f, 0.0f, 1.0f),
            MacroState.Error => new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            MacroState.Completed => new Vector4(0.0f, 0.8f, 1.0f, 1.0f),
            _ => new Vector4(0.5f, 0.5f, 0.5f, 1.0f)
        };
        
        // Draw colored circle
        var drawList = ImGui.GetWindowDrawList();
        var pos = ImGui.GetCursorScreenPos() + new Vector2(10, 10);
        drawList.AddCircleFilled(pos, 7, ImGui.ColorConvertFloat4ToU32(color), 12);
    }
} 