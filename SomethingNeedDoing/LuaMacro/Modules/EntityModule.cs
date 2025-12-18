using Dalamud.Game.ClientState.Objects.SubKinds;
using NLua;
using SomethingNeedDoing.LuaMacro.Wrappers;

namespace SomethingNeedDoing.LuaMacro.Modules;

public unsafe class EntityModule : LuaModuleBase
{
    public override string ModuleName => "Entity";
    protected override object? MetaIndex(LuaTable table, object key) => Svc.Objects[int.Parse(key.ToString() ?? string.Empty)] is { } obj ? new EntityWrapper(obj) : null;

    [LuaFunction] public EntityWrapper? Player => Svc.Objects.LocalPlayer is { } player ? new(player) : null;
    [LuaFunction] public EntityWrapper? Target => Svc.Targets.Target is { } target ? new(target) : null;
    [LuaFunction] public EntityWrapper? FocusTarget => Svc.Targets.FocusTarget is { } target ? new(target) : null;
    [LuaFunction] public EntityWrapper? NearestDeadCharacter => Svc.Objects.OfType<IPlayerCharacter>().OrderBy(ECommons.GameHelpers.Player.DistanceTo).FirstOrDefault(o => o.IsDead) is { } obj ? new(obj) : null;
    [LuaFunction] public EntityWrapper? GetPartyMember(int index) => Svc.Party.GetPartyMemberAddress(index) is { } member ? new(member) : null;
    [LuaFunction] public EntityWrapper? GetAllianceMember(int index) => Svc.Party.GetAllianceMemberAddress(index) is { } member ? new(member) : null;
    [LuaFunction] public EntityWrapper? GetEntityByName(string name) => Svc.Objects.FirstOrDefault(o => o.Name.TextValue.Equals(name, StringComparison.InvariantCultureIgnoreCase)) is { } obj ? new(obj) : null;
}
