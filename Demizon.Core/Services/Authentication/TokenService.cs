using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Demizon.Common.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Demizon.Core.Services.Authentication;

public sealed class TokenService(IOptions<JwtSettings> jwtOptions)
{
    private static readonly JwtSecurityTokenHandler _tokenHandler = new();

    private readonly JwtSettings _settings = jwtOptions.Value;

    public int ExpirationMinutes => _settings.ExpirationMinutes;

    public string GenerateToken(global::Demizon.Dal.Entities.Member member)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, member.Login),
            new(ClaimTypes.PrimarySid, member.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };
        claims.AddRange(member.Roles.Select(r => new Claim(ClaimTypes.Role, r.Role.ToString())));

        var token = new JwtSecurityToken(
            issuer: _settings.Issuer,
            audience: _settings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_settings.ExpirationMinutes),
            signingCredentials: credentials);

        return _tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.SecretKey));

        try
        {
            return _tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = _settings.Issuer,
                ValidateAudience = true,
                ValidAudience = _settings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            }, out _);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }
}
