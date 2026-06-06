using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EndpointMonitoring.Core.Logging;

/// <summary>
/// Logger provider that mirrors every log entry into an <see cref="ILiveLogSink"/> so it can be
/// streamed to the web UI in real time. Standard <c>Logging:LogLevel</c> filter rules apply;
/// provider-specific rules can be set under <c>Logging:LiveLog:LogLevel</c>.
/// </summary>
[ProviderAlias("LiveLog")]
public sealed class LiveLogLoggerProvider : ILoggerProvider
{
    private readonly string _serviceName;
    private readonly ILiveLogSink _sink;
    private readonly string[] _excludedCategoryPrefixes;

    /// <summary>
    /// Creates the provider for the given <paramref name="serviceName"/>. Categories matching one of the
    /// <paramref name="excludedCategoryPrefixes"/> are ignored — used to break feedback loops where the
    /// component that forwards log entries would itself produce new entries while forwarding.
    /// </summary>
    public LiveLogLoggerProvider(string serviceName, ILiveLogSink sink, params string[] excludedCategoryPrefixes)
    {
        _serviceName = serviceName;
        _sink = sink;
        _excludedCategoryPrefixes = excludedCategoryPrefixes;
    }

    /// <inheritdoc/>
    public ILogger CreateLogger(string categoryName) =>
        _excludedCategoryPrefixes.Any(p => categoryName.StartsWith(p, StringComparison.Ordinal))
            ? NullLogger.Instance
            : new LiveLogLogger(_serviceName, categoryName, _sink);

    /// <inheritdoc/>
    public void Dispose()
    {
    }

    private sealed class LiveLogLogger(string serviceName, string category, ILiveLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
                                Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            try
            {
                sink.Write(new LogEntry(DateTime.UtcNow, serviceName, logLevel, category,
                                        formatter(state, exception), exception?.ToString()));
            }
            catch
            {
                // A failing live-log sink must never break the application's logging pipeline.
            }
        }
    }
}
