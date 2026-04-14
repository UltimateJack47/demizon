using System.Globalization;

namespace Demizon.Maui.Converters;

public class DateFormatConverter : IValueConverter
{
    private static readonly CultureInfo CzechCulture = new("cs-CZ");

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value switch
        {
            DateTime dt => dt.ToString("dd. MMMM yyyy, HH:mm", CzechCulture),
            DateTimeOffset dto => dto.ToString("dd. MMMM yyyy, HH:mm", CzechCulture),
            _ => string.Empty
        };
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => null;
}
