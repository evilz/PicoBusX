using System.Text.Json;
using PicoBusX.Web.Options;

namespace PicoBusX.Web.Services;

/// <summary>
/// Stores runtime-overridden Service Bus connection settings in memory and optionally
/// persists them to <c>{ContentRoot}/.picobusx/connection.json</c> so they survive restarts.
/// </summary>
public sealed class ConnectionSettingsStore : IConnectionSettingsStore
{
    private readonly string _settingsFilePath;
    private readonly ILogger<ConnectionSettingsStore> _logger;
    private ServiceBusConnectionOptions? _runtimeSettings;

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    public ConnectionSettingsStore(IWebHostEnvironment env, ILogger<ConnectionSettingsStore> logger)
    {
        _logger = logger;
        var dataDir = Path.Combine(env.ContentRootPath, ".picobusx");
        _settingsFilePath = Path.Combine(dataDir, "connection.json");
        TryLoad();
    }

    public bool HasRuntimeSettings => _runtimeSettings is not null;

    public ServiceBusConnectionOptions? GetRuntimeSettings() => _runtimeSettings;

    public async Task SaveAsync(ServiceBusConnectionOptions settings)
    {
        _runtimeSettings = settings;
        try
        {
            var dir = Path.GetDirectoryName(_settingsFilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(settings, SerializerOptions);
            await File.WriteAllTextAsync(_settingsFilePath, json);
            _logger.LogInformation("Connection settings saved to {Path}", _settingsFilePath);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex,
                "Failed to persist connection settings to file; settings are in memory only for this session.");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex,
                "Failed to persist connection settings to file; settings are in memory only for this session.");
            throw;
        }
    }

    public Task ClearAsync()
    {
        _runtimeSettings = null;
        try
        {
            if (File.Exists(_settingsFilePath))
                File.Delete(_settingsFilePath);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to delete persisted connection settings file.");
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Failed to delete persisted connection settings file.");
            throw;
        }

        return Task.CompletedTask;
    }

    private void TryLoad()
    {
        try
        {
            if (!File.Exists(_settingsFilePath)) return;
            var json = File.ReadAllText(_settingsFilePath);
            _runtimeSettings = JsonSerializer.Deserialize<ServiceBusConnectionOptions>(json);
            _logger.LogInformation("Loaded runtime connection settings from {Path}", _settingsFilePath);
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Failed to load persisted connection settings from {Path}", _settingsFilePath);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning(ex, "Failed to load persisted connection settings from {Path}", _settingsFilePath);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to load persisted connection settings from {Path}", _settingsFilePath);
        }
    }
}
