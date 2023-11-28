namespace Demizon.Mvc.Services.Authentication;

public interface IMyAuthenticationService
{
    Task Login(HttpContext context);
    Task Logout(HttpContext context);
}
