using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.NativeMacro;
/// <summary>
/// Executes native-style macros with command syntax similar to game macros.
/// </summary>
public class NativeMacroEngine(MacroParser parser) : IMacroEngine
{
    /// <inheritdoc/>
    public event EventHandler<MacroErrorEventArgs>? MacroError;

    /// <inheritdoc/>
    public event EventHandler<MacroControlEventArgs>? MacroControlRequested;

    /// <inheritdoc/>
    public event EventHandler<MacroStepCompletedEventArgs>? MacroStepCompleted;

    /// <inheritdoc/>
    public event EventHandler<MacroExecutionRequestedEventArgs>? MacroExecutionRequested;

    /// <summary>
    /// Event raised when loop control is requested.
    /// </summary>
    public event EventHandler<LoopControlEventArgs>? LoopControlRequested;

    /// <summary>
    /// Represents the current execution state of a macro.
    /// </summary>
    private class MacroExecutionState(IMacro macro)
    {
        public IMacro Macro { get; } = macro;
        public List<IMacroCommand> Commands { get; set; } = [];
        public int LoopCount { get; set; }
        public int CurrentLoop { get; set; }
    }

    /// <inheritdoc/>
    public async Task StartMacro(IMacro macro, IMacroInstance instance, CancellationToken token, TriggerEventArgs? triggerArgs = null, int loopCount = 0)
    {
        var state = new MacroExecutionState(macro)
        {
            Commands = ModifyMacroForCraftLoop(macro),
            CurrentLoop = 0,
            LoopCount = loopCount == 0 ? 1 : loopCount
        };

        try
        {
            await ExecuteMacro(state, instance, token);
        }
        catch (Exception ex)
        {
            OnMacroError(macro.Id, "Macro execution failed", ex);
            throw;
        }
    }

    private async Task ExecuteMacro(MacroExecutionState state, IMacroInstance instance, CancellationToken token)
    {
        try
        {
            for (var i = state.CurrentLoop; i < state.LoopCount; i++)
            {
                var totalSteps = state.Commands.Count;
                var currentStep = 0;

                while (currentStep < state.Commands.Count)
                {
                    token.ThrowIfCancellationRequested();

                    // Wait if paused
                    instance.PauseEvent.Wait(token);

                    // Check for loop pause/stop
                    if (instance.PauseAtLoop)
                    {
                        instance.PauseAtLoop = false;
                        instance.PauseEvent.Reset();
                    }

                    if (instance.StopAtLoop)
                    {
                        instance.StopAtLoop = false;
                        instance.CancellationSource.Cancel();
                        return;
                    }

                    var command = state.Commands[currentStep];
                    var context = new MacroContext(state.Macro);

                    context.MacroExecutionRequested += (sender, e) =>
                        MacroExecutionRequested?.Invoke(this, e);

                    context.LoopControlRequested += (sender, e) =>
                        LoopControlRequested?.Invoke(this, e);

                    if (command.RequiresFrameworkThread)
                        await Svc.Framework.RunOnTick(() => command.Execute(context, token), cancellationToken: token);
                    else
                        await command.Execute(context, token);

                    currentStep++;
                    MacroStepCompleted?.Invoke(this, new MacroStepCompletedEventArgs(state.Macro.Id, currentStep, totalSteps));

                    if (context.CurrentStep == -1)
                    {
                        currentStep = 0; // restart
                        context.NextStep(); // reset loop flag
                    }
                }

                state.CurrentLoop++;
            }
        }
        catch (OperationCanceledException) { }
        catch (MacroGateCompleteException) { }
        catch (Exception ex)
        {
            OnMacroError(state.Macro.Id, "Error executing macro command", ex);
            throw;
        }
    }

    protected virtual void OnMacroError(string macroId, string message, Exception? ex = null)
    {
        Svc.Chat.PrintErrorMsg(message);
        MacroError?.Invoke(this, new MacroErrorEventArgs(macroId, message, ex));
    }

    /// <inheritdoc/>
    public IMacro? GetTemporaryMacro(string macroId) => null; // Native engine doesn't create temporary macros

    private List<IMacroCommand> ModifyMacroForCraftLoop(IMacro macro)
    {
        if (!macro.Metadata.CraftingLoop)
            return parser.Parse(macro.ContentSansMetadata());

        var craftCount = macro.Metadata.CraftLoopCount;
        var contents = macro.ContentSansMetadata();
        var inRecipeNote = Svc.GameGui.GetAddonByName("RecipeNote") != IntPtr.Zero;
        if (C.UseCraftLoopTemplate)
        {
            var template = C.CraftLoopTemplate;

            if (craftCount == 0)
                return parser.Parse(contents);

            if (craftCount == -1)
                craftCount = 999_999;

            return !template.Contains("{{macro}}")
                ? throw new MacroSyntaxError("CraftLoop template does not contain the {{macro}} placeholder")
                : parser.Parse(template.Replace("{{macro}}", contents).Replace("{{count}}", craftCount.ToString()));
        }

        var maxwait = C.CraftLoopMaxWait;
        var maxwaitMod = maxwait > 0 ? $" <maxwait.{maxwait}>" : string.Empty;

        var echo = C.CraftLoopEcho;
        var echoMod = echo ? $" <echo>" : string.Empty;

        var craftGateStep = inRecipeNote ? $"/craft {craftCount}{echoMod}" : $"/gate {craftCount - 1}{echoMod}";
        var clickSteps = string.Join("\n",
        [
            $@"/waitaddon ""RecipeNote""{maxwaitMod}",
            $@"/click ""RecipeNote Synthesize""",
            $@"/waitaddon ""Synthesis""{maxwaitMod}",
        ]);

        var loopStep = $"/loop{echoMod}";

        var sb = new StringBuilder();

        if (inRecipeNote)
        {
            if (craftCount == -1)
            {
                sb.AppendLine(clickSteps);
                sb.AppendLine(contents);
                sb.AppendLine(loopStep);
            }
            else if (craftCount == 0)
            {
                sb.AppendLine(contents);
            }
            else if (craftCount == 1)
            {
                sb.AppendLine(clickSteps);
                sb.AppendLine(contents);
            }
            else
            {
                sb.AppendLine(craftGateStep);
                sb.AppendLine(clickSteps);
                sb.AppendLine(contents);
                sb.AppendLine(loopStep);
            }
        }
        else
        {
            if (craftCount == -1)
            {
                sb.AppendLine(contents);
                sb.AppendLine(clickSteps);
                sb.AppendLine(loopStep);
            }
            else if (craftCount is 0 or 1)
            {
                sb.AppendLine(contents);
            }
            else
            {
                sb.AppendLine(contents);
                sb.AppendLine(craftGateStep);
                sb.AppendLine(clickSteps);
                sb.AppendLine(loopStep);
            }
        }

        return parser.Parse(sb.ToString());
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
