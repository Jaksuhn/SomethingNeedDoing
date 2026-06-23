using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.ClientState.Party;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Lumina.Excel.Sheets;
using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.LuaMacro.Wrappers;

public unsafe class EntityWrapper : IWrapper
{
    public EntityWrapper(GameObject* obj) => _obj = obj;
    public EntityWrapper(nint obj) => _obj = (GameObject*)obj;
    public EntityWrapper(IGameObject obj) => _obj = (GameObject*)obj.Address;
    public EntityWrapper(IPartyMember obj) => _obj = (GameObject*)obj.Address;
    public EntityWrapper(GameObjectId id) => _obj = GameObjectManager.Instance()->Objects.GetObjectByGameObjectId(id);

    private readonly GameObject* _obj;
    private IGameObject? DalamudObj => Svc.Objects.CreateObjectReference((nint)_obj);
    private Character* Character => IsCharacter ? (Character*)_obj : null;
    private BattleChara* BattleChara => Type == ObjectKind.BattleNpc ? (BattleChara*)_obj : null;
    private bool IsPlayer => IsCharacter && Type == ObjectKind.Pc;
    private bool IsCharacter => _obj != null && _obj->IsCharacter();
    private T GetCharacterValue<T>(Func<T> getter) => IsCharacter ? getter() : default!;
    private T GetBattleCharaValue<T>(Func<T> getter) => Type == ObjectKind.BattleNpc ? getter() : default!;

    [LuaDocs] public ObjectKind Type => _obj->ObjectKind;
    [LuaDocs] public string Name => _obj->NameString;
    [LuaDocs] public Vector3 Position => _obj->Position;
    [LuaDocs] public float DistanceTo => Player.DistanceTo(Position);

    [LuaDocs] public ulong ContentId => GetCharacterValue(() => Character->ContentId);
    [LuaDocs] public ulong AccountId => GetCharacterValue(() => Character->AccountId);
    [LuaDocs] public ushort CurrentWorld => GetCharacterValue(() => Character->CurrentWorld);
    [LuaDocs] public ushort HomeWorld => GetCharacterValue(() => Character->HomeWorld);

    [LuaDocs] public uint CurrentHp => GetCharacterValue(() => Character->Health);
    [LuaDocs] public uint MaxHp => GetCharacterValue(() => Character->MaxHealth);
    [LuaDocs] public float HealthPercent => (float)CurrentHp / MaxHp * 100f;
    [LuaDocs] public uint CurrentMp => GetCharacterValue(() => Character->Mana);
    [LuaDocs] public uint MaxMp => GetCharacterValue(() => Character->MaxMana);

    [LuaDocs] public EntityWrapper? Target => DalamudObj?.TargetObject is { } target ? new(target) : null;
    [LuaDocs] public bool IsCasting => GetCharacterValue(() => Character->IsCasting);
    [LuaDocs] public bool IsTargetable => _obj->GetIsTargetable();
    [LuaDocs] public bool IsCastInterruptible => GetCharacterValue(() => Character->GetCastInfo()->Interruptible);
    [LuaDocs] public bool IsInCombat => GetCharacterValue(() => Character->InCombat);
    [LuaDocs] public byte HuntRank => FindRow<NotoriousMonster>(x => x.BNpcBase.Value!.RowId == _obj->BaseId)?.Rank ?? 0;
    [LuaDocs][Changelog("15.5")] public bool IsHostile => GetCharacterValue(() => Character->IsHostile);

    [LuaDocs]
    [Changelog("12.15")]
    public bool IsMounted
    {
        get
        {
            if (Type != ObjectKind.Pc) return false;
            if (Character->ObjectIndex + 1 > Svc.Objects.Length) return false;
            return Svc.Objects[Character->ObjectIndex + 1] is { ObjectKind: Dalamud.Game.ClientState.Objects.Enums.ObjectKind.Mount };
        }
    }

    [LuaDocs][Changelog("12.22")] public List<StatusWrapper>? Status => BattleChara != null ? [.. BattleChara->GetStatusManager()->Status.ToArray().Select(x => new StatusWrapper(x))] : null;
    [LuaDocs][Changelog("12.22")] public ushort FateId => GetBattleCharaValue(() => BattleChara->FateId);

    [LuaDocs] public void SetAsTarget() => Svc.Targets.Target = DalamudObj;
    [LuaDocs] public void SetAsFocusTarget() => Svc.Targets.FocusTarget = DalamudObj;
    [LuaDocs] public void ClearTarget() => Svc.Targets.Target = null;
    [LuaDocs] public void Interact() => Game.Interact(DalamudObj);
}
