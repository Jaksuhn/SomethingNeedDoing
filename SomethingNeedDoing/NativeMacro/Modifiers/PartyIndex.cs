using SomethingNeedDoing.Core.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SomethingNeedDoing.NativeMacro.Modifiers;
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
    private static readonly Regex Regex = new(@"(?<modifier><(?:party\.)?(?<index>[1-8])>)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Gets the party member index (1-8).
    /// </summary>
    public int Index { get; } = index;

    /// <inheritdoc/>
    public static bool TryParse(ref string text, out IMacroModifier modifier)
    {
        var match = Regex.Match(text);
        if (!match.Success)
        {
            modifier = new PartyIndexModifier(string.Empty, 0);
            return false;
        }

        var group = match.Groups["modifier"];
        text = text.Remove(group.Index, group.Length);

        var index = int.Parse(match.Groups["index"].Value, CultureInfo.InvariantCulture);
        modifier = new PartyIndexModifier(group.Value, index);
        return true;
    }
}
