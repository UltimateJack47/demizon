using Demizon.Common.Configuration;
using Demizon.Core.Services.FileUpload;
using Demizon.Core.Services.Storage;
using Demizon.Tests.Unit.Fakes;

namespace Demizon.Tests.Unit;

/// <summary>
/// <c>FileUploadService</c> musí odmítnout soubor ještě před dekódováním / zápisem,
/// jinak by kvóta v <c>FileService.CreateAsync</c> přišla pozdě — ImageSharp by už
/// alokoval a dokumentové bajty by už ležely v paměti.
/// </summary>
public class FileUploadServiceQuotaTests
{
    private static FileUploadService CreateService(
        long maxFileBytes = 25L * 1024 * 1024,
        IStorageQuotaService? quota = null) =>
        new(new StubOptionsSnapshot<UploadSettings>(new UploadSettings
        {
            MaxFileBytes = maxFileBytes
        }), quota);

    [Fact]
    public async Task UploadDocumentToDbAsync_soubor_nad_MaxFileBytes_vrati_neuspech()
    {
        var bytes = "tiny"u8.ToArray();
        using var stream = new MemoryStream(bytes);

        var result = await CreateService(maxFileBytes: 10).UploadDocumentToDbAsync(new FileUploadRequest
        {
            Stream = stream,
            FileName = "velky",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSize = 11
        });

        Assert.False(result.IsSuccessful);
        Assert.Contains("MB na soubor", result.ErrorMessage);
        Assert.Null(result.Data);
        Assert.Equal(0, result.FileSize);
        Assert.Equal("velky", result.FileName);
    }

    [Fact]
    public async Task UploadImageToDbAsync_soubor_nad_MaxFileBytes_nedeoduje_obrazek()
    {
        // FileSize lže nahoru — brána musí zabrat dřív, než se stream vůbec čte.
        var jpeg = TestImages.Jpeg(32, 32);
        using var stream = new MemoryStream(jpeg);

        var result = await CreateService(maxFileBytes: 100).UploadImageToDbAsync(new FileUploadRequest
        {
            Stream = stream,
            FileName = "photo",
            FileExtension = ".jpg",
            ContentType = "image/jpeg",
            FileSize = 101
        });

        Assert.False(result.IsSuccessful);
        Assert.Contains("MB na soubor", result.ErrorMessage);
        Assert.Null(result.Data);
        Assert.Equal(0, stream.Position);
    }

    [Fact]
    public async Task UploadDocumentToDbAsync_kdyz_kvota_odmitne_vrati_jeji_duvod()
    {
        var bytes = "ok"u8.ToArray();
        using var stream = new MemoryStream(bytes);

        var result = await CreateService(quota: new RejectingQuota("Dosažen limit počtu souborů (1)."))
            .UploadDocumentToDbAsync(new FileUploadRequest
            {
                Stream = stream,
                FileName = "dalsi",
                FileExtension = ".pdf",
                ContentType = "application/pdf",
                FileSize = bytes.Length
            });

        Assert.False(result.IsSuccessful);
        Assert.Equal("Dosažen limit počtu souborů (1).", result.ErrorMessage);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task UploadDocumentToDbAsync_pod_limitem_bez_kvotni_sluzby_projde()
    {
        var bytes = "ok"u8.ToArray();
        using var stream = new MemoryStream(bytes);

        var result = await CreateService(maxFileBytes: 100).UploadDocumentToDbAsync(new FileUploadRequest
        {
            Stream = stream,
            FileName = "dalsi",
            FileExtension = ".pdf",
            ContentType = "application/pdf",
            FileSize = bytes.Length
        });

        Assert.True(result.IsSuccessful);
        Assert.Equal(bytes, result.Data);
    }

    private sealed class RejectingQuota(string reason) : IStorageQuotaService
    {
        public Task<(bool Allowed, string? Reason)> EnsureCanStoreAsync(
            long additionalBytes, CancellationToken cancellationToken = default)
            => Task.FromResult<(bool, string?)>((false, reason));
    }
}
