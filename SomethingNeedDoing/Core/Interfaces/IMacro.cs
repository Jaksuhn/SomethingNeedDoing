using SomethingNeedDoing.Core.Events;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.Framework.Interfaces;
/// <summary>
/// Represents a macro that can be executed by the macro engine.
/// </summary>
public interface IMacro
{
    /// <summary>
    /// Gets the unique identifier for this macro.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name of the macro.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the type of macro (Native or Lua).
    /// </summary>
    MacroType Type { get; }

    /// <summary>
    /// Gets the raw content/script of the macro.
    /// </summary>
    string Content { get; }

    /// <summary>
    /// Gets or sets the current state of the macro.
    /// </summary>
    MacroState State { get; set; }

    /// <summary>
    /// Event raised when the macro's state changes.
    /// </summary>
    event EventHandler<MacroStateChangedEventArgs>? StateChanged;

    /// <summary>
    /// Gets the metadata for the macro.
    /// </summary>
    MacroMetadata Metadata { get; }
    
    /// <summary>
    /// Deletes this macro from storage.
    /// </summary>
    void Delete();
}

public interface IMacroInstance : IDisposable
{
    public IMacro Macro { get; }
    public CancellationTokenSource CancellationSource { get; }
    public ManualResetEventSlim PauseEvent { get; }
    public Task? ExecutionTask { get; set; }
    public bool PauseAtLoop { get; set; }
    public bool StopAtLoop { get; set; }

    public new void Dispose()
    {
        CancellationSource.Dispose();
        PauseEvent.Dispose();
    }
}

/// <summary>
/// Base class for all macro implementations, providing common state management logic.
/// </summary>
public abstract class MacroBase : IMacro
{
    /// <inheritdoc/>
    public abstract string Id { get; }

    /// <inheritdoc/>
    public abstract string Name { get; set; }

    /// <inheritdoc/>
    public abstract MacroType Type { get; set; }

    /// <inheritdoc/>
    public abstract string Content { get; set; }

    /// <inheritdoc/>
    public MacroState State
    {
        get;
        set
        {
            if (field != value)
            {
                //PluginLog.Debug($"Macro state changed for {Id}: {field} -> {value}"); // why doesn't this work
                Svc.Log.Debug(string.Format("Macro state changed for {0}: {1} -> {2}", Id, field, value));
                StateChanged?.Invoke(this, new MacroStateChangedEventArgs(Id, value, field));
            }
        }
    } = MacroState.Ready;

    /// <inheritdoc/>
    public event EventHandler<MacroStateChangedEventArgs>? StateChanged;

    /// <inheritdoc/>
    public abstract MacroMetadata Metadata { get; set; }

    /// <inheritdoc/>
    public abstract void Delete();
}
