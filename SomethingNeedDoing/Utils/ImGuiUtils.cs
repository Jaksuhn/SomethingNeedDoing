using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Interfaces;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Utils;
public static class ImGuiUtils
{
    public static class Colours
    {
        public static EzColor Gold => new(0.847f, 0.733f, 0.49f);
    }

    public static class Icons
    {
        public const FontAwesomeIcon NativeMacro = FontAwesomeIcon.FileCode;
        public const FontAwesomeIcon LuaMacro = FontAwesomeIcon.Moon;
        public const FontAwesomeIcon GitMacro = FontAwesomeIcon.CodeBranch;
        public static FontAwesomeIcon GetMacroIcon(IMacro macro) => macro switch
        {
            ConfigMacro { IsGitMacro: true } => GitMacro,
            { Type: MacroType.Lua } => LuaMacro,
            _ => NativeMacro
        };
    }

    public static bool IconButton(FontAwesomeIcon icon, string tooltip = "", string id = "SNDButton", Vector2 size = default, bool disabled = false, bool active = false)
    {
        using var iconFont = ImRaii.PushFont(UiBuilder.IconFont);
        if (!id.StartsWith("##")) id = "##" + id;

        var disposables = new List<IDisposable>();

        if (disabled)
        {
            disposables.Add(ImRaii.PushColor(ImGuiCol.Text, ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled]));
            disposables.Add(ImRaii.PushColor(ImGuiCol.ButtonActive, ImGui.GetStyle().Colors[(int)ImGuiCol.Button]));
            disposables.Add(ImRaii.PushColor(ImGuiCol.ButtonHovered, ImGui.GetStyle().Colors[(int)ImGuiCol.Button]));
        }
        else if (active)
        {
            disposables.Add(ImRaii.PushColor(ImGuiCol.Button, ImGui.GetStyle().Colors[(int)ImGuiCol.ButtonActive]));
        }

        var pressed = ImGui.Button(icon.ToIconString() + id, size);

        foreach (var disposable in disposables)
            disposable.Dispose();

        iconFont?.Dispose();

        if (tooltip != string.Empty && ImGui.IsItemHovered())
            ImGui.SetTooltip(tooltip);

        return pressed;
    }

    public static T EnumRadioButtons<T>(T currentValue, string label = "", float itemWidth = 0) where T : Enum
    {
        var values = Enum.GetValues(typeof(T)).Cast<T>();
        var names = Enum.GetNames(typeof(T));
        var result = currentValue;

        if (!string.IsNullOrEmpty(label))
        {
            ImGui.TextUnformatted(label);
            ImGui.SameLine();
        }

        for (var i = 0; i < values.Count(); i++)
        {
            if (i > 0)
                ImGui.SameLine();

            if (itemWidth > 0)
                ImGui.SetNextItemWidth(itemWidth);

            if (ImGui.RadioButton(names[i], EqualityComparer<T>.Default.Equals(currentValue, values.ElementAt(i))))
                result = values.ElementAt(i);
        }
        return result;
    }

    public static void SetFocusIfAppearing()
    {
        if (ImGui.IsWindowAppearing())
            ImGui.SetKeyboardFocusHere();
    }

    public static void CenteredButtons(params (string text, Action action)[] buttons)
    {
        var group = new ImGuiEx.EzButtonGroup() { IsCentered = true };
        foreach (var button in buttons)
            group.Add(button.text, button.action);
        group.Draw();
    }

    public static bool EnumCheckboxes<T>(ref List<T> selectedValues, IEnumerable<T>? excludeValues = null) where T : Enum
    {
        var values = Enum.GetValues(typeof(T)).Cast<T>();
        var names = Enum.GetNames(typeof(T));
        var changed = false;

        for (var i = 0; i < values.Count(); i++)
        {
            var currentValue = values.ElementAt(i);
            if (excludeValues?.Contains(currentValue) == true)
                continue;

            var isSelected = selectedValues.Contains(currentValue);
            if (ImGui.Checkbox(names[i], ref isSelected))
            {
                changed = true;
                if (isSelected)
                    selectedValues.Add(currentValue);
                else
                    selectedValues.Remove(currentValue);
            }
        }
        return changed;
    }

    public static void DrawLink(Vector4 colour, string label, string url)
    {
        ImGuiEx.Text(colour, label);

        if (ImGui.IsItemHovered())
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);

            using var tooltip = ImRaii.Tooltip();
            if (tooltip.Success)
            {
                var pos = ImGui.GetCursorPos();
                ImGui.GetWindowDrawList().AddText(
                    UiBuilder.IconFont, 12,
                    ImGui.GetWindowPos() + pos + new Vector2(2),
                    ImGuiColors.DalamudGrey.ToUint(),
                    FontAwesomeIcon.ExternalLinkAlt.ToIconString()
                );
                ImGui.SetCursorPos(pos + new Vector2(20, 0));
                ImGuiEx.Text(ImGuiColors.DalamudGrey, url);
            }
        }

        if (ImGui.IsItemClicked())
        {
            Task.Run(() => Dalamud.Utility.Util.OpenLink(url));
        }
    }

    public static bool Button(Vector4 btnColour, FontAwesomeIcon icon, string text)
    {
        using var group = ImRaii.Group();
        using var _ = ImRaii.PushColor(ImGuiCol.Button, btnColour)
            .Push(ImGuiCol.ButtonHovered, new Vector4(btnColour.X + 1, btnColour.Y + 1, btnColour.Z + 1, btnColour.W))
            .Push(ImGuiCol.ButtonActive, new Vector4(btnColour.X + 2, btnColour.Y + 2, btnColour.Z + 2, btnColour.W));

        var id = $"##Button_{icon}_{text}";

        // Calculate minimum width based on text and icon
        float iconWidth;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
        var textWidth = ImGui.CalcTextSize(text).X;
        var style = ImGui.GetStyle();
        var minWidth = iconWidth + textWidth + style.FramePadding.X * 2 + style.ItemSpacing.X;
        var buttonSize = new Vector2(minWidth, ImGui.GetFrameHeight());

        var result = ImGui.Button(id, buttonSize);

        var buttonX = ImGui.GetItemRectMin().X;
        var buttonY = ImGui.GetItemRectMin().Y;
        var buttonHeight = ImGui.GetItemRectSize().Y;

        ImGui.SetCursorScreenPos(new Vector2(buttonX + style.FramePadding.X, buttonY));
        ImGui.AlignTextToFramePadding();

        using (ImRaii.PushFont(UiBuilder.IconFont))
            ImGui.TextUnformatted(icon.ToIconString());

        ImGui.SameLine(0, style.ItemSpacing.X);
        ImGui.Text(text);

        return result;
    }

    public static void Section(string name, Action content, bool indentContent = false, ImFontPtr? contentFont = null)
    {
        using var _ = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(7f));
        using var table = ImRaii.Table($"##Section_{name}", 1, ImGuiTableFlags.Borders | ImGuiTableFlags.SizingFixedFit);
        if (!table) return;

        ImGui.TableSetupColumn(name, ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, new EzColor(ImGuiColors.DalamudViolet) with { A = 0.2f });
        ImGuiEx.Text(name);
        ImGui.TableNextRow();
        ImGui.TableNextColumn();
        using var font = ImRaii.PushFont(contentFont ?? ImGui.GetFont());
        using (ImRaii.PushIndent(condition: indentContent))
            content();
    }

    public static void CollapsibleSection(string id, Action content, bool sameLine = true, bool indentContent = true)
    {
        if (sameLine) ImGui.SameLine();

        var storageId = ImGui.GetID($"##{id}_open");
        var isOpen = ImGui.GetStateStorage().GetBool(storageId, false);

        ImGuiEx.Icon(ImGuiColors.DalamudGrey, isOpen ? FontAwesomeIcon.CaretDown : FontAwesomeIcon.CaretRight);
        ImGui.NewLine();

        if (ImGui.IsItemClicked())
        {
            isOpen = !isOpen;
            ImGui.GetStateStorage().SetBool(storageId, isOpen);
        }

        if (isOpen)
        {
            using var _ = ImRaii.PushIndent(condition: indentContent);
            content();
        }
    }

    public static bool IconButtonWithNotification(FontAwesomeIcon icon, string notification, Vector4 notificationColor, string tooltip = "", Vector2 size = default)
    {
        var pressed = ImGuiEx.IconButton(icon, size: size);
        var drawList = ImGui.GetWindowDrawList();

        var circleRadius = 6f;
        var circleCenter = new Vector2(ImGui.GetItemRectMax().X, ImGui.GetItemRectMin().Y);

        drawList.AddCircleFilled(circleCenter, circleRadius, notificationColor.ToUint());

        var textSize = ImGui.CalcTextSize(notification);
        var textPos = circleCenter - textSize / 2;
        drawList.AddText(textPos, ImGuiColors.DalamudWhite.ToUint(), notification);

        if (tooltip != string.Empty)
            ImGuiEx.Tooltip(tooltip);

        return pressed;
    }

    /// <summary>
    /// Creates a menu item with an icon and text.
    /// </summary>
    /// <param name="icon">The icon to use.</param>
    /// <param name="label">The text to display next to the icon.</param>
    /// <param name="selected">Whether the item is selected.</param>
    /// <param name="enabled">Whether the item is enabled.</param>
    /// <returns>True if the menu item was clicked.</returns>
    public static bool IconMenuItem(FontAwesomeIcon icon, string label, bool selected = false, bool enabled = true)
    {
        // Create a unique ID for this menu item
        var menuId = $"##Menu_{icon}_{label}";

        // Use simple approach: create a MenuItem with just an ID, then overlay text
        var result = ImGui.MenuItem(menuId, string.Empty, selected, enabled);

        // Only draw the icon and text if we should be rendering them
        // (this prevents drawing when the menu is closed)
        if (ImGui.IsItemVisible())
        {
            // Get position for drawing
            var itemX = ImGui.GetItemRectMin().X;
            var itemY = ImGui.GetItemRectMin().Y;
            var itemHeight = ImGui.GetItemRectSize().Y;

            // Backup cursor position
            var cursorPos = ImGui.GetCursorPos();

            // Center content vertically
            var offsetY = (itemHeight - ImGui.GetTextLineHeight()) * 0.5f;

            // Draw icon with icon font - position at start of item
            ImGui.SetCursorScreenPos(new Vector2(itemX + 5, itemY + offsetY));
            ImGui.PushFont(UiBuilder.IconFont);
            ImGui.Text(icon.ToIconString());
            var iconWidth = ImGui.CalcTextSize(icon.ToIconString()).X;
            ImGui.PopFont();

            // Draw the label after the icon
            ImGui.SetCursorScreenPos(new Vector2(itemX + 10 + iconWidth, itemY + offsetY));
            ImGui.Text(label);

            // Restore cursor position
            ImGui.SetCursorPos(cursorPos);
        }

        return result;
    }
}
