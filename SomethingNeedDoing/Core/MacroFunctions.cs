using Dalamud.Game.Addon.Lifecycle;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace SomethingNeedDoing.Core;

/// <summary>
/// A function declared in a macro, and what (if anything) it is wired to by naming convention.
/// </summary>
/// <param name="Name">The function name.</param>
/// <param name="TriggerEvent">The trigger event the function handles, if its name starts with one.</param>
/// <param name="AddonName">For <c>OnAddonEvent_Addon_Event</c> functions, the addon name.</param>
/// <param name="AddonEvent">For <c>OnAddonEvent_Addon_Event</c> functions, the addon event.</param>
/// <param name="IsCleanup">Whether the function runs when the macro stops.</param>
public sealed record FunctionInfo(string Name, TriggerEvent? TriggerEvent, string? AddonName, AddonEvent? AddonEvent, bool IsCleanup);

/// <summary>
/// Finds the functions in a macro's content. Only Lua macros declare functions.
/// </summary>
public static partial class MacroFunctions
{
    private static readonly ConditionalWeakTable<string, IReadOnlyList<FunctionInfo>> Cache = [];
    private static readonly HashSet<string> CleanupNames = new(StringComparer.OrdinalIgnoreCase) { "OnCleanup", "OnDispose", "OnStop", "OnEnd", "Cleanup", "Dispose", "Stop", "End" };
    private static readonly TriggerEvent[] TriggerEvents = [.. Enum.GetValues<TriggerEvent>().Where(e => e != Core.TriggerEvent.None)];

    [GeneratedRegex(@"function\s+(\w+)\s*\(")]
    private static partial Regex LuaFunctionRegex();

    public static IReadOnlyList<FunctionInfo> Parse(string content, MacroType type)
        => type != MacroType.Lua ? [] : Cache.GetValue(content, static c => [.. LuaFunctionRegex().Matches(c).Select(m => m.Groups[1].Value).Distinct().Select(Describe)]);

    public static IEnumerable<FunctionInfo> TriggerFunctions(string content, MacroType type) => Parse(content, type).Where(f => f.TriggerEvent != null);
    public static IEnumerable<string> CleanupFunctions(string content, MacroType type) => Parse(content, type).Where(f => f.IsCleanup).Select(f => f.Name);

    private static FunctionInfo Describe(string name)
    {
        var isCleanup = CleanupNames.Contains(name);

        // OnAddonEvent_AddonName_EventType (anything after is ignored)
        if (name.StartsWith($"{nameof(Core.TriggerEvent.OnAddonEvent)}_", StringComparison.OrdinalIgnoreCase))
        {
            var parts = name.Split('_');
            return parts.Length >= 3 && Enum.TryParse<AddonEvent>(parts[2], true, out var addonEvent)
                ? new(name, Core.TriggerEvent.OnAddonEvent, parts[1], addonEvent, isCleanup)
                : new(name, null, null, null, isCleanup);
        }

        var trigger = TriggerEvents.FirstOrDefault(e => name.StartsWith(e.ToString(), StringComparison.OrdinalIgnoreCase));
        // a bare OnAddonEvent function has no addon to listen to
        return new(name, trigger is Core.TriggerEvent.None or Core.TriggerEvent.OnAddonEvent ? null : trigger, null, null, isCleanup);
    }
}
