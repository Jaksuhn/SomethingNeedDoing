using System.Threading.Tasks;

namespace SomethingNeedDoing.NativeMacro.Commands;
/// <summary>
/// Base class for require commands that check conditions.
/// </summary>
public abstract class RequireCommandBase(string text) : MacroCommandBase(text)
{
    protected const int DefaultCheckInterval = 250;
    protected const int DefaultTimeout = 5000;

    /// <inheritdoc/>
    public override bool RequiresFrameworkThread => true;

    protected abstract Task<bool> CheckCondition(MacroContext context);
    protected abstract string GetErrorMessage();
}
