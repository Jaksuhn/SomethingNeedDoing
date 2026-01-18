using ECommons.EzIpcManager;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.External;
public class AllaganTools : IPC
{
    public override string Name => "InventoryTools";
    public override string Repo => Repos.FirstParty;
    private const string _ipcName = "AllaganTools";

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets the count of items in a specific inventory type",
        parameterDescriptions: ["inventoryTypeId", "contentId"])]
    public readonly Func<uint, ulong?, uint> InventoryCountByType = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets the count of items across multiple inventory types",
        parameterDescriptions: ["inventoryTypeIds", "contentId"])]
    public readonly Func<uint[], ulong?, uint> InventoryCountByTypes = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets the count of a specific item in a specific inventory",
        parameterDescriptions: ["itemId", "contentId", "inventoryTypeId"])]
    public readonly Func<uint, ulong, int, uint> ItemCount = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets the count of a specific high-quality item in a specific inventory",
        parameterDescriptions: ["itemId", "contentId", "inventoryTypeId"])]
    public readonly Func<uint, ulong, int, uint> ItemCountHQ = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets the count of a specific item across multiple inventories",
        parameterDescriptions: ["itemId", "onlyCurrentCharacter", "inventoryTypeIds"])]
    public readonly Func<uint, bool, uint[], uint> ItemCountOwned = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Enables a UI filter",
        parameterDescriptions: ["filterKey"])]
    public readonly Func<string, bool> EnableUiFilter = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(description: "Disables all UI filters")]
    public readonly Func<bool> DisableUiFilter = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Toggles a UI filter on/off",
        parameterDescriptions: ["filterKey"])]
    public readonly Func<string, bool> ToggleUiFilter = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Enables a background filter",
        parameterDescriptions: ["filterKey"])]
    public readonly Func<string, bool> EnableBackgroundFilter = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(description: "Disables all background filters")]
    public readonly Func<bool> DisableBackgroundFilter = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Toggles a background filter on/off",
        parameterDescriptions: ["filterKey"])]
    public readonly Func<string, bool> ToggleBackgroundFilter = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Enables a craft list",
        parameterDescriptions: ["filterKey"])]
    public readonly Func<string, bool> EnableCraftList = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(description: "Disables all craft lists")]
    public readonly Func<bool> DisableCraftList = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Toggles a craft list on/off",
        parameterDescriptions: ["filterKey"])]
    public readonly Func<string, bool> ToggleCraftList = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Adds an item to a craft list",
        parameterDescriptions: ["filterKey", "itemId", "quantity"])]
    public readonly Func<string, uint, uint, bool> AddItemToCraftList = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Removes an item from a craft list",
        parameterDescriptions: ["filterKey", "itemId", "quantity"])]
    public readonly Func<string, uint, uint, bool> RemoveItemFromCraftList = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets all items in a filter",
        parameterDescriptions: ["filterKey"])]
    public readonly Func<string, Dictionary<uint, uint>> GetFilterItems = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets all items in a craft list",
        parameterDescriptions: ["filterKey"])]
    public readonly Func<string, Dictionary<uint, uint>> GetCraftItems = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(description: "Gets all items in the retrieval list")]
    public readonly Func<Dictionary<uint, uint>> GetRetrievalItems = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets all items owned by a character",
        parameterDescriptions: ["contentId"])]
    public readonly Func<ulong, HashSet<ulong[]>> GetCharacterItems = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets all characters owned by the active character",
        parameterDescriptions: ["includeOwner"])]
    public readonly Func<bool, HashSet<ulong>> GetCharactersOwnedByActive = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Gets all items of a specific type owned by a character",
        parameterDescriptions: ["contentId", "inventoryTypeId"])]
    public readonly Func<ulong, uint, HashSet<ulong[]>> GetCharacterItemsByType = null!;

    //[EzIPCEvent]
    //[LuaFunction(
    //    description: "Event triggered when an item is added to inventory",
    //    parameterDescriptions: ["itemId", "itemFlags", "contentId", "quantity"])]
    //public readonly Func<(uint, InventoryItem.ItemFlags, ulong, uint), bool> ItemAdded = null!;

    //[EzIPCEvent]
    //[LuaFunction(
    //    description: "Event triggered when an item is removed from inventory",
    //    parameterDescriptions: ["itemId", "itemFlags", "contentId", "quantity"])]
    //public readonly Func<(uint, InventoryItem.ItemFlags, ulong, uint), bool> ItemRemoved = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(description: "Gets all craft lists")]
    public readonly Func<Dictionary<string, string>> GetCraftLists = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(description: "Gets all search filters")]
    public readonly Func<Dictionary<string, string>> GetSearchFilters = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(
        description: "Adds a new craft list",
        parameterDescriptions: ["craftList", "itemsToAdd"])]
    public readonly Func<string, Dictionary<uint, uint>, string> AddNewCraftList = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(description: "Gets the current character ID")]
    public readonly Func<ulong?> CurrentCharacter = null!;

    //[EzIPCEvent]
    //[LuaFunction(
    //    description: "Event triggered when a retainer is changed",
    //    parameterDescriptions: ["retainerId"])]
    //public readonly Func<ulong?, bool> RetainerChanged = null!;

    [EzIPC($"{_ipcName}.%m", false)]
    [LuaFunction(description: "Checks if the plugin is initialized")]
    public readonly Func<bool> IsInitialized = null!;
}
