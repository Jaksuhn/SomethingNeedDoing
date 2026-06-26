using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.Gui.Modals;
using SomethingNeedDoing.Managers;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Gui.Tabs;

public class MacroSettingsSection(IMacroScheduler scheduler, DependencyFactory dependencyFactory, VersionHistoryModal versionHistoryModal, MetadataParser metadataParser, GitMacroManager gitManager, IEnumerable<IDisableable> disableablePlugins)
{
    private string _pluginDependency = string.Empty;
    private string _pluginToDisable = string.Empty;
    private string _gitUrl = string.Empty;
    private string _localFilePath = string.Empty;
    private DependencyType _dependencyType = DependencyType.Local;
    private LocalDependencyType _localDependencyType = LocalDependencyType.Macro;
    private readonly List<string> _disableablePluginNames = [.. disableablePlugins.Select(p => p.InternalName)];

    public Action? OnContentUpdated { get; set; } // for refreshing after writing the metadata

    public void Draw(ConfigMacro? selectedMacro)
    {
        using var child = ImRaii.Child("SettingsChild", new(-1, ImGui.GetContentRegionAvail().Y), false);
        if (!child) return;

        if (selectedMacro != null)
        {
            DrawMacroConfig(selectedMacro);
            DrawGeneralInfo(selectedMacro);
            DrawGitInfo(selectedMacro);

            if (selectedMacro.Type is MacroType.Native)
                DrawCraftLoop(selectedMacro);

            DrawTriggers(selectedMacro);
            DrawPluginDependencies(selectedMacro);
            DrawPluginConflicts(selectedMacro);
            DrawDependencies(selectedMacro);
        }
        else
            ImGui.TextColored(ImGuiColors.DalamudGrey, "Select a macro to view and edit its settings");
    }

    private void DrawMacroConfig(ConfigMacro selectedMacro)
    {
        if (selectedMacro.Metadata.Configs.Count == 0) return;
        ImGuiUtils.Section("Macro Configuration", () =>
        {
            var sectionOrder = new List<string>();
            var grouped = new Dictionary<string, List<KeyValuePair<string, MacroConfigItem>>>();

            foreach (var kvp in selectedMacro.Metadata.Configs)
            {
                var section = kvp.Value.Section ?? string.Empty;
                if (!grouped.TryGetValue(section, out var configs))
                {
                    sectionOrder.Add(section);
                    configs = [];
                    grouped[section] = configs;
                }

                configs.Add(kvp);
            }

            foreach (var section in sectionOrder)
            {
                var configs = grouped[section];
                if (string.IsNullOrEmpty(section))
                {
                    configs.ForEach(kvp => DrawConfigItem(kvp.Key, kvp.Value));
                }
                else if (ImGui.CollapsingHeader(section, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.CollapsingHeader))
                {
                    using var indent = ImRaii.PushIndent();
                    configs.ForEach(kvp => DrawConfigItem(kvp.Key, kvp.Value));
                }
            }
        });
    }

    private void DrawConfigItem(string configName, MacroConfigItem configValue)
    {
        using var _ = ImRaii.PushId(configName);

        ImGui.AlignTextToFramePadding();
        ImGuiEx.Text(ImGuiColors.DalamudOrange, configName);
        ImGui.SameLine();
        ImGuiEx.Text(ImGuiColors.DalamudGrey, configValue.Description);

        ImGui.SameLine(ImGui.GetContentRegionAvail().X - 80);
        using (ImRaii.Disabled(configValue.IsValueDefault()))
        {
            if (ImGui.Button("Reset", new Vector2(70, 0)))
            {
                configValue.Value = configValue.DefaultValue;
                C.Save();
            }
        }
        ImGuiEx.Tooltip($"Reset to default value: {configValue.DefaultValue}");
        ImGui.Spacing();

        var valueChanged = false;
        switch (configValue.Type)
        {
            case var t when t == typeof(int):
                var intValue = configValue.Value.ToInt();
                var intMin = configValue.MinValue != null ? configValue.MinValue.ToInt() : int.MinValue;
                var intMax = configValue.MaxValue != null ? configValue.MaxValue.ToInt() : int.MaxValue;

                ImGui.SetNextItemWidth(200);
                if (ImGui.InputInt($"##{configName}Value", ref intValue))
                {
                    if (intValue < intMin) intValue = intMin;
                    if (intValue > intMax) intValue = intMax;
                    configValue.Value = intValue;
                    valueChanged = true;
                }

                if (configValue.MinValue != null || configValue.MaxValue != null)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, $"Range: {intMin} - {intMax}");
                }
                break;

            case var t when t == typeof(float) || t == typeof(double):
                var floatValue = configValue.Value.ToFloat();
                var floatMin = configValue.MinValue != null ? configValue.MinValue.ToFloat() : float.MinValue;
                var floatMax = configValue.MaxValue != null ? configValue.MaxValue.ToFloat() : float.MaxValue;

                ImGui.SetNextItemWidth(200);
                if (ImGui.InputFloat($"##{configName}Value", ref floatValue))
                {
                    if (floatValue < floatMin) floatValue = floatMin;
                    if (floatValue > floatMax) floatValue = floatMax;
                    configValue.Value = floatValue;
                    valueChanged = true;
                }

                if (configValue.MinValue != null || configValue.MaxValue != null)
                {
                    ImGui.SameLine();
                    ImGui.TextColored(ImGuiColors.DalamudGrey, $"Range: {floatMin:F2} - {floatMax:F2}");
                }
                break;

            case var t when t == typeof(bool):
                var boolValue = configValue.Value.ToBool();
                ImGui.SetNextItemWidth(200);
                if (ImGui.Checkbox($"##{configName}Value", ref boolValue))
                {
                    configValue.Value = boolValue;
                    valueChanged = true;
                }
                break;

            case var t when t == typeof(List<string>):
                if (configValue.IsChoice)
                {
                    var currentChoice = configValue.Value?.ToString() ?? "";
                    var choices = configValue.Choices.ToArray();

                    if (choices.Length > 0)
                    {
                        var currentIndex = Array.IndexOf(choices, currentChoice);
                        if (currentIndex == -1) currentIndex = 0;

                        ImGui.SetNextItemWidth(200);
                        if (ImGui.Combo($"##{configName}Value", ref currentIndex, choices, choices.Length))
                        {
                            configValue.Value = choices[currentIndex];
                            valueChanged = true;
                        }
                    }
                    else
                        ImGui.TextColored(ImGuiColors.DalamudRed, "No choices defined");
                }
                else
                {
                    var list = configValue.Value as List<string> ?? [];

                    ImGui.SetNextItemWidth(200);
                    var entry = "";
                    if (ImGui.InputText($"##{configName}Add", ref entry, 512, ImGuiInputTextFlags.EnterReturnsTrue))
                    {
                        list.Add(entry);
                        valueChanged = true;
                    }

                    for (var i = 0; i < list.Count; i++)
                    {
                        using var id = ImRaii.PushId($"{configName}_{i}");
                        ImGui.SetNextItemWidth(200);
                        var item = list[i];
                        if (ImGui.InputText("", ref item, 512))
                        {
                            list[i] = item;
                            valueChanged = true;
                        }

                        ImGui.SameLine();
                        if (ImGui.Button("Remove"))
                        {
                            list.RemoveAt(i);
                            valueChanged = true;
                            break;
                        }
                    }

                    configValue.Value = list;
                }
                break;

            case var t when t == typeof(string):
                if (configValue.IsChoice)
                {
                    var currentChoice = configValue.Value?.ToString() ?? "";
                    var choices = configValue.Choices.ToArray();

                    if (choices.Length > 0)
                    {
                        var currentIndex = Array.IndexOf(choices, currentChoice);
                        if (currentIndex == -1) currentIndex = 0;

                        ImGui.SetNextItemWidth(200);
                        if (ImGui.Combo($"##{configName}Value", ref currentIndex, choices, choices.Length))
                        {
                            configValue.Value = choices[currentIndex];
                            valueChanged = true;
                        }
                    }
                    else
                        ImGui.TextColored(ImGuiColors.DalamudRed, "No choices defined");
                }
                else
                {
                    var stringValue = configValue.Value.ToString() ?? string.Empty;
                    ImGui.SetNextItemWidth(300);

                    var isValid = true;
                    var validationMessage = string.Empty;
                    if (!string.IsNullOrEmpty(configValue.ValidationPattern))
                    {
                        try
                        {
                            var regex = new System.Text.RegularExpressions.Regex(configValue.ValidationPattern);
                            isValid = regex.IsMatch(stringValue);
                            if (!isValid)
                                validationMessage = configValue.ValidationMessage ?? "Value does not match pattern";
                        }
                        catch (Exception ex)
                        {
                            isValid = false;
                            validationMessage = $"Invalid validation pattern: {ex.Message}";
                        }
                    }

                    using (ImRaii.PushColor(ImGuiCol.Text, isValid ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed, !string.IsNullOrEmpty(configValue.ValidationPattern)))
                    {
                        if (ImGui.InputText($"##{configName}Value", ref stringValue, 1000))
                        {
                            configValue.Value = stringValue;
                            valueChanged = true;
                        }
                    }

                    if (!string.IsNullOrEmpty(configValue.ValidationPattern))
                    {
                        ImGui.SameLine();
                        ImGuiEx.Icon(isValid ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed, isValid ? FontAwesomeIcon.Check : FontAwesomeIcon.ExclamationTriangle);

                        if (!isValid && !string.IsNullOrEmpty(validationMessage))
                            ImGuiEx.Tooltip(validationMessage);
                        else if (isValid)
                            ImGuiEx.Tooltip("Value matches validation pattern");
                    }
                }
                break;

            default:
                var defaultValue = configValue.Value.ToString() ?? string.Empty;
                ImGui.SetNextItemWidth(300);
                if (ImGui.InputText($"##{configName}Value", ref defaultValue, 1000))
                {
                    configValue.Value = defaultValue;
                    valueChanged = true;
                }
                break;
        }

        if (valueChanged)
            C.Save();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();
    }

    private void DrawGeneralInfo(ConfigMacro selectedMacro)
    {
        ImGuiUtils.Section("General Information", () =>
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text("Author:");
            ImGui.SameLine(100);

            var author = selectedMacro.Metadata.Author ?? string.Empty;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText("##Author", ref author, 100))
            {
                selectedMacro.Metadata.Author = author;
                C.Save();
            }

            ImGui.AlignTextToFramePadding();
            ImGui.Text("Version:");
            ImGui.SameLine(100);

            var version = selectedMacro.Metadata.Version ?? string.Empty;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText("##Version", ref version, 50))
            {
                selectedMacro.Metadata.Version = version;
                C.Save();
            }

            ImGui.AlignTextToFramePadding();
            ImGui.Text("Description:");

            var description = selectedMacro.Metadata.Description ?? string.Empty;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputTextMultiline("##Description", ref description, 1000, new Vector2(-1, 100)))
            {
                selectedMacro.Metadata.Description = description;
                C.Save();
            }

            if (ImGui.Button("Write Metadata to Content"))
            {
                if (metadataParser.WriteMetadata(selectedMacro, OnContentUpdated))
                    FrameworkLogger.Debug($"Wrote metadata to macro {selectedMacro.Name}");
                else
                    FrameworkLogger.Error($"Failed to write metadata to macro {selectedMacro.Name}");
            }
            ImGuiEx.Tooltip("Writes the current metadata (author, version, description, dependencies, triggers) to the macro content. If metadata already exists, it will be updated.");

            ImGui.SameLine();

            if (ImGui.Button("Read Metadata from Content"))
            {
                selectedMacro.Metadata = metadataParser.ParseMetadata(selectedMacro.Content);
                C.Save();
            }
            ImGuiEx.Tooltip("Reads metadata (author, version, description, dependencies, triggers) from the macro content and updates the settings.");
        });
    }

    private void DrawGitInfo(ConfigMacro selectedMacro)
    {
        ImGuiUtils.Section("Git Information", () =>
        {
            ImGui.AlignTextToFramePadding();
            ImGui.Text("GitHub URL:");
            ImGui.SameLine(100);

            var repoUrl = selectedMacro.GitInfo.RepositoryUrl;
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputText("##RepoUrl", ref repoUrl, 500))
            {
                selectedMacro.GitInfo.RepositoryUrl = repoUrl;
                C.Save();
            }
            ImGuiEx.Tooltip("Enter a GitHub URL (e.g., https://github.com/owner/repo/blob/branch/path)");

            if (selectedMacro.IsGitMacro)
            {
                ImGui.AlignTextToFramePadding();
                var autoUpdate = selectedMacro.GitInfo.AutoUpdate;
                if (ImGui.Checkbox("Auto Update", ref autoUpdate))
                {
                    selectedMacro.GitInfo.AutoUpdate = autoUpdate;
                    C.Save();
                }

                var group = new ImGuiEx.EzButtonGroup();
                group.AddIconWithText(FontAwesomeIcon.Download, "Import", () =>
                {
                    if (!string.IsNullOrWhiteSpace(repoUrl))
                    {
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await gitManager.AddGitInfoToMacro(selectedMacro, repoUrl);
                            }
                            catch (Exception ex)
                            {
                                FrameworkLogger.Error(ex, $"Failed to import macro from {repoUrl}");
                            }
                        });
                    }
                });
                group.AddIconWithText(FontAwesomeIcon.History, "Version History", () => versionHistoryModal.Open(selectedMacro));
                group.AddIconWithText(FontAwesomeIcon.Sync, "Reset Git Info",
                    () => { selectedMacro.GitInfo = new GitInfo(); C.Save(); }, "Wipes all git information and reverts this macro back to a standard local macro.",
                    new() { ButtonColor = EzColor.Red });
                group.Draw();
            }
        });
    }

    private void DrawCraftLoop(ConfigMacro selectedMacro)
    {
        ImGuiUtils.Section("CraftLoop Settings", () =>
        {
            var craftingLoop = selectedMacro.Metadata.CraftingLoop;
            if (ImGui.Checkbox("Enable Crafting Loop", ref craftingLoop))
            {
                selectedMacro.Metadata.CraftingLoop = craftingLoop;
                C.Save();
            }

            if (craftingLoop)
            {
                ImGui.Indent(20);

                var loopCount = selectedMacro.Metadata.CraftLoopCount;
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
        });
    }

    private void DrawTriggers(ConfigMacro selectedMacro)
    {
        ImGuiUtils.Section("Trigger Events", () =>
        {
            var events = new List<TriggerEvent>(selectedMacro.Metadata.TriggerEvents);
            if (ImGuiUtils.EnumCheckboxes(ref events, [TriggerEvent.None]))
                selectedMacro.SetTriggerEvents(scheduler, events);

            // Show addon event configuration only when OnAddonEvent is selected
            if (events.Contains(TriggerEvent.OnAddonEvent))
            {
                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                ImGui.Text("Addon Event Configuration");
                ImGui.Spacing();

                var addonConfig = selectedMacro.Metadata.AddonEventConfig ?? new AddonEventConfig();
                var addonName = addonConfig.AddonName;
                var eventType = addonConfig.EventType;

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Addon Name:");
                ImGui.SameLine(100);

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGui.InputText("##AddonName", ref addonName, 100))
                {
                    addonConfig.AddonName = addonName;
                    selectedMacro.Metadata.AddonEventConfig = addonConfig;
                    C.Save();
                }

                ImGui.AlignTextToFramePadding();
                ImGui.Text("Event Type:");
                ImGui.SameLine(100);

                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                if (ImGuiEx.EnumCombo("##EventType", ref eventType))
                {
                    addonConfig.EventType = eventType;
                    selectedMacro.Metadata.AddonEventConfig = addonConfig;
                    C.Save();
                }

                if (ImGui.Button("Clear Addon Event Config"))
                {
                    selectedMacro.Metadata.AddonEventConfig = null;
                    C.Save();
                }
            }
        });
    }

    private void DrawPluginDependencies(ConfigMacro selectedMacro)
    {
        ImGuiUtils.Section("Plugin Dependencies", () =>
        {
            var installedPlugins = Svc.PluginInterface.InstalledPlugins
                .Where(p => p.IsLoaded)
                .Select(p => p.InternalName)
                .OrderBy(p => p)
                .ToList();

            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGuiEx.Combo("##PluginSelector", ref _pluginDependency, installedPlugins))
            {
                if (!selectedMacro.Metadata.PluginDependecies.Contains(_pluginDependency))
                {
                    var newDeps = selectedMacro.Metadata.PluginDependecies.ToList();
                    newDeps.Add(_pluginDependency);
                    selectedMacro.Metadata.PluginDependecies = [.. newDeps];
                    C.Save();
                }
            }

            ImGui.Spacing();

            if (selectedMacro.Metadata.PluginDependecies.Length == 0)
                ImGui.TextColored(ImGuiColors.DalamudGrey, "No plugin dependencies configured");
            else
            {
                foreach (var plugin in selectedMacro.Metadata.PluginDependecies)
                {
                    using var __ = ImRaii.PushId(plugin);

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(plugin);
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - 30);

                    if (ImGuiUtils.IconButton(FontAwesomeIcon.Trash, "Remove dependency"))
                    {
                        var newDeps = selectedMacro.Metadata.PluginDependecies.ToList();
                        newDeps.Remove(plugin);
                        selectedMacro.Metadata.PluginDependecies = [.. newDeps];
                        C.Save();
                    }
                }
            }
        });
    }

    private void DrawPluginConflicts(ConfigMacro selectedMacro)
    {
        ImGuiUtils.Section("Plugin Conflicts", () =>
        {
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGuiEx.Combo("##DisableablePluginSelector", ref _pluginToDisable, _disableablePluginNames))
            {
                if (!selectedMacro.Metadata.PluginsToDisable.Contains(_pluginToDisable))
                {
                    var newDeps = selectedMacro.Metadata.PluginsToDisable.ToList();
                    newDeps.Add(_pluginToDisable);
                    selectedMacro.Metadata.PluginsToDisable = [.. newDeps];
                    C.Save();
                }
            }

            ImGui.Spacing();

            if (selectedMacro.Metadata.PluginsToDisable.Length == 0)
                ImGui.TextColored(ImGuiColors.DalamudGrey, "No plugins configured to disable");
            else
            {
                foreach (var plugin in selectedMacro.Metadata.PluginsToDisable)
                {
                    using var __ = ImRaii.PushId(plugin);

                    ImGui.AlignTextToFramePadding();
                    ImGui.Text(plugin);
                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - 30);

                    if (ImGuiUtils.IconButton(FontAwesomeIcon.Trash, "Remove plugin"))
                    {
                        var newDeps = selectedMacro.Metadata.PluginsToDisable.ToList();
                        newDeps.Remove(plugin);
                        selectedMacro.Metadata.PluginsToDisable = [.. newDeps];
                        C.Save();
                    }
                }
            }
        });
    }

    private void DrawDependencies(ConfigMacro selectedMacro)
    {
        ImGuiUtils.Section("Macro Dependencies", () =>
        {
            if (selectedMacro.Metadata.Dependencies.Count == 0)
                ImGui.TextColored(ImGuiColors.DalamudGrey, "No macro dependencies configured");
            else
            {
                for (var i = 0; i < selectedMacro.Metadata.Dependencies.Count; i++)
                {
                    var dependency = selectedMacro.Metadata.Dependencies[i];
                    using var __ = ImRaii.PushId(i);

                    var macroId = dependency.Id;
                    var displayName = $"[{macroId[..7]}] {dependency.Name}";

                    var icon = macroId.StartsWith("git://") ? FontAwesomeIcon.CloudDownloadAlt :
                              dependency is LocalMacroDependency ? FontAwesomeIcon.Code :
                              dependency is LocalDependency ? FontAwesomeIcon.FileAlt :
                              FontAwesomeIcon.Globe;

                    ImGuiEx.IconWithText(icon, displayName);

                    ImGui.SameLine(ImGui.GetContentRegionAvail().X - 30);
                    if (ImGuiUtils.IconButton(FontAwesomeIcon.Trash, "Remove dependency"))
                    {
                        selectedMacro.Metadata.Dependencies.RemoveAt(i--);
                        C.Save();
                    }
                }
            }

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Add New Dependency");
            ImGui.Spacing();

            ImGuiEx.EnumRadio(ref _dependencyType, true);

            ImGui.Spacing();

            if (_dependencyType == DependencyType.Local)
            {
                ImGui.Text("Local Dependency Type:");
                ImGui.Spacing();

                ImGuiEx.EnumRadio(ref _localDependencyType, true);
                ImGui.Spacing();

                if (_localDependencyType == LocalDependencyType.Macro)
                {
                    var localMacros = C.Macros.Where(m => m.Id != selectedMacro.Id).OrderBy(m => m.Name).ToList();
                    var selectedMacroId = string.Empty;
                    var macroNames = localMacros.ToDictionary(m => m.Id, m => $"{m.Name} [{m.FolderPath}]");

                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    if (ImGuiEx.Combo("##LocalMacroSelector", ref selectedMacroId, localMacros.Select(m => m.Id), names: macroNames))
                    {
                        selectedMacro.Metadata.Dependencies.Add(dependencyFactory.CreateDependency(selectedMacroId));
                        C.Save();
                    }
                }
                else
                {
                    ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                    ImGui.InputText("##LocalFilePath", ref _localFilePath, 1000);
                    ImGuiEx.Tooltip("Enter the full path to a local file");

                    if (ImGui.Button("Add File Dependency"))
                    {
                        if (!string.IsNullOrWhiteSpace(_localFilePath))
                        {
                            selectedMacro.Metadata.Dependencies.Add(dependencyFactory.CreateDependency(_localFilePath));
                            C.Save();
                            _localFilePath = string.Empty;
                        }
                    }
                }
            }
            else
            {
                ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
                ImGui.InputText("##GitUrl", ref _gitUrl, 1000);
                ImGuiEx.Tooltip("Enter a GitHub URL (e.g., https://github.com/owner/repo or https://github.com/owner/repo/blob/branch/path)");

                if (ImGui.Button("Add Dependency"))
                {
                    if (!string.IsNullOrWhiteSpace(_gitUrl))
                    {
                        selectedMacro.Metadata.Dependencies.Add(dependencyFactory.CreateDependency(_gitUrl));
                        C.Save();
                        _gitUrl = string.Empty;
                    }
                }
            }
        });
    }
}
