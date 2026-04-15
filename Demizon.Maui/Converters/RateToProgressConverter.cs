using System.Globalization;

namespace Demizon.Maui.Converters;

/// <summary>
/// Converts a percentage value (0-100) to a progress value (0.0-1.0) for ProgressBar.
/// </summary>
public class RateToProgressConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is double rate)
            return Math.Clamp(rate / 100.0, 0.0, 1.0);
        return 0.0;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
