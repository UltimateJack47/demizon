using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Demizon.Contracts.Costumes;
using Demizon.Dal.Entities;
using Demizon.Tests.Unit.Fakes;
using Demizon.Tests.Unit.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Demizon.Tests.Unit;

/// <summary>
/// Krojárka přes HTTP: kdo dosáhne na co podle role a jedna předávka od výdeje po potvrzení
/// tak, jak ji projde mobil — včetně fotky jako multipart a QR tokenu příjemce.
/// </summary>
[Collection("WebHost")]
public class CostumeApiTests : IAsyncLifetime
{
    private CalendarApiFactory _factory = null!;

    public Task InitializeAsync()
    {
        _factory = new CalendarApiFactory();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _factory.DisposeAsync();

    private (HttpClient Client, int MemberId) ClientFor(string login, params UserRole[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Dal.DemizonContext>();
        var member = new Member
        {
            Name = login, Surname = "Test", Login = login, Email = $"{login}@demizon.test",
            PasswordHash = "x", Gender = Gender.Female,
            Roles = roles.Select(r => new MemberRole { Role = r }).ToList(),
        };
        db.Members.Add(member);
        db.SaveChanges();
        var token = scope.ServiceProvider.GetRequiredService<Core.Services.Authentication.TokenService>()
            .GenerateToken(member);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, member.Id);
    }

    private async Task<int> CreateItemAsync(HttpClient keeper, string code)
    {
        var part = await keeper.PostAsJsonAsync("/api/costumes/parts",
            new CostumePartRequest(null, "Kopanická vesta", "Male", null, "Individual"));
        await AssertStatusAsync(HttpStatusCode.OK, part);
        var partId = (await part.Content.ReadFromJsonAsync<IdResponse>())!.Id;
        var item = await keeper.PostAsJsonAsync("/api/costumes/items",
            new CostumeItemRequest(partId, code, "4", "černá", null, 1200));
        await AssertStatusAsync(HttpStatusCode.OK, item);
        return (await item.Content.ReadFromJsonAsync<IdResponse>())!.Id;
    }

    private sealed record IdResponse(int Id);

    private static async Task AssertStatusAsync(HttpStatusCode expected, HttpResponseMessage response) =>
        Assert.True(response.StatusCode == expected,
            $"{expected} expected, got {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

    private static MultipartFormDataContent Photo()
    {
        var content = new ByteArrayContent(TestImages.Jpeg(64, 48));
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        return new MultipartFormDataContent { { content, "file", "stav.jpg" } };
    }

    // ------------------------------------------------------ oprávnění

    [Fact]
    public async Task Katalog_vidi_spravce_admin_i_vudce_ale_ne_bezny_clen()
    {
        var (member, _) = ClientFor("clen");
        var (leader, _) = ClientFor("vudce", UserRole.Leader);
        var (keeper, _) = ClientFor("krojarka", UserRole.CostumeKeeper);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.GetAsync("/api/costumes/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await leader.GetAsync("/api/costumes/catalog")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await keeper.GetAsync("/api/costumes/catalog")).StatusCode);
    }

    [Fact]
    public async Task Katalog_meni_jen_spravce_ne_admin_ani_vudce()
    {
        var (admin, _) = ClientFor("admin2", UserRole.Admin, UserRole.Leader);
        var request = new CostumeRequest("Kopanický", null, 1);

        var response = await admin.PostAsJsonAsync("/api/costumes/costumes", request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Clen_z_krojarky_sam_vydat_nemuze()
    {
        var (keeper, _) = ClientFor("krojarka", UserRole.CostumeKeeper);
        await CreateItemAsync(keeper, "K0001");
        var (member, memberId) = ClientFor("clen");

        var response = await member.PostAsJsonAsync("/api/costumes/transfers",
            new CreateCostumeTransferRequest(null, memberId, null, [new("K0001")], null));

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
    }

    // ------------------------------------------------- celá předávka

    [Fact]
    public async Task Vydej_pres_QR_prijemce_fotka_odeslani_a_potvrzeni()
    {
        var (keeper, _) = ClientFor("krojarka", UserRole.CostumeKeeper);
        var (jana, janaId) = ClientFor("jana");
        await CreateItemAsync(keeper, "K0001");

        // Jana si otevře QR v profilu — tím jí vznikne token.
        var qr = await jana.GetAsync("/api/costumes/me/qr");
        Assert.Equal("image/png", qr.Content.Headers.ContentType?.MediaType);
        string token;
        using (var scope = _factory.Services.CreateScope())
            token = (await scope.ServiceProvider.GetRequiredService<Dal.DemizonContext>().Members
                .SingleAsync(m => m.Id == janaId)).CostumeQrToken!;

        var recipient = await keeper.GetFromJsonAsync<CostumeMemberDto>($"/api/costumes/members/by-qr/{token}");
        Assert.Equal(janaId, recipient!.Id);

        var created = await keeper.PostAsJsonAsync("/api/costumes/transfers",
            new CreateCostumeTransferRequest(null, null, token, [new("k0001")], "Na sezónu"));
        await AssertStatusAsync(HttpStatusCode.Created, created);
        var draft = (await created.Content.ReadFromJsonAsync<CostumeTransferDto>())!;
        Assert.Equal("Draft", draft.Status);
        Assert.Null(draft.From);
        Assert.Equal(janaId, draft.To!.MemberId);

        var line = draft.Lines.Single();
        await AssertStatusAsync(HttpStatusCode.OK,
            await keeper.PostAsync($"/api/costumes/transfers/{draft.Id}/lines/{line.Id}/photos", Photo()));
        var submitted = await keeper.PostAsync($"/api/costumes/transfers/{draft.Id}/submit", null);
        await AssertStatusAsync(HttpStatusCode.OK, submitted);
        Assert.Equal("Pending", (await submitted.Content.ReadFromJsonAsync<CostumeTransferStatusDto>())!.Status);

        var mine = await jana.GetFromJsonAsync<MyCostumesDto>("/api/costumes/me");
        var incoming = Assert.Single(mine!.Incoming);
        var photo = Assert.Single(incoming.Lines.Single().Photos);
        Assert.Equal(HttpStatusCode.OK,
            (await jana.GetAsync($"/api/costumes/transfers/{draft.Id}/photos/{photo.Id}?size=thumb")).StatusCode);
        // Hodnota kusu je jen pro ty, kdo vidí celou krojárku.
        Assert.Null(incoming.Lines.Single().Item.ValueCzk);

        await AssertStatusAsync(HttpStatusCode.NoContent,
            await jana.PostAsync($"/api/costumes/transfers/{draft.Id}/confirm", null));

        mine = await jana.GetFromJsonAsync<MyCostumesDto>("/api/costumes/me");
        Assert.Equal("K0001", Assert.Single(mine!.Holdings).Item.Code);
        Assert.Empty(mine.Incoming);
        var card = await jana.GetFromJsonAsync<CostumeItemCardDto>("/api/costumes/items/by-code/K0001");
        Assert.Equal(0, card!.InStore);
        Assert.Equal(janaId, Assert.Single(card.Holders).Member.MemberId);
    }

    [Fact]
    public async Task Cizi_predavku_ani_jeji_fotky_clen_nevidi()
    {
        var (keeper, _) = ClientFor("krojarka", UserRole.CostumeKeeper);
        var (_, janaId) = ClientFor("jana");
        var (petr, _) = ClientFor("petr");
        await CreateItemAsync(keeper, "K0001");
        var created = await keeper.PostAsJsonAsync("/api/costumes/transfers",
            new CreateCostumeTransferRequest(null, janaId, null, [new("K0001")], null));
        var draft = (await created.Content.ReadFromJsonAsync<CostumeTransferDto>())!;

        Assert.Equal(HttpStatusCode.NotFound, (await petr.GetAsync($"/api/costumes/transfers/{draft.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await petr.GetAsync($"/api/costumes/transfers/{draft.Id}/photos/1")).StatusCode);
    }

    [Fact]
    public async Task Casy_v_odpovedi_jsou_UTC_se_Z()
    {
        var (keeper, _) = ClientFor("krojarka", UserRole.CostumeKeeper);
        var (_, janaId) = ClientFor("jana");
        await CreateItemAsync(keeper, "K0001");
        var created = await keeper.PostAsJsonAsync("/api/costumes/transfers",
            new CreateCostumeTransferRequest(null, janaId, null, [new("K0001")], null));
        var id = (await created.Content.ReadFromJsonAsync<CostumeTransferDto>())!.Id;

        var json = await keeper.GetStringAsync($"/api/costumes/transfers/{id}");

        Assert.Matches("\"createdAt\":\"[^\"]+Z\"", json);
    }

    [Fact]
    public async Task Pripominku_posila_jen_spravce_a_jen_tem_kdo_neco_drzi()
    {
        var (keeper, _) = ClientFor("krojarka", UserRole.CostumeKeeper);
        var (member, _) = ClientFor("clen");
        var (_, janaId) = ClientFor("jana");
        var (_, petrId) = ClientFor("petr");
        var itemId = await CreateItemAsync(keeper, "K0001");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<Dal.DemizonContext>();
            db.CostumeHoldings.Add(new CostumeHolding { ItemId = itemId, MemberId = janaId, Quantity = 1 });
            db.SaveChanges();
        }

        Assert.Equal(HttpStatusCode.Forbidden,
            (await member.PostAsJsonAsync("/api/costumes/reminders", new CostumeReminderRequest(null, null))).StatusCode);

        var all = await keeper.PostAsJsonAsync("/api/costumes/reminders", new CostumeReminderRequest(null, "Vraťte prosím"));
        await AssertStatusAsync(HttpStatusCode.OK, all);
        Assert.Equal(1, (await all.Content.ReadFromJsonAsync<CostumeReminderResponse>())!.Recipients);

        // Petr nic nedrží — výběr ho tiše přeskočí.
        var picked = await keeper.PostAsJsonAsync("/api/costumes/reminders", new CostumeReminderRequest([petrId], null));
        Assert.Equal(0, (await picked.Content.ReadFromJsonAsync<CostumeReminderResponse>())!.Recipients);
    }

    [Fact]
    public async Task Neznamy_QR_prijemce_vrati_srozumitelnou_400()
    {
        var (keeper, _) = ClientFor("krojarka", UserRole.CostumeKeeper);
        await CreateItemAsync(keeper, "K0001");

        var response = await keeper.PostAsJsonAsync("/api/costumes/transfers",
            new CreateCostumeTransferRequest(null, null, "neexistuje", [new("K0001")], null));

        await AssertStatusAsync(HttpStatusCode.BadRequest, response);
        Assert.Contains("QR", await response.Content.ReadAsStringAsync());
    }
}
