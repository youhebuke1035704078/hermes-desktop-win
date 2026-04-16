using HermesDesktop.Models;

namespace HermesDesktop.Services;

public interface ISkillBrowserService
{
    Task<List<SkillInfo>> GetSkillsAsync(ConnectionProfile profile, CancellationToken ct = default);
}
