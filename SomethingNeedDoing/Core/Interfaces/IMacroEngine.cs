using SomethingNeedDoing.Core.Events;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Core.Interfaces;

/// <summary>
/// Interface for the macro engine that executes macros.
/// </summary>
public interface IMacroEngine : IDisposable
{
    /// <summary>
    /// Starts executing a macro.
    /// </summary>
    /// <param name="macro">The macro to execute.</param>
    /// <param name="instance">Scheduler-owned instance that has start/stop control</param>
    /// <param name="token">A token to cancel execution.</param>
    /// <param name="triggerEventArgs">Optional trigger event arguments.</param>
    /// <param name="loopCount">Optional number of times to loop the macro. Only supported by native macros.</param>
    Task StartMacro(IMacro macro, IMacroInstance instance, CancellationToken token, TriggerEventArgs? triggerEventArgs = null, int loopCount = 0);

    /// <summary>
    /// Event raised when a macro execution is requested (for breaking circular dependencies).
    /// </summary>
    event EventHandler<MacroExecutionRequestedEventArgs> MacroExecutionRequested;
}
