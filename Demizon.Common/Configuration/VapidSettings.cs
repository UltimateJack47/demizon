using System.ComponentModel.DataAnnotations;

namespace Demizon.Common.Configuration;

public class VapidSettings
{
    [Required]
    public string PublicKey { get; set; } = null!;

    [Required]
    public string PrivateKey { get; set; } = null!;

    [Required]
    public string Subject { get; set; } = "mailto:info@demizon.cz";
}
