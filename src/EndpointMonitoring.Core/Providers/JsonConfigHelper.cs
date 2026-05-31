using System.Diagnostics;
using System.Text.Json;

namespace EndpointMonitoring.Core.Providers;

/// <summary>
/// Wraps JSON deserialization so that JsonException first-chance events
/// never surface to the debugger and interrupt attached processes.
/// </summary>
internal static class JsonConfigHelper
{
    private static readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Attempts to deserialize <paramref name="json"/> into <typeparamref name="T"/>.
    /// Returns <c>false</c> (and <paramref name="result"/> = default) on any parse error
    /// without ever throwing – the debugger will never pause here.
    /// </summary>
    [DebuggerNonUserCode]
    internal static bool TryDeserialize<T>(string? json, out T? result)
    {
        result = default;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            result = JsonSerializer.Deserialize<T>(json, _options);
            return result is not null;
        }
        catch
        {
            return false;
        }
    }
}
