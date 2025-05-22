using System.Collections.Generic;

namespace SomethingNeedDoing.Framework.Lua;

public class LuaDocumentation
{
    private readonly List<LuaModuleDoc> _modules = new();

    public LuaDocumentation()
    {
        RegisterModules();
    }

    public void RegisterModule(LuaModuleDoc module)
    {
        _modules.Add(module);
    }

    public List<LuaModuleDoc> GetModules()
    {
        return _modules;
    }

    private void RegisterModules()
    {
        // Register core modules
        RegisterModule(new LuaModuleDoc
        {
            Name = "Actions",
            Description = "Provides access to game actions and abilities",
            Functions = new List<LuaFunctionDoc>
            {
                new LuaFunctionDoc
                {
                    Name = "UseAction",
                    Description = "Uses a game action by name",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "actionName", Type = "string", Description = "The name of the action" }
                    },
                    ReturnType = "boolean",
                    Example = "Actions.UseAction(\"Rampart\")"
                },
                new LuaFunctionDoc
                {
                    Name = "ExecuteAction",
                    Description = "Uses a game action by ID",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "actionID", Type = "number", Description = "The action ID" },
                        new LuaParameterDoc { Name = "actionType", Type = "ActionType", Description = "The type of action (optional)" }
                    },
                    ReturnType = "void",
                    Example = "Actions.ExecuteAction(7535)"
                },
                new LuaFunctionDoc
                {
                    Name = "ExecuteGeneralAction",
                    Description = "Uses a general action by ID",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "actionID", Type = "number", Description = "The general action ID" }
                    },
                    ReturnType = "void",
                    Example = "Actions.ExecuteGeneralAction(5)"
                },
                new LuaFunctionDoc
                {
                    Name = "Teleport",
                    Description = "Teleports to an aetheryte",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "aetheryteId", Type = "number", Description = "The aetheryte ID" }
                    },
                    ReturnType = "void",
                    Example = "Actions.Teleport(8)"
                },
                new LuaFunctionDoc
                {
                    Name = "GetActionInfo",
                    Description = "Gets information about an action",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "actionId", Type = "number", Description = "The action ID" }
                    },
                    ReturnType = "ActionWrapper",
                    Example = "local action = Actions.GetActionInfo(7535)"
                }
            }
        });

        RegisterModule(new LuaModuleDoc
        {
            Name = "Addons",
            Description = "Provides access to game UI addons",
            Functions = new List<LuaFunctionDoc>
            {
                new LuaFunctionDoc
                {
                    Name = "IsVisible",
                    Description = "Checks if a specific addon is visible",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "addonName", Type = "string", Description = "The name of the addon" }
                    },
                    ReturnType = "boolean",
                    Example = "if Addons.IsVisible(\"SelectYesno\") then\n    yield(\"/click select_yes\")\nend"
                },
                new LuaFunctionDoc
                {
                    Name = "WaitForAddon",
                    Description = "Waits for an addon to become visible",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "addonName", Type = "string", Description = "The name of the addon" },
                        new LuaParameterDoc { Name = "timeout", Type = "number", Description = "Timeout in seconds (optional)" }
                    },
                    ReturnType = "boolean",
                    Example = "if Addons.WaitForAddon(\"SelectYesno\", 5) then\n    yield(\"/click select_yes\")\nend"
                }
            }
        });

        RegisterModule(new LuaModuleDoc
        {
            Name = "IPC",
            Description = "Provides interprocess communication with other plugins",
            Functions = new List<LuaFunctionDoc>
            {
                new LuaFunctionDoc
                {
                    Name = "IsInstalled",
                    Description = "Checks if an IPC-enabled plugin is installed",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "pluginName", Type = "string", Description = "Name of the plugin" }
                    },
                    ReturnType = "boolean",
                    Example = "if IPC.IsInstalled(\"AutoRetainer\") then\n    -- Use AutoRetainer functions\nend"
                },
                new LuaFunctionDoc
                {
                    Name = "GetAvailablePlugins",
                    Description = "Gets a list of all available IPC-enabled plugins",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "string[]",
                    Example = "local plugins = IPC.GetAvailablePlugins()"
                }
            }
        });

        RegisterModule(new LuaModuleDoc
        {
            Name = "CraftingState",
            Description = "Provides access to crafting state information",
            Functions = new List<LuaFunctionDoc>
            {
                new LuaFunctionDoc
                {
                    Name = "IsCrafting",
                    Description = "Checks if the player is currently crafting",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "boolean",
                    Example = "if CraftingState.IsCrafting() then\n    -- Perform crafting actions\nend"
                },
                new LuaFunctionDoc
                {
                    Name = "GetCurrentProgress",
                    Description = "Gets the current progress of the crafting synthesis",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local progress = CraftingState.GetCurrentProgress()"
                },
                new LuaFunctionDoc
                {
                    Name = "GetMaxProgress",
                    Description = "Gets the maximum progress of the crafting synthesis",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local maxProgress = CraftingState.GetMaxProgress()"
                },
                new LuaFunctionDoc
                {
                    Name = "GetCurrentQuality",
                    Description = "Gets the current quality of the crafting synthesis",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local quality = CraftingState.GetCurrentQuality()"
                },
                new LuaFunctionDoc
                {
                    Name = "GetMaxQuality",
                    Description = "Gets the maximum quality of the crafting synthesis",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local maxQuality = CraftingState.GetMaxQuality()"
                },
                new LuaFunctionDoc
                {
                    Name = "GetCurrentDurability",
                    Description = "Gets the current durability of the crafting synthesis",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local durability = CraftingState.GetCurrentDurability()"
                }
            }
        });

        RegisterModule(new LuaModuleDoc
        {
            Name = "Inventory",
            Description = "Provides access to player inventory",
            Functions = new List<LuaFunctionDoc>
            {
                new LuaFunctionDoc
                {
                    Name = "GetItemCount",
                    Description = "Gets the count of a specific item in inventory",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "itemId", Type = "number", Description = "The item ID" }
                    },
                    ReturnType = "number",
                    Example = "local count = Inventory.GetItemCount(5333)"
                },
                new LuaFunctionDoc
                {
                    Name = "GetItemByName",
                    Description = "Gets item information by name",
                    Parameters = new List<LuaParameterDoc>
                    {
                        new LuaParameterDoc { Name = "itemName", Type = "string", Description = "The item name" }
                    },
                    ReturnType = "InventoryItem",
                    Example = "local item = Inventory.GetItemByName(\"Grade 7 Tincture of Strength\")"
                }
            }
        });

        RegisterModule(new LuaModuleDoc
        {
            Name = "Player",
            Description = "Provides access to player information",
            Functions = new List<LuaFunctionDoc>
            {
                new LuaFunctionDoc
                {
                    Name = "GetName",
                    Description = "Gets the player's name",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "string",
                    Example = "local name = Player.GetName()"
                },
                new LuaFunctionDoc
                {
                    Name = "GetJobID",
                    Description = "Gets the player's current job ID",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local jobId = Player.GetJobID()"
                },
                new LuaFunctionDoc
                {
                    Name = "GetLevel",
                    Description = "Gets the player's current level",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local level = Player.GetLevel()"
                },
                new LuaFunctionDoc
                {
                    Name = "GetHP",
                    Description = "Gets the player's current HP",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local hp = Player.GetHP()"
                },
                new LuaFunctionDoc
                {
                    Name = "GetMaxHP",
                    Description = "Gets the player's maximum HP",
                    Parameters = new List<LuaParameterDoc>(),
                    ReturnType = "number",
                    Example = "local maxHp = Player.GetMaxHP()"
                }
            }
        });
    }
}

public class LuaModuleDoc
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<LuaFunctionDoc> Functions { get; set; } = new();
}

public class LuaFunctionDoc
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<LuaParameterDoc> Parameters { get; set; } = new();
    public string ReturnType { get; set; } = string.Empty;
    public string Example { get; set; } = string.Empty;
}

public class LuaParameterDoc
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
