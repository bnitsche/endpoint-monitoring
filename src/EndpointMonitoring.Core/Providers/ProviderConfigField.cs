namespace EndpointMonitoring.Core.Providers;

public enum ProviderConfigFieldType
{
    Text,
    Number,
    Url
}

public class ProviderConfigField
{
    public string Key { get; init; } = "";
    public string Label { get; init; } = "";
    public ProviderConfigFieldType FieldType { get; init; } = ProviderConfigFieldType.Text;
    public string DefaultValue { get; init; } = "";
    public string? HelperText { get; init; }
    public bool Required { get; init; }
}
