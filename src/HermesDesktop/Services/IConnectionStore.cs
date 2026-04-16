using HermesDesktop.Models;

namespace HermesDesktop.Services;

public interface IConnectionStore
{
    IReadOnlyList<ConnectionProfile> Connections { get; }
    AppPreferences Preferences { get; }

    Task LoadAsync();
    Task SaveConnectionAsync(ConnectionProfile profile);
    Task DeleteConnectionAsync(Guid id);
    Task SavePreferencesAsync(AppPreferences preferences);
}
