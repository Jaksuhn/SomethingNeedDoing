using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;
using System.Threading.Tasks;

namespace SomethingNeedDoing.External;

[RegisterSingleton<IDisableable>(Duplicate = DuplicateStrategy.Append)]
public class YesAlready : IPC, IDisableable
{
    public override string Name => "YesAlready";
    public override string Repo => Repos.Punish;
    public string InternalName => Name;

    [EzIPC]
    [LuaFunction(description: "Gets whether the plugin is active")]
    public Func<bool> IsPluginEnabled = null!;

    [EzIPC]
    [LuaFunction(description: "Sets whether the plugin is active", parameterDescriptions: ["state"])]
    public Action<bool> SetPluginEnabled = null!;

    [EzIPC]
    [LuaFunction(description: "Gets whether the bother is active", parameterDescriptions: ["name"])]
    public Func<string, bool> IsBotherEnabled = null!;

    [EzIPC]
    [LuaFunction(description: "Sets whether the bother is active", parameterDescriptions: ["name", "state"])]
    public Action<string, bool> SetBotherEnabled = null!;

    [EzIPC]
    [LuaFunction(description: "Pauses the plugin for the given amount of milliseconds", parameterDescriptions: ["milliseconds"])]
    public Action<int> PausePlugin = null!;

    [EzIPC]
    [LuaFunction(description: "Pauses the bother for the given amount of milliseconds", parameterDescriptions: ["name", "milliseconds"])]
    public Func<string, int, bool> PauseBother = null!;

    public Task<bool> EnableAsync()
    {
        try
        {
            SetPluginEnabled(true);
            return Task.FromResult(true);
        }
        catch
        {
            FrameworkLogger.Error("Failed to enable plugin");
            return Task.FromResult(false);
        }
    }

    public Task<bool> DisableAsync()
    {
        try
        {
            SetPluginEnabled(false);
            return Task.FromResult(true);
        }
        catch
        {
            FrameworkLogger.Error("Failed to disable plugin");
            return Task.FromResult(false);
        }
    }
}
