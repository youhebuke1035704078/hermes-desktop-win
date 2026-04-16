using System.Diagnostics;
using System.IO;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace HermesDesktop.Services;

public class SshTransport : ISshTransport
{
    private readonly SshConnectionPool _pool;
    private readonly ILogger<SshTransport> _logger;

    public event EventHandler<SshConnectionEventArgs>? ConnectionStateChanged;

    public SshTransport(SshConnectionPool pool, ILogger<SshTransport> logger)
    {
        _pool = pool;
        _logger = logger;
    }

    public async Task<SshCommandResult> ExecuteCommandAsync(
        ConnectionProfile profile,
        string command,
        CancellationToken ct = default,
        TimeSpan? timeout = null)
    {
        var effectiveTimeout = timeout ?? TimeSpan.FromSeconds(30);
        var sw = Stopwatch.StartNew();

        try
        {
            RaiseConnectionState(profile.Id, SshConnectionState.Connecting);
            var client = await _pool.GetOrCreateAsync(profile, ct);
            RaiseConnectionState(profile.Id, SshConnectionState.Connected);

            using var cmd = client.CreateCommand(command);
            cmd.CommandTimeout = effectiveTimeout;

            var result = await Task.Run(() =>
            {
                cmd.Execute();
                return new SshCommandResult(
                    cmd.ExitStatus ?? -1,
                    cmd.Result ?? string.Empty,
                    cmd.Error ?? string.Empty,
                    sw.Elapsed);
            }, ct);

            if (result.ExitCode != 0)
            {
                _logger.LogWarning("SSH command exited {Code} ({Duration}ms): {Stderr}",
                    result.ExitCode, result.Duration.TotalMilliseconds,
                    result.StandardError.Length > 200
                        ? result.StandardError[..200] + "..."
                        : result.StandardError);
            }
            else
            {
                _logger.LogDebug("SSH command completed in {Duration}ms", result.Duration.TotalMilliseconds);
            }

            return result;
        }
        catch (SshConnectionException ex)
        {
            _logger.LogError(ex, "SSH connection error to {Target}", profile.DisplayTarget);
            RaiseConnectionState(profile.Id, SshConnectionState.Error, ex.Message);

            // Evict broken connection and retry once
            await _pool.DisconnectAsync(profile.Id);
            throw;
        }
        catch (SshOperationTimeoutException ex)
        {
            _logger.LogError(ex, "SSH command timed out after {Timeout}s", effectiveTimeout.TotalSeconds);
            throw;
        }
    }

    public async Task<ShellStreamSession> OpenShellAsync(
        ConnectionProfile profile,
        int columns, int rows,
        CancellationToken ct = default)
    {
        RaiseConnectionState(profile.Id, SshConnectionState.Connecting);

        // Terminal sessions get their own dedicated connection (not pooled)
        // because ShellStream ties up the connection
        var client = await Task.Run(() =>
        {
            var pool = _pool; // Use pool's CreateClient logic via a separate connection
            // For terminal, we need a dedicated SshClient
            var authMethods = BuildAuthMethodsForTerminal(profile);
            var connectionInfo = new ConnectionInfo(
                profile.SshHost,
                profile.SshPort,
                profile.SshUser,
                authMethods.ToArray())
            {
                Timeout = TimeSpan.FromSeconds(15),
                Encoding = System.Text.Encoding.UTF8
            };

            var sshClient = new SshClient(connectionInfo);
            sshClient.Connect();
            return sshClient;
        }, ct);

        RaiseConnectionState(profile.Id, SshConnectionState.Connected);

        var stream = client.CreateShellStream(
            "xterm-256color",
            (uint)columns, (uint)rows,
            0, 0,
            4096,
            new Dictionary<TerminalModes, uint>
            {
                { TerminalModes.ECHO, 1 },
                { TerminalModes.TTY_OP_ISPEED, 14400 },
                { TerminalModes.TTY_OP_OSPEED, 14400 }
            });

        return new ShellStreamSession(stream, client);
    }

    public Task DisconnectAsync(Guid profileId)
    {
        RaiseConnectionState(profileId, SshConnectionState.Disconnected);
        return _pool.DisconnectAsync(profileId);
    }

    public async Task DisconnectAllAsync()
    {
        _pool.Dispose();
        await Task.CompletedTask;
    }

    private void RaiseConnectionState(Guid profileId, SshConnectionState state, string? error = null)
    {
        ConnectionStateChanged?.Invoke(this, new SshConnectionEventArgs
        {
            ProfileId = profileId,
            State = state,
            ErrorMessage = error
        });
    }

    private List<AuthenticationMethod> BuildAuthMethodsForTerminal(ConnectionProfile profile)
    {
        var methods = new List<AuthenticationMethod>();
        var sshDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".ssh");

        if (!string.IsNullOrWhiteSpace(profile.SshKeyPath) && File.Exists(profile.SshKeyPath))
        {
            try
            {
                methods.Add(new PrivateKeyAuthenticationMethod(
                    profile.SshUser, new PrivateKeyFile(profile.SshKeyPath)));
            }
            catch { }
        }

        if (Directory.Exists(sshDir))
        {
            foreach (var keyName in new[] { "id_ed25519", "id_rsa", "id_ecdsa" })
            {
                var keyPath = Path.Combine(sshDir, keyName);
                if (!File.Exists(keyPath)) continue;
                if (keyPath.Equals(profile.SshKeyPath, StringComparison.OrdinalIgnoreCase)) continue;

                try
                {
                    methods.Add(new PrivateKeyAuthenticationMethod(
                        profile.SshUser, new PrivateKeyFile(keyPath)));
                }
                catch { }
            }
        }

        return methods;
    }
}
