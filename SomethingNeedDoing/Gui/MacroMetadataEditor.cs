using Dalamud.Interface.Colors;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using SomethingNeedDoing.Framework;
using SomethingNeedDoing.Framework.Interfaces;
using System;
using System.Numerics;

namespace SomethingNeedDoing.Gui;

/// <summary>
/// Enhanced MacroMetadataEditor with better UI and IDE-like experience
/// </summary>
public class MacroMetadataEditor
{
    // Add tab state for better organization
    private enum MetadataTab
    {
        General,
        Crafting,
        Triggers
    }
    
    private MetadataTab _currentTab = MetadataTab.General;
    
    public void Draw(IMacro macro)
    {
        if (macro?.Metadata == null)
            return;

        // Tab bar for metadata sections
        DrawMetadataTabs();
        
        // Spacer
        ImGui.Spacing();
        
        // Draw a container with padding and styling
        ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(10, 10));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.15f, 0.15f, 0.15f, 0.5f));
        
        // Full width container for metadata content
        using var contentChild = ImRaii.Child("MetadataContent", new Vector2(-1, 250), true);
        
        // Draw the appropriate tab content
        switch (_currentTab)
        {
            case MetadataTab.General:
                DrawGeneralTab(macro);
                break;
            case MetadataTab.Crafting:
                DrawCraftingTab(macro);
                break;
            case MetadataTab.Triggers:
                DrawTriggersTab(macro);
                break;
        }
        
        ImGui.PopStyleColor();
        ImGui.PopStyleVar();
    }
    
    private void DrawMetadataTabs()
    {
        float tabWidth = 100;
        float windowWidth = ImGui.GetContentRegionAvail().X;
        float startPos = (windowWidth - (tabWidth * 3)) / 2; // Center the tabs
        
        ImGui.SetCursorPosX(startPos);
        
        // Create styled tabs
        ImGui.PushStyleColor(ImGuiCol.Button, _currentTab == MetadataTab.General 
            ? ImGuiColors.ParsedPurple 
            : new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
        
        if (ImGui.Button("General", new Vector2(tabWidth, 0)))
            _currentTab = MetadataTab.General;
            
        ImGui.PopStyleColor();
        
        ImGui.SameLine();
        
        ImGui.PushStyleColor(ImGuiCol.Button, _currentTab == MetadataTab.Crafting 
            ? ImGuiColors.ParsedPurple 
            : new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            
        if (ImGui.Button("Crafting", new Vector2(tabWidth, 0)))
            _currentTab = MetadataTab.Crafting;
            
        ImGui.PopStyleColor();
        
        ImGui.SameLine();
        
        ImGui.PushStyleColor(ImGuiCol.Button, _currentTab == MetadataTab.Triggers 
            ? ImGuiColors.ParsedPurple 
            : new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            
        if (ImGui.Button("Triggers", new Vector2(tabWidth, 0)))
            _currentTab = MetadataTab.Triggers;
            
        ImGui.PopStyleColor();
    }
    
    private void DrawGeneralTab(IMacro macro)
    {
        // Create a nicer form layout with headers - without using custom fonts
        ImGui.TextColored(ImGuiColors.DalamudViolet, "General Information");
        
        ImGui.Separator();
        ImGui.Spacing();
        
        // Draw with better spacing and layout
        float labelWidth = 100;
        float spacing = 10;
        float inputWidth = ImGui.GetContentRegionAvail().X - labelWidth - spacing - 20;
        
        // Author field with nicer styling
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Author:");
        ImGui.SameLine(labelWidth + spacing);

        var author = macro.Metadata.Author ?? string.Empty;
        ImGui.SetNextItemWidth(inputWidth);
        
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
        bool authorChanged = ImGui.InputText("##Author", ref author, 100);
        ImGui.PopStyleColor();
        
        if (authorChanged)
        {
            macro.Metadata.Author = author;
            C.Save();
        }

        ImGui.Spacing();
        
        // Version field
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Version:");
        ImGui.SameLine(labelWidth + spacing);

        var version = macro.Metadata.Version ?? string.Empty;
        ImGui.SetNextItemWidth(inputWidth);
        
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
        bool versionChanged = ImGui.InputText("##Version", ref version, 50);
        ImGui.PopStyleColor();
        
        if (versionChanged)
        {
            macro.Metadata.Version = version;
            C.Save();
        }

        ImGui.Spacing();
        
        // Description field
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Description:");
        
        // Full width multiline input for description
        ImGui.Spacing();
        var description = macro.Metadata.Description ?? string.Empty;
        
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
        bool descChanged = ImGui.InputTextMultiline("##Description", ref description, 1000, new Vector2(-1, 100));
        ImGui.PopStyleColor();
        
        if (descChanged)
        {
            macro.Metadata.Description = description;
            C.Save();
        }
    }
    
    private void DrawCraftingTab(IMacro macro)
    {
        // Header for crafting section - without custom fonts
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Crafting Settings");
        
        ImGui.Separator();
        ImGui.Spacing();
        
        float labelWidth = 120;
        float spacing = 10;
        
        // Crafting Loop checkbox with better styling
        ImGui.AlignTextToFramePadding();
        ImGui.Text("Enable Crafting Loop:");
        ImGui.SameLine(labelWidth + spacing);

        var craftingLoop = macro.Metadata.CraftingLoop;
        
        // Better looking checkbox
        if (ImGuiComponents.ToggleButton("##CraftingLoop", ref craftingLoop))
        {
            macro.Metadata.CraftingLoop = craftingLoop;
            C.Save();
        }
        
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.HealerGreen, craftingLoop ? "Enabled" : "Disabled");

        // Show loop settings only if crafting loop is enabled
        if (craftingLoop)
        {
            ImGui.Spacing();
            ImGui.Spacing();
            
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Loop Count:");
            ImGui.SameLine(labelWidth + spacing);

            ImGui.SetNextItemWidth(100);
            var craftLoopCount = macro.Metadata.CraftLoopCount;
            
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.2f, 0.2f, 0.2f, 1.0f));
            if (ImGui.InputInt("##LoopCount", ref craftLoopCount))
            {
                if (craftLoopCount < -1)
                    craftLoopCount = -1;

                macro.Metadata.CraftLoopCount = craftLoopCount;
                C.Save();
            }
            ImGui.PopStyleColor();

            ImGui.SameLine();
            ImGui.TextColored(ImGuiColors.DalamudGrey, "(-1 = infinite)");
            
            // Add helpful tip
            ImGui.Spacing();
            ImGui.TextWrapped("When crafting loop is enabled, the macro will automatically repeat for the specified number of crafts.");
        }
    }
    
    private void DrawTriggersTab(IMacro macro)
    {
        // Header for triggers section
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Trigger Events");
        
        ImGui.Separator();
        ImGui.Spacing();
        
        // Create a scrollable area for triggers since we have many options
        using var scrollRegion = ImRaii.Child("TriggersScroll", new Vector2(-1, -1), false);
        
        // Add a description
        ImGui.TextWrapped("Select the events that will automatically trigger this macro to run:");
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
        
        // Helper method to add a trigger toggle
        void AddTriggerToggle(TriggerEvent triggerEvent, string label, string tooltip = "")
        {
            bool isEnabled = macro.Metadata.TriggerEvents.Contains(triggerEvent);
            
            if (ImGuiComponents.ToggleButton($"##{triggerEvent}", ref isEnabled))
            {
                if (isEnabled)
                    macro.Metadata.TriggerEvents.Add(triggerEvent);
                else
                    macro.Metadata.TriggerEvents.Remove(triggerEvent);
                    
                C.Save();
            }
            
            ImGui.SameLine();
            ImGui.Text(label);
            
            if (!string.IsNullOrEmpty(tooltip) && ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
            
            ImGui.Spacing();
        }
        
        // Auto Retainer trigger
        AddTriggerToggle(
            TriggerEvent.OnAutoRetainerCharacterPostProcess, 
            "Run after Auto Retainer completes",
            "Trigger this macro after Auto Retainer finishes processing a character"
        );
        
        // Login trigger
        AddTriggerToggle(
            TriggerEvent.OnLogin, 
            "Run on login",
            "Trigger this macro when you log into the game"
        );
        
        // Logout trigger
        AddTriggerToggle(
            TriggerEvent.OnLogout, 
            "Run on logout",
            "Trigger this macro when you log out of the game"
        );
        
        // Territory change trigger
        AddTriggerToggle(
            TriggerEvent.OnTerritoryChange, 
            "Run on zone change",
            "Trigger this macro when you change zones"
        );
        
        // Combat start trigger
        AddTriggerToggle(
            TriggerEvent.OnCombatStart, 
            "Run when combat starts",
            "Trigger this macro when you enter combat"
        );
        
        // Combat end trigger
        AddTriggerToggle(
            TriggerEvent.OnCombatEnd, 
            "Run when combat ends",
            "Trigger this macro when you exit combat"
        );
        
        // Condition change trigger
        AddTriggerToggle(
            TriggerEvent.OnConditionChange, 
            "Run when conditions change",
            "Trigger this macro when game conditions change (mounted, crafting, etc)"
        );
        
        // Chat message trigger
        AddTriggerToggle(
            TriggerEvent.OnChatMessage, 
            "Run on chat messages",
            "Trigger this macro when specific chat messages are received"
        );
        
        // Update trigger (warning - this runs every frame)
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
        AddTriggerToggle(
            TriggerEvent.OnUpdate, 
            "Run every frame (WARNING: Performance impact)",
            "Triggers on every frame update. Use with caution as this can impact performance."
        );
        ImGui.PopStyleColor();
        
        // Addon event trigger - more complex, requires additional config
        if (macro.Metadata.AddonEventConfig == null)
            macro.Metadata.AddonEventConfig = new AddonEventConfig();
            
        bool addonEventEnabled = macro.Metadata.TriggerEvents.Contains(TriggerEvent.OnAddonEvent);
        
        if (ImGuiComponents.ToggleButton($"##{TriggerEvent.OnAddonEvent}", ref addonEventEnabled))
        {
            if (addonEventEnabled)
                macro.Metadata.TriggerEvents.Add(TriggerEvent.OnAddonEvent);
            else
                macro.Metadata.TriggerEvents.Remove(TriggerEvent.OnAddonEvent);
                
            C.Save();
        }
        
        ImGui.SameLine();
        ImGui.Text("Run on addon events");
        
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Trigger this macro when specific game UI elements appear or change");
        
        // Show addon event config if enabled
        if (addonEventEnabled)
        {
            ImGui.Indent(20);
            
            // Addon name
            ImGui.Text("Addon Name:");
            ImGui.SameLine(100);
            
            var addonName = macro.Metadata.AddonEventConfig.AddonName ?? string.Empty;
            ImGui.SetNextItemWidth(150);
            
            if (ImGui.InputText("##AddonName", ref addonName, 100))
            {
                macro.Metadata.AddonEventConfig.AddonName = addonName;
                C.Save();
            }
            
            ImGui.SameLine();
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("The name of the game UI element to watch (e.g. 'Talk', 'SelectString')");
            
            // Event type dropdown
            ImGui.Text("Event Type:");
            ImGui.SameLine(100);
            
            var eventTypeNames = Enum.GetNames(typeof(MacroAddonEvent));
            var currentEventIndex = (int)macro.Metadata.AddonEventConfig.EventType;
            ImGui.SetNextItemWidth(150);
            
            if (ImGui.Combo("##EventType", ref currentEventIndex, eventTypeNames, eventTypeNames.Length))
            {
                macro.Metadata.AddonEventConfig.EventType = (MacroAddonEvent)currentEventIndex;
                C.Save();
            }
            
            ImGui.Unindent(20);
        }
    }
}
