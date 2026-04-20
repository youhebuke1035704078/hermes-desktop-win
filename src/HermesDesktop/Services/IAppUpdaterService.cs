namespace HermesDesktop.Services;

public enum UpdaterState
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Downloading,
    Downloaded,
    Installing,
    Error,
}

/// <summary>
/// Mirrors the OpenClaw Desktop (Electron) auto-update UX:
///   1. Auto-checks GitHub Releases on startup.
///   2. If a newer tag exists, auto-downloads the single-file <c>HermesDesktop.exe</c> asset.
///   3. Notifies the UI via <see cref="StateChanged"/> at every transition so a badge
///      can show "正在检查更新 / 发现新版本 / 下载中 N% / 就绪 / Ns 后安装".
///   4. <see cref="InstallAndRestart"/> spawns a tiny cmd shim that waits for this
///      process to exit, swaps the on-disk exe with the downloaded one, and
///      relaunches. No admin elevation is required because the exe lives under the
///      user's own install path.
/// </summary>
public interface IAppUpdaterService
{
    UpdaterState State { get; }
    string CurrentVersion { get; }
    string? AvailableVersion { get; }
    double DownloadPercent { get; }
    string? ErrorMessage { get; }

    event EventHandler? StateChanged;

    Task CheckForUpdatesAsync(CancellationToken ct = default);
    Task DownloadUpdateAsync(CancellationToken ct = default);
    void InstallAndRestart();
}
