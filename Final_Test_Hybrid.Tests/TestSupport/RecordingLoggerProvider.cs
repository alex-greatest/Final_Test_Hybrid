using Microsoft.Extensions.Logging;

namespace Final_Test_Hybrid.Tests.TestSupport;

internal sealed class RecordingLoggerProvider : ILoggerProvider
{
    private readonly List<RecordedLogEntry> _entries = [];

    public IReadOnlyList<RecordedLogEntry> Entries => _entries;

    public ILogger CreateLogger(string categoryName)
    {
        return new RecordingLogger(categoryName, _entries);
    }

    public void Dispose()
    {
    }

    private sealed class RecordingLogger(string categoryName, List<RecordedLogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            entries.Add(new RecordedLogEntry(logLevel, categoryName, formatter(state, exception)));
        }
    }
}

internal sealed record RecordedLogEntry(LogLevel Level, string Category, string Message);
