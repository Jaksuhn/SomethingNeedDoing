using Dalamud.Interface.Colors;
using Dalamud.Interface;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using ImGuiNET;
using System;
using System.Numerics;
using System.Threading.Tasks;
using SomethingNeedDoing.Scheduler;
using System.Linq;
using Dalamud.Interface.Components;
using SomethingNeedDoing.Core.Github;
using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.Managers;
using System.Collections.Generic;
using SomethingNeedDoing.Gui;

namespace SomethingNeedDoing.Gui;

public class MainWindow : Window
{
    private readonly RunningMacrosPanel _runningPanel;
    private readonly MacroEditor _macroEditor;
    private readonly HelpUI _helpUI;
    private readonly WindowSystem _ws;
    private readonly IMacroScheduler _scheduler;
    private readonly GitMacroManager _gitManager;
    private readonly MacroStatusWindow _statusWindow;

    // Macro selection
    private string _selectedMacroId = string.Empty;
    private string _searchText = string.Empty;

    // Rename popup
    private string _renameMacroBuffer = string.Empty;
    private bool _showRenamePopup = false;
    private string _macroToRename = string.Empty;
    
    // New macro popup
    private string _newMacroName = "New Macro";
    private bool _showCreateMacroPopup = false;
    private int _newMacroType = 0; // 0 = Native, 1 = Lua

    // Folder management
    private string _selectedFolderId = "General";  // Default to General folder instead of Root
    private string _newFolderName = string.Empty;
    private bool _showCreateFolderPopup = false;

    // Storage for custom folders that might be empty
    private HashSet<string> _customFolders = new HashSet<string>();
    
    // Track which folders are expanded
    private HashSet<string> _expandedFolders = new HashSet<string>();
    
    // Panel resizing
    private float _leftPanelWidth = 250f;
    private float _minLeftPanelWidth = 180f;
    private float _maxLeftPanelWidth = 400f;
    
    // Default folder
    private const string DEFAULT_FOLDER = "General";
    
    // UI state
    private bool _isFolderSectionCollapsed = false;

    public MainWindow(WindowSystem ws, IMacroScheduler scheduler, GitMacroManager gitManager, RunningMacrosPanel runningPanel, MacroEditor macroEditor, HelpUI helpUI, MacroStatusWindow statusWindow)
        : base("Something Need Doing", ImGuiWindowFlags.NoScrollbar)
    {
        _ws = ws;
        _scheduler = scheduler;
        _gitManager = gitManager;
        _runningPanel = runningPanel;
        _macroEditor = macroEditor;
        _helpUI = helpUI;
        _statusWindow = statusWindow;

        Size = new Vector2(1000, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        
        // Allow ImGui to save window position between plugin sessions
        RespectCloseHotkey = true;
        
        // Explicitly ensure we have a title bar with window controls
        Flags &= ~ImGuiWindowFlags.NoTitleBar;
    }

    public override void PreDraw()
    {
        // Make sure we are showing standard window controls - try different flags if needed
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(10, 10));
    }
    
    public override void PostDraw()
    {
        ImGui.PopStyleVar();
    }

    public override void OnOpen()
    {
        base.OnOpen();
        // Ensure we have at least the default folder
        EnsureDefaultFolder();
    }

    private void EnsureDefaultFolder()
    {
        // Check if we have any folders other than Root
        var folders = C.GetFolderPaths();
        bool hasValidFolders = folders.Any(f => f != "Root" && !string.IsNullOrEmpty(f));
        
        // Create default folder if none exist
        if (!hasValidFolders)
        {
            _customFolders.Add(DEFAULT_FOLDER);
            
            // Create a template macro in the default folder
            var templateMacro = new ConfigMacro
            {
                Name = "Template Macro",
                Content = "// Add your macro commands here",
                Type = MacroType.Native,
                FolderPath = DEFAULT_FOLDER
            };
            C.Macros.Add(templateMacro);
            C.Save();
        }
        
        // Move any macros from Root to the default folder
        MoveRootMacrosToDefault();
    }
    
    private void MoveRootMacrosToDefault()
    {
        // Get all macros in Root
        var rootMacros = C.GetMacrosInFolder("Root").ToList();
        
        // Move each one to the default folder
        foreach (var macro in rootMacros)
        {
            if (macro is ConfigMacro configMacro)
            {
                configMacro.FolderPath = DEFAULT_FOLDER;
            }
        }
        
        if (rootMacros.Any())
        {
            C.Save();
        }
    }

    public override void Draw()
    {
        // Main tabs for the window
        ImGui.BeginTabBar("Tabs", ImGuiTabBarFlags.None);

        if (ImGui.BeginTabItem("Macros Library"))
        {
            DrawMacrosTab();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Help"))
        {
            _helpUI.Draw();
            ImGui.EndTabItem();
        }

        if (ImGui.BeginTabItem("Settings"))
        {
            DrawSettingsTab();
            ImGui.EndTabItem();
        }
        
        ImGui.EndTabBar();
        
        // Show rename popup if active
        ShowRenamePopup();
    }

    private void DrawSettingsTab()
    {
        // We'll use a child area with scrolling for the settings
        ImGui.BeginChild("SettingsScrollArea", new Vector2(-1, -1), false);
        
        // Start with collapsing headers for each section
        if (ImGui.CollapsingHeader("General Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.Indent(10);
            
            // Basic settings
            var useCraftLoopTemplate = C.UseCraftLoopTemplate;
            if (ImGui.Checkbox("Enable automatic updates for Git macros", ref useCraftLoopTemplate))
            {
                C.UseCraftLoopTemplate = useCraftLoopTemplate;
                C.Save();
            }

            var craftSkip = C.CraftSkip;
            if (ImGui.Checkbox("Enable unsafe actions during crafting", ref craftSkip))
            {
                C.CraftSkip = craftSkip;
                C.Save();
            }
            
            ImGui.Unindent(10);
        }
        
        // Native macro options
        if (ImGui.CollapsingHeader("Native Macro Options"))
        {
            ImGui.Indent(10);
            
            var qualitySkip = C.QualitySkip;
            if (ImGui.Checkbox("Skip quality actions at max quality", ref qualitySkip))
            {
                C.QualitySkip = qualitySkip;
                C.Save();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("When enabled, quality-increasing actions will be skipped at max quality");
            }

            var loopTotal = C.LoopTotal;
            if (ImGui.Checkbox("Loop command specifies total iterations", ref loopTotal))
            {
                C.LoopTotal = loopTotal;
                C.Save();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("When enabled, /loop 5 means 'loop a total of 5 times' instead of 'loop 5 more times'");
            }
            
            ImGui.Unindent(10);
        }
        
        // UI Automation options
        if (ImGui.CollapsingHeader("UI Automation Options"))
        {
            ImGui.Indent(10);
            
            var stopMacroIfAddonNotFound = C.StopMacroIfAddonNotFound;
            if (ImGui.Checkbox("Stop macro if UI element is not found", ref stopMacroIfAddonNotFound))
            {
                C.StopMacroIfAddonNotFound = stopMacroIfAddonNotFound;
                C.Save();
            }
            
            if (ImGui.IsItemHovered())
            {
                ImGui.SetTooltip("When enabled, macros will stop if a UI element is not found");
            }
            
            ImGui.Unindent(10);
        }
        
        // Git Macros section
        if (ImGui.CollapsingHeader("Git Macros"))
        {
            ImGui.Indent(10);
            
            ImGui.TextWrapped("Git macros allow you to use macros directly from GitHub repositories.");
            ImGui.TextWrapped("These macros can be automatically updated when changes are made to the source repository.");
            
            ImGui.Spacing();
            ImGui.TextColored(ImGuiColors.DalamudViolet, "How to Add a Git Macro:");
            
            ImGui.TextWrapped("1. Copy a GitHub URL to your clipboard. Supported formats:");
            ImGui.Indent(10);
            ImGui.TextColored(ImGuiColors.DalamudYellow, "• GitHub file URL: https://github.com/username/repo/blob/main/path/to/macro.txt");
            ImGui.TextColored(ImGuiColors.DalamudYellow, "• GitHub Gist URL: https://gist.github.com/username/gistid");
            ImGui.Unindent(10);
            
            ImGui.TextWrapped("2. Click the 'New Macro' button in the main interface");
            ImGui.TextWrapped("3. The URL will be detected automatically and the macro will be imported");
            
            ImGui.Spacing();
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Git Macro Features:");
            
            ImGui.TextWrapped("• Automatic Updates: Git macros check for updates when loaded");
            ImGui.TextWrapped("• Metadata: Git macros can include author info, version details, and documentation");
            
            ImGui.Unindent(10);
        }
        
        // Lua Script Options
        if (ImGui.CollapsingHeader("Lua Script Options"))
        {
            ImGui.Indent(10);
            
            ImGui.TextWrapped("Lua require paths (where to look for Lua modules):");
            
            var paths = C.LuaRequirePaths.ToArray();
            for (var index = 0; index < paths.Length; index++)
            {
                var path = paths[index];
                
                if (ImGui.InputText($"Path #{index}", ref path, 200))
                {
                    var newPaths = paths.ToList();
                    newPaths[index] = path;
                    C.LuaRequirePaths = newPaths.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
                    C.Save();
                }
            }

            if (ImGui.Button("Add Path"))
            {
                var newPaths = paths.ToList();
                newPaths.Add(string.Empty);
                C.LuaRequirePaths = newPaths.ToArray();
                C.Save();
            }
            
            ImGui.Unindent(10);
        }
        
        ImGui.Separator();
        ImGui.Spacing();
        
        // Import/Export Section
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Import Configuration");
        ImGui.Spacing();
        
        // Instructions for import
        ImGui.TextWrapped("Import configuration from a previous version of SomethingNeedDoing:");
        ImGui.TextWrapped("1. Copy the old config JSON to clipboard");
        ImGui.TextWrapped("2. Click the Import button below");
        ImGui.Spacing();
        
        // Import button with better label
        if (ImGuiX.IconButton(FontAwesomeHelper.IconImport, "Import from Clipboard"))
        {
            var clipboard = ImGui.GetClipboardText();
            if (!string.IsNullOrEmpty(clipboard))
            {
                try 
                {
                    // Create and show the migration preview window with more explicit logging
                    Svc.Log.Information("Creating migration preview window...");
                    var migrationWindow = new MigrationPreviewWindow(_ws, clipboard);
                    migrationWindow.IsOpen = true;
                    _ws.AddWindow(migrationWindow);
                    Svc.Log.Information($"Migration window created and added to window system. IsOpen: {migrationWindow.IsOpen}");
                    
                    // Force the window to appear in the foreground
                    migrationWindow.BringToFront();
                    
                    // Notify the user
                    Svc.Chat.Print("Migration preview window opened. Please review the changes.");
                }
                catch (Exception ex)
                {
                    Svc.Log.Error(ex, "Failed to create migration preview window");
                    Svc.Chat.PrintError($"Failed to import: {ex.Message}");
                }
            }
            else
            {
                Svc.Chat.PrintError("No configuration data in clipboard");
            }
        }
        
        ImGui.EndChild(); // End SettingsScrollArea
    }

    private void DrawMacrosTab()
    {
        // Get UI scale
        float scale = ImGuiHelpers.GlobalScale;
        
        // Calculate scaled widths
        float minWidth = _minLeftPanelWidth * scale;
        float maxWidth = _maxLeftPanelWidth * scale;
        float leftPanelWidth = _leftPanelWidth * scale;
        float windowPadding = ImGui.GetStyle().WindowPadding.X * 2;
        
        // Start resizable two-column layout
        ImGui.BeginTable("MainLayout", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.Resizable);
        
        // Setup columns
        ImGui.TableSetupColumn("Tree", ImGuiTableColumnFlags.WidthFixed, leftPanelWidth);
        ImGui.TableSetupColumn("Editor", ImGuiTableColumnFlags.WidthStretch);
        
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        
        // Search box
        ImGui.SetNextItemWidth(-1);
        ImGui.InputTextWithHint("##Search", "Search Folders & Macros...", ref _searchText, 100);
        
        ImGui.Separator();
        
        // Folders and macros panel
        ImGui.BeginChild("LeftPanel", new Vector2(0, -1), true);
        DrawFolderMacroTree();
        ImGui.EndChild();
        
        // Store user's panel resizing
        if (ImGui.TableGetColumnFlags(0).HasFlag(ImGuiTableColumnFlags.WidthFixed))
        {
            float currentWidth = ImGui.GetColumnWidth(0) / scale;
            currentWidth = Math.Clamp(currentWidth, _minLeftPanelWidth, _maxLeftPanelWidth);
            
            if (Math.Abs(_leftPanelWidth - currentWidth) > 1f)
            {
                _leftPanelWidth = currentWidth;
            }
        }
        
        // Editor panel
        ImGui.TableNextColumn();
        ImGui.BeginChild("RightPanel", new Vector2(0, -1), false);
        
        var selectedMacro = GetSelectedMacro();
        if (selectedMacro != null)
        {
            _macroEditor.Draw(selectedMacro);
        }
        else
        {
            DrawEmptyState();
        }
        
        ImGui.EndChild();
        ImGui.EndTable();
    }
    
    private void DrawFolderMacroTree()
    {
        // Section for folder/macro tree with better visual hierarchy
        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.3f, 0.3f, 0.4f, 0.7f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.4f, 0.4f, 0.5f, 0.8f));
        
        // If there's no search, display the hierarchical tree
        if (string.IsNullOrEmpty(_searchText))
        {
            // FOLDERS SECTION HEADER WITH COLLAPSE TOGGLE
            ImGui.BeginGroup();
            
            // Draw the FOLDERS text with violet color
            ImGui.TextColored(ImGuiColors.DalamudViolet, "FOLDERS");
            
            // Add buttons next to the FOLDERS text
            float textWidth = ImGui.CalcTextSize("FOLDERS").X;
            ImGui.SameLine(textWidth + 15);
            
            // Add collapse toggle button
            float buttonSize = ImGui.GetFrameHeight() * 1.2f; // Standard button size for all buttons
            
            using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button(_isFolderSectionCollapsed 
                    ? $"{FontAwesomeIcon.AngleDown.ToIconString()}##ExpandFolders" 
                    : $"{FontAwesomeIcon.AngleUp.ToIconString()}##CollapseFolders", 
                    new Vector2(buttonSize)))
                {
                    _isFolderSectionCollapsed = !_isFolderSectionCollapsed;
                }
                
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(_isFolderSectionCollapsed ? "Expand folder tree" : "Collapse folder tree");
            }
            
            // New Macro button - with consistent size
            ImGui.SameLine(0, 5);
            ImGui.PushStyleVar(ImGuiStyleVar.FramePadding, new Vector2(4, 4)); // Consistent padding
            
            using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button($"{FontAwesomeIcon.FileAlt.ToIconString()}##NewMacro", new Vector2(buttonSize)))
                {
                    // Reset popup fields
                    _newMacroName = "New Macro";
                    _newMacroType = 0; // Default to Native
                    _showCreateMacroPopup = true;
                    ImGui.OpenPopup("Create New Macro##Popup");
                }
            }
            
            // Show tooltip
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Create a new macro");
                
            // New Folder button - with consistent size
            ImGui.SameLine(0, 5);
            using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
            {
                if (ImGui.Button($"{FontAwesomeIcon.FolderPlus.ToIconString()}##NewFolder", new Vector2(buttonSize)))
                {
                    // Reset and show folder creation popup
                    _newFolderName = "New Folder";
                    _showCreateFolderPopup = true;
                    ImGui.OpenPopup("Create New Folder##Popup");
                }
            }
            
            // Show tooltip
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Create a new folder");
                
            ImGui.PopStyleVar();
            
            ImGui.EndGroup();
            
            // Only show folder tree if not collapsed
            if (!_isFolderSectionCollapsed)
            {
                // Make a scrollable area for the folder tree to maximize available space
                ImGui.BeginChild("FolderTreeArea", new Vector2(-1, ImGui.GetContentRegionAvail().Y * 0.6f), false);
                
                // Root/All Macros node - make it clearer
                bool isRootSelected = _selectedFolderId == "Root";
                int rootCount = C.Macros.Count; // Total macro count
                
                using (var iconFont = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    ImGui.TextColored(ImGuiColors.DalamudYellow, FontAwesomeIcon.Search.ToIconString());
                }
                ImGui.SameLine();
                
                if (ImGui.TreeNodeEx($"Show All Macros ({rootCount})##root", 
                    isRootSelected ? ImGuiTreeNodeFlags.Selected : ImGuiTreeNodeFlags.None))
                {
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("View macros from all folders");
                        
                    if (ImGui.IsItemClicked())
                    {
                        _selectedFolderId = "Root";
                        _selectedMacroId = string.Empty; // Clear macro selection
                    }
                    
                    // Root is just a view, no sub-items
                    ImGui.TreePop();
                }
                
                // Get all real folders
                var allFolders = new HashSet<string>(C.GetFolderPaths());
                
                // Ensure our selected folder exists in the set
                if (!allFolders.Contains(_selectedFolderId) && _selectedFolderId != "Root")
                {
                    allFolders.Add(_selectedFolderId);
                }
                
                // Display all folders as tree nodes
                foreach (var folderPath in allFolders)
                {
                    if (folderPath != "Root" && !string.IsNullOrEmpty(folderPath))
                    {
                        bool isSelected = _selectedFolderId == folderPath;
                        int folderCount = C.GetMacroCount(folderPath);
                        
                        // Check if this folder should be expanded based on our tracking
                        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanAvailWidth;
                        if (isSelected) flags |= ImGuiTreeNodeFlags.Selected;
                        
                        // Add DefaultOpen flag if this folder is in our tracked expanded folders set
                        if (_expandedFolders.Contains(folderPath))
                            flags |= ImGuiTreeNodeFlags.DefaultOpen;
                        
                        ImGuiX.Icon(FontAwesomeHelper.IconFolder);
                        ImGui.SameLine();
                        
                        bool folderOpen = ImGui.TreeNodeEx($"{folderPath} ({folderCount})##folder_{folderPath}", flags);
                        
                        // Update our expanded folders tracking
                        if (folderOpen && !_expandedFolders.Contains(folderPath))
                            _expandedFolders.Add(folderPath);
                        else if (!folderOpen && _expandedFolders.Contains(folderPath))
                            _expandedFolders.Remove(folderPath);
                        
                        if (ImGui.IsItemClicked())
                        {
                            _selectedFolderId = folderPath;
                            _selectedMacroId = string.Empty; // Clear macro selection
                        }
                        
                        // Context menu for folder operations - simplified
                        if (ImGui.BeginPopupContextItem())
                        {
                            // Show a more readable header
                            ImGui.TextColored(ImGuiColors.DalamudViolet, $"Folder: {folderPath}");
                            ImGui.Separator();
                            
                            // Only show relevant operations
                            if (folderPath != DEFAULT_FOLDER) // Don't allow deleting the default folder
                            {
                                if (ImGui.MenuItem("Delete Folder"))
                                {
                                    DeleteFolder(folderPath);
                                    ImGui.CloseCurrentPopup();
                                }
                                
                                if (ImGui.IsItemHovered())
                                {
                                    ImGui.SetTooltip("Delete this folder and move all macros to Default folder");
                                }
                            }
                            
                            ImGui.EndPopup();
                        }
                        
                        // If folder is open, show macros within this folder
                        if (folderOpen)
                        {
                            // Add indent for macros in the folder
                            ImGui.Indent(10);
                            
                            // List macros in this folder
                            var macrosInFolder = C.GetMacrosInFolder(folderPath);
                            foreach (var macro in macrosInFolder)
                            {
                                DrawMacroTreeNode(macro, false);
                            }
                            
                            ImGui.Unindent(10);
                            ImGui.TreePop();
                        }
                    }
                }
                
                ImGui.EndChild(); // End FolderTreeArea
            }
            
            // Handle the popups regardless of whether folder section is collapsed
            ShowCreateMacroPopup();
            ShowCreateFolderPopup();
            
            // Separator between folders and macro settings
            ImGui.Separator();
            
            // MACRO SETTINGS section
            ImGui.TextColored(ImGuiColors.DalamudViolet, "MACRO SETTINGS");
            
            // Use triangle for collapsible header like in the screenshot
            ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.2f, 0.3f, 0.7f));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.25f, 0.25f, 0.35f, 0.8f));
            
            if (ImGui.CollapsingHeader("MACRO SETTINGS", ImGuiTreeNodeFlags.DefaultOpen))
            {
                // Calculate available height for settings
                // If folders are collapsed, we can use more space for settings
                float availableHeight = ImGui.GetContentRegionAvail().Y;
                
                // Create a scrollable area for settings content that fills remaining space
                ImGui.BeginChild("SettingsScrollArea", new Vector2(-1, availableHeight), false);
                
                var selectedMacro = GetSelectedMacro();
                if (selectedMacro != null)
                {
                    // Show selected macro name and type with proper styling
                    string macroName = selectedMacro.Name;
                    string macroTypeStr = selectedMacro.Type == MacroType.Lua ? "Lua" : "Native";
                    
                    ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudWhite);
                    ImGui.Text($"{macroName} ({macroTypeStr})");
                    ImGui.PopStyleColor();
                    
                    ImGui.Spacing();
                    ImGui.Separator();
                    ImGui.Spacing();
                    
                    // COLLAPSIBLE SECTIONS INSTEAD OF TABS
                    
                    // General Information Section
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.2f, 0.2f, 0.3f, 0.7f));
                    ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.25f, 0.25f, 0.35f, 0.8f));
                    
                    if (ImGui.CollapsingHeader("General Information", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.Spacing();
                        
                        // Author field
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text("Author:");
                        ImGui.SameLine(100);
                        
                        string author = selectedMacro.Metadata.Author ?? string.Empty;
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                        if (ImGui.InputText("##Author", ref author, 100))
                        {
                            selectedMacro.Metadata.Author = author;
                            C.Save();
                        }
                        
                        // Version field
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text("Version:");
                        ImGui.SameLine(100);
                        
                        string version = selectedMacro.Metadata.Version ?? string.Empty;
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                        if (ImGui.InputText("##Version", ref version, 50))
                        {
                            selectedMacro.Metadata.Version = version;
                            C.Save();
                        }
                        
                        // Description field
                        ImGui.AlignTextToFramePadding();
                        ImGui.Text("Description:");
                        
                        string description = selectedMacro.Metadata.Description ?? string.Empty;
                        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                        if (ImGui.InputTextMultiline("##Description", ref description, 1000, new Vector2(-1, 100)))
                        {
                            selectedMacro.Metadata.Description = description;
                            C.Save();
                        }
                    }
                    
                    ImGui.Spacing();
                    
                    // Crafting Settings Section
                    if (ImGui.CollapsingHeader("Crafting Settings", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.Spacing();
                        
                        // Crafting loop toggle
                        bool craftingLoop = selectedMacro.Metadata.CraftingLoop;
                        if (ImGui.Checkbox("Enable Crafting Loop", ref craftingLoop))
                        {
                            selectedMacro.Metadata.CraftingLoop = craftingLoop;
                            C.Save();
                        }
                        
                        if (craftingLoop)
                        {
                            ImGui.Indent(20);
                            
                            // Loop count setting
                            int loopCount = selectedMacro.Metadata.CraftLoopCount;
                            ImGui.SetNextItemWidth(100);
                            if (ImGui.InputInt("Loop Count", ref loopCount))
                            {
                                if (loopCount < -1)
                                    loopCount = -1;
                                
                                selectedMacro.Metadata.CraftLoopCount = loopCount;
                                C.Save();
                            }
                            
                            ImGui.SameLine();
                            ImGui.TextColored(ImGuiColors.DalamudGrey, "(-1 = infinite)");
                            
                            ImGui.Unindent(20);
                        }
                    }
                    
                    ImGui.Spacing();
                    
                    // Trigger Events Section
                    if (ImGui.CollapsingHeader("Trigger Events", ImGuiTreeNodeFlags.DefaultOpen))
                    {
                        ImGui.Spacing();
                        
                        // Auto-Retainer checkbox
                        bool arPostProcess = selectedMacro.Metadata.TriggerEvents.Contains(TriggerEvent.OnAutoRetainerCharacterPostProcess);
                        if (ImGui.Checkbox("Run after Auto Retainer completes", ref arPostProcess))
                        {
                            if (arPostProcess)
                                selectedMacro.Metadata.TriggerEvents.Add(TriggerEvent.OnAutoRetainerCharacterPostProcess);
                            else
                                selectedMacro.Metadata.TriggerEvents.Remove(TriggerEvent.OnAutoRetainerCharacterPostProcess);
                            
                            C.Save();
                        }
                        
                        // Login trigger
                        bool loginTrigger = selectedMacro.Metadata.TriggerEvents.Contains(TriggerEvent.OnLogin);
                        if (ImGui.Checkbox("Run on login", ref loginTrigger))
                        {
                            if (loginTrigger)
                                selectedMacro.Metadata.TriggerEvents.Add(TriggerEvent.OnLogin);
                            else
                                selectedMacro.Metadata.TriggerEvents.Remove(TriggerEvent.OnLogin);
                            
                            C.Save();
                        }
                        
                        // Logout trigger
                        bool logoutTrigger = selectedMacro.Metadata.TriggerEvents.Contains(TriggerEvent.OnLogout);
                        if (ImGui.Checkbox("Run on logout", ref logoutTrigger))
                        {
                            if (logoutTrigger)
                                selectedMacro.Metadata.TriggerEvents.Add(TriggerEvent.OnLogout);
                            else
                                selectedMacro.Metadata.TriggerEvents.Remove(TriggerEvent.OnLogout);
                            
                            C.Save();
                        }
                        
                        // Territory change trigger
                        bool territoryTrigger = selectedMacro.Metadata.TriggerEvents.Contains(TriggerEvent.OnTerritoryChange);
                        if (ImGui.Checkbox("Run on zone change", ref territoryTrigger))
                        {
                            if (territoryTrigger)
                                selectedMacro.Metadata.TriggerEvents.Add(TriggerEvent.OnTerritoryChange);
                            else
                                selectedMacro.Metadata.TriggerEvents.Remove(TriggerEvent.OnTerritoryChange);
                            
                            C.Save();
                        }
                    }
                    
                    ImGui.PopStyleColor(2); // Pop the header colors
                }
                else
                {
                    // No macro selected
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Select a macro to view and edit its settings");
                }
                
                ImGui.EndChild(); // End SettingsScrollArea
            }
            
            ImGui.PopStyleColor(2); // Pop the header colors for MACRO SETTINGS section
        }
        else
        {
            // SEARCH RESULTS
            ImGui.TextColored(ImGuiColors.DalamudViolet, "SEARCH RESULTS");
            
            // Show matching folders first
            var allFolders = new HashSet<string>(C.GetFolderPaths());
            bool foundAnyFolders = false;
            
            // Show folders that match the search
            foreach (var folderPath in allFolders)
            {
                if ((folderPath == "Root" && "All Macros".Contains(_searchText, StringComparison.OrdinalIgnoreCase)) ||
                    (folderPath != "Root" && !string.IsNullOrEmpty(folderPath) && 
                     folderPath.Contains(_searchText, StringComparison.OrdinalIgnoreCase)))
                {
                    foundAnyFolders = true;
                    
                    string displayName = folderPath == "Root" ? "All Macros" : folderPath;
                    int folderCount = folderPath == "Root" ? C.Macros.Count : C.GetMacroCount(folderPath);
                    
                    // Display folder as selectable
                    bool isSelected = _selectedFolderId == folderPath;
                    if (ImGui.Selectable($"📁 {displayName} ({folderCount})", isSelected))
                    {
                        _selectedFolderId = folderPath;
                        _selectedMacroId = string.Empty; // Clear macro selection
                    }
                }
            }
            
            // Show a separator between folders and macros if we found any folders
            if (foundAnyFolders)
            {
                ImGui.Separator();
                ImGui.TextColored(ImGuiColors.DalamudViolet, "MATCHING MACROS");
            }
            
            // Show matching macros
            var matchingMacros = C.SearchMacros(_searchText);
            bool foundAnyMacros = false;
            
            foreach (var macro in matchingMacros)
            {
                foundAnyMacros = true;
                DrawMacroTreeNode(macro, true);
            }
            
            if (!foundAnyFolders && !foundAnyMacros)
            {
                ImGui.TextColored(ImGuiColors.DalamudGrey, "No matching folders or macros");
            }
        }
        
        ImGui.PopStyleColor(2); // Pop the folder tree header colors
    }
    
    private void DrawMacroTreeNode(ConfigMacro macro, bool showFolder)
    {
        // Get icon based on macro type
        FontAwesomeIcon icon;
        if (macro is GitMacro)
            icon = FontAwesomeHelper.IconGitMacro;
        else
            icon = macro.Type == MacroType.Lua ? FontAwesomeHelper.IconLuaMacro : FontAwesomeHelper.IconNativeMacro;
        
        // Show icon
        ImGuiX.Icon(icon);
        ImGui.SameLine();
        
        // Build display name
        string displayName = showFolder 
            ? $"{macro.Name} [{macro.FolderPath}]"
            : macro.Name;
            
        // Macro type indicator
        string typeIndicator = macro.Type == MacroType.Lua ? " (Lua)" : "";
        displayName += typeIndicator;
        
        // Set selection state
        bool isSelected = macro.Id == _selectedMacroId;
        
        // Create selectable item
        if (isSelected)
        {
            ImGui.PushStyleColor(ImGuiCol.Header, ImGuiColors.ParsedPurple);
        }
        
        if (ImGui.Selectable(displayName, isSelected))
        {
            _selectedMacroId = macro.Id;
            
            // When selecting a macro from search or All Macros view, also switch to its folder
            if (showFolder)
            {
                _selectedFolderId = macro.FolderPath;
            }
        }
        
        if (isSelected)
        {
            ImGui.PopStyleColor();
        }
        
        // Move context menu handling to a separate method to avoid ImGui state issues
        HandleMacroContextMenu(macro);
    }

    // Separate method for handling context menu to improve ImGui stability
    private void HandleMacroContextMenu(ConfigMacro macro)
    {
        if (!ImGui.BeginPopupContextItem($"##ContextMenu_{macro.Id}"))
            return;
            
        // Show macro name in context menu
        ImGui.TextColored(ImGuiColors.DalamudViolet, macro.Name);
        ImGui.Separator();
        
        // Basic operations in main menu for clarity
        if (ImGuiX.IconMenuItem(FontAwesomeHelper.IconPlay, "Run"))
        {
            _scheduler.StartMacro(macro);
            ImGui.CloseCurrentPopup();
        }

        if (ImGuiX.IconMenuItem(FontAwesomeHelper.IconCopy, "Copy Content"))
        {
            ImGui.SetClipboardText(macro.Content);
            ImGui.CloseCurrentPopup();
        }
        
        if (ImGuiX.IconMenuItem(FontAwesomeHelper.IconRename, "Rename"))
        {
            _renameMacroBuffer = macro.Name;
            _showRenamePopup = true;
            _macroToRename = macro.Id;
            ImGui.CloseCurrentPopup();
        }

        if (ImGuiX.IconMenuItem(FontAwesomeHelper.IconDelete, "Delete"))
        {
            // Store current folder ID to maintain selection
            string currentFolderId = _selectedFolderId;
            
            // Store the expanded folders state - we'll keep the same state
            var expandedFoldersCopy = new HashSet<string>(_expandedFolders);
            
            macro.Delete();
            C.Save();
            
            // Clear selection if we just deleted the selected macro
            if (_selectedMacroId == macro.Id)
                _selectedMacroId = string.Empty;
                
            // Restore folder selection if possible
            if (!string.IsNullOrEmpty(currentFolderId))
                _selectedFolderId = currentFolderId;
                
            // Restore expanded folders state
            _expandedFolders = expandedFoldersCopy;
                
            ImGui.CloseCurrentPopup();
        }
        
        // Add Type selector to the context menu (only for ConfigMacro, not GitMacro)
        if (macro is ConfigMacro configMacro && !(macro is GitMacro))
        {
            ImGui.Separator();
            
            if (ImGui.BeginMenu("Type"))
            {
                bool isNative = macro.Type == MacroType.Native;
                bool isLua = macro.Type == MacroType.Lua;
                
                if (ImGui.MenuItem("Native", null, isNative))
                {
                    configMacro.Type = MacroType.Native;
                    C.Save();
                }
                
                if (ImGui.MenuItem("Lua", null, isLua))
                {
                    configMacro.Type = MacroType.Lua;
                    C.Save();
                }
                
                ImGui.EndMenu();
            }
        }

        // Move to folder functionality - simplified and more stable
        ImGui.Separator();
        
        if (ImGui.BeginMenu("Move to folder"))
        {
            // Show folders in a more organized way
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Select destination folder:");
            ImGui.Separator();
            
            // Option to move to the default folder
            bool isInDefault = macro.FolderPath == DEFAULT_FOLDER;
            if (ImGui.MenuItem(DEFAULT_FOLDER, null, isInDefault))
            {
                if (!isInDefault)
                {
                    // Store the expanded folders state
                    var expandedFoldersCopy = new HashSet<string>(_expandedFolders);
                    
                    MoveMacroToFolder(macro.Id, DEFAULT_FOLDER);
                    
                    // Restore expanded folders state
                    _expandedFolders = expandedFoldersCopy;
                    
                    // Ensure the destination folder is expanded
                    if (!_expandedFolders.Contains(DEFAULT_FOLDER))
                        _expandedFolders.Add(DEFAULT_FOLDER);
                }
                ImGui.CloseCurrentPopup();
            }

            // Get all folders for better organization
            var folders = new List<string>(C.GetFolderPaths());
            folders.Remove("Root"); // Don't include Root
            folders.Remove(DEFAULT_FOLDER); // Already listed above
            folders.Sort(); // Sort alphabetically
            
            if (folders.Any())
            {
                ImGui.Separator();
                
                foreach (var folder in folders)
                {
                    if (!string.IsNullOrEmpty(folder))
                    {
                        bool isCurrentFolder = macro.FolderPath == folder;
                        
                        // Show current folder with checkmark
                        if (ImGui.MenuItem($"{folder}{(isCurrentFolder ? " (current)" : "")}", null, isCurrentFolder))
                        {
                            if (!isCurrentFolder)
                            {
                                // Store the expanded folders state
                                var expandedFoldersCopy = new HashSet<string>(_expandedFolders);
                                
                                MoveMacroToFolder(macro.Id, folder);
                                
                                // Restore expanded folders state
                                _expandedFolders = expandedFoldersCopy;
                                
                                // Ensure the destination folder is expanded
                                if (!_expandedFolders.Contains(folder))
                                    _expandedFolders.Add(folder);
                            }
                            ImGui.CloseCurrentPopup();
                        }
                    }
                }
            }
            
            // Option to create a new folder
            ImGui.Separator();
            
            if (ImGui.MenuItem("Create new folder..."))
            {
                _newFolderName = "New Folder";
                _showCreateFolderPopup = true;
                ImGui.CloseCurrentPopup();
            }

            ImGui.EndMenu();
        }

        ImGui.EndPopup();
    }

    private void DrawEmptyState()
    {
        var center = ImGui.GetContentRegionAvail() / 2;
        var text = "Select a macro or create a new one";
        var textSize = ImGui.CalcTextSize(text);

        ImGui.SetCursorPos(ImGui.GetCursorPos() + center - textSize / 2);
        ImGui.TextColored(ImGuiColors.DalamudGrey, text);
    }

    private IMacro? GetSelectedMacro()
    {
        foreach (var macro in C.Macros)
        {
            if (macro.Id == _selectedMacroId)
                return macro;
        }
        return null;
    }

    // Implementation for the rename and duplicate buttons
    private void HandleRenameButton(IMacro macro)
    {
        _macroToRename = macro.Id;
        _renameMacroBuffer = macro.Name;
        _showRenamePopup = true;
        ImGui.OpenPopup("RenameMacroPopup");
    }

    private void ShowRenamePopup()
    {
        if (!_showRenamePopup) return;

        // Center the popup
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        // Use a fixed size popup with better styling
        ImGui.SetNextWindowSize(new Vector2(350, 130));
        
        // Use specific flags to make popup work properly
        bool isOpen = _showRenamePopup;
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(15, 15));
        
        if (ImGui.BeginPopupModal("RenameMacroPopup", ref isOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            ImGui.Text("Enter new name:");
            ImGui.SetNextItemWidth(-1);
            
            // Set keyboard focus to the input when opening the popup
            if (ImGui.IsWindowAppearing())
            {
                ImGui.SetKeyboardFocusHere();
            }
            
            // Check for Enter key press - using IsKeyPressed instead of KeysDownDuration
            bool enterPressed = ImGui.IsKeyPressed(ImGuiKey.Enter) && ImGui.IsWindowFocused();
            
            // Input field with better styling
            ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.15f, 0.15f, 0.15f, 1.0f));
            ImGui.InputText("##RenameMacroInput", ref _renameMacroBuffer, 100);
            ImGui.PopStyleColor();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // Confirm button with clear styling
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.4f, 1.0f));
            
            bool confirmed = ImGui.Button("Confirm", new Vector2(150, 0)) || enterPressed;
            
            ImGui.PopStyleColor(2);
            
            if (confirmed && !string.IsNullOrWhiteSpace(_renameMacroBuffer))
            {
                ApplyRename();
                ImGui.CloseCurrentPopup();
                _showRenamePopup = false;
            }

            ImGui.SameLine();
            
            // Cancel button
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.5f, 0.3f, 0.3f, 1.0f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.4f, 0.4f, 1.0f));
            
            if (ImGui.Button("Cancel", new Vector2(150, 0)) || (ImGui.IsKeyPressed(ImGuiKey.Escape) && ImGui.IsWindowFocused()))
            {
                ImGui.CloseCurrentPopup();
                _showRenamePopup = false;
            }
            
            ImGui.PopStyleColor(2);

            ImGui.EndPopup();
        }
        
        ImGui.PopStyleVar();
        
        // Update the flag based on the popup state
        _showRenamePopup = isOpen;
    }
    
    private void ApplyRename()
    {
        var macro = C.GetMacro(_macroToRename);
        if (macro != null)
        {
            // Convert ConfigMacro or GitMacro to their base type (IMacro) first
            // This allows us to set the name property which is declared in MacroBase
            if (macro is MacroBase macroBase)
            {
                macroBase.Name = _renameMacroBuffer;
                C.Save();
            }
        }
    }

    private void DuplicateMacro(IMacro macro)
    {
        if (macro is ConfigMacro configMacro)
        {
            var newMacro = new ConfigMacro
            {
                Name = $"{configMacro.Name} (Copy)",
                Content = configMacro.Content,
                Type = configMacro.Type,
                FolderPath = configMacro.FolderPath
            };
            
            // Copy metadata if it exists
            if (configMacro.Metadata != null)
            {
                newMacro.Metadata.Author = configMacro.Metadata.Author;
                newMacro.Metadata.Version = configMacro.Metadata.Version;
                newMacro.Metadata.Description = configMacro.Metadata.Description;
                newMacro.Metadata.CraftingLoop = configMacro.Metadata.CraftingLoop;
                newMacro.Metadata.CraftLoopCount = configMacro.Metadata.CraftLoopCount;
            }
            
            C.Macros.Add(newMacro);
            C.Save();
            _selectedMacroId = newMacro.Id;
        }
    }

    private void ShowCreateMacroPopup()
    {
        if (!_showCreateMacroPopup) return;

        // Center the popup
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        // Use a fixed size popup with better styling
        ImGui.SetNextWindowSize(new Vector2(400, 200));
        
        // Use specific flags to make popup work properly
        bool isOpen = _showCreateMacroPopup;
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(15, 15));
        
        if (ImGui.BeginPopupModal("Create New Macro##Popup", ref isOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            // Header with icon
            ImGuiX.Icon(FontAwesomeHelper.IconNew);
            ImGui.SameLine();
            ImGui.Text("Create New Macro");
            ImGui.Separator();
            ImGui.Spacing();
            
            // Macro name input
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Name:");
            ImGui.SameLine();
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##MacroName", ref _newMacroName, 100);
            ImGui.PopItemWidth();
            
            ImGui.Spacing();
            
            // Macro type selection with better UI
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Type:");
            ImGui.SameLine();
            
            // Radio buttons for macro type
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(15, 0));
            
            bool isNative = _newMacroType == 0;
            if (ImGui.RadioButton("Native##MacroType", isNative))
            {
                _newMacroType = 0;
            }
            ImGui.SameLine();
            
            bool isLua = _newMacroType == 1;
            if (ImGui.RadioButton("Lua##MacroType", isLua))
            {
                _newMacroType = 1;
            }
            ImGui.PopStyleVar();
            
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Buttons at the bottom
            float buttonWidth = 120;
            float windowWidth = ImGui.GetWindowWidth();
            float totalButtonsWidth = buttonWidth * 2 + 10;  // Two buttons + spacing
            float startPosX = (windowWidth - totalButtonsWidth) / 2;
            
            ImGui.SetCursorPosX(startPosX);
            
            // Create button
            if (ImGui.Button("Create", new Vector2(buttonWidth, 0)))
            {
                MacroType selectedType = _newMacroType == 0 ? MacroType.Native : MacroType.Lua;
                
                var newMacro = new ConfigMacro
                {
                    Name = _newMacroName,
                    Type = selectedType,
                    Content = string.Empty
                };
                
                if (_selectedFolderId != "Root")
                {
                    newMacro.FolderPath = _selectedFolderId;
                }
                
                C.Macros.Add(newMacro);
                _selectedMacroId = newMacro.Id;
                
                _showCreateMacroPopup = false;
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.SameLine();
            
            // Cancel button
            if (ImGui.Button("Cancel", new Vector2(buttonWidth, 0)))
            {
                _showCreateMacroPopup = false;
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }
        
        ImGui.PopStyleVar();
        
        if (!isOpen)
        {
            _showCreateMacroPopup = false;
        }
    }
    
    private void ShowCreateFolderPopup()
    {
        if (!_showCreateFolderPopup) return;

        // Center the popup
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));

        // Use a fixed size popup with better styling
        ImGui.SetNextWindowSize(new Vector2(400, 170));
        
        // Use specific flags to make popup work properly
        bool isOpen = _showCreateFolderPopup;
        
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(15, 15));
        
        if (ImGui.BeginPopupModal("Create New Folder##Popup", ref isOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings))
        {
            // Header with icon
            ImGuiX.Icon(FontAwesomeHelper.IconFolder);
            ImGui.SameLine();
            ImGui.Text("Create New Folder");
            ImGui.Separator();
            ImGui.Spacing();
            
            // Folder name input
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Name:");
            ImGui.SameLine();
            ImGui.PushItemWidth(ImGui.GetContentRegionAvail().X);
            ImGui.InputText("##FolderName", ref _newFolderName, 100);
            ImGui.PopItemWidth();
            
            ImGui.Spacing();
            ImGui.Spacing();
            
            // Buttons at the bottom
            float buttonWidth = 120;
            float windowWidth = ImGui.GetWindowWidth();
            float totalButtonsWidth = buttonWidth * 2 + 10;  // Two buttons + spacing
            float startPosX = (windowWidth - totalButtonsWidth) / 2;
            
            ImGui.SetCursorPosX(startPosX);
            
            // Create button
            if (ImGui.Button("Create", new Vector2(buttonWidth, 0)))
            {
                // Check if folder exists already
                bool folderExists = false;
                foreach (var macro in C.Macros)
                {
                    if (macro.FolderPath == _newFolderName)
                    {
                        folderExists = true;
                        break;
                    }
                }
                
                if (!folderExists && !string.IsNullOrWhiteSpace(_newFolderName))
                {
                    // Create a dummy macro in the folder to ensure it exists
                    var dummyMacro = new ConfigMacro
                    {
                        Name = $"{_newFolderName} Template",
                        Content = "// Add your macro commands here",
                        Type = MacroType.Native,
                        FolderPath = _newFolderName
                    };
                    
                    C.Macros.Add(dummyMacro);
                    _selectedFolderId = _newFolderName;
                    
                    _showCreateFolderPopup = false;
                    ImGui.CloseCurrentPopup();
                }
            }
            
            ImGui.SameLine();
            
            // Cancel button
            if (ImGui.Button("Cancel", new Vector2(buttonWidth, 0)))
            {
                _showCreateFolderPopup = false;
                ImGui.CloseCurrentPopup();
            }
            
            ImGui.EndPopup();
        }
        
        ImGui.PopStyleVar();
        
        if (!isOpen)
        {
            _showCreateFolderPopup = false;
        }
    }

    // Improved MoveMacroToFolder method for better feedback
    private void MoveMacroToFolder(string macroId, string folderPath)
    {
        // Never allow moving to Root directly
        if (folderPath == "Root")
        {
            folderPath = DEFAULT_FOLDER;
        }
        
        var macro = C.GetMacro(macroId);
        if (macro != null && macro is ConfigMacro configMacro)
        {
            // Don't do anything if it's already in this folder
            if (configMacro.FolderPath == folderPath)
                return;
                
            // Remember the old folder for notification
            string oldFolder = configMacro.FolderPath;
            
            // Save expanded folders state
            var expandedFoldersCopy = new HashSet<string>(_expandedFolders);
            
            // Update the folder path
            configMacro.FolderPath = folderPath;
            C.Save();
            
            // Update selection to reflect the move
            _selectedFolderId = folderPath;
            _selectedMacroId = macroId;
            
            // Restore expanded folders state
            _expandedFolders = expandedFoldersCopy;
            
            // Ensure the destination folder is expanded
            if (!_expandedFolders.Contains(folderPath))
                _expandedFolders.Add(folderPath);
            
            // Notify the user
            Svc.Chat.Print($"Moved macro '{configMacro.Name}' from '{oldFolder}' to '{folderPath}'");
        }
        else
        {
            Svc.Chat.PrintError($"Could not move macro: Not found or wrong type");
        }
    }
    
    private void DeleteFolder(string folderPath)
    {
        // Don't delete the default folder
        if (folderPath == DEFAULT_FOLDER)
        {
            Svc.Chat.PrintError("Cannot delete the Default folder");
            return;
        }
        
        // Save expanded folders state
        var expandedFoldersCopy = new HashSet<string>(_expandedFolders);
        
        // Get all macros in this folder
        var macrosInFolder = C.GetMacrosInFolder(folderPath).ToList();
        int macroCount = macrosInFolder.Count;
        
        if (macroCount > 0)
        {
            // Move all macros in this folder to the default folder
            foreach (var macro in macrosInFolder)
            {
                if (macro is ConfigMacro configMacro)
                {
                    configMacro.FolderPath = DEFAULT_FOLDER;
                }
            }
        }
        
        C.Save();
        
        // If the deleted folder was selected, switch to default
        if (_selectedFolderId == folderPath)
        {
            _selectedFolderId = DEFAULT_FOLDER;
        }
        
        // Restore expanded folders state
        _expandedFolders = expandedFoldersCopy;
        
        // Remove the deleted folder from expanded folders
        _expandedFolders.Remove(folderPath);
        
        // Ensure the default folder is expanded since macros moved there
        if (macroCount > 0 && !_expandedFolders.Contains(DEFAULT_FOLDER))
            _expandedFolders.Add(DEFAULT_FOLDER);
        
        // Notify the user
        if (macroCount > 0)
        {
            Svc.Chat.Print($"Deleted folder '{folderPath}' and moved {macroCount} macro(s) to Default folder");
        }
        else
        {
            Svc.Chat.Print($"Deleted empty folder '{folderPath}'");
        }
    }
}
