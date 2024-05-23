using NuGet.Common;

namespace MLNetPredict
{
    public class ConsoleLogger : ILogger
    {
        public static readonly ConsoleLogger Current = new();

        public void LogDebug(string data) => Console.WriteLine($"DEBUG: {data}");
        public void LogVerbose(string data) => Console.WriteLine($"VERBOSE: {data}");
        public void LogInformation(string data) => Console.WriteLine($"INFO: {data}");
        public void LogMinimal(string data) => Console.WriteLine($"MINIMAL: {data}");
        public void LogWarning(string data) => Console.WriteLine($"WARNING: {data}");
        public void LogError(string data) => Console.WriteLine($"ERROR: {data}");
        public void LogInformationSummary(string data) => Console.WriteLine($"SUMMARY: {data}");
        public void Log(LogLevel level, string data)
        {
            switch (level)
            {
                case LogLevel.Debug: LogDebug(data); break;
                case LogLevel.Verbose: LogVerbose(data); break;
                case LogLevel.Information: LogInformation(data); break;
                case LogLevel.Minimal: LogMinimal(data); break;
                case LogLevel.Warning: LogWarning(data); break;
                case LogLevel.Error: LogError(data); break;
                default: LogMinimal(data); break;
            }
        }
        public Task LogAsync(LogLevel level, string data)
        {
            Log(level, data);
            return Task.CompletedTask;
        }
        public void Log(ILogMessage message) => Log(message.Level, message.Message);
        public Task LogAsync(ILogMessage message)
        {
            Log(message);
            return Task.CompletedTask;
        }
    }
}