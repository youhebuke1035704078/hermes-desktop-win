using System.Collections.Concurrent;
using System.IO;
using HermesDesktop.Helpers;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet;

namespace HermesDesktop.Services;

public class SshConnectionPool : IDisposable
{
    private readonly ILogger<SshConnectionPool> _logger;
    private readonly KnownHostStore _knownHostStore;
    private readonly ConcurrentDictionary<Guid, PooledConnection> _connections = new();
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _connectionLocks = new();
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);

    public SshConnectionPool(ILogger<SshConnectionPool> logger, KnownHostStore knownHostStore)
    {
        _logger = logger;
        _knownHostStore = knownHostStore;
    }

    public async Task<SshClient> GetOrCreateAsync(ConnectionProfile profile, CancellationToken ct)
    {
        if (_connections.TryGetValue(profile.Id, out var pooled) && pooled.Client.IsConnected)
        {
            pooled.LastUsed = DateTime.UtcNow;
            return pooled.Client;
        }

        await DisconnectIdleAsync(IdleTimeout);

        var connectionLock = _connectionLocks.GetOrAdd(profile.Id, _ => new SemaphoreSlim(1, 1));
        await connectionLock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring lock
            if (_connections.TryGetValue(profile.Id, out pooled) && pooled.Client.IsConnected)
            {
                pooled.LastUsed = DateTime.UtcNow;
                return pooled.Client;
            }

            // Clean up old connection if it exists but is disconnected
            if (pooled != null)
            {
                pooled.Client.Dispose();
                _connections.TryRemove(profile.Id, out _);
            }

            var client = CreateClient(profile);
            _logger.LogInformation("Connecting to {Target}...", profile.DisplayTarget);
            await Task.Run(() => client.Connect(), ct);
            _logger.LogInformation("Connected to {Target}", profile.DisplayTarget);

            _connections[profile.Id] = new PooledConnection(client);
            return client;
        }
        finally
        {
            connectionLock.Release();
        }
    }

    public async Task DisconnectAsync(Guid profileId)
    {
        if (_connections.TryRemove(profileId, out var pooled))
        {
            await Task.Run(() =>
            {
                try { pooled.Client.Disconnect(); } catch { }
                pooled.Client.Dispose();
            });
            _logger.LogInformation("Disconnected profile {Id}", profileId);
        }
    }

    public void Dispose()
    {
        foreach (var kvp in _connections)
        {
            try { kvp.Value.Client.Dispose(); } catch { }
        }
        _connections.Clear();
        foreach (var connectionLock in _connectionLocks.Values)
        {
            connectionLock.Dispose();
        }
        _connectionLocks.Clear();
    }

    public SshClient CreateClient(ConnectionProfile profile)
    {
        var authMethods = BuildAuthMethods(profile);

        if (authMethods.Count == 0)
            throw new InvalidOperationException(
                $"No SSH authentication methods available for {profile.DisplayTarget}. " +
                "Ensure SSH keys exist in ~/.ssh/ or specify a key path in the connection profile.");

        var connectionInfo = new ConnectionInfo(
            profile.SshHost,
            profile.SshPort,
            profile.SshUser,
            authMethods.ToArray())
        {
            Timeout = TimeSpan.FromSeconds(15),
            RetryAttempts = 1,
            Encoding = System.Text.Encoding.UTF8
        };

        var client = new SshClient(connectionInfo)
        {
            KeepAliveInterval = TimeSpan.FromSeconds(30)
        };
        client.HostKeyReceived += (_, e) => _knownHostStore.VerifyOrTrust(profile, e);
        return client;
    }

    public Task ForgetKnownHostAsync(ConnectionProfile profile, CancellationToken ct = default) =>
        _knownHostStore.ForgetAsync(profile, ct);

    private List<AuthenticationMethod> BuildAuthMethods(ConnectionProfile profile)
    {
        var methods = new List<AuthenticationMethod>();

        // 1. If a specific key path is set, use it
        if (!string.IsNullOrWhiteSpace(profile.SshKeyPath) && File.Exists(profile.SshKeyPath))
        {
            try
            {
                var keyFile = CreatePrivateKeyFile(profile, profile.SshKeyPath);
                methods.Add(new PrivateKeyAuthenticationMethod(profile.SshUser, keyFile));
                _logger.LogDebug("Added key auth from profile: {Path}", profile.SshKeyPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load key from {Path}", profile.SshKeyPath);
            }
        }

        // 2. Try standard key files from ~/.ssh/
        var sshDir = AppPaths.SshDirectory;
        if (Directory.Exists(sshDir))
        {
            foreach (var keyName in new[] { "id_ed25519", "id_rsa", "id_ecdsa" })
            {
                var keyPath = Path.Combine(sshDir, keyName);
                if (!File.Exists(keyPath)) continue;

                // Skip if this is the same as the profile key (already added)
                if (keyPath.Equals(profile.SshKeyPath, StringComparison.OrdinalIgnoreCase))
                    continue;

                try
                {
                    var keyFile = CreatePrivateKeyFile(profile, keyPath);
                    methods.Add(new PrivateKeyAuthenticationMethod(profile.SshUser, keyFile));
                    _logger.LogDebug("Added key auth: {Path}", keyPath);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "Skipping key {Path} (not loadable)", keyPath);
                }
            }
        }

        return methods;
    }

    private static PrivateKeyFile CreatePrivateKeyFile(ConnectionProfile profile, string path)
    {
        var passphrase = ResolvePassphrase(profile);
        return string.IsNullOrEmpty(passphrase)
            ? new PrivateKeyFile(path)
            : new PrivateKeyFile(path, passphrase);
    }

    private static string? ResolvePassphrase(ConnectionProfile profile)
    {
        if (!string.IsNullOrEmpty(profile.SshKeyPassphrase))
        {
            return profile.SshKeyPassphrase;
        }

        var labelKey = new string(profile.Label
            .Where(char.IsLetterOrDigit)
            .Select(char.ToUpperInvariant)
            .ToArray());
        if (!string.IsNullOrWhiteSpace(labelKey))
        {
            var scoped = Environment.GetEnvironmentVariable($"HERMES_DESKTOP_SSH_KEY_PASSPHRASE_{labelKey}");
            if (!string.IsNullOrEmpty(scoped))
            {
                return scoped;
            }
        }

        return Environment.GetEnvironmentVariable("HERMES_DESKTOP_SSH_KEY_PASSPHRASE");
    }

    private async Task DisconnectIdleAsync(TimeSpan idleTimeout)
    {
        var cutoff = DateTime.UtcNow - idleTimeout;
        foreach (var (profileId, pooled) in _connections)
        {
            if (pooled.LastUsed > cutoff)
            {
                continue;
            }

            if (_connections.TryRemove(profileId, out var removed))
            {
                await Task.Run(() =>
                {
                    try { removed.Client.Disconnect(); } catch { }
                    removed.Client.Dispose();
                });
                _logger.LogDebug("Evicted idle SSH connection for profile {Id}", profileId);
            }
        }
    }

    private class PooledConnection
    {
        public SshClient Client { get; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime LastUsed { get; set; } = DateTime.UtcNow;

        public PooledConnection(SshClient client)
        {
            Client = client;
        }
    }
}
