using Dalamud.Interface;
using ImGuiNET;

namespace SomethingNeedDoing.Utils;

/// <summary>
/// Constants for FontAwesome icons used throughout the application
/// </summary>
public static class FontAwesomeHelper
{
    // General UI
    public const FontAwesomeIcon IconHome = FontAwesomeIcon.Home;
    public const FontAwesomeIcon IconFolder = FontAwesomeIcon.Folder;
    public const FontAwesomeIcon IconNew = FontAwesomeIcon.Plus;
    public const FontAwesomeIcon IconImport = FontAwesomeIcon.FileImport;
    public const FontAwesomeIcon IconExport = FontAwesomeIcon.FileExport;
    
    // Actions
    public const FontAwesomeIcon IconPlay = FontAwesomeIcon.PlayCircle;
    public const FontAwesomeIcon IconStop = FontAwesomeIcon.StopCircle;
    public const FontAwesomeIcon IconPause = FontAwesomeIcon.Pause;
    public const FontAwesomeIcon IconEdit = FontAwesomeIcon.Edit;
    public const FontAwesomeIcon IconRename = FontAwesomeIcon.Pen;
    public const FontAwesomeIcon IconDelete = FontAwesomeIcon.TrashAlt;
    public const FontAwesomeIcon IconCopy = FontAwesomeIcon.Clipboard;
    
    // Macro types
    public const FontAwesomeIcon IconNativeMacro = FontAwesomeIcon.FileCode;
    public const FontAwesomeIcon IconLuaMacro = FontAwesomeIcon.Moon;  // Lua logo is moon-like
    public const FontAwesomeIcon IconGitMacro = FontAwesomeIcon.CodeBranch;
    
    // Status indicators
    public const FontAwesomeIcon IconRunning = FontAwesomeIcon.PlayCircle;
    public const FontAwesomeIcon IconSuccess = FontAwesomeIcon.Check;
    public const FontAwesomeIcon IconError = FontAwesomeIcon.Times;
    public const FontAwesomeIcon IconWarning = FontAwesomeIcon.ExclamationTriangle;
    public const FontAwesomeIcon IconCheck = FontAwesomeIcon.Check;
    
    // Settings and menus
    public const FontAwesomeIcon IconSettings = FontAwesomeIcon.Cog;
    public const FontAwesomeIcon IconHelp = FontAwesomeIcon.QuestionCircle;
    public const FontAwesomeIcon IconInfo = FontAwesomeIcon.InfoCircle;

    // Navigation icons
    public const FontAwesomeIcon IconFolderOpen = FontAwesomeIcon.FolderOpen;
    public const FontAwesomeIcon IconCollapsed = FontAwesomeIcon.ChevronRight;
    public const FontAwesomeIcon IconExpanded = FontAwesomeIcon.ChevronDown;

    // Tab icons
    public const FontAwesomeIcon IconMacros = FontAwesomeIcon.FileAlt;
    public const FontAwesomeIcon IconRunningStatus = FontAwesomeIcon.Spinner;
    public const FontAwesomeIcon IconPausedStatus = FontAwesomeIcon.PauseCircle;
    public const FontAwesomeIcon IconCompletedStatus = FontAwesomeIcon.CheckCircle;
    public const FontAwesomeIcon IconErrorStatus = FontAwesomeIcon.ExclamationTriangle;

    // Search and filter
    public const FontAwesomeIcon IconSearch = FontAwesomeIcon.Search;
    public const FontAwesomeIcon IconFilter = FontAwesomeIcon.Filter;
    public const FontAwesomeIcon IconClear = FontAwesomeIcon.TimesCircle;
    public const FontAwesomeIcon IconSortAsc = FontAwesomeIcon.SortAmountUp;
    public const FontAwesomeIcon IconSortDesc = FontAwesomeIcon.SortAmountDown;
    public const FontAwesomeIcon IconXmark = FontAwesomeIcon.Times;
    
    // Text formatting
    public const FontAwesomeIcon IconIndent = FontAwesomeIcon.Indent;
    public const FontAwesomeIcon IconAlignLeft = FontAwesomeIcon.AlignLeft;
    public const FontAwesomeIcon IconAlignRight = FontAwesomeIcon.AlignRight;
    public const FontAwesomeIcon IconAlignCenter = FontAwesomeIcon.AlignCenter;
    public const FontAwesomeIcon IconAlignJustify = FontAwesomeIcon.AlignJustify;

    // Settings controls
    public const FontAwesomeIcon IconToggleOn = FontAwesomeIcon.ToggleOn;
    public const FontAwesomeIcon IconToggleOff = FontAwesomeIcon.ToggleOff;

    // Collapsible panels
    public const FontAwesomeIcon IconCollapsedPanel = FontAwesomeIcon.PlusSquare;
    public const FontAwesomeIcon IconExpandedPanel = FontAwesomeIcon.MinusSquare;
    
    /// <summary>
    /// Gets the string representation of a FontAwesome icon with proper font handling
    /// </summary>
    /// <param name="icon">The FontAwesome icon to convert</param>
    /// <returns>The string representation of the icon</returns>
    public static string GetIconString(FontAwesomeIcon icon)
    {
        return icon.ToIconString();
    }
    
    /// <summary>
    /// Draws a FontAwesome icon by pushing the icon font, drawing the icon, and popping the font
    /// </summary>
    /// <param name="icon">The FontAwesome icon to draw</param>
    public static void DrawIcon(FontAwesomeIcon icon)
    {
        ImGui.PushFont(UiBuilder.IconFont);
        ImGui.TextUnformatted(icon.ToIconString());
        ImGui.PopFont();
    }
}
