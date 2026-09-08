using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Demizon.Common.Configuration;
using Demizon.Core.Services.Authentication;
using Demizon.Dal.Entities;
using Demizon.Tests.Unit.Fakes;

namespace Demizon.Tests.Unit;

/// <summary>
/// JWT je jediný důkaz identity mobilního klienta. Tichá regrese ve claims
/// (chybějící <c>PrimarySid</c>, špatná role) by se v UI projevila až jako
/// „člen nenalezen“ / 403 na každém endpointu.
/// </summary>
public class TokenServiceTests
{
    private static TokenService Service(JwtSettings? settings = null) =>
        new(new StubOptionsSnapshot<JwtSettings>(settings ?? ValidSettings()));

    private static JwtSettings ValidSettings() => new()
    {
        SecretKey = "test-jwt-secret-key-32chars-min!",
        Issuer = "demizon-app",
        Audience = "demizon-api",
        ExpirationMinutes = 60
    };

    private static Member Member(UserRole role = UserRole.Standard) => new()
    {
        Id = 42,
        Login = "jana",
        Name = "Jana",
        Surname = "Novakova",
        PasswordHash = "x",
        Role = role,
        Gender = Gender.Female
    };

    [Fact]
    public void GenerateToken_zapise_login_roli_a_member_id()
    {
        var token = Service().GenerateToken(Member(UserRole.Admin));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal("jana", jwt.Claims.First(c => c.Type == ClaimTypes.Name).Value);
        Assert.Equal("Admin", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
        Assert.Equal("42", jwt.Claims.First(c => c.Type == ClaimTypes.PrimarySid).Value);
        Assert.False(string.IsNullOrEmpty(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value));
    }

    [Fact]
    public void ValidateToken_prijme_vlastni_token()
    {
        var service = Service();
        var principal = service.ValidateToken(service.GenerateToken(Member()));

        Assert.NotNull(principal);
        Assert.Equal("42", principal.FindFirst(ClaimTypes.PrimarySid)?.Value);
    }

    [Fact]
    public void ValidateToken_odmitne_podpis_cizim_klicem()
    {
        var issued = Service().GenerateToken(Member());
        var other = Service(new JwtSettings
        {
            SecretKey = "other-jwt-secret-key-32chars-min",
            Issuer = "demizon-app",
            Audience = "demizon-api",
            ExpirationMinutes = 60
        });

        Assert.Null(other.ValidateToken(issued));
    }

    [Fact]
    public void ValidateToken_odmitne_ciziho_issuer()
    {
        var issued = Service(new JwtSettings
        {
            SecretKey = "test-jwt-secret-key-32chars-min!",
            Issuer = "ne-demizon",
            Audience = "demizon-api",
            ExpirationMinutes = 60
        }).GenerateToken(Member());

        Assert.Null(Service().ValidateToken(issued));
    }

    [Fact]
    public void ValidateToken_odmitne_expirovany_token()
    {
        var issued = Service(new JwtSettings
        {
            SecretKey = "test-jwt-secret-key-32chars-min!",
            Issuer = "demizon-app",
            Audience = "demizon-api",
            ExpirationMinutes = -1
        }).GenerateToken(Member());

        Assert.Null(Service().ValidateToken(issued));
    }
}
