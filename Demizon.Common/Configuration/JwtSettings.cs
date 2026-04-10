using System.ComponentModel.DataAnnotations;

namespace Demizon.Common.Configuration;

public class JwtSettings
{
    [Required, MinLength(32)]
    public string SecretKey { get; set; } = null!;

    [Required]
    public string Issuer { get; set; } = "demizon-app";

    [Required]
    public string Audience { get; set; } = "demizon-api";

    [Range(1, 10080)]
    public int ExpirationMinutes { get; set; } = 60;
}
