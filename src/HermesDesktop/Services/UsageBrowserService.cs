using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

public class UsageBrowserService : IUsageBrowserService
{
    private readonly IRemoteScriptExecutor _executor;
    private readonly ILogger<UsageBrowserService> _logger;

    public UsageBrowserService(IRemoteScriptExecutor executor, ILogger<UsageBrowserService> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task<UsageSummary> GetUsageSummaryAsync(ConnectionProfile profile, CancellationToken ct)
    {
        return await _executor.ExecuteAsync<UsageSummary>(
            profile, "query_usage.py", null, ct);
    }
}
