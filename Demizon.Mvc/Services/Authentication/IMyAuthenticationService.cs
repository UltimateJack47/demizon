namespace Demizon.Mvc.Services.Authentication;

public interface IMyAuthenticationService
{
    Task Login(HttpContext context);
    Task Logout(HttpContext context);

    /// <summary>
    /// Vydá JWT token na základě login/hesla z těla POST požadavku.
    /// Určeno pro budoucí API integraci – mobilní klient, externí nástroje.
    /// </summary>
    Task IssueToken(HttpContext context);
}
