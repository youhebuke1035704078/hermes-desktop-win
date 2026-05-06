using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using HermesDesktop.Helpers;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;
using Renci.SshNet.Common;

namespace HermesDesktop.Services;

public sealed class KnownHostStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly ILogger<KnownHostStore> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);

    public KnownHostStore(ILogger<KnownHostStore> logger)
    {
        _logger = logger;
    }

    public void VerifyOrTrust(ConnectionProfile profile, HostKeyEventArgs e)
    {
        var keyId = BuildKeyId(profile);
        var fingerprint = e.FingerPrintSHA256;
        var hostKeyName = string.IsNullOrWhiteSpace(e.HostKeyName) ? null : e.HostKeyName;

        _fileLock.Wait();
        try
        {
            var entries = LoadEntries();
            if (entries.TryGetValue(keyId, out var existing))
            {
                if (string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal))
                {
                    e.CanTrust = true;
                    return;
                }

                e.CanTrust = false;
                _logger.LogWarning(
                    "Rejected SSH host key change for {Target}. Expected {Expected}, got {Actual}",
                    profile.DisplayTarget,
                    existing.Fingerprint,
                    fingerprint);
                return;
            }

            entries[keyId] = new KnownHostEntry
            {
                Host = profile.SshHost,
                Port = profile.SshPort,
                HostKeyName = hostKeyName,
                Fingerprint = fingerprint,
                FirstSeenUtc = DateTime.UtcNow,
                LastSeenUtc = DateTime.UtcNow
            };
            SaveEntries(entries);
            e.CanTrust = true;
            _logger.LogInformation("Trusted first-seen SSH host key for {Target}: {Fingerprint}",
                profile.DisplayTarget,
                fingerprint);
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task ForgetAsync(ConnectionProfile profile, CancellationToken ct = default)
    {
        var keyId = BuildKeyId(profile);
        await _fileLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var entries = LoadEntries();
            if (entries.Remove(keyId))
            {
                SaveEntries(entries);
            }
        }
        finally
        {
            _fileLock.Release();
        }
    }

    private static string BuildKeyId(ConnectionProfile profile) =>
        $"{profile.SshHost.Trim().ToLowerInvariant()}:{profile.SshPort}";

    private static Dictionary<string, KnownHostEntry> LoadEntries()
    {
        if (!File.Exists(AppPaths.KnownHostsFile))
        {
            return new Dictionary<string, KnownHostEntry>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            var json = File.ReadAllText(AppPaths.KnownHostsFile);
            return JsonSerializer.Deserialize<Dictionary<string, KnownHostEntry>>(json, JsonOptions)
                   ?? new Dictionary<string, KnownHostEntry>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, KnownHostEntry>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static void SaveEntries(Dictionary<string, KnownHostEntry> entries)
    {
        Directory.CreateDirectory(AppPaths.AppDataDirectory);
        var temp = AppPaths.KnownHostsFile + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(entries, JsonOptions));
        if (File.Exists(AppPaths.KnownHostsFile))
        {
            File.Replace(temp, AppPaths.KnownHostsFile, null);
        }
        else
        {
            File.Move(temp, AppPaths.KnownHostsFile);
        }
    }

    private sealed class KnownHostEntry
    {
        [JsonPropertyName("host")]
        public string Host { get; set; } = string.Empty;

        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("hostKeyName")]
        public string? HostKeyName { get; set; }

        [JsonPropertyName("fingerprint")]
        public string Fingerprint { get; set; } = string.Empty;

        [JsonPropertyName("firstSeenUtc")]
        public DateTime FirstSeenUtc { get; set; }

        [JsonPropertyName("lastSeenUtc")]
        public DateTime LastSeenUtc { get; set; }
    }
}
