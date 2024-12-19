using NuGet.Common;

namespace MLNetPredict;

/// <summary>
/// Console logger implementation for NuGet operations
/// </summary>
public class ConsoleLogger : ILogger
{
    private static ConsoleLogger? _current;
    public static ConsoleLogger Current => _current ??= new ConsoleLogger();

    private ConsoleLogger() { }

    public void LogDebug(string data) =>
        WriteWithColor($"DEBUG: {data}", ConsoleColor.Gray);

    public void LogVerbose(string data) =>
        WriteWithColor($"VERBOSE: {data}", ConsoleColor.DarkGray);

    public void LogInformation(string data) =>
        WriteWithColor($"INFO: {data}", ConsoleColor.White);

    public void LogMinimal(string data) =>
        WriteWithColor($"MINIMAL: {data}", ConsoleColor.White);

    public void LogWarning(string data) =>
        WriteWithColor($"WARNING: {data}", ConsoleColor.Yellow);

    public void LogError(string data) =>
        WriteWithColor($"ERROR: {data}", ConsoleColor.Red);

    public void LogInformationSummary(string data) =>
        WriteWithColor($"SUMMARY: {data}", ConsoleColor.Cyan);

    public void Log(LogLevel level, string data)
    {
        switch (level)
        {
            case LogLevel.Debug:
                LogDebug(data);
                break;
            case LogLevel.Verbose:
                LogVerbose(data);
                break;
            case LogLevel.Information:
                LogInformation(data);
                break;
            case LogLevel.Minimal:
                LogMinimal(data);
                break;
            case LogLevel.Warning:
                LogWarning(data);
                break;
            case LogLevel.Error:
                LogError(data);
                break;
            default:
                LogMinimal(data);
                break;
        }
    }

    public Task LogAsync(LogLevel level, string data)
    {
        Log(level, data);
        return Task.CompletedTask;
    }

    public void Log(ILogMessage message) =>
        Log(message.Level, message.Message);

    public Task LogAsync(ILogMessage message) =>
        LogAsync(message.Level, message.Message);

    private static void WriteWithColor(string message, ConsoleColor color)
    {
        var originalColor = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = color;
            Console.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = originalColor;
        }
    }
}