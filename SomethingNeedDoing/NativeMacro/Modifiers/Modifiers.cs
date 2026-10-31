using SomethingNeedDoing.Core.Interfaces;

namespace SomethingNeedDoing.NativeMacro.Modifiers;

/// <summary>
/// Modifier for specifying wait duration after a command. With an until value, a random duration in that range is used.
/// </summary>
[GenericDoc(
    "Specify wait duration after a command",
    ["wait", "until"],
    ["/ac Groundwork <wait.3>", "ac Groundwork <wait.3.5", "/ac Groundwork <wait.1-5>", "/ac Groundwork <wait.1.5-5.5>"]
)]
public class WaitModifier(string text, int waitDuration, int maxWaitDuration = 0) : MacroModifierBase(text)
{
    /// <summary>Gets the wait duration in milliseconds.</summary>
    public int WaitDuration { get; } = waitDuration;

    /// <summary>Gets the maximum wait duration in milliseconds for random waits, or 0.</summary>
    public int MaxWaitDuration { get; } = maxWaitDuration;
}

/// <summary>
/// Modifier for the maximum time some commands wait for a state.
/// </summary>
[GenericDoc(
    "For certain commands, the maximum time to wait for a certain state to be achieved. By default, this is 5 seconds.",
    ["wait"],
    ["/waitaddon RecipeNote <maxwait.10>"]
)]
public class MaxWaitModifier(string text, int maxWaitMs) : MacroModifierBase(text)
{
    /// <summary>Gets the maximum wait in milliseconds.</summary>
    public int MaxWaitMilliseconds { get; } = maxWaitMs;
}

/// <summary>
/// Modifier for controlling loop count echo behavior.
/// </summary>
[GenericDoc(
    "Echo the amount of loops remaining after executing a /loop command.",
    [],
    ["/loop 10 <echo>"]
)]
public class EchoModifier(string text, bool shouldEcho) : MacroModifierBase(text)
{
    /// <summary>Gets whether to echo loop counts.</summary>
    public bool ShouldEcho { get; } = shouldEcho;
}

/// <summary>
/// Modifier for specifying unsafe action execution.
/// </summary>
[GenericDoc(
    "Prevent the /action command from waiting for a positive server response and attempting to execute the command anyways",
    [],
    ["/ac \"Tricks of the Trade\" <unsafe>"]
)]
public class UnsafeModifier(string text, bool isUnsafe) : MacroModifierBase(text)
{
    /// <summary>Gets whether unsafe mode is enabled.</summary>
    public bool IsUnsafe { get; } = isUnsafe;
}

/// <summary>
/// Modifier for specifying high-quality items.
/// </summary>
[GenericDoc(
    "Specify high-quality items",
    [],
    ["/item Calamari Ripieni <hq>"]
)]
public class HqModifier(string text, bool isHq) : MacroModifierBase(text)
{
    /// <summary>Gets whether the item should be high quality.</summary>
    public bool IsHighQuality { get; } = isHq;
}

/// <summary>
/// Modifier for specifying conditions in crafting commands.
/// </summary>
[GenericDoc(
    "Require a crafting condition to perform the action specified. This is taken from the Synthesis window and may be localized to your client language.",
    ["conditions"],
    ["/ac Observe <condition.poor>", "/ac \"Precise Touch\" <condition.good,excellent>", "/ac \"Byregot's Blessing\" <condition.not.poor>", "/ac \"Byregot's Blessing\" <condition.!poor>"]
)]
public class ConditionModifier(string text, string[] conditions, bool isNegated) : MacroModifierBase(text)
{
    /// <summary>Gets the conditions to check for.</summary>
    public string[] Conditions { get; } = conditions;

    /// <summary>Gets whether the condition check should be negated.</summary>
    public bool IsNegated { get; } = isNegated;

    /// <summary>Whether the current crafting condition satisfies this modifier.</summary>
    public bool IsMet(string currentCondition) => Conditions.Length == 0 || Conditions.Any(c => c.EqualsIgnoreCase(currentCondition)) != IsNegated;
}

/// <summary>
/// Modifier for specifying object indices.
/// </summary>
[GenericDoc(
    "Specify object indices for targeting and interaction",
    ["index"],
    ["/target \"Zodiark\" <index.1>", "/interact \"Y'shtola\" <index.2>"]
)]
public class IndexModifier(string text, int index) : MacroModifierBase(text)
{
    /// <summary>Gets the object index.</summary>
    public int Index { get; } = index;
}

/// <summary>
/// Modifier for specifying list indices.
/// </summary>
[GenericDoc(
    "Specify list indices for multi-target operations",
    ["index"],
    ["/target \"Right Arm\" <list.1>", "/target \"Right Arm\" <list.2>"]
)]
public class ListIndexModifier(string text, int index) : MacroModifierBase(text)
{
    /// <summary>Gets the list index.</summary>
    public int Index { get; } = index;
}

/// <summary>
/// Modifier for specifying party member indices.
/// </summary>
[GenericDoc(
    "Specify party member indices",
    ["index"],
    ["/ac Cure <1>", "/ac Cure <party.1>"]
)]
public class PartyIndexModifier(string text, int index) : MacroModifierBase(text)
{
    /// <summary>Gets the party member index (1-8).</summary>
    public int Index { get; } = index;
}

/// <summary>
/// Modifier for specifying maximum distance for targeting and interaction.
/// </summary>
[GenericDoc(
    "Specify maximum distance for targeting and interaction",
    ["distance"],
    ["/target \"Zodiark\" <distance.3>", "/interact \"Y'shtola\" <distance.5.5>"]
)]
public class DistanceModifier(string text, float distance) : MacroModifierBase(text)
{
    /// <summary>Gets the maximum distance in yalms.</summary>
    public float Distance { get; } = distance;
}

/// <summary>
/// Represents the different error conditions that can be checked.
/// </summary>
public enum ErrorCondition
{
    /// <summary>
    /// Error when an action times out.
    /// </summary>
    ActionTimeout,

    /// <summary>
    /// Error when an item is not found.
    /// </summary>
    ItemNotFound,

    /// <summary>
    /// Error when an item cannot be used.
    /// </summary>
    CantUseItem,

    /// <summary>
    /// Error when a target is not found.
    /// </summary>
    TargetNotFound,

    /// <summary>
    /// Error when an addon is not found.
    /// </summary>
    AddonNotFound
}

/// <summary>
/// Modifier for specifying error conditions that should stop the macro.
/// </summary>
[GenericDoc(
    "Stop the macro if a specific error condition occurs",
    ["condition"],
    ["<errorif.actiontimeout>", "<errorif.itemnotfound>", "<errorif.cantuseitem>", "<errorif.targetnotfound>", "<errorif.addonnotfound>"]
)]
public class ErrorIfModifier(string text, ErrorCondition condition) : MacroModifierBase(text)
{
    /// <summary>Gets the error condition to check.</summary>
    public ErrorCondition Condition { get; } = condition;
}
