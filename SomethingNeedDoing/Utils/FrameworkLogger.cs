using System.IO;
using Serilog.Events;
using System.Runtime.CompilerServices;

namespace SomethingNeedDoing.Utils;

/// <summary>
/// A logger that automatically prefixes messages with the calling class name.
/// </summary>
public static class FrameworkLogger
{
    /// <summary>
    /// Logs an informational message with the calling class name as prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Info(string message, [CallerFilePath] string path = "")
    {
        Svc.Log.Information($"[{Path.GetFileNameWithoutExtension(path)}] {message}");
    }

    /// <summary>
    /// Logs a debug message with the calling member name as prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="path">The calling member name (automatically provided by the compiler).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Debug(string message, [CallerFilePath] string path = "")
    {
        Svc.Log.Debug($"[{Path.GetFileNameWithoutExtension(path)}] {message}");
    }

    /// <inheritdoc cref="Debug(string, string)"/>
    /// <remarks>The message is only formatted if debug logging is enabled.</remarks>
    public static void Debug(ref DebugLogHandler message, [CallerFilePath] string path = "")
    {
        if (message.Enabled)
            Debug(message.ToStringAndClear(), path);
    }

    /// <summary>
    /// Logs a verbose message with the calling member name as prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="path">The calling member name (automatically provided by the compiler).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Verbose(string message, [CallerFilePath] string path = "")
    {
        Svc.Log.Verbose($"[{Path.GetFileNameWithoutExtension(path)}] {message}");
    }

    /// <inheritdoc cref="Verbose(string, string)"/>
    /// <remarks>The message is only formatted if verbose logging is enabled.</remarks>
    public static void Verbose(ref VerboseLogHandler message, [CallerFilePath] string path = "")
    {
        if (message.Enabled)
            Verbose(message.ToStringAndClear(), path);
    }

    /// <summary>
    /// Logs a warning message with the calling member name as prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="path">The calling member name (automatically provided by the compiler).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Warning(string message, [CallerFilePath] string path = "")
    {
        Svc.Log.Warning($"[{Path.GetFileNameWithoutExtension(path)}] {message}");
    }

    /// <summary>
    /// Logs an error message with the calling member name as prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="path">The calling member name (automatically provided by the compiler).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(string message, [CallerFilePath] string path = "")
    {
        Svc.Log.Error($"[{Path.GetFileNameWithoutExtension(path)}] {message}");
    }

    /// <summary>
    /// Logs an error message with an exception and the calling member name as prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="path">The calling member name (automatically provided by the compiler).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Error(string message, Exception exception, [CallerFilePath] string path = "")
    {
        Svc.Log.Error(exception, $"[{Path.GetFileNameWithoutExtension(path)}] {message}");
    }

    public static void Error(Exception ex, string message, [CallerFilePath] string path = "") => Error(message, ex, path);

    /// <summary>
    /// Logs a fatal error message with the calling member name as prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="path">The calling member name (automatically provided by the compiler).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fatal(string message, [CallerFilePath] string path = "")
    {
        Svc.Log.Fatal($"[{Path.GetFileNameWithoutExtension(path)}] {message}");
    }

    /// <summary>
    /// Logs a fatal error message with an exception and the calling member name as prefix.
    /// </summary>
    /// <param name="message">The message to log.</param>
    /// <param name="exception">The exception to log.</param>
    /// <param name="path">The calling member name (automatically provided by the compiler).</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Fatal(string message, Exception exception, [CallerFilePath] string path = "")
    {
        Svc.Log.Fatal(exception, $"[{Path.GetFileNameWithoutExtension(path)}] {message}");
    }
}

/// <summary>
/// Builds a debug log message only when debug logging is enabled, so disabled log calls cost nothing.
/// </summary>
[InterpolatedStringHandler]
public ref struct DebugLogHandler
{
    private LogMessageBuilder _builder;
    public DebugLogHandler(int literalLength, int formattedCount, out bool shouldAppend) => _builder = new(literalLength, formattedCount, LogEventLevel.Debug, out shouldAppend);
    public readonly bool Enabled => _builder.Enabled;
    public void AppendLiteral(string value) => _builder.Inner.AppendLiteral(value);
    public void AppendFormatted<T>(T value) => _builder.Inner.AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => _builder.Inner.AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment, string? format = null) => _builder.Inner.AppendFormatted(value, alignment, format);
    public string ToStringAndClear() => _builder.Inner.ToStringAndClear();
}

/// <summary>
/// Builds a verbose log message only when verbose logging is enabled, so disabled log calls cost nothing.
/// </summary>
[InterpolatedStringHandler]
public ref struct VerboseLogHandler
{
    private LogMessageBuilder _builder;
    public VerboseLogHandler(int literalLength, int formattedCount, out bool shouldAppend) => _builder = new(literalLength, formattedCount, LogEventLevel.Verbose, out shouldAppend);
    public readonly bool Enabled => _builder.Enabled;
    public void AppendLiteral(string value) => _builder.Inner.AppendLiteral(value);
    public void AppendFormatted<T>(T value) => _builder.Inner.AppendFormatted(value);
    public void AppendFormatted<T>(T value, string? format) => _builder.Inner.AppendFormatted(value, format);
    public void AppendFormatted<T>(T value, int alignment, string? format = null) => _builder.Inner.AppendFormatted(value, alignment, format);
    public string ToStringAndClear() => _builder.Inner.ToStringAndClear();
}

internal ref struct LogMessageBuilder
{
    public DefaultInterpolatedStringHandler Inner;
    public readonly bool Enabled;

    public LogMessageBuilder(int literalLength, int formattedCount, LogEventLevel level, out bool shouldAppend)
    {
        Enabled = shouldAppend = Svc.Log.MinimumLogLevel <= level;
        Inner = Enabled ? new DefaultInterpolatedStringHandler(literalLength, formattedCount) : default;
    }
}
