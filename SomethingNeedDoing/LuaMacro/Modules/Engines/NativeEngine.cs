using SomethingNeedDoing.Core;
using SomethingNeedDoing.Core.Events;
using SomethingNeedDoing.NativeMacro;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.LuaMacro.Modules.Engines;

/// <summary>
/// Engine for executing native commands.
/// </summary>
public class NativeEngine : IEngine
{
    /// <summary>
    /// Event raised when a macro execution is requested.
    /// </summary>
    public event EventHandler<MacroExecutionRequestedEventArgs>? MacroExecutionRequested;

    /// <summary>
    /// Event raised when loop control is requested.
    /// </summary>
    public event EventHandler<LoopControlEventArgs>? LoopControlRequested;

    public string Name => "Native";

    public async Task ExecuteAsync(string content, CancellationToken cancellationToken = default)
    {
        try
        {
            var tempMacro = new TemporaryMacro(content) { Type = MacroType.Native };
            MacroExecutionRequested?.Invoke(this, new MacroExecutionRequestedEventArgs(tempMacro));
        }
        catch (Exception ex)
        {
            FrameworkLogger.Error($"Error executing native command '{content}': {ex}");
            throw;
        }
    }

    public bool CanExecute(string content) => !string.IsNullOrWhiteSpace(content) && content.StartsWith('/');
}
