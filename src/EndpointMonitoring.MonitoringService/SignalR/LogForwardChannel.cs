using System.Threading.Channels;
using EndpointMonitoring.Core.Logging;

namespace EndpointMonitoring.MonitoringService.SignalR;

/// <summary>
/// Bounded queue between the logging pipeline and <see cref="MonitoringHubClient"/>. Entries are
/// buffered while the hub connection is down (e.g. during startup); the oldest entries are dropped
/// when the queue is full. Deliberately has no dependencies so it can be created before the logger
/// factory without a DI cycle.
/// </summary>
public class LogForwardChannel : ILiveLogSink
{
    private readonly Channel<LogEntry> _channel = Channel.CreateBounded<LogEntry>(
        new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });

    /// <summary>Reader side, drained by <see cref="MonitoringHubClient"/>.</summary>
    public ChannelReader<LogEntry> Reader => _channel.Reader;

    /// <inheritdoc/>
    public void Write(LogEntry entry) => _channel.Writer.TryWrite(entry);
}
