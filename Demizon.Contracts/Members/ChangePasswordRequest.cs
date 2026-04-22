namespace Demizon.Contracts.Members;

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);
