using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;
using Renci.SshNet;

namespace HermesDesktop.Controls;

public partial class TerminalControl : UserControl, IDisposable
{
    private ShellStream? _shellStream;
    private SshClient? _sshClient;
    private CancellationTokenSource? _readCts;
    private bool _isInitialized;
    private readonly ILogger? _logger;

    public TerminalControl()
    {
        InitializeComponent();
    }

    public TerminalControl(ILogger logger) : this()
    {
        _logger = logger;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;

        try
        {
            await TerminalWebView.EnsureCoreWebView2Async();

            // Map local assets folder to a virtual host
            var assetsPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Assets", "Terminal");

            // If running from source, assets might be in the project directory
            if (!Directory.Exists(assetsPath))
            {
                assetsPath = FindAssetsPath();
            }

            if (assetsPath != null && Directory.Exists(assetsPath))
            {
                TerminalWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "hermes.terminal",
                    assetsPath,
                    CoreWebView2HostResourceAccessKind.Allow);
            }

            // Handle messages from xterm.js
            TerminalWebView.CoreWebView2.WebMessageReceived += OnWebMessageReceived;

            // Navigate to the terminal page
            TerminalWebView.CoreWebView2.Navigate("https://hermes.terminal/terminal.html");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize WebView2 for terminal");
        }
    }

    public void AttachSession(ShellStream shellStream, SshClient sshClient)
    {
        _shellStream = shellStream;
        _sshClient = sshClient;
        StartReading();
    }

    private void StartReading()
    {
        if (_shellStream == null) return;

        _readCts = new CancellationTokenSource();
        _ = ReadLoopAsync(_readCts.Token);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        var buffer = new byte[8192];
        try
        {
            while (!ct.IsCancellationRequested && _shellStream != null)
            {
                int bytesRead;
                try
                {
                    bytesRead = await _shellStream.ReadAsync(buffer, 0, buffer.Length, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Terminal read error");
                    break;
                }

                if (bytesRead <= 0)
                {
                    await Task.Delay(10, ct);
                    continue;
                }

                var data = new byte[bytesRead];
                Array.Copy(buffer, data, bytesRead);
                var base64 = Convert.ToBase64String(data);

                // Marshal to UI thread for WebView2 interop
                await Dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        if (TerminalWebView.CoreWebView2 != null)
                        {
                            // Escape the base64 string for JS
                            var escaped = base64.Replace("\\", "\\\\").Replace("'", "\\'");
                            await TerminalWebView.CoreWebView2.ExecuteScriptAsync(
                                $"terminalWrite('{escaped}')");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogDebug(ex, "Failed to write to terminal WebView");
                    }
                });
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
    }

    private async void OnWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var type = root.GetProperty("type").GetString();

            switch (type)
            {
                case "input":
                    var inputData = root.GetProperty("data").GetString();
                    if (inputData != null && _shellStream != null)
                    {
                        var bytes = Convert.FromBase64String(inputData);
                        await _shellStream.WriteAsync(bytes, 0, bytes.Length);
                        await _shellStream.FlushAsync();
                    }
                    break;

                case "resize":
                    var cols = root.GetProperty("cols").GetUInt32();
                    var rows = root.GetProperty("rows").GetUInt32();
                    SendWindowResize(cols, rows);
                    break;

                case "ready":
                    // Terminal is initialized, focus it
                    await TerminalWebView.CoreWebView2.ExecuteScriptAsync("terminalFocus()");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error handling terminal web message");
        }
    }

    private void SendWindowResize(uint cols, uint rows)
    {
        // SSH.NET 2025: SendWindowChangeRequest is on the Channel, not ShellStream.
        // Access it via reflection since there's no public API on ShellStream.
        try
        {
            if (_shellStream == null) return;
            var channelField = typeof(ShellStream).GetField("_channel",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var channel = channelField?.GetValue(_shellStream);
            if (channel != null)
            {
                var method = channel.GetType().GetMethod("SendWindowChangeRequest");
                method?.Invoke(channel, new object[] { cols, rows, 0u, 0u });
            }
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex, "Failed to send window change request");
        }
    }

    public new void Focus()
    {
        // Focus the WPF WebView2 control so it receives keyboard input
        TerminalWebView.Focus();
        Keyboard.Focus(TerminalWebView);

        // Also focus xterm.js inside the WebView2
        if (TerminalWebView.CoreWebView2 != null)
        {
            _ = TerminalWebView.CoreWebView2.ExecuteScriptAsync("terminalFocus()");
        }
    }

    private static string? FindAssetsPath()
    {
        // Walk up from the current directory looking for the Assets/Terminal folder
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, "Assets", "Terminal");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "terminal.html")))
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }

    public void Dispose()
    {
        _readCts?.Cancel();
        _readCts?.Dispose();

        try { _shellStream?.Dispose(); } catch { }
        try { _sshClient?.Dispose(); } catch { }

        _shellStream = null;
        _sshClient = null;
    }
}
