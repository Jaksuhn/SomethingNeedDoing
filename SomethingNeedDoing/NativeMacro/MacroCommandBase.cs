using SomethingNeedDoing.Core.Interfaces;
using SomethingNeedDoing.NativeMacro.Modifiers;
using System.Threading;
using System.Threading.Tasks;

namespace SomethingNeedDoing.NativeMacro;
/// <summary>
/// Base class for all macro commands providing common functionality.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="MacroCommandBase"/> class.
/// </remarks>
/// <param name="text">The original command text.</param>
/// <param name="waitDuration">The wait duration in milliseconds.</param>
public abstract class MacroCommandBase(string text) : IMacroCommand
{
    /// <summary>
    /// Gets the original text of the command.
    /// </summary>
    public string CommandText { get; } = text;

    /// <summary>
    /// Gets the wait duration in milliseconds.
    /// </summary>
    public int WaitDuration { get; set; }

    /// <summary>
    /// Gets whether this command must run on the framework thread.
    /// </summary>
    public abstract bool RequiresFrameworkThread { get; }

    /// <inheritdoc/>
    public WaitModifier? WaitModifier { get; set; }

    /// <inheritdoc/>
    public EchoModifier? EchoModifier { get; set; }

    /// <inheritdoc/>
    public UnsafeModifier? UnsafeModifier { get; set; }

    /// <inheritdoc/>
    public ConditionModifier? ConditionModifier { get; set; }

    /// <inheritdoc/>
    public MaxWaitModifier? MaxWaitModifier { get; set; }

    /// <inheritdoc/>
    public IndexModifier? IndexModifier { get; set; }

    /// <inheritdoc/>
    public ListIndexModifier? ListIndexModifier { get; set; }

    /// <inheritdoc/>
    public PartyIndexModifier? PartyIndexModifier { get; set; }

    /// <inheritdoc/>
    public DistanceModifier? DistanceModifier { get; set; }

    /// <inheritdoc/>
    public HqModifier? ItemQualityModifier { get; set; }

    /// <inheritdoc/>
    public ErrorIfModifier? ErrorIfModifier { get; set; }

    /// <inheritdoc/>
    public abstract Task Execute(MacroContext context, CancellationToken token);

    /// <summary>
    /// Performs the wait specified by the wait modifier.
    /// </summary>
    protected async Task PerformWait(CancellationToken token)
    {
        var duration = WaitModifier is { MaxWaitDuration: > 0 } random ? Random.Shared.Next(random.WaitDuration, random.MaxWaitDuration + 1) : WaitDuration;
        if (duration > 0)
            await Task.Delay(duration, token);
    }
}
