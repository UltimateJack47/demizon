using Demizon.Common.Services;

namespace Demizon.Tests.Integration.Infrastructure;

/// <summary>
/// Testovací dvojník za <see cref="ICurrentUserAccessor"/>. V produkci ho implementuje
/// MVC vrstva nad HTTP kontextem; audit interceptor z něj bere jen login.
/// </summary>
public sealed class TestCurrentUserAccessor(string? login = null) : ICurrentUserAccessor
{
    public string? Login { get; set; } = login;

    public string? GetCurrentUserLogin() => Login;
}
