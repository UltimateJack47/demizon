namespace Demizon.Maui;

/// <summary>
/// Jediný zdroj pravdy pro všechny Shell routy.
/// Absolutní routy (//…) se používají POUZE pro přepnutí autentizačního kontextu
/// (login ↔ main). Vše ostatní je relativní push na aktuální navigační zásobník.
/// </summary>
public static class AppRoutes
{
    // ── Absolutní (autentizační přechody) ──────────────────────
    public const string Login = "//login";
    public const string MainTabs = "//main/attendance";

    // ── Detail / push stránky (VŽDY relativní) ────────────────
    public const string EventDetail = "events/detail";
    public const string EventCreate = "events/create";
    public const string DanceDetail = "dances/detail";
    public const string AttdStats = "attd-stats";
    public const string AttdOverview = "attd-overview";
}
