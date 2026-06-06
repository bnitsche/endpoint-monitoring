using EndpointMonitoring.Core.Logging;

namespace EndpointMonitoring.Web.Services;

/// <summary>
/// Singleton ring buffer holding the most recent log entries from both the web app (via
/// <see cref="LiveLogLoggerProvider"/>) and the monitoring service (via the SignalR hub).
/// Bridges incoming entries to Blazor components through <see cref="OnLogReceived"/>.
/// </summary>
public class LogStreamService : ILiveLogSink
{
    /// <summary>Maximum number of entries kept in the buffer.</summary>
    public const int Capacity = 1000;

    private readonly Queue<LogEntry> _buffer = new(Capacity);
    private readonly object _lock = new();

    /// <summary>Raised for every new log entry, after it has been added to the buffer.</summary>
    public event Action<LogEntry>? OnLogReceived;

    /// <inheritdoc/>
    public void Write(LogEntry entry)
    {
        lock (_lock)
        {
            _buffer.Enqueue(entry);
            while (_buffer.Count > Capacity)
                _buffer.Dequeue();
        }

        OnLogReceived?.Invoke(entry);
    }

    /// <summary>Returns a copy of the buffered entries, oldest first.</summary>
    public IReadOnlyList<LogEntry> GetSnapshot()
    {
        lock (_lock)
            return [.. _buffer];
    }
}
