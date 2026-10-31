using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.Gui.Modals;

/// <summary>
/// A modal asking for a single line of text, e.g. a new name.
/// </summary>
public static class TextInputModal
{
    private const string PopupId = $"TextInputPopup##{nameof(TextInputModal)}";
    private static readonly Vector2 Size = new(400, 170);
    private static bool IsOpen;

    private static FontAwesomeIcon _icon;
    private static string _title = string.Empty;
    private static string _buffer = string.Empty;
    private static string _confirmLabel = string.Empty;
    private static Func<string, string?>? _validate;
    private static Action<string>? _onConfirm;

    /// <summary>
    /// Opens the modal.
    /// </summary>
    /// <param name="icon">Icon shown next to the title.</param>
    /// <param name="title">The title.</param>
    /// <param name="initial">The initial text.</param>
    /// <param name="confirmLabel">The label of the confirm button.</param>
    /// <param name="validate">Returns an error message for invalid input, or null if valid.</param>
    /// <param name="onConfirm">Called with the entered text when confirmed.</param>
    public static void Open(FontAwesomeIcon icon, string title, string initial, string confirmLabel, Func<string, string?>? validate, Action<string> onConfirm)
    {
        IsOpen = true;
        (_icon, _title, _buffer, _confirmLabel, _validate, _onConfirm) = (icon, title, initial, confirmLabel, validate, onConfirm);
    }

    public static void OpenRenameMacro(IMacro macro)
        => Open(FontAwesomeHelper.IconRename, "Rename Macro", macro.Name, "Rename", null, name =>
        {
            if (C.GetMacro(macro.Id) is ConfigMacro configMacro)
                configMacro.Rename(name);
        });

    public static void OpenRenameFolder(string folderPath)
        => Open(FontAwesomeHelper.IconRename, "Rename Folder", folderPath, "Rename", FolderExistsError, name => C.RenameFolder(folderPath, name));

    public static void OpenCreateFolder()
        => Open(FontAwesomeHelper.IconFolder, "Create New Folder", "New Folder", "Create", FolderExistsError, name =>
        {
            // Create a dummy macro in the folder to ensure it exists (TODO: find a way around this?)
            C.Macros.Add(new ConfigMacro
            {
                Name = C.GetUniqueMacroName($"{name} Template"),
                Content = "// Add your macro commands here",
                Type = MacroType.Native,
                FolderPath = name
            });
            C.Save();
        });

    private static string? FolderExistsError(string name) => C.GetFolderPaths().Contains(name) ? $"Folder '{name}' already exists." : null;

    private static void Close()
    {
        IsOpen = false;
        ImGui.CloseCurrentPopup();
    }

    public static void DrawModal()
    {
        if (!IsOpen) return;

        ImGui.OpenPopup(PopupId);
        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(Size);

        using var style = ImRaii.PushStyle(ImGuiStyleVar.WindowPadding, new Vector2(15, 15));
        using var popup = ImRaii.PopupModal(PopupId, ref IsOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
        if (!popup) return;

        ImGuiEx.Icon(_icon);
        ImGui.SameLine();
        ImGui.Text(_title);
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.SetNextItemWidth(-1);
        ImGuiUtils.SetFocusIfAppearing();
        var enterPressed = ImGui.IsKeyPressed(ImGuiKey.Enter) && ImGui.IsWindowFocused();
        using (ImRaii.PushColor(ImGuiCol.FrameBg, new Vector4(0.15f, 0.15f, 0.15f, 1.0f)))
            ImGui.InputText("##TextInput", ref _buffer, 100);

        ImGui.Spacing();

        var error = string.IsNullOrWhiteSpace(_buffer) ? null : _validate?.Invoke(_buffer);
        if (error != null)
            ImGuiEx.Text(ImGuiColors.DalamudRed, error);

        var confirmed = false;
        using (ImRaii.Disabled(error != null))
        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.3f, 0.5f, 0.3f, 1.0f)).Push(ImGuiCol.ButtonHovered, new Vector4(0.4f, 0.6f, 0.4f, 1.0f)))
            confirmed = ImGui.Button(_confirmLabel, new Vector2(150, 0)) || (enterPressed && error == null);

        if (confirmed && !string.IsNullOrWhiteSpace(_buffer))
        {
            try
            {
                _onConfirm?.Invoke(_buffer);
                Close();
            }
            catch (Exception ex)
            {
                FrameworkLogger.Error(ex, $"Error in {_title}");
            }
        }

        ImGui.SameLine();

        using (ImRaii.PushColor(ImGuiCol.Button, new Vector4(0.5f, 0.3f, 0.3f, 1.0f)).Push(ImGuiCol.ButtonHovered, new Vector4(0.6f, 0.4f, 0.4f, 1.0f)))
            if (ImGui.Button("Cancel", new Vector2(150, 0)) || (ImGui.IsKeyPressed(ImGuiKey.Escape) && ImGui.IsWindowFocused()))
                Close();
    }
}
