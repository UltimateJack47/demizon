namespace Demizon.Tests.Unit.Infrastructure;

/// <summary>
/// Všechny testy nad <c>WebApplicationFactory</c> patří do této kolekce.
/// Ne kvůli sdílení fixture, ale kvůli **serializaci**: hosty se konfigurují
/// proměnnými prostředí procesu (viz <see cref="TestHostEnvironment"/>),
/// takže dvě fixture, které se staví současně, by si connection string
/// přepsaly navzájem.
/// </summary>
[CollectionDefinition("WebHost")]
public sealed class WebHostCollection
    : ICollectionFixture<AuthApiFactory>, ICollectionFixture<SeedApiFactory>;
