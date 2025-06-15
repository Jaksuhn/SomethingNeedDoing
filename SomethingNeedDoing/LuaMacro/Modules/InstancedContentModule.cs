using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Event;
using FFXIVClientStructs.FFXIV.Client.Game.InstanceContent;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro.Wrappers;

namespace SomethingNeedDoing.LuaMacro.Modules;
/// <summary>
/// Module for deep dungeons and forays
/// </summary>
public unsafe class InstancedContentModule : LuaModuleBase
{
    public override string ModuleName => "InstancedContent";

    [LuaFunction] public float ContentTimeLeft => EventFramework.Instance()->GetInstanceContentDirector()->ContentDirector.ContentTimeLeft;

    [LuaFunction] public OceanFishingWrapper OceanFishing => new();
    [LuaFunction] public OccultCrescentWrapper OccultCrescent => new(this);

    public class OccultCrescentWrapper(InstancedContentModule parentModule) : IWrapper
    {
        [LuaDocs] public List<DynamicEventWrapper> Events
            => [.. PublicContentOccultCrescent.GetInstance()->DynamicEventContainer.Events.ToArray().Select(e => new DynamicEventWrapper(e, parentModule))];
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
        [LuaDocs] public uint CipherItemId => data->CipherItemId;
        [LuaDocs] public int CipherNameId => data->CipherNameId;
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
    }
}
