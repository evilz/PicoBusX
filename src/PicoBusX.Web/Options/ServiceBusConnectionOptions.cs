namespace PicoBusX.Web.Options;

public class ServiceBusConnectionOptions
{
    public const string SectionName = "ServiceBus";

    // Auth type constant values
    public const string AuthTypeConnectionString = "ConnectionString";
    public const string AuthTypeDefaultAzureCredential = "DefaultAzureCredential";
    public const string AuthTypeServicePrincipal = "ServicePrincipal";

    /// <summary>
    /// Authentication type. Valid values: <c>ConnectionString</c>, <c>DefaultAzureCredential</c>, <c>ServicePrincipal</c>.
    /// Defaults to <c>ConnectionString</c>.
    /// </summary>
    public string AuthType { get; set; } = AuthTypeConnectionString;

    // Connection String auth
    [ConfigurationKeyName("SERVICEBUS_CONNECTIONSTRING")]
    public string? ConnectionString { get; set; }

    [ConfigurationKeyName("SERVICEBUS_ADMINCONNECTIONSTRING")]
    public string? AdminConnectionString { get; set; }

    // Azure AD auth (DefaultAzureCredential or ServicePrincipal)
    [ConfigurationKeyName("SERVICEBUS_FULLYQUALIFIEDNAMESPACE")]
    public string? FullyQualifiedNamespace { get; set; }

    // Service Principal auth
    public string? TenantId { get; set; }
    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public string TransportType { get; set; } = "AmqpTcp";
    public int EntityMaxPeek { get; set; } = 10;
}
