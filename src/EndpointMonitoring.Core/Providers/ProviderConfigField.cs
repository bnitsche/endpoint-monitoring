namespace EndpointMonitoring.Core.Providers;

/// <summary>The data-entry type for a provider configuration field.</summary>
public enum ProviderConfigFieldType
{
    /// <summary>Plain text input.</summary>
    Text,

    /// <summary>Numeric input.</summary>
    Number,

    /// <summary>URL input.</summary>
    Url
}

/// <summary>Describes a single configuration field exposed by an <see cref="IMonitoringProvider"/>.</summary>
public class ProviderConfigField
{
    /// <summary>JSON property key written into the provider config.</summary>
    public string Key { get; init; } = "";

    /// <summary>Human-readable label shown in the config form.</summary>
    public string Label { get; init; } = "";

    /// <summary>Controls the input widget and validation behaviour.</summary>
    public ProviderConfigFieldType FieldType { get; init; } = ProviderConfigFieldType.Text;

    /// <summary>Value pre-filled when the user creates a new endpoint.</summary>
    public string DefaultValue { get; init; } = "";

    /// <summary>Optional hint text displayed below the field.</summary>
    public string? HelperText { get; init; }

    /// <summary>When true, the form must not submit an empty value for this field.</summary>
    public bool Required { get; init; }
}
