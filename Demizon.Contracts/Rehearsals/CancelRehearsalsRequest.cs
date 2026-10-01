namespace Demizon.Contracts.Rehearsals;

/// <param name="DateTo">Včetně. Jedna zkouška: <c>DateFrom == DateTo</c>.</param>
/// <param name="Notify">Poslat push členům, kteří na zkoušku nemají „ne“.</param>
public sealed record CancelRehearsalsRequest(
    DateOnly DateFrom,
    DateOnly DateTo,
    string? Reason,
    bool Notify = false);
