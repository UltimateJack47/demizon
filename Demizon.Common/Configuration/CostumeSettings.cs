namespace Demizon.Common.Configuration;

/// <summary>Sekce <c>Costumes</c>. Fotky krojárky mají vlastní limit, nesdílí kvótu s galerií.</summary>
public class CostumeSettings
{
    /// <summary>Strop součtu fotek z předávek a vzorových fotek (výchozí 500 MB, odhad ~120 MB/rok).</summary>
    public long MaxPhotoStorageBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>Fotka pořízená dřív než tohle před nahráním dostane u příjemce varování.</summary>
    public TimeSpan StalePhotoAge { get; set; } = TimeSpan.FromHours(1);
}
