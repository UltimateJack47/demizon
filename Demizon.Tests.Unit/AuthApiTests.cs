using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Demizon.Contracts.Auth;
using Demizon.Tests.Unit.Infrastructure;

namespace Demizon.Tests.Unit;

/// <summary>
/// HTTP kontrakt autentizace a autorizace. Unit testy služeb tohle nechytí:
/// JWT schéma vs. cookie default, rate limiter a mapování <c>TokenResponse.MemberId</c>
/// žijí až ve web hostu.
/// </summary>
[Collection("WebHost")]
public class AuthApiTests : IAsyncLifetime
{
    private readonly AuthApiFactory _factory;
    private HttpClient _client = null!;

    public AuthApiTests(AuthApiFactory factory) => _factory = factory;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _factory.EnsureSeeded();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Health_vrati_Healthy()
    {
        var response = await _client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("Healthy", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Token_odmitne_spatne_heslo()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/token",
            new TokenRequest(AuthApiFactory.StandardLogin, "spatne"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_odmitne_neexistujiciho_clena()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/token",
            new TokenRequest("nikdo", AuthApiFactory.StandardPassword));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_odmitne_externistu()
    {
        _factory.SeedMember("host", "host-heslo-1", isExternal: true);

        var response = await _client.PostAsJsonAsync("/api/auth/token",
            new TokenRequest("host", "host-heslo-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_odmitne_mekce_smazaneho_clena()
    {
        _factory.SeedMember("pryc", "pryc-heslo-1", deletedAt: DateTime.UtcNow);

        var response = await _client.PostAsJsonAsync("/api/auth/token",
            new TokenRequest("pryc", "pryc-heslo-1"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_vrati_jwt_s_member_id_a_roli()
    {
        var body = await LoginAsync(AuthApiFactory.StandardLogin, AuthApiFactory.StandardPassword);

        Assert.Equal(_factory.StandardMemberId, body.MemberId);
        Assert.Equal("Standard", body.Role);
        Assert.Equal(3600, body.ExpiresIn);
        Assert.False(string.IsNullOrEmpty(body.Token));
        Assert.False(string.IsNullOrEmpty(body.RefreshToken));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(body.Token);
        Assert.Equal(_factory.StandardMemberId.ToString(), jwt.Claims.First(c => c.Type == ClaimTypes.PrimarySid).Value);
        Assert.Equal("Standard", jwt.Claims.First(c => c.Type == ClaimTypes.Role).Value);
    }

    [Fact]
    public async Task Refresh_vrati_novy_par_a_stejne_member_id()
    {
        var login = await LoginAsync(AuthApiFactory.StandardLogin, AuthApiFactory.StandardPassword);

        var refresh = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(login.RefreshToken));
        refresh.EnsureSuccessStatusCode();
        var body = await refresh.Content.ReadFromJsonAsync<TokenResponse>();

        Assert.NotNull(body);
        Assert.Equal(_factory.StandardMemberId, body.MemberId);
        Assert.NotEqual(login.Token, body.Token);
        Assert.NotEqual(login.RefreshToken, body.RefreshToken);
    }

    [Fact]
    public async Task Refresh_jednorazovy_token_podruhe_odmitne()
    {
        var login = await LoginAsync(AuthApiFactory.StandardLogin, AuthApiFactory.StandardPassword);

        var first = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(login.RefreshToken));
        first.EnsureSuccessStatusCode();

        var replay = await _client.PostAsJsonAsync("/api/auth/refresh",
            new RefreshRequest(login.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task AttendancesMe_bez_tokenu_vrati_401()
    {
        var response = await _client.GetAsync("/api/attendances/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AttendancesMe_s_jwt_vrati_200()
    {
        var login = await LoginAsync(AuthApiFactory.StandardLogin, AuthApiFactory.StandardPassword);
        using var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var response = await authed.GetAsync("/api/attendances/me");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoint_standardnimu_clenu_vrati_403()
    {
        var login = await LoginAsync(AuthApiFactory.StandardLogin, AuthApiFactory.StandardPassword);
        using var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var response = await authed.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/events/99999"));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Admin_endpoint_prijme_admin_jwt()
    {
        var login = await LoginAsync(AuthApiFactory.AdminLogin, AuthApiFactory.AdminPassword);
        using var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        // Auth prošla — akce 99999 neexistuje, takže 404, ne 401/403.
        var response = await authed.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "/api/events/99999"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(_factory.AdminMemberId, login.MemberId);
    }

    [Fact]
    public async Task MembersMe_nevraci_password_hash()
    {
        var login = await LoginAsync(AuthApiFactory.StandardLogin, AuthApiFactory.StandardPassword);
        using var authed = _factory.CreateClient();
        authed.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login.Token);

        var response = await authed.GetAsync("/api/members/me");
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(json);
        Assert.False(doc.RootElement.TryGetProperty("passwordHash", out _));
        Assert.Equal(AuthApiFactory.StandardLogin, doc.RootElement.GetProperty("login").GetString());
    }

    private async Task<TokenResponse> LoginAsync(string login, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/token", new TokenRequest(login, password));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(body);
        return body;
    }
}

[CollectionDefinition("WebHost")]
public sealed class WebHostCollection : ICollectionFixture<AuthApiFactory>;
