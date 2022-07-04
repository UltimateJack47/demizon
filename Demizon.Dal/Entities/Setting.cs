using System.Globalization;

namespace Demizon.Dal.Entities;

public class Setting
{
    public int Id { get; set; }
    
    public SettingKey Key { get; set; }

    public string Value { get; set; } = null!;

    public bool IsPublic { get; set; } = false;

    public static implicit operator string(Setting setting) => setting.Value;

    public static implicit operator int(Setting setting) =>
        int.Parse(setting.Value, CultureInfo.CreateSpecificCulture("en-US"));

    public static implicit operator double(Setting setting) =>
        double.Parse(setting.Value, CultureInfo.CreateSpecificCulture("en-US"));

    public static implicit operator long(Setting setting) =>
        long.Parse(setting.Value, CultureInfo.CreateSpecificCulture("en-US"));

    public static implicit operator decimal(Setting setting) =>
        decimal.Parse(setting.Value, CultureInfo.CreateSpecificCulture("en-US"));
}

public enum SettingKey
{
    DevelopedBy
}
