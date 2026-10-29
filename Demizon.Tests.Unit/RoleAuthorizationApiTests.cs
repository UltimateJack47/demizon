using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Demizon.Contracts.Rehearsals;
using Demizon.Dal.Entities;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task Vudce_rusi_a_obnovuje_zkousky()
    {
        using var client = ClientFor("vudce", UserRole.Leader);

        var created = await client.PostAsJsonAsync("/api/rehearsals/cancellations",
            new CancelRehearsalsRequest(new DateOnly(2026, 6, 12), new DateOnly(2026, 6, 12), "Málo lidí"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var cancellation = await created.Content.ReadFromJsonAsync<RehearsalCancellationDto>();
        var restored = await client.DeleteAsync($"/api/rehearsals/cancellations/{cancellation!.Id}");
        Assert.Equal(HttpStatusCode.NoContent, restored.StatusCode);
    }

    [Fact]
    public async Task Spravce_krojarny_nesmi_rusit_zkousky()
    {
        using var client = ClientFor("krojarka", UserRole.CostumeKeeper);

        var response = await client.DeleteAsync("/api/rehearsals/cancellations/999");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // ------------------------------------------- token neodpovídá databázi

    private void ChangeMember(string login, Action<Member> change)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dal.DemizonContext>();
        change(db.Members.Include(m => m.Roles).Single(m => m.Login == login));
        db.SaveChanges();
    }

    /// <summary>
    /// 401, ne 403: mobilní klient na 401 obnoví token a ten už nese aktuální role.
    /// </summary>
    [Fact]
    public async Task Token_s_odebranou_roli_vrati_401()
    {
        using var client = ClientFor("byvaly-redaktor", UserRole.ContentManager);
        ChangeMember("byvaly-redaktor", m => m.Roles.Clear());

        var response = await client.GetAsync("/api/videos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_s_nove_pridanou_roli_vrati_401_aby_si_klient_vzal_novy()
    {
        using var client = ClientFor("novy-redaktor");
        ChangeMember("novy-redaktor", m => m.Roles.Add(new MemberRole { Role = UserRole.ContentManager }));

        var response = await client.GetAsync("/api/videos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_smazaneho_clena_vrati_401()
    {
        using var client = ClientFor("odesly");
        ChangeMember("odesly", m => m.DeletedAt = DateTime.UtcNow);

        var response = await client.GetAsync("/api/videos");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
