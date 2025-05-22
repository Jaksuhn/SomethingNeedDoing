using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Newtonsoft.Json;
using SomethingNeedDoing.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace SomethingNeedDoing.Gui;
public class MigrationPreviewWindow : Window
{
    private readonly WindowSystem _ws;
    private readonly string _oldConfigJson;
    private readonly Dictionary<string, (string OldValue, string NewValue, bool Selected)> changes = [];
    private readonly Dictionary<string, (ConfigMacro Macro, bool Selected)> newMacros = [];
    private readonly Dictionary<string, (ConfigMacro Macro, bool Selected)> removedMacros = [];
    private bool migrationValid = true;
    private string errorMessage = string.Empty;
    private bool selectAllNewMacros = true;
    private bool selectAllRemovedMacros = true;
    private bool selectAllChanges = true;

    public MigrationPreviewWindow(WindowSystem ws, string oldConfigJson) : base("Migration Preview", ImGuiWindowFlags.AlwaysAutoResize | ImGuiWindowFlags.NoSavedSettings)
    {
        Svc.Log.Debug("MigrationPreviewWindow constructor called");
        _ws = ws;
        _oldConfigJson = oldConfigJson;
        Size = new Vector2(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        PreviewMigration();
        Svc.Log.Debug($"MigrationPreviewWindow initialized. Migrating {newMacros.Count} new macros, {removedMacros.Count} removed macros.");
    }
    public override void OnClose() => _ws.RemoveWindow(this);

    /// <summary>
    /// Brings this window to the front of the window stack
    /// </summary>
    public new void BringToFront()
    {
        // Call the base implementation first
        base.BringToFront();
        
        // Make sure window is open
        IsOpen = true;
        
        // Force window to foreground by setting focus flag
        Flags |= ImGuiWindowFlags.NoSavedSettings;
        
        // Log that we're trying to bring the window to front
        Svc.Log.Debug("Attempting to bring migration window to front");
    }

    private void PreviewMigration()
    {
        try
        {
            var oldConfig = JsonConvert.DeserializeObject<dynamic>(_oldConfigJson);
            if (oldConfig == null)
            {
                migrationValid = false;
                errorMessage = "Failed to parse old configuration";
                return;
            }

            // Log the old config structure for debugging
            Svc.Log.Debug($"Old config type: {oldConfig.GetType().Name}");

            // Preview general settings
            AddChange("LockWindow", oldConfig.LockWindow?.ToString(), C.LockWindow.ToString());
            AddChange("DisableMonospaced", oldConfig.DisableMonospaced?.ToString(), C.DisableMonospaced.ToString());
            AddChange("ChatType", oldConfig.ChatType?.ToString(), C.ChatType.ToString());
            AddChange("ErrorChatType", oldConfig.ErrorChatType?.ToString(), C.ErrorChatType.ToString());

            // Preview macros
            var oldMacros = new HashSet<string>();
            if (oldConfig.RootFolder?.Children != null)
            {
                Svc.Log.Debug($"Found children in root folder");
                TraverseMacroNodes(oldConfig.RootFolder, "/", oldMacros);
            }
            else
            {
                Svc.Log.Warning("No macros found in old config");
            }

            Svc.Log.Debug($"Current config has {C.Macros.Count} macros");
            foreach (var currentMacro in C.Macros)
            {
                Svc.Log.Debug($"Checking current macro: {currentMacro.Name} in {currentMacro.FolderPath}");
                if (!oldMacros.Contains(currentMacro.Name))
                {
                    Svc.Log.Debug($"Adding {currentMacro.Name} to removed macros");
                    removedMacros[currentMacro.Name] = (currentMacro, true);
                }
            }

            // Log migration summary
            Svc.Log.Info($"Migration preview summary:");
            Svc.Log.Info($"- New macros: {newMacros.Count}");
            Svc.Log.Info($"- Removed macros: {removedMacros.Count}");
            Svc.Log.Info($"- Changed settings: {changes.Count}");

            // Preview other settings
            AddChange("CraftSkip", oldConfig.CraftSkip?.ToString(), C.CraftSkip.ToString());
            AddChange("SmartWait", oldConfig.SmartWait?.ToString(), C.SmartWait.ToString());
            // TODO: the rest

            migrationValid = true;
        }
        catch (Exception ex)
        {
            migrationValid = false;
            errorMessage = $"Error previewing migration: {ex.Message}";
            Svc.Log.Error(ex, "Failed to preview migration");
        }
    }

    private void TraverseMacroNodes(dynamic node, string currentPath, HashSet<string> oldMacros)
    {
        try
        {
            if (node == null) return;

            // If it has contents, it's a macro node and not a folder node
            if (node.Contents != null)
            {
                var name = node.Name?.ToString() ?? "Unknown";
                var language = node.Language?.ToString() ?? "0";
                var content = node.Contents.ToString();
                var craftingLoop = node.CraftingLoop ?? false;
                var craftLoopCount = node.CraftLoopCount ?? 0;
                var isPostProcess = node.IsPostProcess ?? false;

                Svc.Log.Debug($"Processing old macro: {name} (Type: {language}, Folder: {currentPath})");

                oldMacros.Add(name);
                var macro = new ConfigMacro
                {
                    Name = name,
                    Type = language == "1" ? MacroType.Lua : MacroType.Native,
                    Content = content,
                    FolderPath = currentPath,
                    Metadata = new MacroMetadata
                    {
                        LastModified = DateTime.Now,
                        CraftingLoop = craftingLoop,
                        CraftLoopCount = craftLoopCount,
                        TriggerEvents = isPostProcess ? [TriggerEvent.OnAutoRetainerCharacterPostProcess] : [],
                    }
                };
                newMacros[name] = (macro, true);
            }
            // Check if this is a folder node by looking for Children property
            else if (node.Children != null)
            {
                var folderName = node.Name?.ToString() ?? "Unknown";
                var newPath = Path.Combine(currentPath, folderName);

                foreach (dynamic child in node.Children)
                    TraverseMacroNodes(child, newPath, oldMacros);
            }
        }
        catch (Exception ex)
        {
            Svc.Log.Error(ex, $"Error processing node in folder {currentPath}");
        }
    }

    private void AddChange(string key, string? oldValue, string newValue)
    {
        if (oldValue != newValue)
        {
            Svc.Log.Debug($"Adding change: {key} = {oldValue} -> {newValue}");
            changes[key] = (oldValue ?? "null", newValue, true);
        }
    }

    public override void Draw()
    {
        Svc.Log.Debug("MigrationPreviewWindow Draw called");
        
        if (!migrationValid)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudRed);
            ImGui.TextUnformatted("Migration Preview Failed");
            ImGui.PopStyleColor();
            
            using (var errorBox = ImRaii.Child("ErrorBox", new Vector2(400, 100), true))
            {
                ImGui.TextWrapped(errorMessage);
            }
            
            using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button($"{FontAwesomeIcon.TimesCircle.ToIconString()} Close"))
                    IsOpen = false;
            }
                
            return;
        }
        
        // Header with summary
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Import Configuration");
        ImGui.TextUnformatted("Review the changes that will be applied to your configuration.");
        ImGui.Separator();
        ImGui.Spacing();

        // General Settings Section
        bool settingsOpen = false;
        using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
        {
            settingsOpen = ImGui.CollapsingHeader($"{FontAwesomeIcon.Cog.ToIconString()} General Settings ({changes.Count})");
        }
        if (settingsOpen)
        {
            using var settingsChild = ImRaii.Child("SettingsSection", new Vector2(-1, 150), true);
            
            if (ImGui.Checkbox("Select All Changes", ref selectAllChanges))
            {
                var keys = changes.Keys.ToList();
                foreach (var key in keys)
                {
                    var (oldValue, newValue, _) = changes[key];
                    changes[key] = (oldValue, newValue, selectAllChanges);
                }
            }
            ImGui.Separator();

            using var table = ImRaii.Table("SettingsTable", 5, ImGuiTableFlags.RowBg);
            if (table)
            {
                ImGui.TableSetupColumn("Select", ImGuiTableColumnFlags.WidthFixed, 40);
                ImGui.TableSetupColumn("Setting", ImGuiTableColumnFlags.WidthFixed, 150);
                ImGui.TableSetupColumn("Old Value", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 30);
                ImGui.TableSetupColumn("New Value", ImGuiTableColumnFlags.WidthStretch);
                
                ImGui.TableHeadersRow();
                
                foreach (var (key, (oldValue, newValue, selected)) in changes.Where(c => !c.Key.StartsWith("Macro")))
                {
                    ImGui.TableNextRow();
                    
                    // Checkbox column
                    ImGui.TableNextColumn();
                    var newSelected = selected;
                    if (ImGui.Checkbox($"##{key}", ref newSelected))
                    {
                        changes[key] = (oldValue, newValue, newSelected);
                    }
                    
                    // Setting name column
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(key);
                    
                    // Old value column
                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.DalamudRed, oldValue);
                    
                    // Arrow column
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted("→");
                    
                    // New value column
                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.HealerGreen, newValue);
                }
            }
        }

        // New Macros Section
        bool newMacrosOpen = false;
        using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
        {
            newMacrosOpen = ImGui.CollapsingHeader($"{FontAwesomeIcon.Plus.ToIconString()} New Macros ({newMacros.Count})");
        }
        if (newMacrosOpen)
        {
            using var newMacrosChild = ImRaii.Child("NewMacrosSection", new Vector2(-1, 200), true);
            
            if (ImGui.Checkbox("Select All New Macros", ref selectAllNewMacros))
            {
                var keys = newMacros.Keys.ToList();
                foreach (var key in keys)
                {
                    var (macro, _) = newMacros[key];
                    newMacros[key] = (macro, selectAllNewMacros);
                }
            }
            ImGui.Separator();

            using var table = ImRaii.Table("NewMacrosTable", 4, ImGuiTableFlags.RowBg);
            if (table)
            {
                ImGui.TableSetupColumn("Select", ImGuiTableColumnFlags.WidthFixed, 40);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Path", ImGuiTableColumnFlags.WidthStretch);
                
                ImGui.TableHeadersRow();
                
                foreach (var (name, (macro, selected)) in newMacros)
                {
                    ImGui.TableNextRow();
                    
                    // Checkbox column
                    ImGui.TableNextColumn();
                    var newSelected = selected;
                    if (ImGui.Checkbox($"##new{name}", ref newSelected))
                        newMacros[name] = (macro, newSelected);
                    
                    // Name column
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name);
                    
                    // Type column
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(macro.Type.ToString());
                    
                    // Path column
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(macro.FolderPath);
                }
            }
        }

        // Removed Macros Section
        bool removedMacrosOpen = false;
        using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
        {
            removedMacrosOpen = ImGui.CollapsingHeader($"{FontAwesomeIcon.TrashAlt.ToIconString()} Removed Macros ({removedMacros.Count})");
        }
        if (removedMacrosOpen)
        {
            using var removedMacrosChild = ImRaii.Child("RemovedMacrosSection", new Vector2(-1, 200), true);
            
            if (ImGui.Checkbox("Select All Removed Macros", ref selectAllRemovedMacros))
            {
                var keys = removedMacros.Keys.ToList();
                foreach (var key in keys)
                {
                    var (macro, _) = removedMacros[key];
                    removedMacros[key] = (macro, selectAllRemovedMacros);
                }
            }
            ImGui.Separator();

            using var table = ImRaii.Table("RemovedMacrosTable", 4, ImGuiTableFlags.RowBg);
            if (table)
            {
                ImGui.TableSetupColumn("Select", ImGuiTableColumnFlags.WidthFixed, 40);
                ImGui.TableSetupColumn("Name", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, 80);
                ImGui.TableSetupColumn("Path", ImGuiTableColumnFlags.WidthStretch);
                
                ImGui.TableHeadersRow();
                
                foreach (var (name, (macro, selected)) in removedMacros)
                {
                    ImGui.TableNextRow();
                    
                    // Checkbox column
                    ImGui.TableNextColumn();
                    var newSelected = selected;
                    if (ImGui.Checkbox($"##removed{name}", ref newSelected))
                    {
                        removedMacros[name] = (macro, newSelected);
                    }
                    
                    // Name column
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(name);
                    
                    // Type column
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(macro.Type.ToString());
                    
                    // Path column
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(macro.FolderPath);
                }
            }
        }

        ImGui.Separator();
        ImGui.Spacing();
        
        // Action buttons
        float buttonWidth = 200;
        float windowWidth = ImGui.GetWindowWidth();
        float buttonsWidth = buttonWidth * 2 + ImGui.GetStyle().ItemSpacing.X;
        float startPos = (windowWidth - buttonsWidth) / 2;
        
        ImGui.SetCursorPosX(startPos);
        
        using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button($"{FontAwesomeIcon.PlayCircle.ToIconString()} Apply Selected Changes", new Vector2(buttonWidth, 0)))
            {
                ApplySelectedChanges();
                IsOpen = false;
            }
        }
        
        ImGui.SameLine();
        
        using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.Button($"{FontAwesomeIcon.TimesCircle.ToIconString()} Cancel", new Vector2(buttonWidth, 0)))
                IsOpen = false;
        }
    }

    private void ApplySelectedChanges()
    {
        try
        {
            // General settings
            foreach (var (key, (oldValue, newValue, selected)) in changes.Where(c => c.Value.Selected))
            {
                if (C.GetType().GetProperty(key) is { } property)
                {
                    try
                    {
                        var convertedValue = property.PropertyType.IsEnum
                            ? Enum.Parse(property.PropertyType, newValue)
                            : Convert.ChangeType(newValue, property.PropertyType);
                        property.SetValue(C, convertedValue);
                    }
                    catch (Exception ex)
                    {
                        Svc.Log.Error(ex, $"Failed to convert value for property {key}: {newValue} to type {property.PropertyType}");
                    }
                }
            }

            // Apply selected new macros
            foreach (var (_, (macro, selected)) in newMacros.Where(m => m.Value.Selected))
                C.Macros.Add(macro);

            // Remove selected macros
            foreach (var (name, (macro, selected)) in removedMacros.Where(m => m.Value.Selected))
                C.Macros.RemoveAll(m => m.Name == name);

            C.Save();
            Svc.Chat.Print("Selected changes applied successfully!");
        }
        catch (Exception ex)
        {
            Svc.Chat.PrintError($"Failed to apply selected changes: {ex.Message}");
        }
    }
}
