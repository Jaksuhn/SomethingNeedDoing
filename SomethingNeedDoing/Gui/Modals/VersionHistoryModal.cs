using Dalamud.Interface.Utility.Raii;
using ECommons.ImGuiMethods;
using SomethingNeedDoing.Managers;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Gui.Modals;

[RegisterSingleton, AutoConstruct]
public partial class VersionHistoryModal
{
    private readonly GitMacroManager _gitManager;
    private static Vector2 Size = new(600, 400);
    private static bool IsOpen;
    private static ConfigMacro? _macro;
    private static List<GitMacroManager.GitCommit> _commits = [];
    private static bool _isLoading;
    private static string _errorMessage = string.Empty;

    public void Open(ConfigMacro macro)
    {
        IsOpen = true;
        _macro = macro;
        _commits = [];
        _errorMessage = string.Empty;
        _ = LoadCommits();
    }

    public void Close()
    {
        IsOpen = false;
        ImGui.CloseCurrentPopup();
    }

    private async Task LoadCommits()
    {
        if (_macro == null) return;

        _isLoading = true;
        _errorMessage = string.Empty;

        try
        {
            _commits = await _gitManager.GetCommitHistory(_macro);
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load commit history: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }

    public void Draw()
    {
        if (!IsOpen || _macro == null) return;

        ImGui.OpenPopup($"VersionHistoryPopup##{nameof(VersionHistoryModal)}");

        ImGui.SetNextWindowPos(ImGui.GetMainViewport().GetCenter(), ImGuiCond.Appearing, new Vector2(0.5f, 0.5f));
        ImGui.SetNextWindowSize(Size);

        using var popup = ImRaii.PopupModal($"VersionHistoryPopup##{nameof(VersionHistoryModal)}", ref IsOpen, ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoTitleBar);
        if (!popup) return;

        using var child = ImRaii.Child("##Commits", new Vector2(-1, -ImGui.GetFrameHeightWithSpacing() - 10));
        if (child)
        {
            if (_isLoading)
                ImGui.Text("Loading commit history...");
            else if (!string.IsNullOrEmpty(_errorMessage))
                ImGuiEx.Text(EzColor.RedBright, _errorMessage);
            else if (_commits.Count == 0)
                ImGui.Text("No commit history available.");
            else
            {
                foreach (var commit in _commits)
                {
                    using var id = ImRaii.PushId(commit.Hash);

                    var isCurrentVersion = commit.Hash == _macro.GitInfo.CommitHash;
                    var isLatestVersion = commit == _commits[0];

                    ImGuiUtils.DrawLink(isCurrentVersion ? new Vector4(0, 1, 0, 1) : isLatestVersion ? new Vector4(1, 0.8f, 0, 1) : ImGui.GetStyle().Colors[(int)ImGuiCol.Text],
                        $"{commit.Hash[..7]} - {commit.Message}", commit.HtmlUrl);

                    ImGui.SameLine(ImGui.GetWindowWidth() - 100);

                    if (isCurrentVersion)
                    {
                        using var disabled = ImRaii.Disabled();
                        ImGui.Button("Current Version", new Vector2(100, 0));
                    }
                    else if (isLatestVersion)
                    {
                        if (ImGui.Button("Update", new Vector2(100, 0)))
                            _ = UpdateToVersion(commit.Hash);
                    }
                    else
                    {
                        if (ImGui.Button("Restore", new Vector2(100, 0)))
                            _ = UpdateToVersion(commit.Hash);
                    }

                    ImGuiEx.Text(ImGui.GetStyle().Colors[(int)ImGuiCol.TextDisabled], $"Author: {commit.Author} - {commit.Date:g}");
                    ImGui.Separator();
                }
            }
        }

        ImGuiUtils.CenteredButtons(("Close", Close));
    }

    private async Task UpdateToVersion(string commitHash)
    {
        if (_macro == null) return;

        _isLoading = true;
        _errorMessage = string.Empty;

        try
        {
            await _gitManager.UpdateToCommit(_macro, commitHash);
            IsOpen = false;
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to update to version {commitHash}: {ex.Message}";
        }
        finally
        {
            _isLoading = false;
        }
    }
}
