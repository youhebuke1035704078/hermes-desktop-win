using HermesDesktop.Models;

namespace HermesDesktop.Services;

public interface IUsageBrowserService
{
    Task<UsageSummary> GetUsageSummaryAsync(ConnectionProfile profile, CancellationToken ct = default);
}
