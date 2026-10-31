namespace SomethingNeedDoing.Core;
/// <summary>
/// Exception thrown when a macro command times out.
/// </summary>
public class MacroTimeoutException : MacroException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="MacroTimeoutException"/> class.
    /// </summary>
    public MacroTimeoutException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MacroTimeoutException"/> class.
    /// </summary>
    public MacroTimeoutException(string message, Exception innerException) : base(message, innerException) { }
}
