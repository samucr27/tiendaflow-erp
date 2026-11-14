using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace TiendaFlow.IntegrationTests;

/// <summary>
/// Collects the web application log so a failing smoke test can show the real
/// exception that the friendly page message hides. Test-only: production keeps
/// the console provider configured in Program.cs.
/// </summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

    public void Dispose() { }

    private sealed class CapturingLogger : ILogger
    {
        private readonly CapturingLoggerProvider _provider;
        private readonly string _category;

        public CapturingLogger(CapturingLoggerProvider provider, string category)
        {
            _provider = provider;
            _category = category;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var message = $"{logLevel} {_category}: {formatter(state, exception)}";

            if (exception is not null)
                message += Environment.NewLine + exception;

            _provider._entries.Enqueue(message);
        }
    }
}
