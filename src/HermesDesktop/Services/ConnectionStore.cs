using System.IO;
using System.Text.Json;
using HermesDesktop.Helpers;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

public class ConnectionStore : IConnectionStore
{
    private readonly ILogger<ConnectionStore> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private List<ConnectionProfile> _connections = new();
    private AppPreferences _preferences = new();

    public IReadOnlyList<ConnectionProfile> Connections => _connections.AsReadOnly();
    public AppPreferences Preferences => _preferences;

    public ConnectionStore(ILogger<ConnectionStore> logger)
    {
        _logger = logger;
    }

    public async Task LoadAsync()
    {
        AppPaths.EnsureDirectories();
        await LoadConnectionsAsync();
        await LoadPreferencesAsync();
    }

    public async Task SaveConnectionAsync(ConnectionProfile profile)
    {
        var existing = _connections.FindIndex(c => c.Id == profile.Id);
        profile.UpdatedAt = DateTime.UtcNow;

        if (existing >= 0)
            _connections[existing] = profile;
        else
            _connections.Add(profile);

        _connections.Sort((a, b) => string.Compare(a.Label, b.Label, StringComparison.OrdinalIgnoreCase));
        await WriteConnectionsAsync();
    }

    public async Task DeleteConnectionAsync(Guid id)
    {
        _connections.RemoveAll(c => c.Id == id);
        await WriteConnectionsAsync();
    }

    public async Task SavePreferencesAsync(AppPreferences preferences)
    {
        _preferences = preferences;
        await WriteAtomicAsync(AppPaths.PreferencesFile, preferences);
    }

    private async Task LoadConnectionsAsync()
    {
        try
        {
            if (File.Exists(AppPaths.ConnectionsFile))
            {
                var json = await File.ReadAllTextAsync(AppPaths.ConnectionsFile);
                _connections = JsonSerializer.Deserialize<List<ConnectionProfile>>(json, _jsonOptions) ?? new();
                _logger.LogInformation("Loaded {Count} connections", _connections.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load connections");
            _connections = new();
        }
    }

    private async Task LoadPreferencesAsync()
    {
        try
        {
            if (File.Exists(AppPaths.PreferencesFile))
            {
                var json = await File.ReadAllTextAsync(AppPaths.PreferencesFile);
                _preferences = JsonSerializer.Deserialize<AppPreferences>(json, _jsonOptions) ?? new();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load preferences");
            _preferences = new();
        }
    }

    private Task WriteConnectionsAsync() =>
        WriteAtomicAsync(AppPaths.ConnectionsFile, _connections);

    private async Task WriteAtomicAsync<T>(string path, T data)
    {
        var tempPath = path + ".tmp";
        try
        {
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, path, overwrite: true);
            _logger.LogDebug("Wrote {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write {Path}", path);
            try { File.Delete(tempPath); } catch { }
            throw;
        }
    }
}
