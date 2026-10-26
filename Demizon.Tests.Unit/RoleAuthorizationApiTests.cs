using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Demizon.Contracts.Rehearsals;
using Demizon.Dal.Entities;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Tests.Unit;

/// <summary>
/// Role jsou od <c>AddMemberRoles</c> seznam, ne jedna hodnota. Tady se hlídá, že správce
/// obsahu dosáhne na obsah webu, ale ne na nic dalšího, a že člen bez role zůstane členem.
/// </summary>
[Collection("WebHost")]
public class RoleAuthorizationApiTests : IAsyncLifetime
{
    private CalendarApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new CalendarApiFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private HttpClient ClientFor(string login, params UserRole[] roles)
    {
        string token;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Dal.DemizonContext>();
            var member = new Member
            {
                Name = login, Surname = "Test", Login = login, Email = $"{login}@demizon.test",
                PasswordHash = "x", Gender = Gender.Female,
                Roles = roles.Select(r => new MemberRole { Role = r }).ToList(),
            };
            db.Members.Add(member);
            db.SaveChanges();
            token = scope.ServiceProvider
                .GetRequiredService<Core.Services.Authentication.TokenService>()
                .GenerateToken(member);
        }

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Spravce_obsahu_projde_na_spravu_videi()
    {
        using var client = ClientFor("redaktor", UserRole.ContentManager);

        // 404 = autorizací prošel a služba video jen nenašla.
        var response = await client.DeleteAsync("/api/videos/999");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Clen_bez_role_na_spravu_videi_nesmi()
    {
        using var client = ClientFor("clen");

        var response = await client.DeleteAsync("/api/videos/999");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Clen_bez_role_vidi_bezne_endpointy()
    {
        using var client = ClientFor("clen");

        var response = await client.GetAsync("/api/videos");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Spravce_obsahu_nesmi_rusit_zkousky()
    {
        using var client = ClientFor("redaktor", UserRole.ContentManager);

        var response = await client.PostAsJsonAsync("/api/rehearsals/cancellations",
            new CancelRehearsalsRequest(new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 12), null));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
