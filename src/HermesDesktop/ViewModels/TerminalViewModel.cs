using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HermesDesktop.Controls;
using HermesDesktop.Models;
using HermesDesktop.Services;
using Microsoft.Extensions.Logging;

namespace HermesDesktop.ViewModels;

public partial class TerminalViewModel : ObservableObject
{
    private readonly ISshTransport _sshTransport;
    private readonly MainViewModel _mainVm;
    private readonly ILogger<TerminalViewModel> _logger;

    [ObservableProperty]
    private ObservableCollection<TerminalTabViewModel> _tabs = new();

    [ObservableProperty]
    private TerminalTabViewModel? _activeTab;

    [ObservableProperty]
    private string? _errorMessage;

    public TerminalViewModel(
        ISshTransport sshTransport,
        MainViewModel mainVm,
        ILogger<TerminalViewModel> logger)
    {
        _sshTransport = sshTransport;
        _mainVm = mainVm;
        _logger = logger;
    }

    partial void OnActiveTabChanged(TerminalTabViewModel? value)
    {
        foreach (var tab in Tabs)
            tab.IsActive = tab == value;
    }

    [RelayCommand]
    private async Task NewTabAsync()
    {
        if (_mainVm.ActiveConnection == null)
        {
            ErrorMessage = "当前没有活动连接。";
            return;
        }

        try
        {
            ErrorMessage = null;
            var session = await _sshTransport.OpenShellAsync(
                _mainVm.ActiveConnection, 120, 40);

            var tab = new TerminalTabViewModel(session,
                _mainVm.ActiveConnection.Label ?? $"终端 {Tabs.Count + 1}",
                _mainVm.ActiveConnection,
                _sshTransport);
            Tabs.Add(tab);
            ActiveTab = tab;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"打开终端失败：{ex.Message}";
            _logger.LogError(ex, "Failed to open terminal tab");
        }
    }

    [RelayCommand]
    private void CloseTab(TerminalTabViewModel tab)
    {
        tab.Dispose();
        Tabs.Remove(tab);
        if (ActiveTab == tab)
            ActiveTab = Tabs.LastOrDefault();
    }
}

public partial class TerminalTabViewModel : ObservableObject, IDisposable
{
    private readonly ConnectionProfile _profile;
    private readonly ISshTransport _sshTransport;

    public ShellStreamSession Session { get; private set; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isDisconnected;

    [ObservableProperty]
    private int? _exitCode;

    [ObservableProperty]
    private bool _isReconnecting;

    public TerminalControl? TerminalControl { get; set; }

    public string StatusText
    {
        get
        {
            if (IsReconnecting) return "正在重新连接...";
            if (ExitCode.HasValue) return $"Shell 已退出（代码 {ExitCode.Value}）";
            if (IsDisconnected) return "连接已断开";
            return "";
        }
    }

    public TerminalTabViewModel(ShellStreamSession session, string title,
        ConnectionProfile profile, ISshTransport sshTransport)
    {
        Session = session;
        _title = title;
        _profile = profile;
        _sshTransport = sshTransport;

        // Monitor connection health
        _ = MonitorConnectionAsync();
    }

    private async Task MonitorConnectionAsync()
    {
        while (!IsDisconnected)
        {
            await Task.Delay(2000);
            try
            {
                if (Session.Client == null || !Session.Client.IsConnected)
                {
                    IsDisconnected = true;
                    OnPropertyChanged(nameof(StatusText));
                    break;
                }
            }
            catch
            {
                IsDisconnected = true;
                OnPropertyChanged(nameof(StatusText));
                break;
            }
        }
    }

    [RelayCommand]
    private async Task ReconnectAsync()
    {
        try
        {
            IsReconnecting = true;
            OnPropertyChanged(nameof(StatusText));

            // Dispose old session
            try { Session.Stream?.Dispose(); } catch { }
            try { Session.Client?.Dispose(); } catch { }

            // Open new session
            var newSession = await _sshTransport.OpenShellAsync(_profile, 120, 40);
            Session = newSession;
            IsDisconnected = false;
            ExitCode = null;

            // Re-attach to the terminal control
            TerminalControl?.AttachSession(newSession.Stream, newSession.Client);

            OnPropertyChanged(nameof(StatusText));
        }
        catch (Exception)
        {
            IsDisconnected = true;
            OnPropertyChanged(nameof(StatusText));
        }
        finally
        {
            IsReconnecting = false;
            OnPropertyChanged(nameof(StatusText));
        }
    }

    public void Dispose()
    {
        IsDisconnected = true;
        TerminalControl?.Dispose();
        try { Session.Stream?.Dispose(); } catch { }
        try { Session.Client?.Dispose(); } catch { }
    }
}
