namespace EndpointMonitoring.Core.Logging;

/// <summary>Receives log entries captured by <see cref="LiveLogLoggerProvider"/> for real-time streaming.</summary>
public interface ILiveLogSink
{
    /// <summary>Accepts a captured log entry. Must be fast and must never throw.</summary>
    void Write(LogEntry entry);
}
