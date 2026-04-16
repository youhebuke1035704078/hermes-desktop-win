# Development Guide

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Windows 10 or later
- WebView2 runtime (comes with Edge)
- An SSH-accessible host with Hermes installed (for testing)

## Getting started

```bash
git clone <repo-url>
cd hermes-desktop-win

# Restore and build
dotnet build

# Run in debug
dotnet run --project src/HermesDesktop
```

## Project layout

| Directory | Contents |
|-----------|----------|
| `src/HermesDesktop/Models/` | Data models (`ConnectionProfile`, `Session`, etc.) |
| `src/HermesDesktop/Services/` | Business logic and SSH communication |
| `src/HermesDesktop/ViewModels/` | MVVM ViewModels with CommunityToolkit.Mvvm |
| `src/HermesDesktop/Views/` | XAML views |
| `src/HermesDesktop/Controls/` | Reusable WPF controls (terminal, markdown, chart) |
| `src/HermesDesktop/Scripts/` | Python scripts embedded as resources |
| `src/HermesDesktop/Assets/` | Client-side JS/HTML/CSS for WebView2 |
| `src/HermesDesktop/Resources/` | Theme dictionaries and app icon |
| `src/HermesDesktop/Helpers/` | Static utility classes |
| `src/HermesDesktop/Converters/` | WPF value converters for XAML bindings |
| `build/` | Build and publish scripts |
| `docs/` | Documentation |

## Adding a new view

1. Create a ViewModel class in `ViewModels/` inheriting `ObservableObject`.
2. Create a XAML UserControl in `Views/`.
3. Add a `DataTemplate` mapping in `App.xaml`.
4. Register the ViewModel as transient in `App.xaml.cs`.
5. Add a `NavigationSection` enum value and wire it in `MainViewModel.NavigateToSection()`.

## Adding a new remote script

1. Write the Python script in `Scripts/`. It receives a `payload` dict and must print JSON with `"ok": true/false`.
2. The `.csproj` has `<EmbeddedResource Include="Scripts\*.py" />` so new `.py` files are automatically embedded.
3. Call it from a service via `IRemoteScriptExecutor.ExecuteAsync<T>(profile, "script_name.py", parameters)`.

## MVVM patterns

This project uses [CommunityToolkit.Mvvm](https://learn.microsoft.com/en-us/dotnet/communitytoolkit/mvvm/) with source generators:

```csharp
public partial class MyViewModel : ObservableObject
{
    [ObservableProperty]                    // Generates property + INotifyPropertyChanged
    private string _myField = "";

    [RelayCommand]                          // Generates IRelayCommand property
    private async Task DoSomethingAsync()
    {
        // ...
    }

    partial void OnMyFieldChanged(string value)  // Auto-called on change
    {
        // React to property change
    }
}
```

## SSH testing

To test against a local SSH server:

```bash
# Windows OpenSSH server (if enabled)
ssh localhost

# Or use WSL
ssh user@localhost -p 22
```

Create a connection profile pointing to `localhost` with your username.

## Logs

Debug logs go to `%APPDATA%\HermesDesktop\logs\`. Check these when troubleshooting SSH or script issues:

```bash
# PowerShell
Get-Content "$env:APPDATA\HermesDesktop\logs\hermes-*.log" -Tail 50
```

## Publishing a release

```powershell
.\build\publish.ps1

# Output in ./publish/
# - HermesDesktop.exe (self-contained, ~73 MB)
# - Assets/Terminal/ (xterm.js files)
# - Assets/Markdown/ (marked.js)
```

To create a distributable ZIP:

```powershell
Compress-Archive -Path ./publish/* -DestinationPath ./HermesDesktop-win-x64.zip
```
