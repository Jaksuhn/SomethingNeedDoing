using SomethingNeedDoing.Core.Interfaces;
using System.Text;

namespace SomethingNeedDoing.NativeMacro;

/// <summary>
/// Wraps a native macro in the steps needed to repeat a craft, when <see cref="MacroMetadata.CraftingLoop"/> is enabled.
/// </summary>
public static class CraftLoopBuilder
{
    /// <summary>
    /// Returns the text to execute for a macro: its content, wrapped in craft loop steps if enabled.
    /// </summary>
    public static string Build(IMacro macro)
    {
        var contents = macro.ContentSansMetadata();
        if (!macro.Metadata.CraftingLoop)
            return contents;

        var craftCount = macro.Metadata.CraftLoopCount;
        if (C.UseCraftLoopTemplate)
        {
            var template = C.CraftLoopTemplate;

            if (craftCount == 0)
                return contents;

            if (craftCount == -1)
                craftCount = 999_999;

            return !template.Contains("{{macro}}")
                ? throw new MacroSyntaxError("CraftLoop template does not contain the {{macro}} placeholder")
                : template.Replace("{{macro}}", contents).Replace("{{count}}", craftCount.ToString());
        }

        var inRecipeNote = Svc.GameGui.GetAddonByName("RecipeNote") != IntPtr.Zero;
        var maxwaitMod = C.CraftLoopMaxWait > 0 ? $" <maxwait.{C.CraftLoopMaxWait}>" : string.Empty;
        var echoMod = C.CraftLoopEcho ? " <echo>" : string.Empty;

        var craftGateStep = inRecipeNote ? $"/craft {craftCount}{echoMod}" : $"/gate {craftCount - 1}{echoMod}";
        var clickSteps = string.Join("\n",
        [
            $@"/waitaddon ""RecipeNote""{maxwaitMod}",
            $@"/click ""RecipeNote Synthesize""",
            $@"/waitaddon ""Synthesis""{maxwaitMod}",
        ]);
        var loopStep = $"/loop{echoMod}";

        string[] steps = (inRecipeNote, craftCount) switch
        {
            (true, -1) => [clickSteps, contents, loopStep],
            (true, 0) => [contents],
            (true, 1) => [clickSteps, contents],
            (true, _) => [craftGateStep, clickSteps, contents, loopStep],
            (false, -1) => [contents, clickSteps, loopStep],
            (false, 0 or 1) => [contents],
            (false, _) => [contents, craftGateStep, clickSteps, loopStep],
        };

        var sb = new StringBuilder();
        foreach (var step in steps)
            sb.AppendLine(step);
        return sb.ToString();
    }
}
