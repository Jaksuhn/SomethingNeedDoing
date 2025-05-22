using Dalamud.Interface.Windowing;
using Dalamud.Interface;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.Scheduler;
using SomethingNeedDoing.Utils;
using System.Numerics;

namespace SomethingNeedDoing.Gui;

public class RunningMacrosTab : Window
{
    private readonly RunningMacrosPanel _panel;
    private readonly IMacroScheduler _scheduler;
    private readonly MacroHierarchyManager _hierarchyManager;
    private readonly MacroStatusWindow _statusWindow;

    public RunningMacrosTab(IMacroScheduler scheduler, MacroHierarchyManager hierarchyManager, MacroStatusWindow statusWindow) 
        : base("Running Macros", ImGuiWindowFlags.NoScrollbar)
    {
        _scheduler = scheduler;
        _hierarchyManager = hierarchyManager;
        _statusWindow = statusWindow;
        _panel = new RunningMacrosPanel(scheduler, hierarchyManager);
        
        Size = new Vector2(600, 400);
        SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        // Add a button to open the status window in a floating mode
        using var header = ImRaii.Child("StatusWindowHeader", new Vector2(-1, 40), false);
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.1f, 0.1f, 0.1f, 1.0f));
        
        ImGui.Text("Running Macros");
        ImGui.SameLine(ImGui.GetWindowWidth() - 230);
        
        if (ImGuiX.IconTextButton(FontAwesomeIcon.ExternalLinkAlt, "Open Status Window"))
        {
            _statusWindow.IsOpen = true;
            _statusWindow.BringToFront();
        }
        
        ImGui.PopStyleColor();
        
        // Draw the running macros panel at full size
        _panel.DrawDetailed();
        
        // Optional: Draw macro hierarchy tree below
        DrawMacroHierarchy();
    }

    private void DrawMacroHierarchy()
    {
        if (ImGui.CollapsingHeader("Macro Hierarchy", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var macros = _scheduler.GetMacros();
            if (!macros.Any())
            {
                ImGui.TextColored(new Vector4(0.5f, 0.5f, 0.5f, 1.0f), "No running macros");
                return;
            }

            foreach (var macro in macros)
            {
                // Only show parent macros in the hierarchy view
                if (_hierarchyManager.GetParentMacro(macro.Id) == null)
                {
                    DrawMacroNode(macro);
                }
            }
        }
    }

    private void DrawMacroNode(IMacro macro)
    {
        var children = _hierarchyManager.GetChildMacros(macro.Id);
        bool hasChildren = children.Any();
        
        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.None;
        if (!hasChildren)
            flags |= ImGuiTreeNodeFlags.Leaf;

        bool isOpen = ImGui.TreeNodeEx($"{macro.Name} ({macro.State})", flags);
        
        if (isOpen)
        {
            if (hasChildren)
            {
                foreach (var child in children)
                {
                    DrawMacroNode(child);
                }
            }
            
            ImGui.TreePop();
        }
    }
} 