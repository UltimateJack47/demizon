namespace Demizon.Dal.Entities;

/// <summary>
/// Fotka stavu kusu při předávce (<see cref="TransferLineId"/>) nebo vzorová fotka součástky
/// (<see cref="PartId"/>) — vždy právě jedno z toho. Vlastní tabulka, ne <c>Files</c>: nesdílí
/// kvótu s galerií a nemůže se objevit ve veřejných dotazech.
/// </summary>
public class CostumePhoto
{
    public int Id { get; set; }

    public int? TransferLineId { get; set; }

    public virtual CostumeTransferLine? TransferLine { get; set; }

    public int? PartId { get; set; }

    public virtual CostumePart? Part { get; set; }

    public byte[] Data { get; set; } = null!;

    public byte[] ThumbnailData { get; set; } = null!;

    public long FileSize { get; set; }

    public int UploadedById { get; set; }

    /// <summary>Čas serveru. Fotku z galerie tím neodhalí, to dělá <see cref="CapturedAt"/>.</summary>
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>EXIF <c>DateTimeOriginal</c> přečtený před zahozením metadat; null, když chybí.</summary>
    public DateTime? CapturedAt { get; set; }
}
