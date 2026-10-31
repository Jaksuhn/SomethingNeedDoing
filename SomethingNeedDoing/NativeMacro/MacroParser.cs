using Dalamud.Game.ClientState.Keys;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.NativeMacro.Commands;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SomethingNeedDoing.NativeMacro;

/// <summary>
/// A two-step parser that first extracts modifiers and then parses the command.
/// </summary>
[RegisterSingleton]
public partial class MacroParser
{
    private static readonly Regex SndModifierRegex = new(
        ModifierDefinitions.BuildRegexPattern(ModifierDefinitions.SndModifiers),
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

    [GeneratedRegex(@"^/(\S+)(?:\s+(.*))?$")]
    private static partial Regex CommandRegex();

    [GeneratedRegex(@"^(?:""(?<name>[^""]+)""|(?<name>[^<]+?))(?<rest>(?:\s*<[^>]+>)*)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex ActionRegex();

    [GeneratedRegex(@"<party\.([1-8])>", RegexOptions.IgnoreCase)]
    private static partial Regex PartyPlaceholderRegex();

    [GeneratedRegex(@"^(?<addon>.*?)\s+(?<update>.*?)\s+(?<values>.*?)\s*$", RegexOptions.IgnoreCase)]
    private static partial Regex CallbackRegex();

    [GeneratedRegex(@"^(?<count>\d+)?\s*$")]
    private static partial Regex CountRegex();

    public List<IMacroCommand> Parse(string text) => [.. text.Split('\n')
        .Where(line => !string.IsNullOrWhiteSpace(line))
        .Select(ParseLine)];

    /// <summary>
    /// Parses a command line and returns the appropriate command.
    /// </summary>
    /// <param name="text">The command line to parse.</param>
    /// <returns>The parsed command.</returns>
    /// <exception cref="MacroSyntaxError">Thrown when the command line cannot be parsed.</exception>
    public IMacroCommand ParseLine(string text)
    {
        // only SND modifiers are extracted, since game-supported modifiers (e.g. <1>) are needed by native commands
        var (textWithoutModifiers, modifiers) = ExtractSndModifiers(text);
        FrameworkLogger.Verbose($"Extracted modifiers: {string.Join(", ", modifiers.Select(m => m.Match.Value))}. Leftover text: [{textWithoutModifiers}]");

        var match = CommandRegex().Match(textWithoutModifiers);
        if (!match.Success)
            throw new MacroSyntaxError(text);

        var command = CreateCommand(match.Groups[1].Value.ToLowerInvariant(), match.Groups[2].Success ? match.Groups[2].Value : string.Empty, textWithoutModifiers);
        foreach (var (definition, modifierMatch) in modifiers)
            definition.Apply(command, modifierMatch);
        return command;
    }

    /// <summary>
    /// Extracts SND-specific modifiers from the command text.
    /// </summary>
    /// <param name="text">The command text.</param>
    /// <returns>The text without SND modifiers and the modifiers found, last first.</returns>
    private static (string TextWithoutModifiers, List<(ModifierDefinition Definition, Match Match)> Modifiers) ExtractSndModifiers(string text)
    {
        var modifiers = new List<(ModifierDefinition, Match)>();
        var textWithoutModifiers = text;

        foreach (var match in SndModifierRegex.Matches(text).Reverse())
        {
            if (ModifierDefinitions.FindMatchingModifier(match, ModifierDefinitions.SndModifiers) is not { } definition)
            {
                FrameworkLogger.Warning($"Unknown SND modifier match: {match.Value}");
                continue;
            }

            textWithoutModifiers = textWithoutModifiers.Remove(match.Index, match.Length);
            modifiers.Add((definition, match));
        }

        return (textWithoutModifiers.Trim(), modifiers);
    }

    /// <summary>
    /// Creates a command from its name and parameters.
    /// </summary>
    /// <exception cref="MacroSyntaxError">Thrown when the command cannot be created.</exception>
    private static IMacroCommand CreateCommand(string name, string parameters, string fullText) => name switch
    {
        "target" => new TargetCommand(parameters, Unquote(parameters)),
        "action" or "ac" => ParseActionCommand(parameters),
        "callback" => ParseCallbackCommand(parameters),
        "click" => ParseClickCommand(parameters),
        "craft" or "gate" => new GateCommand(parameters, ParseCount(parameters, 1)),
        "wait" => ParseWaitCommand(parameters),
        "loop" => new LoopCommand(parameters, ParseCount(parameters, -1)),
        "item" => new ItemCommand(parameters, Unquote(parameters)),
        "keyitem" => new KeyItemCommand(parameters, Unquote(parameters)),
        "recipe" => new RecipeCommand(parameters, Unquote(parameters)),
        "require" => new RequireCommand(parameters, Unquote(parameters)),
        "requirerepair" => string.IsNullOrWhiteSpace(parameters) ? new RequireRepairCommand(parameters, 0)
            : int.TryParse(parameters.Trim(), out var durability) ? new RequireRepairCommand(parameters, durability)
            : throw new MacroSyntaxError($"Invalid durability threshold: {parameters}"),
        "requirespiritbond" => string.IsNullOrWhiteSpace(parameters) ? new RequireSpiritbondCommand(parameters, 100f)
            : float.TryParse(parameters.Trim(), out var within) ? new RequireSpiritbondCommand(parameters, within)
            : throw new MacroSyntaxError($"Invalid within percentage: {parameters}"),
        "runmacro" => new RunMacroCommand(parameters, Unquote(parameters)),
        "send" => ParseKeyCommand(parameters, (keys, mods) => new SendCommand(parameters, keys, mods)),
        "hold" => ParseKeyCommand(parameters, (keys, mods) => new HoldCommand(parameters, keys, mods)),
        "release" => ParseKeyCommand(parameters, (keys, mods) => new ReleaseCommand(parameters, keys, mods)),
        "interact" => new InteractCommand(parameters),
        "equipitem" => new EquipItemCommand(parameters, uint.Parse(parameters)),
        "targetenemy" => new TargetEnemyCommand(parameters),
        "waitaddon" => new WaitAddonCommand(parameters, Unquote(parameters)),
        _ => new NativeCommand(fullText),
    };

    #region Command Parsing
    private static string Unquote(string parameters) => parameters.Trim().Trim('"');

    private static int ParseCount(string parameters, int defaultCount)
    {
        var match = CountRegex().Match(parameters);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);
        return match.Groups["count"].Success ? int.Parse(match.Groups["count"].Value) : defaultCount;
    }

    private static ActionCommand ParseActionCommand(string parameters)
    {
        // separate placeholders from the action name
        var match = ActionRegex().Match(parameters);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var nameValue = match.Groups["name"].Value.Trim();
        if (string.IsNullOrEmpty(nameValue))
            throw new MacroSyntaxError(parameters);

        // convert <party.N> to <N>
        var rest = PartyPlaceholderRegex().Replace(match.Groups["rest"].Value.Trim(), "<$1>");
        var fullCommandText = string.IsNullOrEmpty(rest) ? $"/ac \"{nameValue}\"" : $"/ac \"{nameValue}\" {rest}";
        return new ActionCommand(fullCommandText, nameValue);
    }

    private static CallbackCommand ParseCallbackCommand(string parameters)
    {
        var match = CallbackRegex().Match(parameters);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var addonValue = Unquote(match.Groups["addon"].Value);
        var updateValue = bool.Parse(Unquote(match.Groups["update"].Value));
        var values = ParseCallbackValues(match.Groups["values"].Value);
        return new CallbackCommand(parameters, addonValue, updateValue, values);

        static object[] ParseCallbackValues(string valuesText)
        {
            var values = new List<object>();
            var current = "";
            var inQuotes = false;

            foreach (var token in valuesText.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!inQuotes)
                {
                    if (token.StartsWith('"'))
                    {
                        inQuotes = true;
                        current = token[1..];
                    }
                    else
                        values.Add(ParseValue(token));
                }
                else
                {
                    if (token.EndsWith('"'))
                    {
                        inQuotes = false;
                        current += " " + token[..^1];
                        values.Add(current);
                        current = "";
                    }
                    else
                        current += " " + token;
                }
            }

            if (!string.IsNullOrEmpty(current))
                throw new MacroSyntaxError(valuesText, "Unclosed quotes in values");

            return [.. values];
        }

        static object ParseValue(string value) => value switch
        {
            _ when bool.TryParse(value, out var boolValue) => boolValue,
            _ when int.TryParse(value, out var intValue) => intValue,
            _ when uint.TryParse(value.TrimEnd('U', 'u'), out var uintValue) => uintValue,
            _ => value,
        };
    }

    private static ClickCommand ParseClickCommand(string parameters)
    {
        var parts = parameters.Split([' '], 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new MacroSyntaxError($"Invalid click command: {parameters}");

        var methodParams = parts.Length > 2 ? parts[2].Split(' ') : [];
        return new ClickCommand(parameters, parts[0], parts[1], methodParams);
    }

    private static WaitCommand ParseWaitCommand(string parameters)
    {
        if (double.TryParse(parameters, CultureInfo.InvariantCulture, out var @double))
            return new WaitCommand(parameters) { WaitDuration = (int)(@double * 1000) };
        if (int.TryParse(parameters, CultureInfo.InvariantCulture, out var @int))
            return new WaitCommand(parameters) { WaitDuration = @int * 1000 };
        throw new MacroSyntaxError($"Invalid wait command: {parameters}");
    }

    private static IMacroCommand ParseKeyCommand(string parameters, Func<VirtualKey[], VirtualKey[], IMacroCommand> create)
    {
        static VirtualKey ParseVirtualKey(string key) => Enum.TryParse<VirtualKey>(key, true, out var vk) ? vk : throw new MacroSyntaxError($"Invalid virtual key: {key}");

        var keyStrings = parameters.Split('+');
        var modifiers = keyStrings.Length > 1 ? keyStrings[..^1].Select(ParseVirtualKey).ToArray() : [];
        return create([ParseVirtualKey(keyStrings[^1])], modifiers);
    }
    #endregion
}
