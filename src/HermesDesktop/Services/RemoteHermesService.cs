using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

public class RemoteHermesService : IRemoteHermesService
{
    private readonly ISshTransport _ssh;
    private readonly IRemoteScriptExecutor _executor;
    private readonly ILogger<RemoteHermesService> _logger;

    public RemoteHermesService(
        ISshTransport ssh,
        IRemoteScriptExecutor executor,
        ILogger<RemoteHermesService> logger)
    {
        _ssh = ssh;
        _executor = executor;
        _logger = logger;
    }

    public async Task<HermesOverview> GetOverviewAsync(ConnectionProfile profile, CancellationToken ct)
    {
        return await _executor.ExecuteAsync<HermesOverview>(
            profile, "discover_hermes.py", null, ct);
    }

    public async Task<SshCommandResult> TestConnectionAsync(ConnectionProfile profile, CancellationToken ct)
    {
        // Test 1: basic connectivity
        var echoResult = await _ssh.ExecuteCommandAsync(
            profile, "echo 'hermes-desktop-ok'", ct, TimeSpan.FromSeconds(10));

        if (echoResult.ExitCode != 0)
            return echoResult;

        // Test 2: python3 available
        var pythonResult = await _ssh.ExecuteCommandAsync(
            profile, "python3 --version", ct, TimeSpan.FromSeconds(10));

        return pythonResult;
    }
}
