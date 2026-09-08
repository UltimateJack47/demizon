using Demizon.Core.Services.Storage;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

/// <summary>
/// Hodinový úklid na Stardustu: AuditLog 90 dní, revokované/expirované refresh tokeny,
/// SentNotifications 180 dní. Bez toho AuditLog naroste o stovky MB/rok.
/// </summary>
public class DiskMaintenanceServiceTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new();

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private DiskMaintenanceService Service(Demizon.Dal.DemizonContext db) =>
        new(db, NullLogger<DiskMaintenanceService>.Instance);

    [Fact]
    public async Task Prazdna_databaze_cyklus_prezije()
    {
        await using var db = _fixture.NewContext();
        await Service(db).RunCycleAsync();
    }

    [Fact]
    public async Task Purge_smaze_stare_audit_logy_a_necha_cerstve()
    {
        await using var seed = _fixture.NewContext();
        seed.AuditLogs.Add(Log("stary", DateTime.UtcNow - DiskMaintenanceService.AuditLogRetention - TimeSpan.FromDays(1)));
        seed.AuditLogs.Add(Log("cerstvy", DateTime.UtcNow.AddDays(-1)));
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        await Service(db).RunCycleAsync();

        await using var verify = _fixture.NewContext();
        var remaining = Assert.Single(await verify.AuditLogs.ToListAsync());
        Assert.Equal("cerstvy", remaining.EntityId);
    }

    [Fact]
    public async Task Purge_smaze_revokovane_i_expirovane_tokeny_a_necha_platne()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        seed.RefreshTokens.Add(Token(member.Id, prefix: "revoked1", revoked: true, expiresAt: DateTime.UtcNow.AddDays(10)));
        seed.RefreshTokens.Add(Token(member.Id, prefix: "expired1", revoked: false, expiresAt: DateTime.UtcNow.AddMinutes(-1)));
        seed.RefreshTokens.Add(Token(member.Id, prefix: "validtok", revoked: false, expiresAt: DateTime.UtcNow.AddDays(10)));
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        await Service(db).RunCycleAsync();

        await using var verify = _fixture.NewContext();
        var remaining = Assert.Single(await verify.RefreshTokens.ToListAsync());
        Assert.Equal("validtok", remaining.TokenPrefix);
    }

    [Fact]
    public async Task Purge_smaze_stare_notifikace_a_necha_cerstve()
    {
        await using var seed = _fixture.NewContext();
        seed.SentNotifications.Add(Notif(DateTime.UtcNow - DiskMaintenanceService.SentNotificationRetention - TimeSpan.FromDays(1)));
        seed.SentNotifications.Add(Notif(DateTime.UtcNow.AddDays(-1)));
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        await Service(db).RunCycleAsync();

        await using var verify = _fixture.NewContext();
        Assert.Single(await verify.SentNotifications.ToListAsync());
        Assert.True((await verify.SentNotifications.SingleAsync()).SentAt > DateTime.UtcNow.AddDays(-2));
    }

    [Fact]
    public async Task Purge_nesaha_na_cleny_ani_soubory()
    {
        await using var seed = _fixture.NewContext();
        await TestData.SeedMemberAsync(seed, login: "zustane");
        seed.Files.Add(TestData.StoredFile(path: "zustane"));
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        await Service(db).RunCycleAsync();

        await using var verify = _fixture.NewContext();
        Assert.Equal("zustane", (await verify.Members.SingleAsync()).Login);
        Assert.Equal("zustane", (await verify.Files.SingleAsync()).Path);
    }

    private static AuditLog Log(string entityId, DateTime timestamp) => new()
    {
        EntityType = nameof(Member),
        EntityId = entityId,
        Action = "Added",
        UserId = "system",
        Timestamp = timestamp
    };

    private static RefreshToken Token(int memberId, string prefix, bool revoked, DateTime expiresAt) => new()
    {
        MemberId = memberId,
        TokenHash = $"hash-{prefix}",
        TokenPrefix = prefix,
        IsRevoked = revoked,
        ExpiresAt = expiresAt
    };

    private static SentNotification Notif(DateTime sentAt) => new()
    {
        NotificationType = NotificationType.NewEvent,
        SentAt = sentAt
    };
}
