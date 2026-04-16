using HermesDesktop.Models;

namespace HermesDesktop.Services;

public interface ISessionBrowserService
{
    Task<PaginatedResult<Session>> GetSessionsAsync(
        ConnectionProfile profile, int page = 1, int pageSize = 50,
        string? search = null, CancellationToken ct = default);

    Task<SessionDetail> GetSessionDetailAsync(
        ConnectionProfile profile, string sessionId, CancellationToken ct = default);

    Task DeleteSessionAsync(
        ConnectionProfile profile, string sessionId, CancellationToken ct = default);
}
