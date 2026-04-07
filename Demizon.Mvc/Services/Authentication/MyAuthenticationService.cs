using System.Security.Claims;
using CryptoHelper;
using Demizon.Core.Services.Member;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Demizon.Mvc.Services.Authentication;

public sealed class MyAuthenticationService(IMemberService memberService, TokenService tokenService) : IMyAuthenticationService
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

    public async Task IssueToken(HttpContext context)
    {
        string? login = null;
        string? password = null;

        if (context.Request.HasJsonContentType())
        {
            var body = await context.Request.ReadFromJsonAsync<TokenRequest>();
            login = body?.Login;
            password = body?.Password;
        }
        else
        {
            login = context.Request.Form["Login"].ToString();
            password = context.Request.Form["Password"].ToString();
        }

        var userAccount = MemberService.GetOneByLogin(login ?? string.Empty);
        var isPasswordCorrect = Crypto.VerifyHashedPassword(userAccount?.PasswordHash, password ?? string.Empty);

        if (userAccount is null || !isPasswordCorrect)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Invalid credentials." });
            return;
        }

        var token = tokenService.GenerateToken(userAccount);
        await context.Response.WriteAsJsonAsync(new
        {
            token,
            expiresIn = tokenService.ExpirationMinutes * 60,
            role = userAccount.Role.ToString(),
        });
    }

    private sealed record TokenRequest(string Login, string Password);
}