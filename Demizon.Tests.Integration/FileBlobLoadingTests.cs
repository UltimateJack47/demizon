using Demizon.Core.Services.File;
using Demizon.Core.Services.Storage;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Demizon.Tests.Integration;

/// <summary>
/// BLOBy v SQLite se nesmí tahat do RAM, dokud je stránka opravdu nechce poslat
/// (náhled, plná fotka, dokument). Seznam tanců / fotek stačí metadata.
/// </summary>
public class FileBlobLoadingTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new();

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private FileService Service(Demizon.Dal.DemizonContext db) =>
        new(db, new AllowQuota(), NullLogger<FileService>.Instance);

    [Fact]
    public async Task GetOneAsync_nenacte_Data_ani_ThumbnailData()
    {
        var id = await SeedPhotoAsync();

        await using var db = _fixture.NewContext();
        var file = await Service(db).GetOneAsync(id);

        Assert.Null(file.Data);
        Assert.Null(file.ThumbnailData);
        Assert.True(file.HasStoredData);
        Assert.Equal("db-stored", file.Path);
    }

    [Fact]
    public async Task GetContentAsync_vrati_jen_plna_data()
    {
        var id = await SeedPhotoAsync();

        await using var db = _fixture.NewContext();
        var full = await Service(db).GetContentAsync(id);
        var thumb = await Service(db).GetContentAsync(id, thumbnail: true);

        Assert.Equal("FULL"u8.ToArray(), full);
        Assert.Equal("THUMB"u8.ToArray(), thumb);
    }

    [Fact]
    public async Task UpdateAsync_zmena_IsPublic_nesmaze_BLOB()
    {
        var id = await SeedPhotoAsync();

        await using var db = _fixture.NewContext();
        var service = Service(db);
        var meta = await service.GetOneAsync(id);
        meta.IsPublic = true;
        await service.UpdateAsync(id, meta);

        await using var verify = _fixture.NewContext();
        var stored = await verify.Files.AsNoTracking().SingleAsync(f => f.Id == id);
        Assert.True(stored.IsPublic);
        Assert.Equal("FULL"u8.ToArray(), stored.Data);
        Assert.Equal("THUMB"u8.ToArray(), stored.ThumbnailData);
    }

    [Fact]
    public async Task DeleteAsync_smaze_radek_bez_nacteni_BLOBu()
    {
        var id = await SeedPhotoAsync();

        await using var db = _fixture.NewContext();
        Assert.True(await Service(db).DeleteAsync(id));

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.Files.ToListAsync());
    }

    [Fact]
    public async Task Metadata_projekce_HasDbData_nenacte_bajty()
    {
        await SeedPhotoAsync();

        await using var db = _fixture.NewContext();
        var rows = await Service(db).GetAll()
            .Select(f => new { f.Id, HasData = f.Data != null, f.Path })
            .ToListAsync();

        var row = Assert.Single(rows);
        Assert.True(row.HasData);
        Assert.Equal("db-stored", row.Path);
    }

    private async Task<int> SeedPhotoAsync()
    {
        await using var seed = _fixture.NewContext();
        var file = TestData.StoredFile(fileSize: 4);
        file.Data = "FULL"u8.ToArray();
        file.ThumbnailData = "THUMB"u8.ToArray();
        seed.Files.Add(file);
        await seed.SaveChangesAsync();
        return file.Id;
    }

    private sealed class AllowQuota : IStorageQuotaService
    {
        public Task<(bool Allowed, string? Reason)> EnsureCanStoreAsync(
            long additionalBytes, CancellationToken cancellationToken = default)
            => Task.FromResult((true, (string?)null));
    }
}
