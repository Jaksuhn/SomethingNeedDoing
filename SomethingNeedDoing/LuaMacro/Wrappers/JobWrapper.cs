using ECommons.ExcelServices;
using Lumina.Excel.Sheets;
using SomethingNeedDoing.Core.Interfaces;
using Role = SomethingNeedDoing.Utils.ClassJobExtensions.Role;

namespace SomethingNeedDoing.LuaMacro.Wrappers;

public class JobWrapper(uint classJobId) : IWrapper
{
    private ClassJob Row => GetRow<ClassJob>(Id) ?? throw new Exception($"ClassJob #{Id} not found");
    [LuaDocs] public uint Id => classJobId;
    [LuaDocs] public string Name => GetRow<ClassJob>(Id)?.Name.ToString() ?? string.Empty;
    [LuaDocs] public string Abbreviation => GetRow<ClassJob>(Id)?.Abbreviation.ToString() ?? string.Empty;
    [LuaDocs] public bool IsCrafter => Id is >= 8 and <= 15;
    [LuaDocs] public bool IsGatherer => Id is >= 16 and <= 18;
    [LuaDocs][Changelog("12.61", ChangelogType.Fixed)] public bool IsMeleeDPS => Row.GetRole() is Role.Melee;
    [LuaDocs][Changelog("12.61", ChangelogType.Fixed)] public bool IsRangedDPS => Row.GetRole() is Role.PhysRanged;
    [LuaDocs][Changelog("12.61", ChangelogType.Fixed)] public bool IsMagicDPS => Row.GetRole() is Role.Caster;
    [LuaDocs][Changelog("12.61", ChangelogType.Fixed)] public bool IsHealer => Row.GetRole() is Role.Healer;
    [LuaDocs][Changelog("12.61", ChangelogType.Fixed)] public bool IsTank => Row.GetRole() is Role.Tank;
    [LuaDocs] public bool IsDPS => IsMeleeDPS || IsRangedDPS || IsMagicDPS;
    [LuaDocs] public bool IsDiscipleOfWar => IsMeleeDPS || IsRangedDPS || IsTank;
    [LuaDocs] public bool IsDiscipleOfMagic => IsMagicDPS || IsHealer;
    [LuaDocs] public bool IsBlu => Id is 36;
    [LuaDocs] public bool IsLimited => IsBlu;
    [LuaDocs(description: "Unsynced level (only for LocalPlayer)")] public int Level => Player.GetLevel((Job)Id);
}
