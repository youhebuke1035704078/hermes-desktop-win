using System.Windows;
using Microsoft.Win32;

namespace HermesDesktop.Helpers;

public static class ThemeManager
{
    private const string RegistryKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string RegistryValueName = "AppsUseLightTheme";

    public static bool IsSystemDarkMode()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            var value = key?.GetValue(RegistryValueName);
            return value is int intVal && intVal == 0;
        }
        catch
        {
            return false;
        }
    }

    public static void ApplyTheme(bool dark)
    {
        var dict = new ResourceDictionary
        {
            Source = new Uri(dark
                ? "pack://application:,,,/Resources/DarkTheme.xaml"
                : "pack://application:,,,/Resources/LightTheme.xaml")
        };

        var app = Application.Current;

        // Remove existing theme dictionaries
        var toRemove = app.Resources.MergedDictionaries
            .Where(d => d.Source?.ToString().Contains("Theme.xaml") == true)
            .ToList();
        foreach (var d in toRemove)
            app.Resources.MergedDictionaries.Remove(d);

        app.Resources.MergedDictionaries.Add(dict);
    }

    public static void ApplySystemTheme()
    {
        ApplyTheme(IsSystemDarkMode());
    }
}
