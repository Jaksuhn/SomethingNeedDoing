using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Scheduler;

namespace SomethingNeedDoing.Gui.Tabs;
public static class HelpGeneralTab
{
    public static void DrawTab()
    {
        using var child = ImRaii.Child(nameof(HelpGeneralTab));
        ImGuiUtils.Section(P.Name, () =>
        {
            ImGui.TextWrapped($"{P.Name} is an expansion of the native macro system, with smart helpers, additional commands and modifiers, and unlimited macros.");
            ImGui.TextWrapped("It also supports scripting with Lua, so you can write macros that are more complex than the native system can handle.");
        });

        ImGuiUtils.Section("Status Monitoring", () =>
        {
            ImGui.TextWrapped("That status window shows all currently running macros and their current state");
            ImGui.Spacing();

            ImGuiEx.Text(ImGuiColors.DalamudOrange, "Macro States");
            ImGui.BulletText("Ready: Macro has been loaded but hasn't started running");
            ImGui.BulletText("Running: Macro is currently executing");
            ImGui.BulletText("Paused: Macro execution has been temporarily stopped");
            ImGui.BulletText("Completed: Macro has finished execution");
            ImGui.BulletText("Failed: Macro encountered an error during execution");
        });

        ImGuiUtils.Section("Trigger Events", () =>
        {
            ImGui.TextWrapped("Macros can be configured to trigger automatically based on specific game events:");
            Enum.GetNames<TriggerEvent>().Each(name => ImGui.BulletText(name));

            ImGui.TextWrapped("Lua macros can also be configured to have individual functions trigger automatically (provided the script was already running).");
            ImGui.TextWrapped($"Any function that begins with the name of a TriggerEvent will be registered in the {nameof(TriggerEventManager)} when the script is started.");
            ImGui.TextWrapped($"For {TriggerEvent.OnAddonEvent} specifically, the event name must be followed by the addon name and event type, such as");
            ImGui.SameLine();
            ImGuiEx.Text(ImGuiColors.DalamudOrange, "OnAddonEvent_SelectYesno_PostSetup");
        });

        ImGuiUtils.Section("Macro Metadata", () =>
        {
            ImGuiEx.Text(ImGuiColors.DalamudOrange, "General");
            ImGui.TextWrapped("Macros can be configured with metadata to provide specific configurations to the framework regarding macro execution.");
            ImGui.TextWrapped("The metadata can be edited in the Macros Settings section when a macro is selected in the library.");
            ImGui.TextWrapped("The metadata can be written to the file using the button provided in the above section. This is crucial for macros stored remotely (i.e. github) for the framework to know what settings to use when they're imported");

            ImGuiEx.Text(ImGuiColors.DalamudOrange, "Dependencies and conflicts");
            ImGui.TextWrapped("Macros can also be configured to require other plugins to run, and a message will be printed if the requirement is not met.");
            ImGui.TextWrapped("Similiarly, macros can be configured to disable other plugins while running, though this requires plugins to be pre-defined in the framework to support this.");
            ImGui.TextWrapped("Like plugin dependencies, macros can support relying on other macros, whether they're local (already loaded into snd) or remote (i.e. github)");
        });

        ImGuiUtils.Section("Git Integration", () =>
        {
            ImGui.TextWrapped("Macros can now be connected to a github url and auto converted into a \"Git Macro\"");
            ImGui.TextWrapped("Git macros support auto updating when a new version is released (checked on startup), and going between versions via the version history modal (found in the Macro Settings section)");
            ImGui.TextWrapped("A macro is automatically identified as a git macro when it has a repository url in its metadata. To convert a git macro back into a local macro, simply wipe the url or click the Reset Git Data button in the settings");
        });
    }
}
