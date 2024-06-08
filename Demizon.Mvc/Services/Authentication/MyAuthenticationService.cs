using System.Security.Claims;
using CryptoHelper;
using Demizon.Core.Services.Member;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Demizon.Mvc.Services.Authentication;

public sealed class MyAuthenticationService(IMemberService memberService) : IMyAuthenticationService
{
    private IMemberService MemberService { get; set; } = memberService;

    public async Task Login(HttpContext context)
    {
        var userAccount = MemberService.GetOneByLogin(context.Request.Form["Login"].ToString());
        var isPasswordCorrect =
            Crypto.VerifyHashedPassword(userAccount?.PasswordHash, context.Request.Form["Password"]);
        if (userAccount is null || !isPasswordCorrect)
        {
            context.Response.Redirect("/Login/true");
            return;
        }

        await context.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(
            new List<Claim>
            {
                new(ClaimTypes.Name, userAccount.Login),
                new(ClaimTypes.Role, userAccount.Role.ToString()),
                new(ClaimTypes.PrimarySid, userAccount.Id.ToString())
            }, CookieAuthenticationDefaults.AuthenticationScheme)));

        context.Response.Redirect("/Admin");
    }

    public async Task Logout(HttpContext context)
    {
        await context.SignOutAsync();
        context.Response.Redirect("/");
    }
}