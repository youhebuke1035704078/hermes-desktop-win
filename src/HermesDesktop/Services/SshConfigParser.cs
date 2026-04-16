using System.IO;
using HermesDesktop.Helpers;
using HermesDesktop.Models;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

/// <summary>
/// Parses ~/.ssh/config to extract host entries for import into connection profiles.
/// </summary>
public class SshConfigParser
{
    private readonly ILogger<SshConfigParser> _logger;

    public SshConfigParser(ILogger<SshConfigParser> logger)
    {
        _logger = logger;
    }

    public List<SshConfigEntry> Parse()
    {
        var configPath = Path.Combine(AppPaths.SshDirectory, "config");
        if (!File.Exists(configPath))
        {
            _logger.LogDebug("No SSH config found at {Path}", configPath);
            return new();
        }

        try
        {
            var lines = File.ReadAllLines(configPath);
            return ParseLines(lines);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse SSH config at {Path}", configPath);
            return new();
        }
    }

    private List<SshConfigEntry> ParseLines(string[] lines)
    {
        var entries = new List<SshConfigEntry>();
        SshConfigEntry? current = null;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();

            // Skip comments and empty lines
            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            // Split on first whitespace or =
            var (key, value) = SplitDirective(line);
            if (key == null || value == null)
                continue;

            if (key.Equals("Host", StringComparison.OrdinalIgnoreCase))
            {
                // Skip wildcard entries
                if (value.Contains('*') || value.Contains('?'))
                {
                    current = null;
                    continue;
                }

                current = new SshConfigEntry { Alias = value };
                entries.Add(current);
            }
            else if (current != null)
            {
                switch (key.ToLowerInvariant())
                {
                    case "hostname":
                        current.HostName = value;
                        break;
                    case "user":
                        current.User = value;
                        break;
                    case "port":
                        if (int.TryParse(value, out var port))
                            current.Port = port;
                        break;
                    case "identityfile":
                        // Expand ~ in path
                        current.IdentityFile = value.StartsWith("~/")
                            ? Path.Combine(
                                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                                value[2..])
                            : value;
                        break;
                }
            }
        }

        return entries;
    }

    private static (string? key, string? value) SplitDirective(string line)
    {
        // SSH config allows "Key Value" or "Key=Value"
        var eqIdx = line.IndexOf('=');
        var spIdx = line.IndexOf(' ');

        int splitAt;
        if (eqIdx >= 0 && (spIdx < 0 || eqIdx < spIdx))
            splitAt = eqIdx;
        else if (spIdx >= 0)
            splitAt = spIdx;
        else
            return (null, null);

        var key = line[..splitAt].Trim();
        var value = line[(splitAt + 1)..].Trim();

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
            return (null, null);

        return (key, value);
    }
}

public class SshConfigEntry
{
    public string Alias { get; set; } = string.Empty;
    public string? HostName { get; set; }
    public string? User { get; set; }
    public int? Port { get; set; }
    public string? IdentityFile { get; set; }

    public string DisplayLabel => $"{Alias} ({HostName ?? Alias})";

    public ConnectionProfile ToConnectionProfile() => new()
    {
        Label = Alias,
        SshHost = HostName ?? Alias,
        SshUser = User ?? Environment.UserName,
        SshPort = Port ?? 22,
        SshKeyPath = IdentityFile,
    };
}
