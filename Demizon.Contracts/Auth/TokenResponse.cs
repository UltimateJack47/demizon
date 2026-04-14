namespace Demizon.Contracts.Auth;
public sealed record TokenResponse(string Token, string RefreshToken, int ExpiresIn, string Role, bool IsGoogleCalendarConnected = false);
