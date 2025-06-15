using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Interfaces;
using System.Reflection;

namespace SomethingNeedDoing.Gui.Tabs;

public class HelpCommandsTab
{
    private readonly Dictionary<string, CommandInfo> Commands = [];
    private readonly Dictionary<string, ModifierInfo> Modifiers = [];

    public HelpCommandsTab()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var commandTypes = assembly.GetTypes().Where(t => typeof(IMacroCommand).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
        foreach (var type in commandTypes)
        {
            var doc = type.GetCustomAttribute<GenericDocAttribute>();
            if (doc == null) continue;

            var name = type.Name.ToLower().Replace("command", "");
            Commands[name] = new CommandInfo(doc.Description, doc.Parameters, doc.Examples);
        }

        var modifierTypes = assembly.GetTypes().Where(t => typeof(IMacroModifier).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
        foreach (var type in modifierTypes)
        {
            var doc = type.GetCustomAttribute<GenericDocAttribute>();
            if (doc == null) continue;

            var name = type.Name.ToLower().Replace("modifier", "");
            Modifiers[name] = new ModifierInfo(doc.Description, doc.Parameters, doc.Examples);
        }
    }

    public void DrawTab()
    {
        using var child = ImRaii.Child(nameof(HelpCommandsTab));
        ImGuiUtils.Section("Commands", () =>
        {
            foreach (var (name, info) in Commands.OrderBy(x => x.Key))
            {
                ImGuiEx.Text(ImGuiColors.DalamudOrange, $"/{name}");
                ImGui.SameLine();
                ImGuiEx.Text(ImGuiColors.DalamudGrey, $"→ {info.Description}");

                if (info.Examples.Length > 0)
                    ImGuiUtils.CollapsibleSection(name, () => info.Examples.Each(x => ImGuiEx.Text(ImGuiColors.DalamudGrey, x)));
                ImGui.Separator();
            }
        });

        ImGuiUtils.Section("Modifiers", () =>
        {
            foreach (var (name, info) in Modifiers.OrderBy(x => x.Key))
            {
                ImGuiEx.Text(ImGuiColors.DalamudOrange, $"<{name}>");
                ImGui.SameLine();
                ImGuiEx.Text(ImGuiColors.DalamudGrey, $"→ {info.Description}");

                if (info.Examples.Length > 0)
                    ImGuiUtils.CollapsibleSection(name, () => info.Examples.Each(x => ImGuiEx.Text(ImGuiColors.DalamudGrey, x)));
                ImGui.Separator();
            }
        });
    }

    private record CommandInfo(string Description, string[] Parameters, string[] Examples);
    private record ModifierInfo(string Description, string[] Parameters, string[] Examples);
}
