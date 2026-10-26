namespace Demizon.Contracts.Auth;
public sealed record TokenResponse(string Token, string RefreshToken, int ExpiresIn, IReadOnlyList<string> Roles, bool IsGoogleCalendarConnected = false, int MemberId = 0);
