namespace Scottplotz.Logging;

public sealed class JsonFileLoggerProvider(string path) : ILoggerProvider
{
    private readonly object _gate = new();

    public ILogger CreateLogger(string categoryName) => new JsonFileLogger(categoryName, path, _gate);

    public void Dispose()
    {
    }

    private sealed class JsonFileLogger(string category, string path, object gate) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var line = LogLine.Format(DateTimeOffset.UtcNow, logLevel.ToString(), category, formatter(state, exception), exception?.ToString());
            try
            {
                lock (gate)
                {
                    var folder = Path.GetDirectoryName(path);
                    if (!string.IsNullOrEmpty(folder))
                        Directory.CreateDirectory(folder);

                    File.AppendAllText(path, line + Environment.NewLine);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
