namespace SomethingNeedDoing.Core.Exceptions;
/// <summary>
/// Exception thrown when a macro gate command completes.
/// </summary>
public class MacroGateCompleteException : MacroException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MacroGateCompleteException"/> class.
    /// </summary>
    public MacroGateCompleteException() : base("Gate reached") { }
}
