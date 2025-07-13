using ECommons.EzIpcManager;

namespace SomethingNeedDoing.Core.Interfaces;

/// <summary>
/// Interface for all IPC classes to implement.
/// This allows for consistent discovery and handling of IPC classes.
/// </summary>
public interface IIPC
{
    /// <summary>
    /// Gets the name of the IPC interface as it will appear in Lua.
    /// </summary>
    string Name { get; }
    string Repo { get; }
}

public abstract class IPC : IIPC
{
    public abstract string Name { get; }
    public abstract string Repo { get; }
    public bool IsInstalled => Svc.PluginInterface.InstalledPlugins.Any(p => p.Name == Name && p.IsLoaded);
    public IPC() => EzIPC.Init(this, Name);

    public class Repos
    {
        public const string FirstParty = "";
        public const string Liza = "https://git.carvel.li/liza/";
        public const string Punish = "https://love.puni.sh/ment.json";
        public const string Limiana = "https://github.com/NightmareXIV/MyDalamudPlugins/raw/main/pluginmaster.json";
        public const string Herc = $"{Dynamis}herc";
        public const string Kawaii = $"{Dynamis}kawaii";
        public const string Veyn = $"{Dynamis}veyn";
        public const string Croizat = $"{Dynamis}croizat";
        public const string CombatReborn = "https://raw.githubusercontent.com/FFXIV-CombatReborn/CombatRebornRepo/main/pluginmaster.json";

        private const string Dynamis = "https://puni.sh/api/repository/";
    }
}
