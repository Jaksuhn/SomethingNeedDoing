using Dalamud.Game.ClientState.Objects.Types;
using ECommons.ExcelServices;
using ECommons.UIHelpers.AddonMasterImplementations;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace SomethingNeedDoing.Utils;

public static unsafe class Game
{
    public static bool CanUseAction(ActionType actionType, uint actionId) => ActionManager.Instance()->GetActionStatus(actionType, actionId) == 0;
    public static bool CanUseCraftAction(uint actionId) => CanUseAction(actionId >= 100000 ? ActionType.CraftAction : ActionType.Action, actionId);
    public static bool Interact(IGameObject? obj) => obj != null && TargetSystem.Instance()->InteractWithObject(obj.Struct(), false) != 0;
    public static int GetInventoryItemCount(uint itemId, bool isHQ)
    {
        var inventoryManager = InventoryManager.Instance();
        return inventoryManager == null
            ? throw new MacroException("InventoryManager not found")
            : inventoryManager->GetInventoryItemCount(itemId, isHQ);
    }

    public static void UseItem(uint itemId, bool isHq)
    {
        var agent = AgentInventoryContext.Instance();
        if (agent == null)
            throw new MacroException("AgentInventoryContext not found");

        if (isHq)
            itemId += 1_000_000;

        var result = agent->UseItem(itemId);
        if (result != 0)
            throw new MacroException("Failed to use item");
    }

    public static bool NeedsRepair(int durabilityThreshold = 0)
    {
        var container = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (container == null) return false;

        for (var i = 0; i < container->Size; i++)
        {
            var item = container->GetInventorySlot(i);
            if (item is null) continue;

            var durabilityPercent = Convert.ToInt32(Convert.ToDouble(item->Condition) / 30000.0 * 100.0);
            if (durabilityPercent <= durabilityThreshold)
                return true;
        }
        return false;
    }

    public static bool HasSpiritbondedItems(float within = 100)
    {
        var equipped = InventoryManager.Instance()->GetInventoryContainer(InventoryType.EquippedItems);
        if (equipped == null || !equipped->IsLoaded) return false;

        var hasFullySpiritbonded = false;
        var maxPartialSpiritbond = 0f;
        var itemCount = 0;

        for (var i = 0; i < equipped->Size; i++)
        {
            var item = equipped->GetInventorySlot(i);
            if (item == null || item->ItemId == 0) continue;

            itemCount++;
            var spiritbond = item->SpiritbondOrCollectability / 100f;

            if (spiritbond >= 100f)
                hasFullySpiritbonded = true;
            else
                maxPartialSpiritbond = Math.Max(maxPartialSpiritbond, spiritbond);
        }

        if (itemCount == 0) return false;

        // All items are fully spiritbonded
        if (maxPartialSpiritbond == 0f)
            return true;

        // No fully spiritbonded items
        if (!hasFullySpiritbonded) return false;

        // 100 = a single spiritbond is good enough
        if (within >= 100f)
            return true;

        // Continue if next highest is within threshold, otherwise pause
        var shouldPause = maxPartialSpiritbond < within;
        FrameworkLogger.Debug($"Highest partial spiritbond: {maxPartialSpiritbond:F1}%, threshold: {within:F1}%, pausing: {shouldPause}");
        return shouldPause;
    }

    public static class Crafting
    {
        public static Dictionary<Job, List<CraftActionData>> CraftActions
        {
            get
            {
                if (field == null)
                {
                    field = [];

                    var craftActions = FindRows<CraftAction>(r => r.ClassJob.RowId is >= 8 and <= 15);
                    var actions = FindRows<Sheets.Action>(r => r.ClassJob.RowId is >= 8 and <= 15);

                    foreach (var craftAction in craftActions)
                    {
                        var jobId = (Job)craftAction.ClassJob.RowId;
                        if (!field.ContainsKey(jobId))
                            field[jobId] = [];

                        field[jobId].Add(new CraftActionData
                        {
                            Id = craftAction.RowId,
                            Name = craftAction.Name.ExtractText(),
                            IsCraftAction = true,
                            ClassJob = jobId
                        });
                    }

                    foreach (var action in actions)
                    {
                        var jobId = (Job)action.ClassJob.RowId;
                        if (!field.ContainsKey(jobId))
                            field[jobId] = [];

                        field[jobId].Add(new CraftActionData
                        {
                            Id = action.RowId,
                            Name = action.Name.ExtractText(),
                            IsCraftAction = false,
                            ClassJob = jobId
                        });
                    }
                }

                return field;
            }
        }

        public record class CraftActionData
        {
            public uint Id { get; init; }
            public string Name { get; init; } = string.Empty;
            public bool IsCraftAction { get; init; }
            public Job ClassJob { get; init; }

            public bool IncreasesProgress => GetActionResult(Id).Progress > 0;
            public bool IncreasesQuality => GetActionResult(Id).Quality > 0;
            public bool CanExecute => ActionManager.Instance()->GetActionStatus(IsCraftAction ? ActionType.CraftAction : ActionType.Action, Id) == 0;
        }

        public static bool IsCraftActionByName(string name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
            => CraftActions.Values.SelectMany(actions => actions).Any(action => action.Name.Equals(name, comparison));

        public static CraftActionData? FindCraftActionByName(string name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
            => CraftActions.Values.SelectMany(actions => actions).FirstOrDefault(action => action.Name.Equals(name, comparison));

        public static List<CraftActionData> FindAllCraftActionsByName(string name, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
            => [.. CraftActions.Values.SelectMany(actions => actions).Where(action => action.Name.Equals(name, comparison))];

        public static CraftActionData? FindCraftActionByNameAndJob(string name, Job job, StringComparison comparison = StringComparison.OrdinalIgnoreCase)
            => CraftActions.TryGetValue(job, out var actions) ? actions.FirstOrDefault(action => action.Name.Equals(name, comparison)) : null;

        public static AddonMaster.Synthesis.Condition GetCondition()
            => TryGetAddonMaster<AddonMaster.Synthesis>(out var addon) ? addon.Reader.Condition : AddonMaster.Synthesis.Condition.Unknown;

        public static void OpenRecipe(uint recipeId)
        {
            var agent = AgentRecipeNote.Instance();
            if (agent == null)
                throw new MacroException("AgentRecipeNote not found");

            agent->OpenRecipeByRecipeId(recipeId);
        }

        public static uint GetRecipeIdByName(string recipeName)
        {
            var recipes = FindRows<Recipe>(r =>
                r.ItemResult.Value!.Name.ToString().Equals(recipeName, StringComparison.InvariantCultureIgnoreCase))
                .ToList();

            return recipes.Count switch
            {
                0 => throw new MacroException("Recipe not found"),
                1 => recipes.First().RowId,
                _ => recipes.FirstOrDefault(r => r.CraftType.RowId + 8 == Player.ClassJob.RowId).RowId
            };
        }

        public static bool IsCrafting() => Svc.Condition[ConditionFlag.Crafting] && !Svc.Condition[ConditionFlag.PreparingToCraft];
        public static bool IsMaxProgress() => TryGetAddonMaster<AddonMaster.Synthesis>(out var am) && am.Reader.IsMaxProgress;
        public static bool IsMaxQuality() => TryGetAddonMaster<AddonMaster.Synthesis>(out var am) && am.Reader.IsMaxQuality;
        public static bool IsCraftAction(string name) => FindRows<CraftAction>(x => !x.Name.IsEmpty).Select(x => x.Name).Distinct().Contains(name);
        public static uint GetCraftAction(string name, uint classJob)
            => FindRows<CraftAction>(x => !x.Name.IsEmpty).First(x => x.Name.ExtractText().Equals(name, StringComparison.OrdinalIgnoreCase) && x.ClassJob.RowId == classJob).RowId;
        public static bool IsCraftActionProgressIncrease(uint id) => GetActionResult(id).Progress > 0;
        public static bool IsCraftActionQualityIncrease(uint id) => GetActionResult(id).Quality > 0;

        public static (uint Progress, uint Quality) GetActionResult(uint id)
        {
            var agent = AgentCraftActionSimulator.Instance();
            if (agent == null) return (0, 0);

            var progress = agent->Progress.FirstOrDefault(p => p.ActionId == id).ProgressIncrease;
            var quality = agent->Quality.FirstOrDefault(q => q.ActionId == id).QualityIncrease;

            return (progress, quality);
        }

        public static CraftState GetCraftState()
        {
            if (TryGetAddonMaster<AddonMaster.Synthesis>(out var addon))
                return new()
                {
                    Index = (int)addon.Reader.StepCount,
                    Progress = (int)addon.Reader.Progress,
                    Quality = (int)addon.Reader.Quality,
                    Durability = (int)addon.Reader.Durability,
                    RemainingCP = (int)Player.Object.CurrentCp,
                    Condition = addon.Reader.Condition,
                    IQStacks = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.InnerQuiet)?.Param ?? 0,
                    WasteNotLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.WasteNot)?.Param ?? 0,
                    ManipulationLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.Manipulation)?.Param ?? 0,
                    GreatStridesLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.GreatStrides)?.Param ?? 0,
                    InnovationLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.Innovation)?.Param ?? 0,
                    VenerationLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.Veneration)?.Param ?? 0,
                    MuscleMemoryLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.MuscleMemory)?.Param ?? 0,
                    FinalAppraisalLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.FinalAppraisal)?.Param ?? 0,
                    //CarefulObservationLeft = CanUseCraftAction(),
                    HeartAndSoulActive = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.HeartAndSoul) is not null,
                    //HeartAndSoulAvailable = CanUseCraftAction(),
                    ExpedienceLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.Expedience)?.Param ?? 0,
                    QuickInnoLeft = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.Innovation)?.Param ?? 0,
                    //QuickInnoAvailable = CanUseCraftAction(),
                    //TrainedPerfectionAvailable = CanUseCraftAction(),
                    TrainedPerfectionActive = Player.Status.FirstOrDefault(s => s.StatusId == (uint)Buffs.TrainedPerfection) is not null,
                };
            throw new ArgumentNullException("Synthesis addon null");
        }

        public enum Buffs : uint
        {
            InnerQuiet = 251,
            Innovation = 2189,
            Veneration = 2226,
            GreatStrides = 254,
            Manipulation = 1164,
            WasteNot = 252,
            WasteNot2 = 257,
            FinalAppraisal = 2190,
            MuscleMemory = 2191,
            HeartAndSoul = 2665,
            Expedience = 3812,
            TrainedPerfection = 3813,
        }

        public record class CraftState
        {
            public int Index;
            public int Progress;
            public int Quality;
            public int Durability;
            public int RemainingCP;
            public AddonMaster.Synthesis.Condition Condition;
            public int IQStacks;
            public int WasteNotLeft;
            public int ManipulationLeft;
            public int GreatStridesLeft;
            public int InnovationLeft;
            public int VenerationLeft;
            public int MuscleMemoryLeft;
            public int FinalAppraisalLeft;
            //public int CarefulObservationLeft;
            public bool HeartAndSoulActive;
            //public bool HeartAndSoulAvailable;
            public int ExpedienceLeft;
            public int QuickInnoLeft;
            //public bool QuickInnoAvailable;
            //public bool TrainedPerfectionAvailable;
            public bool TrainedPerfectionActive;
        }
    }
}
