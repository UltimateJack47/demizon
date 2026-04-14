using System.Globalization;
using Demizon.Contracts.Attendances;

namespace Demizon.Maui.Converters;

public class AttendanceColorConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string resourceKey = value switch
        {
            AttendanceDto { Attends: true } => "AttendanceGreen",
            AttendanceDto { Attends: false } => "AttendanceRed",
            _ => "AttendanceGray"
        };

        if (Application.Current?.Resources.TryGetValue(resourceKey, out var color) == true && color is Color c)
            return c;

        return Colors.Gray;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
