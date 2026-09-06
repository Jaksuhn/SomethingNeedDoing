using AutoRetainerAPI;
using AutoRetainerAPI.Configuration;
using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;
using GCInfo = (uint ShopDataID, uint ExchangeDataID, System.Numerics.Vector3 Position);

namespace SomethingNeedDoing.External;

public class AutoRetainer : IPC
{
    public override string Name => "AutoRetainer";
    public override string Repo => Repos.Punish;

    private readonly AutoRetainerApi _api = new();

    [EzIPC]
    [LuaFunction(description: "Gets whether multi-mode is enabled")]
    public readonly Func<bool> GetMultiModeEnabled = null!;

    [EzIPC]
    [LuaFunction(
        description: "Sets whether multi-mode is enabled",
        parameterDescriptions: ["enabled"])]
    public readonly Action<bool> SetMultiModeEnabled = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Checks if the plugin is busy")]
    public readonly Func<bool> IsBusy = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Gets the number of free inventory slots")]
    public readonly Func<int> GetInventoryFreeSlotCount = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Gets all enabled retainers")]
    public readonly Func<Dictionary<ulong, HashSet<string>>> GetEnabledRetainers = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Checks if any retainers are available for the current character")]
    public readonly Func<bool> AreAnyRetainersAvailableForCurrentChara = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Aborts all tasks")]
    public readonly Action AbortAllTasks = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Disables all functions")]
    public readonly Action DisableAllFunctions = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Enables multi-mode")]
    public readonly Action EnableMultiMode = null!;

    /// <summary>
    /// Action onFailure
    /// </summary>
    [EzIPC("PluginState.%m")]
    [LuaFunction(
        description: "Enqueues a high-end task",
        parameterDescriptions: ["onFailure"])]
    public readonly Action<Action> EnqueueHET = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Checks if auto-login is possible")]
    public readonly Func<bool> CanAutoLogin = null!;

    /// <summary>
    /// string charaNameWithWorld
    /// </summary>
    [EzIPC("PluginState.%m")]
    [LuaFunction(
        description: "Relogs to a specific character",
        parameterDescriptions: ["charaNameWithWorld"])]
    public readonly Func<string, bool> Relog = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Gets whether retainer sense is enabled")]
    public readonly Func<bool> GetOptionRetainerSense = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(
        description: "Sets whether retainer sense is enabled",
        parameterDescriptions: ["enabled"])]
    public readonly Action<bool> SetOptionRetainerSense = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(description: "Gets the retainer sense threshold")]
    public readonly Func<int> GetOptionRetainerSenseThreshold = null!;

    [EzIPC("PluginState.%m")]
    [LuaFunction(
        description: "Sets the retainer sense threshold",
        parameterDescriptions: ["threshold"])]
    public readonly Action<int> SetOptionRetainerSenseThreshold = null!;

    /// <summary>
    /// ulong CID
    /// </summary>
    [EzIPC("PluginState.%m")]
    [LuaFunction(
        description: "Gets the closest retainer venture seconds remaining",
        parameterDescriptions: ["cid"])]
    public readonly Func<ulong, long?> GetClosestRetainerVentureSecondsRemaining = null!;

    [EzIPC("GC.%m")]
    [LuaFunction(description: "Enqueues initiation")]
    public readonly Action EnqueueInitiation = null!;

    [EzIPC("GC.%m")]
    [LuaFunction(description: "Gets GC information")]
    public readonly Func<GCInfo?> GetGCInfo = null!;

    [LuaFunction(description: "Gets all registered characters")]
    [Changelog("12.19")]
    public List<ulong> GetRegisteredCharacters() => _api.GetRegisteredCharacters();

    [LuaFunction(
        description: "Gets offline character data for a specific character ID",
        parameterDescriptions: ["cid"])]
    [Changelog("12.19")]
    public OfflineCharacterDataWrapper GetOfflineCharacterData(ulong cid) => new(_api.GetOfflineCharacterData(cid));

    [LuaFunction(
        description: "Sets suppressed state in which AutoRetainer will not perform any actions regardless of configuration",
        parameterDescriptions: ["boolean"])]
    [Changelog("13.3")]
    public void SetSuppressed(bool suppressed) => _api.Suppressed = suppressed;

    public class OfflineCharacterDataWrapper(OfflineCharacterData data) : IWrapper
    {
        [LuaDocs][Changelog("12.19")] public ulong CID => data.CID;
        [LuaDocs][Changelog("12.19")] public string Name => data.Name;
        [LuaDocs][Changelog("12.19")] public string World => data.World;
        [LuaDocs][Changelog("12.19")] public bool Enabled => data.Enabled;
        [LuaDocs][Changelog("13.3")] public bool WorkshopEnabled => data.WorkshopEnabled;
        [LuaDocs][Changelog("12.19")] public List<OfflineRetainerDataWrapper> RetainerData => [.. data.RetainerData.Select(x => new OfflineRetainerDataWrapper(x))];
        [LuaDocs][Changelog("12.19")] public uint InventorySpace => data.InventorySpace;
        [LuaDocs][Changelog("12.19")] public uint VentureCoffers => data.VentureCoffers;
        [LuaDocs][Changelog("12.19")] public uint Gil => data.Gil;
        [LuaDocs][Changelog("12.19")] public List<OfflineVesselDataWrapper> OfflineAirshipData => [.. data.OfflineAirshipData.Select(x => new OfflineVesselDataWrapper(x))];
        [LuaDocs][Changelog("12.19")] public List<OfflineVesselDataWrapper> OfflineSubmarineData => [.. data.OfflineSubmarineData.Select(x => new OfflineVesselDataWrapper(x))];
        [LuaDocs][Changelog("12.19")] public int Ceruleum => data.Ceruleum;
        [LuaDocs][Changelog("12.19")] public int RepairKits => data.RepairKits;
        [LuaDocs][Changelog("12.19")] public bool RetainersAwaitingProcessing => RetainerData.Any(x => x.HasVenture && x.VentureEndsAt <= TimeProvider.System.GetUtcNow().ToUnixTimeSeconds());
        [LuaDocs][Changelog("12.19")] public bool SubsAwaitingProcessing => OfflineSubmarineData.Any(x => x.ReturnTime <= TimeProvider.System.GetUtcNow().ToUnixTimeSeconds());
        [LuaDocs][Changelog("12.19")] public bool AnyAwaitingProcessing => RetainersAwaitingProcessing || SubsAwaitingProcessing;
        [LuaDocs][Changelog("13.3")] public List<short> ClassJobLevelArray => [.. data.ClassJobLevelArray];
    }

    public class OfflineRetainerDataWrapper(OfflineRetainerData data) : IWrapper
    {
        [LuaDocs][Changelog("12.19")] public string Name => data.Name;
        [LuaDocs][Changelog("12.19")] public long VentureEndsAt => data.VentureEndsAt;
        [LuaDocs][Changelog("12.19")] public bool HasVenture => data.HasVenture;
        [LuaDocs][Changelog("12.19")] public int Level => data.Level;
        [LuaDocs][Changelog("12.19")] public long VentureBeginsAt => data.VentureBeginsAt;
        [LuaDocs][Changelog("12.19")] public uint Job => data.Job;
        [LuaDocs][Changelog("12.19")] public uint VentureID => data.VentureID;
        [LuaDocs][Changelog("12.19")] public uint Gil => data.Gil;
        [LuaDocs][Changelog("12.19")] public ulong RetainerID => data.RetainerID;
        [LuaDocs][Changelog("12.19")] public int MBItems => data.MBItems;
    }

    public class OfflineVesselDataWrapper(OfflineVesselData data) : IWrapper
    {
        [LuaDocs][Changelog("12.19")] public string Name => data.Name;
        [LuaDocs][Changelog("12.19")] public uint ReturnTime => data.ReturnTime;
    }
}
