namespace Demizon.Common.Configuration;

public class JwtSettings
{
    public string SecretKey { get; set; } = null!;
    public string Issuer { get; set; } = "demizon-app";
    public string Audience { get; set; } = "demizon-api";
    public int ExpirationMinutes { get; set; } = 60;
}
