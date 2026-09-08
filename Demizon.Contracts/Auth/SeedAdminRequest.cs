using System.ComponentModel.DataAnnotations;

namespace Demizon.Contracts.Auth;

/// <summary>
/// Vstup pro založení prvního admina. Přihlašovací údaje si volí ten, kdo
/// nasazuje — v kódu žádné výchozí heslo není.
/// </summary>
/// <remarks>
/// Atributy patří na <b>parametry</b> primárního konstruktoru, ne na property.
/// .NET 10 na <c>[property: Required]</c> u záznamu vyhodí
/// <c>InvalidOperationException</c> ještě před spuštěním akce.
/// </remarks>
public sealed record SeedAdminRequest(
    [Required, MinLength(3), MaxLength(100)] string Login,
    [Required, MinLength(12), MaxLength(200)] string Password,
    [Required, MaxLength(100)] string Name,
    [Required, MaxLength(100)] string Surname,
    [EmailAddress, MaxLength(200)] string? Email = null);
