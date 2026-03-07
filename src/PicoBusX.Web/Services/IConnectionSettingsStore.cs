using PicoBusX.Web.Options;

namespace PicoBusX.Web.Services;

/// <summary>
/// Stores runtime-overridden connection settings so that users can configure the Service Bus
/// connection through the UI without modifying appsettings.json.
/// </summary>
public interface IConnectionSettingsStore
{
    /// <summary>Returns true if runtime settings have been saved by the user.</summary>
    bool HasRuntimeSettings { get; }

    /// <summary>Returns the runtime settings, or <c>null</c> if none have been saved.</summary>
    ServiceBusConnectionOptions? GetRuntimeSettings();

    /// <summary>Persists the given settings as the active runtime configuration.</summary>
    Task SaveAsync(ServiceBusConnectionOptions settings);

    /// <summary>Removes any saved runtime settings, reverting to appsettings / environment config.</summary>
    Task ClearAsync();
}
