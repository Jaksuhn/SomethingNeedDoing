using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Github;
using SomethingNeedDoing.Framework.Interfaces;
using SomethingNeedDoing.Managers;
using SomethingNeedDoing.Utils;
using System.Threading.Tasks;
using System.IO;

namespace SomethingNeedDoing.Gui;
public class MacroUI : Window
{
    private string selectedMacroId = string.Empty;
    private string selectedFolderPath = "/";
    private string searchText = string.Empty;
    private bool isEditing = false;
    private string editingContent = string.Empty;
    private readonly HashSet<string> collapsedFolders = [];
    private readonly RunningMacrosPanel _panel;
    private readonly WindowSystem _ws;
    private readonly IMacroScheduler _scheduler;
    private readonly GitMacroManager _gitManager;
    private bool showVersionHistory = false;
    private List<GitCommitInfo>? versionHistory;
    private string? newMacroContent;
    private readonly MacroMetadataEditor _metadataEditor = new();

    public MacroUI(WindowSystem ws, RunningMacrosPanel panel, IMacroScheduler scheduler, GitMacroManager gitManager) : base("Macro Manager", ImGuiWindowFlags.NoScrollbar)
    {
        _ws = ws;
        Size = new Vector2(800, 600);
        SizeCondition = ImGuiCond.FirstUseEver;
        _panel = panel;
        _scheduler = scheduler;
        _gitManager = gitManager;
    }

    public override void Draw()
    {
        // Create a toolbar with improved import options
        using var toolbarStyle = ImRaii.PushStyle(ImGuiStyleVar.FramePadding, new Vector2(8, 4));
        
        // Toolbar is now empty since we moved both buttons
        
        toolbarStyle.Pop();

        // Draw the running macros panel at the top
        _panel.Draw();

        // Split window into sidebar and main content
        var sidebarWidth = 250f;
        var mainWidth = ImGui.GetWindowWidth() - sidebarWidth;

        using (var sidebar = ImRaii.Child("Sidebar", new(sidebarWidth, -1)))
            DrawSidebar();

        ImGui.SameLine();

        using var mainContent = ImRaii.Child("MainContent", new(mainWidth, -1));
        DrawMainContent();
    }

    private void DrawSidebar()
    {
        // Search bar
        if (ImGui.InputText("Search", ref searchText, 100))
        {
            // Filter macros based on search
        }

        // Add Folders header with buttons
        ImGui.Separator();
        
        // HEADER ROW WITH BUTTONS - complete redesign
        
        // First create a row to contain everything
        ImGui.BeginGroup();
        
        // 1. Draw the FOLDERS text with violet color
        ImGui.PushStyleColor(ImGuiCol.Text, ImGuiColors.DalamudViolet);
        ImGui.Text("FOLDERS");
        ImGui.PopStyleColor();
        
        // 2. Calculate spacing to place buttons on the same line
        float headerWidth = ImGui.CalcTextSize("FOLDERS").X;
        ImGui.SameLine(headerWidth + 10);
        
        // 3. Draw the buttons - use explicit styling and positioning
        float buttonSize = ImGui.GetFrameHeight() * 0.8f;
        Vector2 buttonDims = new Vector2(buttonSize, buttonSize);
        
        // New Folder button
        ImGui.PushFont(UiBuilder.IconFont);
        if (ImGui.Button($"{FontAwesomeIcon.FolderPlus.ToIconString()}##NewFolder", buttonDims))
        {
            // Create a new folder based on current selection
            string newPath;
            if (selectedFolderPath == "/" || string.IsNullOrEmpty(selectedFolderPath))
            {
                newPath = "/New Folder";
            }
            else
            {
                newPath = Path.Combine(selectedFolderPath, "New Folder").Replace("\\", "/");
            }

            // Ensure the name is unique
            int suffix = 1;
            string basePath = newPath;
            while (C.GetMacrosInFolder(newPath).Any() || C.GetSubfolders(newPath).Any())
            {
                newPath = $"{basePath} {suffix}";
                suffix++;
            }

            // Create folder by adding a dummy macro and then removing it
            var dummyMacro = new ConfigMacro
            {
                Name = "__dummy",
                Content = "",
                FolderPath = newPath
            };
            C.Macros.Add(dummyMacro);
            C.Macros.Remove(dummyMacro);
            C.Save();

            // Select the new folder
            selectedFolderPath = newPath;
            selectedMacroId = string.Empty;
            Svc.Chat.Print($"Created new folder: {newPath}");
        }
        
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Create a new folder");
        
        // New Macro button
        ImGui.SameLine(0, 5);
        if (ImGui.Button($"{FontAwesomeIcon.FileMedical.ToIconString()}##NewMacro", buttonDims))
        {
            var clipboard = ImGui.GetClipboardText();
            if (!string.IsNullOrEmpty(clipboard))
            {
                try
                {
                    // Create a new macro from clipboard content
                    var macro = new ConfigMacro
                    {
                        Name = "New Macro",
                        Content = clipboard,
                        FolderPath = selectedFolderPath
                    };
                    C.Macros.Add(macro);
                    C.Save();
                    selectedMacroId = macro.Id;
                    Svc.Chat.Print("Added new macro from clipboard");
                }
                catch (Exception ex)
                {
                    Svc.Chat.PrintError($"Failed to add macro: {ex.Message}");
                }
            }
            else
            {
                // Create an empty macro
                var macro = new ConfigMacro
                {
                    Name = "New Macro",
                    Content = "",
                    FolderPath = selectedFolderPath
                };
                C.Macros.Add(macro);
                C.Save();
                selectedMacroId = macro.Id;
                Svc.Chat.Print("Added new empty macro");
            }
        }
        
        ImGui.PopFont();
        
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Create a new macro (uses clipboard content if available)");
            
        ImGui.EndGroup();
        
        // Folder tree with drag and drop - now in a child window with explicit size
        ImGui.BeginChild("FolderTree", new Vector2(-1, -1), true);
        DrawFolderTree();
        ImGui.EndChild();
    }

    private void DrawFolderTree()
    {
        var folders = C.GetFolderTreeWithCounts().ToList();

        foreach (var ((path, depth, count), idx) in folders.WithIndex())
        {
            // Skip the root folder itself
            if (path == "/")
                continue;

            var folderName = path.Split('/').Last();
            var indent = new string(' ', depth * 2);
            var isCollapsed = collapsedFolders.Contains(path);
            var hasChildren = C.GetMacrosInFolderRecursive(path).Any() || C.GetSubfolders(path).Any();

            // Draw folder with collapse arrow
            if (hasChildren)
            {
                using var col = ImRaii.PushColor(ImGuiCol.Text, EzColor.OrangeBright.U32);
                using var tree = ImRaii.TreeNode($"{indent} {folderName} ({count})", ImGuiTreeNodeFlags.DefaultOpen);
                col.Pop();
                if (tree)
                {
                    ImGuiUtils.ContextMenu($"{folderName}_{idx}", ("Delete Folder", () => DeleteFolder(path)));

                    // Show macros in this folder
                    var macros = C.GetMacrosInFolder(path);
                    foreach (var macro in macros)
                    {
                        var macroIndent = new string(' ', (depth + 1) * 2);
                        var prefix = macro is GitMacro ? "📦 " : "";

                        // Macro node
                        if (ImGui.Selectable($"{macroIndent} {prefix}{macro.Name}", macro.Id == selectedMacroId))
                        {
                            selectedMacroId = macro.Id;
                            selectedFolderPath = path;
                        }

                        ImGuiUtils.ContextMenu($"{macro.Name}_{idx}", ("Delete", () => { macro.Delete(); if (selectedMacroId == macro.Id) selectedMacroId = string.Empty; }));
                    }
                }
                else
                    ImGuiUtils.ContextMenu($"{folderName}_{idx}", ("Delete Folder", () => DeleteFolder(path)));
            }
            else
            {
                // Folder without children
                if (ImGui.Selectable($"{indent} {folderName} ({count})", path == selectedFolderPath))
                {
                    selectedFolderPath = path;
                    selectedMacroId = string.Empty;
                }

                ImGuiUtils.ContextMenu($"{folderName}_{idx}", ("Delete Folder", () => DeleteFolder(path)));
            }
        }

        // Replace the "macros in root folder" section with macro settings UI
        ImGui.Separator();
        
        // Draw macro settings here
        if (ImGui.CollapsingHeader("Macro Settings", ImGuiTreeNodeFlags.DefaultOpen))
        {
            // Use a child window for better styling
            using var settingsChild = ImRaii.Child("SettingsPanel", new Vector2(-1, 250), true);
            
            // Check if a macro is selected to show macro-specific settings
            if (!string.IsNullOrEmpty(selectedMacroId))
            {
                var selectedMacro = C.GetMacro(selectedMacroId);
                if (selectedMacro != null)
                {
                    // Draw the macro-specific metadata editor
                    _metadataEditor.Draw(selectedMacro);
                }
                else
                {
                    ImGui.TextColored(ImGuiColors.DalamudGrey, "Selected macro not found.");
                }
            }
            else
            {
                // No macro selected, show a message
                ImGui.TextColored(ImGuiColors.DalamudGrey, "Select a macro to view and edit its settings.");
            }
        }
    }

    private void DeleteFolder(string folderPath)
    {
        // Get all macros in this folder and subfolders
        var macrosToDelete = C.GetMacrosInFolderRecursive(folderPath).ToList();

        // Delete each macro
        macrosToDelete.ForEach(m => m.Delete());

        // Clear selection if the selected macro was in this folder
        if (selectedMacroId != string.Empty && macrosToDelete.Any(m => m.Id == selectedMacroId))
            selectedMacroId = string.Empty;

        // Update selected folder if it was the deleted one
        if (selectedFolderPath == folderPath || selectedFolderPath.StartsWith(folderPath + "/"))
            selectedFolderPath = "/";

        Svc.Chat.Print($"Deleted folder {folderPath} and {macrosToDelete.Count} macros");
    }

    private void DrawMainContent()
    {
        if (string.IsNullOrEmpty(selectedMacroId))
        {
            DrawEmptyState();
            return;
        }

        var macro = C.GetMacro(selectedMacroId);
        if (macro == null) return;

        // Top bar with macro info and controls
        DrawMacroHeader(macro);
        // Bottom bar with execution controls
        DrawMacroControls(macro);
        // Macro content editor
        DrawMacroContent(macro);
    }

    private void DrawMacroHeader(IMacro macro)
    {
        using var header = ImRaii.Child("MacroHeader", new(-1, 60));

        // Macro name and type
        ImGui.Text($"Name: {macro.Name}");
        ImGui.SameLine();
        ImGui.Text($"Type: {macro.Type}");

        //var lang = macro.en;
        //if (ImGuiEx.EnumCombo("Language", ref lang))
        //{
        //    macro.Type = lang;
        //    C.Save();
        //}
        //ImGui.SameLine();

        // Action buttons
        if (ImGui.Button("Rename"))
        {
            // Show rename dialog
        }
        ImGui.SameLine();
        if (ImGui.Button("Duplicate")) { }
        //macro.Duplicate();
        ImGui.SameLine();
        if (ImGui.Button("Delete"))
        {
            //macro.Delete();
            //selectedMacroId = string.Empty;
        }

        // Git-specific controls
        if (macro is GitMacro gitMacro)
        {
            ImGui.Separator();

            if (ImGui.Button("Check for Updates"))
            {
                _ = _gitManager.CheckForUpdates(gitMacro);
            }
            ImGui.SameLine();

            if (ImGui.Button("Version History"))
            {
                showVersionHistory = true;
                versionHistory = null;
                _ = LoadVersionHistory(gitMacro);
            }
            ImGui.SameLine();

            if (gitMacro.HasUpdate)
            {
                if (ImGui.Button("Update Available"))
                {
                    _ = _gitManager.CheckForUpdates(gitMacro);
                }
            }

            if (showVersionHistory && versionHistory != null)
            {
                ImGui.Separator();
                ImGui.Text("Version History:");
                foreach (var commit in versionHistory)
                {
                    if (ImGui.Selectable($"{commit.CommitHash[..8]} - {commit.Commit.Message}"))
                    {
                        _ = _gitManager.DowngradeToVersion(gitMacro, commit.CommitHash);
                        showVersionHistory = false;
                    }
                }
            }
        }
    }

    private async Task LoadVersionHistory(GitMacro macro)
    {
        versionHistory = await _gitManager.GetVersionHistory(macro);
    }

    private void DrawMacroControls(ConfigMacro macro)
    {
        using var controls = ImRaii.Child("MacroControls", new Vector2(-1, 40));

        if (ImGui.Button("Edit"))
        {
            isEditing = !isEditing;
            if (isEditing)
            {
                editingContent = macro.Content;
            }
        }
        ImGui.SameLine();

        if (ImGui.Button("Copy"))
            ImGui.SetClipboardText(macro.Content);
        ImGui.SameLine();
        if (ImGui.Button("Run"))
            _scheduler.StartMacro(macro);
        ImGui.SameLine();

        if (ImGui.Button("Stop"))
            _scheduler.StopMacro(macro.Id);
    }

    private void DrawMacroContent(ConfigMacro macro)
    {
        using var _ = ImRaii.Child("MacroContent", new(-1, -1));
        var contents = macro.Content;
        if (ImGui.InputTextMultiline($"##{macro.Name}-editor", ref contents, 1_000_000, new Vector2(-1, -1)))
        {
            macro.Content = contents;
            C.Save();
        }
    }

    private void DrawEmptyState()
    {
        var center = ImGui.GetWindowSize() / 2;
        ImGui.SetCursorPos(center - new Vector2(100, 20));
        ImGui.Text("Select a macro or create a new one");
    }
}
