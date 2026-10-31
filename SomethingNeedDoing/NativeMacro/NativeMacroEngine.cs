using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.Core.Interfaces;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.NativeMacro;
/// <summary>
/// Executes native-style macros with command syntax similar to game macros.
/// </summary>
[RegisterSingleton, AutoConstruct]
public partial class NativeMacroEngine : IMacroEngine
{
    private readonly MacroParser _parser;

    /// <inheritdoc/>
    public event EventHandler<MacroExecutionRequestedEventArgs>? MacroExecutionRequested;

    /// <summary>
    /// Event raised at each loop point, so the scheduler can apply pause/stop at next loop.
    /// </summary>
    public event EventHandler<LoopControlEventArgs>? LoopControlRequested;

    /// <inheritdoc/>
    public async Task StartMacro(IMacro macro, IMacroInstance instance, CancellationToken token, TriggerEventArgs? triggerArgs = null, int loopCount = 0)
    {
        try
        {
            await ExecuteMacro(macro, _parser.Parse(CraftLoopBuilder.Build(macro)), loopCount == 0 ? 1 : loopCount, instance, token);
        }
        catch
        {
            Svc.Chat.PrintErrorMsg("Macro execution failed");
            throw;
        }
    }

    private async Task ExecuteMacro(IMacro macro, List<IMacroCommand> commands, int loopCount, IMacroInstance instance, CancellationToken token)
    {
        try
        {
            for (var i = 0; i < loopCount; i++)
            {
                if (i > 0)
                {
                    LoopControlRequested?.Invoke(this, new LoopControlEventArgs(macro.Id, LoopControlType.Pause));
                    LoopControlRequested?.Invoke(this, new LoopControlEventArgs(macro.Id, LoopControlType.Stop));
                }

                var currentStep = 0;
                while (currentStep < commands.Count)
                {
                    token.ThrowIfCancellationRequested();
                    await MacroPause.WaitAsync(instance.PauseEvent, token);

                    var command = commands[currentStep];
                    var context = new MacroContext(macro);
                    context.MacroExecutionRequested += (sender, e) => MacroExecutionRequested?.Invoke(this, e);
                    context.LoopControlRequested += (sender, e) => LoopControlRequested?.Invoke(this, e);

                    if (command.RequiresFrameworkThread)
                        await Svc.Framework.RunOnTick(() => command.Execute(context, token), cancellationToken: token);
                    else
                        await command.Execute(context, token);

                    currentStep++;
                    if (context.CurrentStep == -1)
                    {
                        currentStep = 0; // restart
                        context.NextStep(); // reset loop flag
                    }
                }
            }
        }
        catch (OperationCanceledException) { }
        catch (MacroGateCompleteException) { }
        catch
        {
            Svc.Chat.PrintErrorMsg("Error executing macro command");
            throw;
        }
    }

    /// <inheritdoc/>
    public void Dispose() { }
}
