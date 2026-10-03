using System.Globalization;

namespace DayPlannio.App.Converters;

public class IgualStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.Equals(
            value?.ToString(),
            parameter?.ToString(),
            StringComparison.Ordinal);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true)
            return parameter?.ToString();

        return Binding.DoNothing;
    }
}