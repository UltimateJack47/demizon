using System.Security.Claims;
using CryptoHelper;
using Demizon.Core.Services.User;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Demizon.Mvc.Services.Authentication;

public sealed class MyAuthenticationService : IMyAuthenticationService
{
    private IUserService UserService { get; set; }

    public MyAuthenticationService(IUserService userService)
    {
        UserService = userService;
    }

    public async Task Login(HttpContext context)
    {
        var userAccount = UserService.GetOneByLogin(context.Request.Form["Login"].ToString());
        var isPasswordCorrect = Crypto.VerifyHashedPassword(userAccount?.PasswordHash, context.Request.Form["Password"]);
        if (userAccount is null || !isPasswordCorrect)
        {
            context.Response.Redirect("/Login/true");
            return;
        }

        await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim>
            {
                new (ClaimTypes.Name, userAccount.Name),
                new (ClaimTypes.Role, userAccount.Role.ToString()),
                new (ClaimTypes.PrimarySid, userAccount.Id.ToString())
            }, CookieAuthenticationDefaults.AuthenticationScheme)));

        context.Response.Redirect("/Admin");
    }

    public async Task Logout(HttpContext context)
    {
        await context.SignOutAsync();        
        context.Response.Redirect("/");        
    }
}
