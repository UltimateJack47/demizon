namespace Demizon.Contracts.Members;

public sealed record UpdateProfileRequest(string Name, string Surname, string? Email);
