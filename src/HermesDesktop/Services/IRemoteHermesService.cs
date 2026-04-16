using HermesDesktop.Models;

namespace HermesDesktop.Services;

public interface IRemoteHermesService
{
    Task<HermesOverview> GetOverviewAsync(ConnectionProfile profile, CancellationToken ct = default);
    Task<SshCommandResult> TestConnectionAsync(ConnectionProfile profile, CancellationToken ct = default);
}
