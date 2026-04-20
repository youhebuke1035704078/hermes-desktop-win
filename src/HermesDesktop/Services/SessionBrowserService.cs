using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

public class SessionBrowserService : ISessionBrowserService
{
    private readonly IRemoteScriptExecutor _executor;
    private readonly ILogger<SessionBrowserService> _logger;

    public SessionBrowserService(IRemoteScriptExecutor executor, ILogger<SessionBrowserService> logger)
    {
        _executor = executor;
        _logger = logger;
    }

    public async Task<PaginatedResult<Session>> GetSessionsAsync(
        ConnectionProfile profile, int page, int pageSize,
        string? search, CancellationToken ct)
    {
        var parameters = new Dictionary<string, object>
        {
            ["page"] = page,
            ["page_size"] = pageSize,
        };
        if (!string.IsNullOrWhiteSpace(search))
            parameters["search"] = search;

        return await _executor.ExecuteAsync<PaginatedResult<Session>>(
            profile, "query_sessions.py", parameters, ct);
    }

    public async Task<SessionDetail> GetSessionDetailAsync(
        ConnectionProfile profile, string sessionId, CancellationToken ct)
    {
        return await _executor.ExecuteAsync<SessionDetail>(
            profile, "query_session_detail.py",
            new() { ["session_id"] = sessionId }, ct);
    }

    public async Task DeleteSessionAsync(
        ConnectionProfile profile, string sessionId, CancellationToken ct)
    {
        var result = await _executor.ExecuteAsync<DeleteResult>(
            profile, "delete_session.py",
            new() { ["session_id"] = sessionId }, ct);

        if (!result.Success)
            throw new InvalidOperationException(result.Error ?? "删除会话失败");
    }

    private class DeleteResult
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
    }
}
