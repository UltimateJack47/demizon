namespace Demizon.Contracts.Dances;

public sealed record CreateVideoLinkRequest(
    string Name,
    string Url,
    int Year,
    bool IsVisible,
    bool IsInternal,
    int? DanceId);
