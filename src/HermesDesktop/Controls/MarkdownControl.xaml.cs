using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using Microsoft.Web.WebView2.Core;

namespace HermesDesktop.Controls;

public partial class MarkdownControl : UserControl
{
    private bool _isReady;
    private string? _pendingContent;
    private readonly ILogger? _logger;

    public static readonly DependencyProperty MarkdownTextProperty =
        DependencyProperty.Register(nameof(MarkdownText), typeof(string), typeof(MarkdownControl),
            new PropertyMetadata(null, OnMarkdownTextChanged));

    public string? MarkdownText
    {
        get => (string?)GetValue(MarkdownTextProperty);
        set => SetValue(MarkdownTextProperty, value);
    }

    public MarkdownControl()
    {
        InitializeComponent();
    }

    public MarkdownControl(ILogger logger) : this()
    {
        _logger = logger;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_isReady) return;

        try
        {
            await MarkdownWebView.EnsureCoreWebView2Async();
            ConfigureWebView();

            var assetsPath = FindAssetsPath("Markdown");
            if (assetsPath != null)
            {
                MarkdownWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "hermes.markdown", assetsPath,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
            }

            MarkdownWebView.CoreWebView2.NavigationStarting += (_, args) =>
            {
                if (!args.Uri.StartsWith("https://hermes.markdown/", StringComparison.OrdinalIgnoreCase))
                {
                    args.Cancel = true;
                }
            };
            MarkdownWebView.CoreWebView2.NewWindowRequested += (_, args) =>
            {
                args.Handled = true;
            };

            MarkdownWebView.CoreWebView2.NavigationCompleted += (_, _) =>
            {
                _isReady = true;
                if (_pendingContent != null)
                    RenderContent(_pendingContent);
            };

            MarkdownWebView.CoreWebView2.Navigate("https://hermes.markdown/markdown.html");
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to initialize WebView2 for markdown");
        }
    }

    private static void OnMarkdownTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MarkdownControl control)
        {
            var text = e.NewValue as string;
            if (control._isReady)
                control.RenderContent(text);
            else
                control._pendingContent = text;
        }
    }

    private void RenderContent(string? content)
    {
        if (string.IsNullOrEmpty(content) || MarkdownWebView.CoreWebView2 == null)
            return;

        var base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(content));
        var isDark = Helpers.ThemeManager.IsSystemDarkMode() ? "true" : "false";
        _ = MarkdownWebView.CoreWebView2.ExecuteScriptAsync(
            $"renderMarkdown('{base64}', {isDark})");
    }

    private void ConfigureWebView()
    {
        var settings = MarkdownWebView.CoreWebView2.Settings;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsWebMessageEnabled = false;
        settings.AreHostObjectsAllowed = false;
    }

    private static string? FindAssetsPath(string subfolder)
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        for (int i = 0; i < 6; i++)
        {
            var candidate = Path.Combine(dir, "Assets", subfolder);
            if (Directory.Exists(candidate))
                return candidate;
            var parent = Directory.GetParent(dir);
            if (parent == null) break;
            dir = parent.FullName;
        }
        return null;
    }
}
