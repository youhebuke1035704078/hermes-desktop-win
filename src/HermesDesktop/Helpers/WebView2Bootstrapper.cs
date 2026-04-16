using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace HermesDesktop.Helpers;

public static class WebView2Bootstrapper
{
    private const string BootstrapperUrl = "https://go.microsoft.com/fwlink/p/?LinkId=2124703";

    public static async Task<bool> EnsureInstalledAsync()
    {
        if (IsInstalled())
            return true;

        Log.Warning("WebView2 Runtime not detected — prompting for install");

        var result = MessageBox.Show(
            "Hermes Desktop requires the Microsoft WebView2 Runtime, which is not installed on this machine.\n\n" +
            "Would you like to install it now? (requires internet connection)",
            "WebView2 Runtime Required",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information);

        if (result != MessageBoxResult.Yes)
            return false;

        var success = await DownloadAndInstallAsync();

        if (success && IsInstalled())
        {
            Log.Information("WebView2 Runtime installed successfully");
            return true;
        }

        MessageBox.Show(
            "WebView2 Runtime installation failed.\n\n" +
            "You can install it manually from:\nhttps://developer.microsoft.com/en-us/microsoft-edge/webview2/",
            "Installation Failed",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        return false;
    }

    private static bool IsInstalled()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            Log.Debug("WebView2 Runtime detected: {Version}", version);
            return !string.IsNullOrEmpty(version);
        }
        catch
        {
            return false;
        }
    }

    private static async Task<bool> DownloadAndInstallAsync()
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");

        try
        {
            // Show progress window
            var progressWindow = new Window
            {
                Title = "Hermes Desktop",
                Width = 400,
                Height = 120,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.ToolWindow,
                Content = new System.Windows.Controls.TextBlock
                {
                    Text = "Downloading and installing WebView2 Runtime...\nThis may take a minute.",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    FontSize = 14
                }
            };
            progressWindow.Show();

            try
            {
                // Download bootstrapper
                Log.Information("Downloading WebView2 bootstrapper to {Path}", tempPath);
                using var http = new HttpClient();
                var bytes = await http.GetByteArrayAsync(BootstrapperUrl);
                await File.WriteAllBytesAsync(tempPath, bytes);

                // Run silent install
                Log.Information("Running WebView2 bootstrapper (silent install)");
                var psi = new ProcessStartInfo
                {
                    FileName = tempPath,
                    Arguments = "/silent /install",
                    UseShellExecute = true,
                    Verb = "runas"
                };

                var process = Process.Start(psi);
                if (process == null)
                {
                    Log.Error("Failed to start WebView2 bootstrapper process");
                    return false;
                }

                await process.WaitForExitAsync();
                Log.Information("WebView2 bootstrapper exited with code {ExitCode}", process.ExitCode);
                return process.ExitCode == 0;
            }
            finally
            {
                progressWindow.Close();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "WebView2 bootstrapper download/install failed");
            return false;
        }
        finally
        {
            try { File.Delete(tempPath); } catch { }
        }
    }
}
