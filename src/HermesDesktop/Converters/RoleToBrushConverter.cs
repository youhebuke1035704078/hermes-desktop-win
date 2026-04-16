using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HermesDesktop.Converters;

/// <summary>
/// Maps a chat message role string to the corresponding theme brush.
/// Used because WPF DataTrigger setters cannot use DynamicResource directly.
/// </summary>
public class RoleToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var role = value as string ?? "";
        var key = role.ToLowerInvariant() switch
        {
            "user" => "BubbleUserBackground",
            "assistant" => "BubbleAssistantBackground",
            _ => "BubbleSystemBackground"
        };
        return Application.Current.FindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Maps a boolean IsActive to terminal tab background brush.
/// </summary>
public class TabActiveToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isActive = value is true;
        var key = isActive ? "TerminalTabActive" : "TerminalTabBackground";
        return Application.Current.FindResource(key) as Brush ?? Brushes.Transparent;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
