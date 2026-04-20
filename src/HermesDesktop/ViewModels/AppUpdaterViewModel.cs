using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesDesktop.Services;

namespace HermesDesktop.ViewModels;

/// <summary>
/// Mirrors the OpenClaw Desktop ConnectionStatus.vue flow: auto-check →
/// auto-download → show status badge → 3s countdown before auto-install.
/// </summary>
public partial class AppUpdaterViewModel : ObservableObject
{
    private readonly IAppUpdaterService _updater;
    private DispatcherTimer? _countdownTimer;

    [ObservableProperty]
    private UpdaterState _state = UpdaterState.Idle;

    [ObservableProperty]
    private string? _availableVersion;

    [ObservableProperty]
    private double _downloadPercent;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _installCountdown;

    [ObservableProperty]
    private bool _isFlyoutOpen;

    public string CurrentVersion => _updater.CurrentVersion;

    public string StatusText => State switch
    {
        UpdaterState.Checking => "正在检查更新...",
        UpdaterState.Downloading => $"下载中 {DownloadPercent:0}%",
        UpdaterState.Downloaded => InstallCountdown > 0
            ? $"{InstallCountdown}s 后安装"
            : "就绪",
        UpdaterState.Installing => "正在安装...",
        UpdaterState.UpdateAvailable => "发现新版本",
        UpdaterState.UpToDate => "已是最新",
        UpdaterState.Error => ErrorMessage is { Length: > 0 } ? $"错误：{ErrorMessage}" : "更新失败",
        _ => "检查更新",
    };

    public string BadgeText => $"桌面 v{CurrentVersion} · {StatusText}";

    public string BadgeType => State switch
    {
        UpdaterState.UpdateAvailable or UpdaterState.Downloading => "warning",
        UpdaterState.Downloaded or UpdaterState.Installing => "success",
        UpdaterState.Error => "error",
        _ => "default",
    };

    public bool IsChecking => State == UpdaterState.Checking;
    public bool IsUpdateAvailable => State == UpdaterState.UpdateAvailable;
    public bool IsDownloading => State == UpdaterState.Downloading;
    public bool IsDownloaded => State == UpdaterState.Downloaded;
    public bool ShowProgressBar => State == UpdaterState.Downloading || State == UpdaterState.Downloaded;
    public bool CanManualCheck => State is UpdaterState.Idle or UpdaterState.UpToDate or UpdaterState.Error;

    public string VersionLine => AvailableVersion is { Length: > 0 } av
        ? $"当前版本 v{CurrentVersion}  →  可升级到 v{av}"
        : $"当前版本 v{CurrentVersion}";

    public AppUpdaterViewModel(IAppUpdaterService updater)
    {
        _updater = updater;
        _updater.StateChanged += OnServiceStateChanged;
        SyncFromService(initial: true);
    }

    private void OnServiceStateChanged(object? sender, EventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            SyncFromService();
        }
        else
        {
            dispatcher.InvokeAsync(() => SyncFromService());
        }
    }

    private void SyncFromService(bool initial = false)
    {
        var previousState = State;

        State = _updater.State;
        AvailableVersion = _updater.AvailableVersion;
        DownloadPercent = _updater.DownloadPercent;
        ErrorMessage = _updater.ErrorMessage;

        // Derived props depend on these — kick the bindings manually because
        // StatusText / BadgeText are computed, not observable.
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(BadgeText));
        OnPropertyChanged(nameof(BadgeType));
        OnPropertyChanged(nameof(IsChecking));
        OnPropertyChanged(nameof(IsUpdateAvailable));
        OnPropertyChanged(nameof(IsDownloading));
        OnPropertyChanged(nameof(IsDownloaded));
        OnPropertyChanged(nameof(ShowProgressBar));
        OnPropertyChanged(nameof(CanManualCheck));
        OnPropertyChanged(nameof(VersionLine));

        if (initial) return;

        // Kick off auto-download when an update becomes available.
        if (State == UpdaterState.UpdateAvailable && previousState != UpdaterState.UpdateAvailable)
        {
            _ = _updater.DownloadUpdateAsync();
        }

        // Start the 3-second install countdown on fresh Downloaded transitions.
        if (State == UpdaterState.Downloaded && previousState != UpdaterState.Downloaded)
        {
            StartInstallCountdown();
        }
    }

    private void StartInstallCountdown()
    {
        StopCountdown();
        InstallCountdown = 3;
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(BadgeText));

        _countdownTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _countdownTimer.Tick += (_, _) =>
        {
            InstallCountdown--;
            OnPropertyChanged(nameof(StatusText));
            OnPropertyChanged(nameof(BadgeText));
            if (InstallCountdown <= 0)
            {
                StopCountdown();
                _updater.InstallAndRestart();
            }
        };
        _countdownTimer.Start();
    }

    private void StopCountdown()
    {
        if (_countdownTimer == null) return;
        _countdownTimer.Stop();
        _countdownTimer = null;
    }

    [RelayCommand(CanExecute = nameof(CanManualCheck))]
    private async Task CheckForUpdateAsync()
    {
        await _updater.CheckForUpdatesAsync();
    }

    [RelayCommand]
    private void InstallNow()
    {
        StopCountdown();
        InstallCountdown = 0;
        _updater.InstallAndRestart();
    }

    [RelayCommand]
    private void ToggleFlyout()
    {
        IsFlyoutOpen = !IsFlyoutOpen;
    }

    /// <summary>
    /// Fire an initial check 5s after startup — gives the app a chance to
    /// settle and matches the OpenClaw Desktop cadence.
    /// </summary>
    public async Task AutoCheckOnStartupAsync()
    {
        try
        {
            await Task.Delay(5000);
            await _updater.CheckForUpdatesAsync();
        }
        catch (Exception)
        {
            // swallow — auto-check failures shouldn't crash startup
        }
    }
}
