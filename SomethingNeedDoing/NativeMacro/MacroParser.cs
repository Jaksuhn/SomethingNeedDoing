using System.Globalization;
using System.Text.RegularExpressions;
using Dalamud.Game.ClientState.Keys;
using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.NativeMacro.Commands;
using SomethingNeedDoing.NativeMacro.Modifiers;

namespace SomethingNeedDoing.NativeMacro;

/// <summary>
/// A two-step parser that first extracts modifiers and then parses the command.
/// </summary>
public class MacroParser
{
    private static readonly Regex SndModifierRegex = new(
        ModifierDefinitions.BuildRegexPattern(ModifierDefinitions.SndModifiers),
        RegexOptions.Compiled | RegexOptions.IgnoreCase
    );

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
        var (textWithoutModifiers, modifiers) = ExtractSndModifiers(text); // this is because the index modifier is a native modifier that some non-snd commands use
        FrameworkLogger.Verbose($"Extracted modifiers: {string.Join(", ", modifiers)}. Leftover text: [{textWithoutModifiers}]");
        var commandInfo = ParseCommandStructure(textWithoutModifiers) ?? throw new MacroSyntaxError(text);
        var command = CreateCommand(commandInfo with { RemainingText = textWithoutModifiers });
        ApplyModifiers(modifiers, command);
        return command;
    }

    /// <summary>
    /// Extracts only SND-specific modifiers from the command text, preserving game-supported modifiers.
    /// </summary>
    /// <param name="text">The command text.</param>
    /// <returns>A tuple containing the text without SND modifiers and the list of SND modifiers.</returns>
    private (string TextWithoutModifiers, List<ModifierInfo> Modifiers) ExtractSndModifiers(string text)
    {
        var modifiers = new List<ModifierInfo>();
        var textWithoutModifiers = text;

        var matches = SndModifierRegex.Matches(text).Cast<Match>().Reverse();
        foreach (var match in matches)
        {
            var modifierDefinition = ModifierDefinitions.FindMatchingModifier(match, ModifierDefinitions.SndModifiers);
            if (modifierDefinition == null)
            {
                FrameworkLogger.Warning($"Unknown SND modifier match: {match.Value}");
                continue;
            }

            var modifierText = match.Value;
            var modifierParam = modifierDefinition.ParameterExtractor(match);

            textWithoutModifiers = textWithoutModifiers.Remove(match.Index, match.Length);
            modifiers.Add(new ModifierInfo(modifierDefinition.Name, modifierParam, modifierText));
        }

        return (textWithoutModifiers.Trim(), modifiers);
    }

    /// <summary>
    /// Parses the command structure from the text.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>The command parse info, or null if the text cannot be parsed.</returns>
    private CommandParseInfo? ParseCommandStructure(string text)
    {
        var match = Regex.Match(text, @"^/(\S+)(?:\s+(.*))?$");
        if (!match.Success)
            return null;

        var commandName = match.Groups[1].Value.ToLowerInvariant();
        var parameters = match.Groups[2].Success ? match.Groups[2].Value : string.Empty;

        return new CommandParseInfo(commandName, parameters, text);
    }

    /// <summary>
    /// Creates a command based on the command parse info.
    /// </summary>
    /// <param name="info">The command parse info.</param>
    /// <returns>The created command.</returns>
    /// <exception cref="MacroSyntaxError">Thrown when the command cannot be created.</exception>
    private IMacroCommand CreateCommand(CommandParseInfo info)
    {
        return info.CommandName switch
        {
            "target" => ParseTargetCommand(info.Parameters),
            "action" or "ac" => ParseActionCommand(info.Parameters),
            "callback" => ParseCallbackCommand(info.Parameters),
            "click" => ParseClickCommand(info.Parameters),
            "craft" => ParseGateCommand(info.Parameters),
            "wait" => ParseWaitCommand(info.Parameters),
            "loop" => ParseLoopCommand(info.Parameters),
            "gate" => ParseGateCommand(info.Parameters),
            "item" => ParseItemCommand(info.Parameters),
            "keyitem" => ParseKeyItemCommand(info.Parameters),
            "recipe" => ParseRecipeCommand(info.Parameters),
            "require" => ParseRequireCommand(info.Parameters),
            "requirerepair" => ParseRequireRepairCommand(info.Parameters),
            "requirespiritbond" => ParseRequireSpiritbondCommand(info.Parameters),
            "runmacro" => ParseRunMacroCommand(info.Parameters),
            "send" => ParseKeyCommand<SendCommand>(info.Parameters),
            "hold" => ParseKeyCommand<HoldCommand>(info.Parameters),
            "release" => ParseKeyCommand<ReleaseCommand>(info.Parameters),
            "interact" => ParseInteractCommand(info.Parameters),
            "equipitem" => ParseEquipItemCommand(info.Parameters),
            "targetenemy" => ParseTargetEnemyCommand(info.Parameters),
            "waitaddon" => ParseWaitAddonCommand(info.Parameters),
            _ => ParseNativeCommand(info),
        };
    }

    /// <summary>
    /// Applies modifiers to the command.
    /// </summary>
    /// <param name="modifiers">The modifiers to apply.</param>
    /// <param name="command">The command to apply modifiers to.</param>
    private void ApplyModifiers(List<ModifierInfo> modifiers, IMacroCommand command)
    {
        var commandInfo = new CommandParseInfo(command.CommandText, string.Empty, string.Empty);

        foreach (var modifier in modifiers)
        {
            var macroModifier = CreateModifier(modifier, commandInfo);

            switch (macroModifier)
            {
                case WaitModifier waitMod:
                    command.WaitModifier = waitMod;
                    if (command is MacroCommandBase baseCommand)
                        baseCommand.WaitDuration = waitMod.WaitDuration;
                    break;
                case EchoModifier echoMod:
                    command.EchoModifier = echoMod;
                    break;
                case UnsafeModifier unsafeMod:
                    command.UnsafeModifier = unsafeMod;
                    break;
                case ConditionModifier conditionMod:
                    command.ConditionModifier = conditionMod;
                    break;
                case MaxWaitModifier maxWaitMod:
                    command.MaxWaitModifier = maxWaitMod;
                    break;
                case IndexModifier indexMod:
                    command.IndexModifier = indexMod;
                    break;
                case ListIndexModifier listMod:
                    command.ListIndexModifier = listMod;
                    break;
                case PartyIndexModifier partyMod:
                    command.PartyIndexModifier = partyMod;
                    break;
                case DistanceModifier distanceMod:
                    command.DistanceModifier = distanceMod;
                    break;
                case HqModifier qualityMod:
                    command.ItemQualityModifier = qualityMod;
                    break;
                case ErrorIfModifier errorIfMod:
                    command.ErrorIfModifier = errorIfMod;
                    break;
            }
        }
    }

    #region Command Parsing
    private NativeCommand ParseNativeCommand(CommandParseInfo info) => new(info.RemainingText);

    private TargetCommand ParseTargetCommand(string parameters)
    {
        var match = Regex.Match(parameters, @"^(?<name>.*?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var nameValue = match.ExtractAndUnquote("name");
        return new TargetCommand(parameters, nameValue);
    }

    private ActionCommand ParseActionCommand(string parameters)
    {
        var match = Regex.Match(parameters, @"^(?<name>.*?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var nameValue = match.ExtractAndUnquote("name");
        var fullCommandText = $"/ac \"{nameValue}\"";
        return new ActionCommand(fullCommandText, nameValue);
    }

    private CallbackCommand ParseCallbackCommand(string parameters)
    {
        var match = Regex.Match(parameters, @"^(?<addon>.*?)\s+(?<update>.*?)\s+(?<values>.*?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var addonValue = match.ExtractAndUnquote("addon");
        var updateValue = bool.Parse(match.ExtractAndUnquote("update"));
        var valuesText = match.Groups["values"].Value;

        var values = ParseCallbackValues(valuesText);
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

    private ClickCommand ParseClickCommand(string parameters)
    {
        var parts = parameters.Split([' '], 3, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            throw new MacroSyntaxError($"Invalid click command: {parameters}");

        var addonName = parts[0];
        var method = parts[1];
        var methodParams = parts.Length > 2 ? parts[2].Split(' ') : [];

        return new ClickCommand(parameters, addonName, method, methodParams);
    }

    private WaitCommand ParseWaitCommand(string parameters)
    {
        if (double.TryParse(parameters, CultureInfo.InvariantCulture, out var @double))
            return new WaitCommand(parameters) { WaitDuration = (int)(@double * 1000) };
        if (int.TryParse(parameters, CultureInfo.InvariantCulture, out var @int))
            return new WaitCommand(parameters) { WaitDuration = @int * 1000 };
        throw new MacroSyntaxError($"Invalid wait command: {parameters}");
    }

    private LoopCommand ParseLoopCommand(string parameters)
    {
        var match = Regex.Match(parameters, @"^(?<count>\d+)?\s*$", RegexOptions.Compiled);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var count = match.Groups["count"].Success ? int.Parse(match.Groups["count"].Value) : -1;
        return new LoopCommand(parameters, count);
    }

    private GateCommand ParseGateCommand(string parameters)
    {
        var match = Regex.Match(parameters, @"^(?<count>\d+)?\s*$", RegexOptions.Compiled);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var count = match.Groups["count"].Success ? int.Parse(match.Groups["count"].Value) : 1;
        return new GateCommand(parameters, count);
    }

    private ItemCommand ParseItemCommand(string parameters)
    {
        var itemName = parameters.Trim('"');
        return new ItemCommand(parameters, itemName);
    }

    private KeyItemCommand ParseKeyItemCommand(string parameters)
    {
        var itemName = parameters.Trim('"');
        return new KeyItemCommand(parameters, itemName);
    }

    private RecipeCommand ParseRecipeCommand(string parameters)
    {
        var recipeName = parameters.Trim('"');
        return new RecipeCommand(parameters, recipeName);
    }

    private RequireCommand ParseRequireCommand(string parameters) => new(parameters, parameters.Trim('"'));

    private RequireRepairCommand ParseRequireRepairCommand(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return new RequireRepairCommand(parameters, 0);

        if (int.TryParse(parameters.Trim(), out var durabilityThreshold))
            return new RequireRepairCommand(parameters, durabilityThreshold);

        throw new MacroSyntaxError($"Invalid durability threshold: {parameters}");
    }

    private RequireSpiritbondCommand ParseRequireSpiritbondCommand(string parameters)
    {
        if (string.IsNullOrWhiteSpace(parameters))
            return new RequireSpiritbondCommand(parameters, 100f);

        if (float.TryParse(parameters.Trim(), out var within))
            return new RequireSpiritbondCommand(parameters, within);

        throw new MacroSyntaxError($"Invalid within percentage: {parameters}");
    }

    private RunMacroCommand ParseRunMacroCommand(string parameters)
    {
        var match = Regex.Match(parameters, @"^(?<name>.*?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var nameValue = match.ExtractAndUnquote("name");
        return new RunMacroCommand(parameters, nameValue);
    }

    private IMacroCommand ParseKeyCommand<T>(string parameters) where T : class, IMacroCommand
    {
        static VirtualKey ParseVirtualKey(string key) => Enum.TryParse<VirtualKey>(key, true, out var vk) ? vk : throw new MacroSyntaxError($"Invalid virtual key: {key}");

        var keyStrings = parameters.Split('+');
        var keys = new[] { ParseVirtualKey(keyStrings[^1]) };
        var modifiers = keyStrings.Length > 1 ? keyStrings[..^1].Select(ParseVirtualKey).ToArray() : [];

        return typeof(T) switch
        {
            Type type when type == typeof(SendCommand) => new SendCommand(parameters, keys, modifiers),
            Type type when type == typeof(HoldCommand) => new HoldCommand(parameters, keys, modifiers),
            Type type when type == typeof(ReleaseCommand) => new ReleaseCommand(parameters, keys, modifiers),
            _ => throw new MacroSyntaxError($"Invalid key command: {parameters}"),
        };
    }

    private InteractCommand ParseInteractCommand(string parameters)
    {
        return new InteractCommand(parameters);
    }

    private EquipItemCommand ParseEquipItemCommand(string parameters)
    {
        var itemId = uint.Parse(parameters);
        return new EquipItemCommand(parameters, itemId);
    }

    private TargetEnemyCommand ParseTargetEnemyCommand(string parameters)
    {
        return new TargetEnemyCommand(parameters);
    }

    private WaitAddonCommand ParseWaitAddonCommand(string parameters)
    {
        var match = Regex.Match(parameters, @"^(?<name>.*?)\s*$", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        if (!match.Success)
            throw new MacroSyntaxError(parameters);

        var nameValue = match.ExtractAndUnquote("name");
        return new WaitAddonCommand(parameters, nameValue);
    }
    #endregion

    #region Modifier Parsing
    private IMacroModifier CreateModifier(ModifierInfo info, CommandParseInfo commandInfo)
    {
        return info.Name.ToLowerInvariant() switch
        {
            "wait" => new WaitModifier(info.OriginalText, (int)(float.Parse(info.Parameter, CultureInfo.InvariantCulture) * 1000)),
            "echo" => new EchoModifier(info.OriginalText, true),
            "unsafe" => new UnsafeModifier(info.OriginalText, true),
            "condition" => new ConditionModifier(info.OriginalText, [info.Parameter], false),
            "maxwait" => new MaxWaitModifier(info.OriginalText, (int)(float.Parse(info.Parameter, CultureInfo.InvariantCulture) * 1000)),
            "index" => new IndexModifier(info.OriginalText, int.Parse(info.Parameter)),
            "list" or "listindex" => new ListIndexModifier(info.OriginalText, int.Parse(info.Parameter)),
            "partyindex" => new PartyIndexModifier(info.OriginalText, int.Parse(info.Parameter)),
            "distance" => new DistanceModifier(info.OriginalText, float.Parse(info.Parameter, CultureInfo.InvariantCulture)),
            "hq" => new HqModifier(info.OriginalText, true),
            "errorif" => new ErrorIfModifier(info.OriginalText, Enum.Parse<ErrorCondition>(info.Parameter, true)),
            _ => throw new MacroSyntaxError($"Unknown modifier: {info.Name}"),
        };
    }
    #endregion
}
