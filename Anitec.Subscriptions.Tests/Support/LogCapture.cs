using Microsoft.Extensions.Logging;

namespace Anitec.Subscriptions.Tests.Support;

/// <summary>Keeps every log line of the service together with the correlation id of the scope that was open.</summary>
public sealed class LogCapture : ILoggerProvider, ISupportExternalScope
{
    private readonly List<CapturedLine> _lines = [];
    private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyList<CapturedLine> Lines
    {
        get
        {
            lock (_lines)
            {
                return _lines.ToList();
            }
        }
    }

    public void Dispose()
    {
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new CaptureLogger(this, categoryName);
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider)
    {
        _scopes = scopeProvider;
    }

    private sealed class CaptureLogger(LogCapture owner, string category) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return owner._scopes.Push(state);
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            string? correlationId = null;
            owner._scopes.ForEachScope((scope, _) =>
            {
                if (scope is IEnumerable<KeyValuePair<string, object>> pairs)
                    foreach (var pair in pairs)
                        if (pair.Key == "CorrelationId")
                            correlationId = pair.Value as string;
            }, 0);

            lock (owner._lines)
            {
                owner._lines.Add(new CapturedLine(category, logLevel, formatter(state, exception), correlationId));
            }
        }
    }
}

public sealed record CapturedLine(string Category, LogLevel Level, string Message, string? CorrelationId);
