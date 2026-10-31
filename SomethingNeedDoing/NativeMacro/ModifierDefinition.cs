using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.NativeMacro.Modifiers;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SomethingNeedDoing.NativeMacro;

/// <summary>
/// Defines an SND modifier: its name, the regex that matches it, and how it is applied to a command.
/// </summary>
/// <param name="Name">The name of the modifier.</param>
/// <param name="RegexPattern">The regex pattern for matching this modifier.</param>
/// <param name="Apply">Sets the modifier on a command from a match of <paramref name="RegexPattern"/>.</param>
public sealed record ModifierDefinition(string Name, string RegexPattern, Action<IMacroCommand, Match> Apply);

/// <summary>
/// Static class containing all modifier definitions.
/// </summary>
public static class ModifierDefinitions
{
    /// <summary>
    /// Gets all SND-specific modifier definitions. These are removed from the command text before it is parsed.
    /// </summary>
    public static readonly ModifierDefinition[] SndModifiers = [
        new("wait", @"<wait\.(?<wait>\d+(?:\.\d+)?)(?:-(?<until>\d+(?:\.\d+)?))?>", (command, match) =>
        {
            var wait = Milliseconds(match.Groups["wait"].Value);
            var until = match.Groups["until"].Success ? Milliseconds(match.Groups["until"].Value) : 0;
            if (until > 0 && wait > until)
                throw new MacroSyntaxError("Until value cannot be lower than the wait value");

            command.WaitModifier = new WaitModifier(match.Value, wait, until);
            if (command is MacroCommandBase baseCommand)
                baseCommand.WaitDuration = wait;
        }),

        new("maxwait", @"<maxwait\.(?<maxwait>\d+(?:\.\d+)?)>",
            (command, match) => command.MaxWaitModifier = new MaxWaitModifier(match.Value, Milliseconds(match.Groups["maxwait"].Value))),

        new("echo", @"<echo>",
            (command, match) => command.EchoModifier = new EchoModifier(match.Value, true)),

        new("unsafe", @"<unsafe>",
            (command, match) => command.UnsafeModifier = new UnsafeModifier(match.Value, true)),

        new("hq", @"<hq>",
            (command, match) => command.ItemQualityModifier = new HqModifier(match.Value, true)),

        new("condition", @"<condition\.(?<not>(not\.|\!))?(?<conditions>[^>]+)>", (command, match) =>
        {
            var conditions = match.Groups["conditions"].Value.ToLowerInvariant().Split(',').Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();
            command.ConditionModifier = new ConditionModifier(match.Value, conditions, match.Groups["not"].Success);
        }),

        new("index", @"<index\.(?<index>\d+)>",
            (command, match) => command.IndexModifier = new IndexModifier(match.Value, int.Parse(match.Groups["index"].Value))),

        new("list", @"<list\.(?<list>\d+)>",
            (command, match) => command.ListIndexModifier = new ListIndexModifier(match.Value, int.Parse(match.Groups["list"].Value))),

        new("distance", @"<distance\.(?<distance>\d+(?:\.\d+)?)>",
            (command, match) => command.DistanceModifier = new DistanceModifier(match.Value, float.Parse(match.Groups["distance"].Value, CultureInfo.InvariantCulture))),

        new("errorif", @"<errorif\.(?<errorif>[^>]+)>",
            (command, match) => command.ErrorIfModifier = new ErrorIfModifier(match.Value, Enum.Parse<ErrorCondition>(match.Groups["errorif"].Value, true))),
    ];

    private static int Milliseconds(string seconds) => (int)(float.Parse(seconds, CultureInfo.InvariantCulture) * 1000);

    /// <summary>
    /// Builds a regex pattern from the given modifier definitions.
    /// </summary>
    /// <param name="modifiers">The modifier definitions to include.</param>
    /// <returns>The combined regex pattern.</returns>
    public static string BuildRegexPattern(IEnumerable<ModifierDefinition> modifiers)
        => string.Join("|", modifiers.Select(m => $"(?<mod_{m.Name}>{m.RegexPattern})"));

    /// <summary>
    /// Finds the modifier definition that matches the given regex match.
    /// </summary>
    /// <param name="match">The regex match.</param>
    /// <param name="modifiers">The modifier definitions to search.</param>
    /// <returns>The matching modifier definition, or null if none found.</returns>
    public static ModifierDefinition? FindMatchingModifier(Match match, IEnumerable<ModifierDefinition> modifiers)
        => modifiers.FirstOrDefault(m => match.Groups[$"mod_{m.Name}"].Success);
}
