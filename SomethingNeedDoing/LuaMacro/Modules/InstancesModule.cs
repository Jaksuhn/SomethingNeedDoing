using Dalamud.Game.ClientState.Aetherytes;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Client.UI.Info;
using NLua;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.LuaMacro.Wrappers;
using static FFXIVClientStructs.FFXIV.Client.UI.Info.InfoProxyCommonList.CharacterData;

namespace SomethingNeedDoing.LuaMacro.Modules;
public unsafe class InstancesModule : LuaModuleBase
{
    public override string ModuleName => "Instances";
    public override void Register(Lua lua)
    {
        lua.RegisterEnum<OnlineStatus>();
        lua.RegisterEnum<GrandCompany>();
        lua.RegisterEnum<Language>();
        base.Register(lua);
    }

    [LuaFunction] public DutyFinderWrapper DutyFinder => new();
    public unsafe class DutyFinderWrapper : IWrapper
    {
        [LuaDocs] public void OpenRouletteDuty(byte contentRouletteID) => AgentContentsFinder.Instance()->OpenRouletteDuty(contentRouletteID);
        [LuaDocs] public void OpenRegularDuty(uint contentsFinderCondition) => AgentContentsFinder.Instance()->OpenRegularDuty(contentsFinderCondition);

        [LuaDocs] public bool IsUnrestrictedParty { get => ContentsFinder.Instance()->IsUnrestrictedParty; set => ContentsFinder.Instance()->IsUnrestrictedParty = value; }
        [LuaDocs] public bool IsLevelSync { get => ContentsFinder.Instance()->IsLevelSync; set => ContentsFinder.Instance()->IsLevelSync = value; }
        [LuaDocs] public bool IsMinIL { get => ContentsFinder.Instance()->IsMinimalIL; set => ContentsFinder.Instance()->IsMinimalIL = value; }
        [LuaDocs] public bool IsSilenceEcho { get => ContentsFinder.Instance()->IsSilenceEcho; set => ContentsFinder.Instance()->IsSilenceEcho = value; }
        [LuaDocs] public bool IsExplorerMode { get => ContentsFinder.Instance()->IsExplorerMode; set => ContentsFinder.Instance()->IsExplorerMode = value; }
        [LuaDocs] public bool IsLimitedLevelingRoulette { get => ContentsFinder.Instance()->IsLimitedLevelingRoulette; set => ContentsFinder.Instance()->IsLimitedLevelingRoulette = value; }
    }

    [LuaFunction] public FriendsListWrapper FriendsList => new();
    public class FriendsListWrapper : IWrapper
    {
        [LuaDocs]
        public List<FriendWrapper> Friends
        {
            get
            {
                var friends = new List<FriendWrapper>();
                for (var i = 0; i < AgentFriendlist.Instance()->InfoProxy->CharDataSpan.Length; i++)
                    friends.Add(new(AgentFriendlist.Instance()->InfoProxy->CharDataSpan[i]));
                return friends;
            }
        }

        [LuaDocs] public FriendWrapper? GetFriendByName(string name) => Friends.FirstOrDefault(f => f.Name == name);
    }

    public class FriendWrapper(InfoProxyCommonList.CharacterData data) : IWrapper
    {
        [LuaDocs] public string Name => data.NameString;
        [LuaDocs] public ulong ContentId => data.ContentId;
        [LuaDocs] public OnlineStatus State => data.State;
        [LuaDocs] public bool IsOtherServer => data.IsOtherServer;
        [LuaDocs] public ushort CurrentWorld => data.CurrentWorld;
        [LuaDocs] public ushort HomeWorld => data.HomeWorld;
        [LuaDocs] public ushort Location => data.Location;
        [LuaDocs] public GrandCompany GrandCompany => data.GrandCompany;
        [LuaDocs] public Language ClientLanguage => data.ClientLanguage;
        [LuaDocs] public byte Sex => data.Sex;
        [LuaDocs] public JobWrapper Job => new(data.Job);
    }

    [LuaFunction]
    [Changelog("12.8")]
    public MapWrapper Map => new();
    public class MapWrapper : IWrapper
    {
        [LuaDocs][Changelog("12.8")] public bool IsFlagMarkerSet => AgentMap.Instance()->IsFlagMarkerSet;

        [LuaDocs][Changelog("12.8")] public FlagWrapper Flag => new(AgentMap.Instance()->FlagMapMarker);
    }

    public class FlagWrapper(FlagMapMarker data) : IWrapper
    {
        [LuaDocs][Changelog("12.8")] public uint TerritoryId => data.TerritoryId;
        [LuaDocs][Changelog("12.8")] public uint MapId => data.MapId;
        [LuaDocs][Changelog("12.8")] public float XFloat => data.XFloat;
        [LuaDocs][Changelog("12.8")] public float YFloat => data.YFloat;
        [LuaDocs][Changelog("12.8")] public Vector2 Vector2 => new(XFloat, YFloat);
        [LuaDocs][Changelog("12.8")] public Vector3 Vector3 => new(XFloat, 0, YFloat); // TODO use navmesh PointOnFloor

        [LuaDocs][Changelog("12.22")] public void SetFlagMapMarker(uint territoryId, uint mapId, float x, float y) => AgentMap.Instance()->SetFlagMapMarker(territoryId, mapId, new Vector3(x, 0, y));
        [LuaDocs][Changelog("12.22")] public void SetFlagMapMarker(uint territoryId, float x, float y) => SetFlagMapMarker(territoryId, GetRow<Sheets.TerritoryType>(territoryId)!.Value.Map.RowId, x, y);
    }

    public class MapMarkerDataWrapper(MapMarkerData data) : IWrapper
    {
        [LuaDocs][Changelog("12.8")] public uint LevelId => data.LevelId;
        [LuaDocs][Changelog("12.8")] public uint ObjectiveId => data.ObjectiveId;
        [LuaDocs][Changelog("12.8")] public string TooltipString => data.TooltipString->ToString();
        [LuaDocs][Changelog("12.8")] public uint IconId => data.IconId;
        [LuaDocs][Changelog("12.8")] public Vector3 Position => data.Position;
        [LuaDocs][Changelog("12.8")] public float Radius => data.Radius;
        [LuaDocs][Changelog("12.8")] public uint MapId => data.MapId;
        [LuaDocs][Changelog("12.8")] public uint PlaceNameZoneId => data.PlaceNameZoneId;
        [LuaDocs][Changelog("12.8")] public uint PlaceNameId => data.PlaceNameId;
        [LuaDocs][Changelog("12.8")] public int EndTimestamp => data.EndTimestamp;
        [LuaDocs][Changelog("12.8")] public ushort RecommendedLevel => data.RecommendedLevel;
        [LuaDocs][Changelog("12.8")] public ushort TerritoryTypeId => data.TerritoryTypeId;
        [LuaDocs][Changelog("12.8")] public ushort DataId => data.DataId;
        [LuaDocs][Changelog("12.8")] public byte MarkerType => data.MarkerType;
        [LuaDocs][Changelog("12.8")] public sbyte EventState => data.EventState;
        [LuaDocs][Changelog("12.8")] public byte Flags => data.Flags;
    }

    [LuaFunction] public FrameworkWrapper Framework => new();
    public class FrameworkWrapper : IWrapper
    {
        [LuaDocs][Changelog("12.9")] public long EorzeaTime => FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->ClientTime.EorzeaTime;
        [LuaDocs][Changelog("12.9")] public byte ClientLanguage => FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->ClientLanguage;
        [LuaDocs][Changelog("12.9")] public byte Region => FFXIVClientStructs.FFXIV.Client.System.Framework.Framework.Instance()->Region;
    }

    [LuaFunction] public TelepoWrapper Telepo => new();
    public class TelepoWrapper : IWrapper
    {
        [LuaDocs][Changelog("12.18")] public void Teleport(IAetheryteEntry aetheryte) => FFXIVClientStructs.FFXIV.Client.Game.UI.Telepo.Instance()->Teleport(aetheryte.AetheryteId, aetheryte.SubIndex);
        [LuaDocs][Changelog("12.18")] public void Teleport(uint aetheryteId, byte subIndex) => FFXIVClientStructs.FFXIV.Client.Game.UI.Telepo.Instance()->Teleport(aetheryteId, subIndex);
        [LuaDocs][Changelog("12.18")] public Vector3 GetAetherytePosition(uint aetheryteId) => ECommons.GameHelpers.Map.AetherytePosition(aetheryteId);
        [LuaDocs][Changelog("12.18")] public bool IsAetheryteUnlocked(uint aetheryteId) => UIState.Instance()->IsAetheryteUnlocked(aetheryteId);
    }

    [LuaFunction] public EnvManagerWrapper EnvManager => new();
    public class EnvManagerWrapper : IWrapper
    {
        [LuaDocs][Changelog("12.20")] public float DayTimeSeconds => FFXIVClientStructs.FFXIV.Client.Graphics.Environment.EnvManager.Instance()->DayTimeSeconds;
        [LuaDocs][Changelog("12.20")] public float ActiveTransitionTime => FFXIVClientStructs.FFXIV.Client.Graphics.Environment.EnvManager.Instance()->ActiveTransitionTime;
        [LuaDocs][Changelog("12.20")] public float CurrentTransitionTime => FFXIVClientStructs.FFXIV.Client.Graphics.Environment.EnvManager.Instance()->CurrentTransitionTime;
        [LuaDocs][Changelog("12.20")] public byte ActiveWeather => FFXIVClientStructs.FFXIV.Client.Graphics.Environment.EnvManager.Instance()->ActiveWeather;
        [LuaDocs][Changelog("12.20")] public float TransitionTime => FFXIVClientStructs.FFXIV.Client.Graphics.Environment.EnvManager.Instance()->TransitionTime;
    }

    [LuaFunction][Changelog("12.22")] public BuddyWrapper Buddy => new();
}
