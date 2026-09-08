using Demizon.Common.Configuration;
using Demizon.Core.Services.File;
using Demizon.Core.Services.Storage;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

/// <summary>
/// Kvóty na Stardustu: 25 MB/soubor, 2 000 souborů, 2 GB celkem.
/// Bez nich stačí 400 PDF × 25 MB a 10GB disk je plný.
/// </summary>
public class StorageQuotaServiceTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new();

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private static StorageQuotaService Quota(Demizon.Dal.DemizonContext db, UploadSettings settings) =>
        new(db, new StubOptionsSnapshot<UploadSettings>(settings));

    private static FileService FileService(Demizon.Dal.DemizonContext db, UploadSettings settings) =>
        new(db, Quota(db, settings), NullLogger<FileService>.Instance);

    private static UploadSettings Tight(
        long maxFileBytes = 100,
        int maxFileCount = 2_000,
        long maxTotalStorageBytes = 2L * 1024 * 1024 * 1024) => new()
    {
        MaxFileBytes = maxFileBytes,
        MaxFileCount = maxFileCount,
        MaxTotalStorageBytes = maxTotalStorageBytes
    };

    [Fact]
    public async Task Soubor_nad_MaxFileBytes_se_odmitne()
    {
        await using var db = _fixture.NewContext();
        var (allowed, reason) = await Quota(db, Tight(maxFileBytes: 50)).EnsureCanStoreAsync(51);

        Assert.False(allowed);
        Assert.Contains("MB na soubor", reason);
    }

    [Fact]
    public async Task Soubor_na_limitu_MaxFileBytes_projde()
    {
        await using var db = _fixture.NewContext();
        var (allowed, reason) = await Quota(db, Tight(maxFileBytes: 50)).EnsureCanStoreAsync(50);

        Assert.True(allowed);
        Assert.Null(reason);
    }

    [Fact]
    public async Task Dosazeny_pocet_souboru_se_odmitne()
    {
        await using var seed = _fixture.NewContext();
        seed.Files.Add(TestData.StoredFile(fileSize: 1, path: "a"));
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        var (allowed, reason) = await Quota(db, Tight(maxFileCount: 1)).EnsureCanStoreAsync(1);

        Assert.False(allowed);
        Assert.Contains("limit počtu souborů", reason);
    }

    [Fact]
    public async Task Prekroceny_celkovy_objem_se_odmitne()
    {
        await using var seed = _fixture.NewContext();
        seed.Files.Add(TestData.StoredFile(fileSize: 80, path: "a"));
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        var (allowed, reason) = await Quota(db, Tight(maxTotalStorageBytes: 100)).EnsureCanStoreAsync(30);

        Assert.False(allowed);
        Assert.Contains("Nedostatek místa", reason);
    }

    [Fact]
    public async Task Soucet_presne_na_limitu_celkoveho_objemu_projde()
    {
        await using var seed = _fixture.NewContext();
        seed.Files.Add(TestData.StoredFile(fileSize: 80, path: "a"));
        await seed.SaveChangesAsync();

        await using var db = _fixture.NewContext();
        var (allowed, _) = await Quota(db, Tight(maxTotalStorageBytes: 100)).EnsureCanStoreAsync(20);

        Assert.True(allowed);
    }

    [Fact]
    public async Task FileService_CreateAsync_pri_prekroceni_kvoty_neulozi_nic()
    {
        await using var db = _fixture.NewContext();
        var service = FileService(db, Tight(maxFileBytes: 10));

        Assert.False(await service.CreateAsync(TestData.StoredFile(fileSize: 11)));

        Assert.DoesNotContain(db.ChangeTracker.Entries<Dal.Entities.File>(),
            e => e.State == EntityState.Added);
        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.Files.ToListAsync());
    }

    [Fact]
    public async Task FileService_CreateAsync_pod_kvotou_soubor_ulozi()
    {
        await using var db = _fixture.NewContext();
        var service = FileService(db, Tight(maxFileBytes: 100));

        Assert.True(await service.CreateAsync(TestData.StoredFile(fileSize: 10, path: "ok")));

        await using var verify = _fixture.NewContext();
        Assert.Equal("ok", (await verify.Files.SingleAsync()).Path);
    }
}
