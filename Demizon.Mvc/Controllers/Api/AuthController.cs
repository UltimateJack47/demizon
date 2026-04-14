using CryptoHelper;
using Demizon.Common.Configuration;
using Demizon.Common.Exceptions;
using Demizon.Contracts.Auth;
using Demizon.Core.Services.Authentication;
using Demizon.Core.Services.Member;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Demizon.Mvc.Controllers.Api;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IMemberService memberService,
    TokenService tokenService,
    RefreshTokenService refreshTokenService,
    IOptions<JwtSettings> jwtOptions) : ControllerBase
{
    private static readonly string DummyHash = Crypto.HashPassword("timing-attack-defense");

    [HttpPost("token")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponse>> Token([FromBody] TokenRequest request)
    {
        var member = memberService.GetOneByLogin(request.Login);
        var isValid = Crypto.VerifyHashedPassword(member?.PasswordHash ?? DummyHash, request.Password);

        if (member is null || !isValid || member.IsExternal)
            return Unauthorized(new { error = "Invalid credentials." });

        var token = tokenService.GenerateToken(member);
        var refreshToken = await refreshTokenService.CreateAsync(member.Id, jwtOptions.Value.RefreshTokenExpirationDays);

        return Ok(new TokenResponse(token, refreshToken, tokenService.ExpirationMinutes * 60, member.Role.ToString()));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("auth")]
    public async Task<ActionResult<TokenResponse>> Refresh([FromBody] RefreshRequest request)
    {
        var memberId = await refreshTokenService.ValidateAsync(request.RefreshToken);
        if (memberId is null)
            return Unauthorized(new { error = "Invalid or expired refresh token." });

        try
        {
            var member = await memberService.GetOneAsync(memberId.Value);

            if (member.IsExternal)
                return Unauthorized(new { error = "Invalid credentials." });

            var token = tokenService.GenerateToken(member);
            var newRefreshToken = await refreshTokenService.CreateAsync(member.Id, jwtOptions.Value.RefreshTokenExpirationDays);

            return Ok(new TokenResponse(token, newRefreshToken, tokenService.ExpirationMinutes * 60, member.Role.ToString()));
        }
        catch (EntityNotFoundException)
        {
            return Unauthorized(new { error = "Member no longer exists." });
        }
    }
}
