namespace Demizon.Core.Services.FileUpload;

public class FileUploadResult
{
    public bool IsSuccessful { get; set; }

    /// <summary>
    /// Důvod neúspěchu formulovaný pro uživatele. Vyplněný jen když je
    /// <see cref="IsSuccessful"/> false — chyba je na vstupu, ne na serveru,
    /// takže ji volající má přeložit na HTTP 400, ne 500.
    /// </summary>
    public string? ErrorMessage { get; set; }

    public string FileName { get; set; } = null!;

    public string FileExtension { get; set; } = null!;

    public string RelativePath { get; set; } = null!;

    public long FileSize { get; set; }

    public string ContentType { get; set; } = null!;

    public byte[]? Data { get; set; }

    public byte[]? ThumbnailData { get; set; }
}