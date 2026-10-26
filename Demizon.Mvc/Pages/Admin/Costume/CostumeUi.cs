using System.Security.Claims;
using Demizon.Core.Services.Costume;
using Demizon.Dal.Entities;
using Demizon.Mvc.Extensions;
using Demizon.Mvc.Services.Authentication;
using MudBlazor;

namespace Demizon.Mvc.Pages.Admin.Costume;

/// <summary>Popisky a pravidla zobrazení krojárky sdílené stránkami.</summary>
public static class CostumeUi
{
    public static bool IsKeeper(this ClaimsPrincipal user) => user.IsInRole(RoleNames.CostumeKeeper);

    public static bool IsCostumeViewer(this ClaimsPrincipal user) =>
        user.IsKeeper() || user.IsInRole(RoleNames.Admin) || user.IsInRole(RoleNames.Leader);

    public static CostumeActor ToCostumeActor(this ClaimsPrincipal user) => new(user.GetMemberId(), user.IsKeeper());

    public static string Label(this CostumeItemState state) => state switch
    {
        CostumeItemState.Available => "dostupný",
        CostumeItemState.InMaintenance => "v údržbě",
        CostumeItemState.Lost => "ztracený",
        CostumeItemState.Retired => "vyřazený",
        _ => state.ToString()
    };

    public static Color Color(this CostumeItemState state) => state switch
    {
        CostumeItemState.Available => MudBlazor.Color.Success,
        CostumeItemState.InMaintenance => MudBlazor.Color.Warning,
        _ => MudBlazor.Color.Error
    };

    public static string Label(this CostumeGender gender) => gender switch
    {
        CostumeGender.Male => "muž",
        CostumeGender.Female => "žena",
        _ => "oboje"
    };

    public static string Label(this CostumeTrackingMode mode) =>
        mode == CostumeTrackingMode.Individual ? "po kusech (každý kus svůj QR)" : "počtem (šarže, jeden QR)";

    public static string Label(CostumeMaintenanceKind kind) => kind switch
    {
        CostumeMaintenanceKind.Washing => "praní",
        CostumeMaintenanceKind.Cleaning => "čištění",
        CostumeMaintenanceKind.Repair => "oprava",
        _ => "jiné"
    };

    public static string Label(this CostumeTransferStatus status) => status switch
    {
        CostumeTransferStatus.Draft => "rozpracovaná",
        CostumeTransferStatus.Pending => "čeká na potvrzení",
        CostumeTransferStatus.Confirmed => "potvrzená",
        CostumeTransferStatus.Rejected => "odmítnutá",
        CostumeTransferStatus.Cancelled => "zrušená",
        _ => status.ToString()
    };

    public static Color Color(this CostumeTransferStatus status) => status switch
    {
        CostumeTransferStatus.Pending => MudBlazor.Color.Warning,
        CostumeTransferStatus.Confirmed => MudBlazor.Color.Success,
        CostumeTransferStatus.Rejected => MudBlazor.Color.Error,
        _ => MudBlazor.Color.Default
    };

    /// <summary>„Kopanický · Vesta · vel. 4 · černá“ — co je to za kus, na jeden řádek.</summary>
    public static string Describe(this CostumeItem item)
    {
        var parts = new List<string?> { item.Part.Costume?.Name, item.Part.Name };
        if (!string.IsNullOrWhiteSpace(item.Size))
            parts.Add($"vel. {item.Size}");
        parts.Add(item.Color);
        return string.Join(" · ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public static string Party(int? memberId, IReadOnlyDictionary<int, CostumeMemberName> names) =>
        memberId is not { } id ? "Krojárka"
        : names.TryGetValue(id, out var name) ? name.IsDeleted ? $"{name.FullName} (bývalý člen)" : name.FullName
        : "Neznámý člen";

    /// <summary>
    /// Čas z DB je UTC bez <c>Kind</c> (SQLite). Zóna výslovně z <c>GoogleCalendar:TimeZone</c>:
    /// kontejner nemá nastavené <c>TZ</c>, takže <c>ToLocalTime()</c> by ukázal UTC.
    /// </summary>
    public static string Local(DateTime utc, string timeZoneId) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc),
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)).ToString("d. M. yyyy H:mm");
}
