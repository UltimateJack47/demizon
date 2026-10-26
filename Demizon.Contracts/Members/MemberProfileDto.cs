namespace Demizon.Contracts.Members;
public sealed record MemberProfileDto(int Id, string Name, string Surname, string Login, string? Email, IReadOnlyList<string> Roles, string Gender);
