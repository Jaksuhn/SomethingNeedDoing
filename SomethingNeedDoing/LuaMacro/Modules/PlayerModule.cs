using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Misc;
using NLua;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro.Wrappers;
using static FFXIVClientStructs.FFXIV.Client.Game.UI.PlayerState;
using static SomethingNeedDoing.LuaMacro.Modules.InventoryModule;

namespace SomethingNeedDoing.LuaMacro.Modules;
public unsafe class PlayerModule : LuaModuleBase
{
    public override string ModuleName => "Player";
    public override void Register(Lua lua)
    {
        lua.RegisterEnum<WeeklyBingoTaskStatus>();
        base.Register(lua);
    }

    private PlayerState* Ps => Instance();

    [LuaFunction] public byte GrandCompany => Ps->GrandCompany;
    [LuaFunction] public byte GCRankMaelstrom { get => Ps->GCRankMaelstrom; set => Ps->GCRankMaelstrom = value; }
    [LuaFunction] public byte GCRankImmortalFlames { get => Ps->GCRankImmortalFlames; set => Ps->GCRankImmortalFlames = value; }
    [LuaFunction] public byte GCRankTwinAdders { get => Ps->GCRankTwinAdders; set => Ps->GCRankTwinAdders = value; }

    [LuaFunction] public uint FishingBait => Ps->FishingBait;

    [LuaFunction] public EntityWrapper Entity => new(Player.Object);
    [LuaFunction] public FreeCompanyWrapper FreeCompany => new();

    [LuaFunction] public JobWrapper Job => new(Player.JobId);
    [LuaFunction] public JobWrapper GetJob(uint classJobId) => new(classJobId);
    [LuaFunction][Changelog("12.21")] public GearsetWrapper Gearset => new(RaptureGearsetModule.Instance()->CurrentGearsetIndex);
    [LuaFunction][Changelog("12.21")] public GearsetWrapper GetGearset(int id) => new(id);
    [LuaFunction][Changelog("12.21")] public List<GearsetWrapper> Gearsets => [.. RaptureGearsetModule.Instance()->Entries.ToArray().Select((g, i) => new GearsetWrapper(i))];
    public class GearsetWrapper(int id) : IWrapper
    {
        [LuaDocs][Changelog("12.21")] public bool IsValid => RaptureGearsetModule.Instance()->IsValidGearset(id);
        [LuaDocs][Changelog("12.21")] public byte ClassJob => RaptureGearsetModule.Instance()->GetGearset(id)->ClassJob;
        [LuaDocs][Changelog("12.21")] public byte GlamourSetLink => RaptureGearsetModule.Instance()->GetGearset(id)->GlamourSetLink;
        [LuaDocs][Changelog("12.21")] public short ItemLevel => RaptureGearsetModule.Instance()->GetGearset(id)->ItemLevel;
        [LuaDocs][Changelog("12.21")] public byte BannerIndex => RaptureGearsetModule.Instance()->GetGearset(id)->BannerIndex;
        [LuaDocs][Changelog("12.21")] public string Name => RaptureGearsetModule.Instance()->GetGearset(id)->NameString;
        [LuaDocs][Changelog("12.21")] public List<InventoryItemWrapper> Items => [.. RaptureGearsetModule.Instance()->GetGearset(id)->Items.ToArray().Select(i => new InventoryItemWrapper(i.ItemId))];
        [LuaDocs][Changelog("12.21")] public void Equip() => RaptureGearsetModule.Instance()->EquipGearset(id);
        [LuaDocs][Changelog("12.21")] public void Update() => RaptureGearsetModule.Instance()->UpdateGearset(id);
    }

    [LuaFunction] public bool IsMoving => Player.IsMoving;
    [LuaFunction] public bool IsInDuty => Player.IsInDuty;
    [LuaFunction] public bool IsOnIsland => Player.IsOnIsland;
    [LuaFunction] public bool CanMount => Player.CanMount;
    [LuaFunction] public bool CanFly => Player.CanFly;
    [LuaFunction] public bool Revivable => Player.Revivable;
    [LuaFunction] public bool Available => Player.Available;
    [LuaFunction][Changelog("12.40")] public bool IsLevelSynced => Player.IsLevelSynced;
    [LuaFunction][Changelog("12.40")] public int SyncedLevel => Player.SyncedLevel;

    [LuaFunction][Changelog("12.8")] public bool IsBusy => Player.IsBusy;
    [LuaFunction][Changelog("12.22")] public List<StatusWrapper> Status => [.. Player.BattleChara->GetStatusManager()->Status.ToArray().Select(s => new StatusWrapper(s))];

    [LuaFunction][Changelog("12.12")] public BingoWrapper Bingo => new(this);
    public class BingoWrapper(PlayerModule parentModule) : IWrapper
    {
        private PlayerState* Ps => Instance();

        [LuaDocs] public bool HasWeeklyBingoJournal => Ps->HasWeeklyBingoJournal;
        [LuaDocs] public bool IsWeeklyBingoExpired => Ps->IsWeeklyBingoExpired();
        [LuaDocs] public uint WeeklyBingoNumSecondChancePoints => Ps->WeeklyBingoNumSecondChancePoints;
        [LuaDocs] public int WeeklyBingoNumPlacedStickers => Ps->WeeklyBingoNumPlacedStickers;
        [LuaDocs] public object? GetWeeklyBingoOrderDataRow(int wonderousTailsIndex) => parentModule.GetModule<ExcelModule>()?.GetRow("WeeklyBingoOrderData", Ps->WeeklyBingoOrderData[wonderousTailsIndex]);
        [LuaDocs] public WeeklyBingoTaskStatus GetWeeklyBingoTaskStatus(int wonderousTailsIndex) => Ps->GetWeeklyBingoTaskStatus(wonderousTailsIndex);
    }
}
