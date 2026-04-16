using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

public class SkillBrowserService : ISkillBrowserService
{
    private readonly IRemoteScriptExecutor _executor;
    private readonly ILogger<SkillBrowserService> _logger;

    public SkillBrowserService(IRemoteScriptExecutor executor, ILogger<SkillBrowserService> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task<List<SkillInfo>> GetSkillsAsync(ConnectionProfile profile, CancellationToken ct)
    {
        var result = await _executor.ExecuteAsync<SkillsResponse>(
            profile, "discover_skills.py", null, ct);
        return result.Skills;
    }

    private class SkillsResponse
    {
        public List<SkillInfo> Skills { get; set; } = new();
    }
}
