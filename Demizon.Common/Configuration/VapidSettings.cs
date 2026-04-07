namespace Demizon.Common.Configuration;

public class VapidSettings
{
    public string PublicKey { get; set; } = null!;
    public string PrivateKey { get; set; } = null!;
    public string Subject { get; set; } = "mailto:info@demizon.cz";
}
