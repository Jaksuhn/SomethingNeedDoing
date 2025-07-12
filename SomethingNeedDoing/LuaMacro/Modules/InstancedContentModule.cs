using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using NLua;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro.Wrappers;
using ContentType = FFXIVClientStructs.FFXIV.Client.Game.Event.ContentType;

namespace SomethingNeedDoing.LuaMacro.Modules;
/// <summary>
/// Module for deep dungeons and forays
/// </summary>
public unsafe class InstancedContentModule : LuaModuleBase
{
    public override string ModuleName => "InstancedContent";
    public override void Register(Lua lua)
    {
        lua.RegisterEnum<DynamicEventState>();
        lua.RegisterEnum<InstanceContentOceanFishing.OceanFishingStatus>();
        lua.RegisterEnum<ContentType>();
        base.Register(lua);
    }

    [LuaFunction]
    public float ContentTimeLeft
    {
        get
        {
            var director = EventFramework.Instance()->GetContentDirector();
            return director == null ? 0f : director->ContentTimeLeft;
        }
    }

    [LuaFunction]
    [Changelog("12.47")]
    [Changelog("12.55", ChangelogType.Changed, "Changed name")]
    public uint GetCurrentContentId() => EventFramework.GetCurrentContentId();

    [LuaFunction]
    [Changelog("12.47")]
    [Changelog("12.55", ChangelogType.Changed, "Changed name")]
    public FFXIVClientStructs.FFXIV.Client.Game.Event.ContentType GetCurrentContentType() => EventFramework.GetCurrentContentType();
    [LuaFunction]
    [Changelog("12.47")]
    [Changelog("12.55", ChangelogType.Changed, "Changed name")]
    public bool CanLeaveCurrentContent() => EventFramework.CanLeaveCurrentContent();

    [LuaFunction][Changelog("12.47")] public void LeaveCurrentContent() => EventFramework.LeaveCurrentContent(true);

    [LuaFunction] public OceanFishingWrapper OceanFishing => new();
    [LuaFunction] public OccultCrescentWrapper OccultCrescent => new(this);

    public class OccultCrescentWrapper(InstancedContentModule parentModule) : IWrapper
    {
        [LuaDocs] public List<DynamicEventWrapper> Events => [.. PublicContentOccultCrescent.GetInstance()->DynamicEventContainer.Events.ToArray().Select(e => new DynamicEventWrapper(e, parentModule))];
        [LuaDocs] public MKDDataWrapper MKDData => new(PublicContentOccultCrescent.GetMKDData());
        [LuaDocs] public OccultCrescentStateWrapper OccultCrescentState => new(PublicContentOccultCrescent.GetState());
        [LuaDocs] public List<EntityWrapper>? ChainTargets => [.. Svc.Objects.OfType<IBattleChara>().Where(o => PublicContentOccultCrescent.IsChainTarget(o.Character())).Select(o => new EntityWrapper(o))];
    }

    public class DynamicEventWrapper(DynamicEvent evt, InstancedContentModule parentModule) : IWrapper
    {
        [LuaDocs] public uint Quest => evt.Quest;
        [LuaDocs] public object? QuestRow => parentModule.GetModule<ExcelModule>()?.GetRow("Quest", evt.Quest);
        [LuaDocs] public uint Announce => evt.Announce;
        [LuaDocs] public byte EventType => evt.EventType;
        [LuaDocs] public object? EventTypeRow => parentModule.GetModule<ExcelModule>()?.GetRow("EventType", evt.EventType);
        [LuaDocs] public byte EnemyType => evt.EnemyType;
        [LuaDocs] public object? EnemyTypeRow => parentModule.GetModule<ExcelModule>()?.GetRow("EnemyType", evt.EnemyType);
        [LuaDocs] public byte MaxParticipants => evt.MaxParticipants;
        [LuaDocs] public byte SingleBattle => evt.SingleBattle;
        [LuaDocs] public object? SingleBattleRow => parentModule.GetModule<ExcelModule>()?.GetRow("DynamicEventSingleBattle", evt.SingleBattle);
        [LuaDocs] public int StartTimestamp => evt.StartTimestamp;
        [LuaDocs] public uint SecondsLeft => evt.SecondsLeft;
        [LuaDocs] public uint SecondsDuration => evt.SecondsDuration;
        [LuaDocs] public byte Participants => evt.Participants;
        [LuaDocs] public string Name => evt.Name.ToString();
        [LuaDocs] public string Description => evt.Description.ToString();
        [LuaDocs] public byte Progress => evt.Progress;
        [LuaDocs] public DynamicEventState State => evt.State;
        [LuaDocs] public bool IsActive => evt.IsActive();
    }

    public class MKDDataWrapper(OccultCrescentMKDData* data) : IWrapper
    {
        [LuaDocs] public uint QuestId => data->QuestId;
        [LuaDocs] public uint ZoneNameId => data->ZoneNameId;
        [LuaDocs] public uint CipherItemId => data->CurrencyItemIds[2];
        [LuaDocs] public uint CipherNameId => data->CurrencyNameIds[2];
    }

    public class OccultCrescentStateWrapper(OccultCrescentState* state) : IWrapper
    {
        [LuaDocs] public uint CurrentKnowledge => state->CurrentKnowledge;
        [LuaDocs] public uint NeededKnowledge => state->NeededKnowledge;
        [LuaDocs] public uint NeededJobExperience => state->NeededJobExperience;
        [LuaDocs] public ushort Silver => state->Silver;
        [LuaDocs] public ushort Gold => state->Gold;
        [LuaDocs] public byte CurrentSupportJob => state->CurrentSupportJob;
        [LuaDocs] public byte KnowledgeLevelSync => state->KnowledgeLevelSync;
        [LuaDocs][Changelog("12.47")] public uint[] SupportJobExperience => state->SupportJobExperience.ToArray();
        [LuaDocs][Changelog("12.47")] public byte[] SupportJobLevels => state->SupportJobLevels.ToArray();
    }

    [LuaFunction][Changelog("12.22")] public PublicInstanceWrapper PublicInstance => new();
    public class PublicInstanceWrapper : IWrapper
    {
        [LuaDocs][Changelog("12.22")] public uint TerritoryTypeId => UIState.Instance()->PublicInstance.TerritoryTypeId;
        [LuaDocs][Changelog("12.22")] public uint InstanceId => UIState.Instance()->PublicInstance.InstanceId;
        [LuaDocs][Changelog("12.22")] public bool IsInstancedArea => UIState.Instance()->PublicInstance.IsInstancedArea();
    }
}
