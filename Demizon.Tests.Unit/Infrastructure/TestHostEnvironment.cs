namespace Demizon.Tests.Unit.Infrastructure;

/// <summary>
/// Proměnné prostředí, které <c>Program.cs</c> potřebuje ještě před
/// <c>ConfigureWebHost</c> (connection string, JWT secret, VAPID).
/// Jsou globální pro proces, takže každá fixture si po zavolání musí host
/// postavit hned — jinak by se postavila nad databází fixture, která
/// nastavila env jako poslední.
/// </summary>
internal static class TestHostEnvironment
{
    public const string JwtSecret = "test-jwt-secret-key-32chars-min!";

    /// <param name="authPermitLimit">
    /// Výchozích 10000 vypne rate limiter, aby se suite netrefila do 429.
    /// Test, který 429 přímo zkoumá, si ho sníží.
    /// </param>
    /// <param name="maxTotalStorageBytes">
    /// Null = nechat výchozí 2 GB. Test kvóty ho sníží, aby ji šlo přetéct
    /// jedním malým obrázkem.
    /// </param>
    public static void Apply(string databasePath, int authPermitLimit = 10000, long? maxTotalStorageBytes = null)
    {
        Environment.SetEnvironmentVariable("ConnectionStrings__Default", $"Data Source={databasePath}");
        Environment.SetEnvironmentVariable("Jwt__SecretKey", JwtSecret);
        Environment.SetEnvironmentVariable("Vapid__PublicKey", "test-vapid-public");
        Environment.SetEnvironmentVariable("Vapid__PrivateKey", "test-vapid-private");
        Environment.SetEnvironmentVariable("Vapid__Subject", "mailto:test@demizon.test");
        // Bez zvednutého limitu by se suite trefila do HTTP 429 na /api/auth/token.
        Environment.SetEnvironmentVariable("RateLimiting__AuthPermitLimit", authPermitLimit.ToString());
        Environment.SetEnvironmentVariable("AllowedHosts", "*");
        Environment.SetEnvironmentVariable(
            "Upload__MaxTotalStorageBytes",
            maxTotalStorageBytes?.ToString());
        // Seed token se záměrně NEnastavuje přes env: je globální pro proces
        // a zapnul by endpoint i hostům, které ho mít zapnutý nemají.
        // Fixture, která ho potřebuje, ho vkládá přes PostConfigure.
    }

    public static string NewDatabasePath(string prefix) => Path.Combine(
        Path.GetTempPath(),
        $"demizon-{prefix}-{Guid.NewGuid():N}.sqlite");

    public static void DeleteDatabase(string databasePath)
    {
        foreach (var path in new[] { databasePath, databasePath + "-wal", databasePath + "-shm" })
        {
            try { File.Delete(path); }
            catch (IOException) { /* still locked on some hosts; temp will GC */ }
        }
    }
}
