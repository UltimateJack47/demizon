using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Demizon.Dal.Extensions;

public static class UtcDateAnnotation
{
    private const string IsUtcAnnotation = "IsUtc";

    private static readonly ValueConverter<DateTime, DateTime> UtcConverter =
        new(convertTo => DateTime.SpecifyKind(convertTo, DateTimeKind.Utc), convertFrom => convertFrom);

    public static PropertyBuilder<TProperty> IsUtc<TProperty>(this PropertyBuilder<TProperty> builder, bool isUtc = true) =>
        builder.HasAnnotation(IsUtcAnnotation, isUtc);

    private static bool IsUtc(this IReadOnlyPropertyBase? property)
    {
        if (property == null || property.PropertyInfo == null) return true;
        var attribute = property.PropertyInfo.GetCustomAttribute<IsUtcAttribute>();
        if (attribute is not null && attribute.IsUtc)
        {
            return true;
        }

        return ((bool?)property.FindAnnotation(IsUtcAnnotation)?.Value) ?? true;

    }

    /// <summary>
    /// Make sure this is called after configuring all your entities.
    /// </summary>
    public static void ApplyUtcDateTimeConverter(this ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                if (!property.IsUtc())
                {
                    continue;
                }

                if (property.ClrType == typeof(DateTime) ||
                    property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(UtcConverter);
                }
            }
        }
    }
}

public class IsUtcAttribute : Attribute
{
    public IsUtcAttribute(bool isUtc = true) => this.IsUtc = isUtc;
    public bool IsUtc { get; }
}
