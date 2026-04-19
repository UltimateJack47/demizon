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

    // ── Detail / push stránky (VŽDY relativní, VŽDY ploché názvy bez /) ─
    // DŮLEŽITÉ: Route NESMÍ obsahovat "/" — MAUI Shell by první segment
    // interpretoval jako tab-route a navigace by selhala s chybou
    // "Relative routing to shell elements is currently not supported."
    public const string EventDetail = "event-detail";
    public const string EventCreate = "event-create";
    public const string DanceDetail = "dance-detail";
    public const string AttdStats = "attd-stats";
    public const string AttdOverview = "attd-overview";
    public const string MemberAttdDetail = "member-attd-detail";
    public const string EditProfile = "edit-profile";
    public const string EditEvent = "edit-event";
    public const string Gallery = "gallery";
    public const string PhotoViewer = "photo-viewer";
}
