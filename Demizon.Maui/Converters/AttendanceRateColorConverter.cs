using System.Globalization;

namespace Demizon.Maui.Converters;

/// <summary>
/// Converts an attendance rate (0-100 double) to a color:
/// ≥80 → green, ≥50 → yellow, &lt;50 → red.
/// </summary>
public class AttendanceRateColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            double rate when rate >= 80 => "AttendanceGreen",
            double rate when rate >= 50 => "AttendanceYellow",
            double => "AttendanceRed",
            _ => "AttendanceGray"
        };

        if (Application.Current?.Resources.TryGetValue(resourceKey, out var color) == true && color is Color c)
            return c;

        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
