using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.Services;

/// <summary>
/// GitHub Releases-backed self-updater. Intentionally dependency-free (plain
/// <see cref="HttpClient"/> + <see cref="JsonDocument"/>) so the single-file
/// publish stays small.
/// </summary>
public sealed class AppUpdaterService : IAppUpdaterService, IDisposable
{
    private const string RepoOwner = "youhebuke1035704078";
    private const string RepoName = "hermes-desktop-win";
    private const string ReleaseAssetName = "HermesDesktop.exe";
    private const string UserAgent = "HermesDesktop-Updater";

    private readonly HttpClient _http;
    private readonly ILogger<AppUpdaterService> _logger;

    private UpdaterState _state = UpdaterState.Idle;
    private string? _availableVersion;
    private double _downloadPercent;
    private string? _errorMessage;
    private string? _assetDownloadUrl;
    private string? _downloadedFilePath;

    public AppUpdaterService(ILogger<AppUpdaterService> logger)
    {
        _logger = logger;
        _http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        CurrentVersion = ResolveCurrentVersion();
    }

    public UpdaterState State
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string CurrentVersion { get; }

    public string? AvailableVersion
    {
        get => _availableVersion;
        private set
        {
            if (_availableVersion == value) return;
            _availableVersion = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public double DownloadPercent
    {
        get => _downloadPercent;
        private set
        {
            if (Math.Abs(_downloadPercent - value) < 0.5) return; // dedupe noise
            _downloadPercent = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string? ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            StateChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public event EventHandler? StateChanged;

    public async Task CheckForUpdatesAsync(CancellationToken ct = default)
    {
        if (State == UpdaterState.Checking
            || State == UpdaterState.Downloading
            || State == UpdaterState.Downloaded
            || State == UpdaterState.Installing)
        {
            return;
        }

        ErrorMessage = null;
        State = UpdaterState.Checking;

        try
        {
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct).ConfigureAwait(false);

            var tagName = doc.RootElement.TryGetProperty("tag_name", out var tagEl)
                ? tagEl.GetString() ?? string.Empty
                : string.Empty;
            if (string.IsNullOrWhiteSpace(tagName))
            {
                State = UpdaterState.UpToDate;
                return;
            }

            var latestVer = NormalizeVersion(tagName);
            var currentVer = NormalizeVersion(CurrentVersion);

            if (!IsNewer(latestVer, currentVer))
            {
                AvailableVersion = null;
                State = UpdaterState.UpToDate;
                return;
            }

            // Find the HermesDesktop.exe asset
            _assetDownloadUrl = null;
            if (doc.RootElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    var name = asset.TryGetProperty("name", out var n) ? n.GetString() : null;
                    if (string.Equals(name, ReleaseAssetName, StringComparison.OrdinalIgnoreCase))
                    {
                        _assetDownloadUrl = asset.TryGetProperty("browser_download_url", out var u)
                            ? u.GetString()
                            : null;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(_assetDownloadUrl))
            {
                // Newer release exists but no usable asset — fall back to "up-to-date"
                // rather than erroring out, since the user can't do anything about it.
                _logger.LogWarning("Release {Tag} has no {Asset} asset; skipping", tagName, ReleaseAssetName);
                State = UpdaterState.UpToDate;
                return;
            }

            AvailableVersion = tagName.TrimStart('v', 'V');
            State = UpdaterState.UpdateAvailable;
        }
        catch (OperationCanceledException)
        {
            State = UpdaterState.Idle;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check failed");
            ErrorMessage = ex.Message;
            State = UpdaterState.Error;
        }
    }

    public async Task DownloadUpdateAsync(CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_assetDownloadUrl))
        {
            ErrorMessage = "没有可下载的更新";
            State = UpdaterState.Error;
            return;
        }
        if (State == UpdaterState.Downloading || State == UpdaterState.Downloaded)
        {
            return;
        }

        DownloadPercent = 0;
        ErrorMessage = null;
        State = UpdaterState.Downloading;

        try
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "HermesDesktop-update");
            Directory.CreateDirectory(tempDir);
            var targetPath = Path.Combine(
                tempDir,
                $"HermesDesktop-{AvailableVersion ?? Guid.NewGuid().ToString("N")}.exe");

            using var req = new HttpRequestMessage(HttpMethod.Get, _assetDownloadUrl);
            using var resp = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct)
                .ConfigureAwait(false);
            resp.EnsureSuccessStatusCode();

            var total = resp.Content.Headers.ContentLength ?? -1L;

            await using (var src = await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false))
            await using (var dst = File.Create(targetPath))
            {
                var buffer = new byte[64 * 1024];
                long read = 0;
                int n;
                while ((n = await src.ReadAsync(buffer, ct).ConfigureAwait(false)) > 0)
                {
                    await dst.WriteAsync(buffer.AsMemory(0, n), ct).ConfigureAwait(false);
                    read += n;
                    if (total > 0)
                    {
                        DownloadPercent = read * 100.0 / total;
                    }
                }
            }

            DownloadPercent = 100;
            _downloadedFilePath = targetPath;
            State = UpdaterState.Downloaded;
        }
        catch (OperationCanceledException)
        {
            State = UpdaterState.UpdateAvailable;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update download failed");
            ErrorMessage = ex.Message;
            State = UpdaterState.Error;
        }
    }

    public void InstallAndRestart()
    {
        if (State != UpdaterState.Downloaded
            || string.IsNullOrWhiteSpace(_downloadedFilePath)
            || !File.Exists(_downloadedFilePath))
        {
            return;
        }

        State = UpdaterState.Installing;

        try
        {
            var currentExe = GetCurrentExecutablePath();
            if (string.IsNullOrWhiteSpace(currentExe))
            {
                ErrorMessage = "无法定位当前可执行文件";
                State = UpdaterState.Error;
                return;
            }

            var pid = Environment.ProcessId;
            var scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"HermesDesktop-install-{pid}.cmd");

            // The shim: wait for our PID to exit, swap the exe, relaunch, delete itself.
            // Uses `move /y` which atomically replaces on NTFS once the old handle is
            // released. If the move fails (e.g. the app hasn't fully exited yet) we
            // retry up to 30 × 1s before giving up.
            var script = $@"@echo off
setlocal
set ""PID={pid}""
set ""SRC={_downloadedFilePath}""
set ""DST={currentExe}""

:wait
tasklist /FI ""PID eq %PID%"" 2>nul | find ""%PID%"" >nul
if not errorlevel 1 (
    timeout /t 1 /nobreak >nul
    goto wait
)

set /a tries=0
:retry
move /y ""%SRC%"" ""%DST%"" >nul
if errorlevel 1 (
    set /a tries+=1
    if %tries% lss 30 (
        timeout /t 1 /nobreak >nul
        goto retry
    )
    exit /b 1
)

start """" ""%DST%""
del /q ""%~f0""
endlocal
";
            File.WriteAllText(scriptPath, script);

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"{scriptPath}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };
            Process.Start(psi);

            // Give the shim a beat to enter its wait-loop before we exit.
            Thread.Sleep(300);

            Application.Current?.Dispatcher.InvokeAsync(() => Application.Current.Shutdown());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install-and-restart failed");
            ErrorMessage = ex.Message;
            State = UpdaterState.Error;
        }
    }

    private static string ResolveCurrentVersion()
    {
        // Prefer AssemblyInformationalVersion — the release workflow passes the
        // tag in via `dotnet publish -p:Version=<tag>`, which populates this
        // attribute. Fall back to the assembly version (1.0.0.0 by default) so
        // dev runs don't explode if the attribute is missing.
        var asm = Assembly.GetExecutingAssembly();
        var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(info))
        {
            // Strip any "+<commit-hash>" suffix MSBuild sometimes appends.
            var plus = info.IndexOf('+');
            return plus >= 0 ? info[..plus] : info;
        }
        return asm.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string GetCurrentExecutablePath()
    {
        var p = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(p)) return p;
        using var proc = Process.GetCurrentProcess();
        return proc.MainModule?.FileName ?? string.Empty;
    }

    /// <summary>
    /// Parses release tags like <c>v2026.04.16.1</c>, <c>1.2.0</c>, or
    /// <c>2026.4.16.1</c> into a <see cref="Version"/>. Unknown formats become
    /// <c>0.0.0.0</c> so they never beat the current version.
    /// </summary>
    private static Version NormalizeVersion(string raw)
    {
        var s = raw.Trim().TrimStart('v', 'V');
        // Version wants exactly major.minor[.build[.revision]]; be lenient
        var parts = s.Split('.');
        if (parts.Length < 2) return new Version(0, 0, 0, 0);
        var ints = new int[4];
        for (var i = 0; i < Math.Min(parts.Length, 4); i++)
        {
            if (!int.TryParse(parts[i], out ints[i])) return new Version(0, 0, 0, 0);
        }
        return new Version(ints[0], ints[1], ints[2], ints[3]);
    }

    private static bool IsNewer(Version latest, Version current) => latest.CompareTo(current) > 0;

    public void Dispose()
    {
        _http.Dispose();
    }
}
