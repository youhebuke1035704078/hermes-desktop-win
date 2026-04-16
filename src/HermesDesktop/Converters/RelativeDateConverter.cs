using System.Globalization;
using System.Windows.Data;
using HermesDesktop.Helpers;

namespace HermesDesktop.Converters;

public class RelativeDateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        return RelativeDateHelper.ToRelativeDate(value);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
