using Demizon.Common.Services;
using Demizon.Dal;
using Demizon.Dal.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Tests.Integration.Infrastructure;

/// <summary>
/// Jedna izolovaná SQLite databáze na jeden test.
/// </summary>
/// <remarks>
/// Záměrně se používá <b>skutečná SQLite</b>, ne EF InMemory provider. Testovaný kód se
/// opírá o relační chování, které InMemory nemá: <c>BeginTransactionAsync</c>
/// v <c>RefreshTokenService.CreateAsync</c>, <c>ExecuteUpdateAsync</c> v
/// <c>ValidateAsync</c> a PRAGMA v <see cref="SqliteBusyTimeoutInterceptor"/>.
/// <para>
/// In-memory SQLite žije jen dokud je otevřené alespoň jedno spojení, proto si fixture
/// jedno drží po celou dobu života. Schéma se staví přes <c>EnsureCreated</c>, takže se
/// testuje aktuální EF model (migrace hlídá zvlášť <c>MigrationsUpToDateTests</c>).
/// </para>
/// </remarks>
public sealed class DatabaseFixture : IAsyncDisposable, IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly List<DemizonContext> _contexts = [];

    public DatabaseFixture(string? currentUserLogin = null)
    {
        CurrentUser = new TestCurrentUserAccessor(currentUserLogin);

        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = NewContext();
        context.Database.EnsureCreated();
    }

    public TestCurrentUserAccessor CurrentUser { get; }

    /// <summary>
    /// Vytvoří nový kontext nad stejnou databází. Vlastní kontext na operaci je způsob,
    /// jak si test vynutí čtení z DB a ne z change trackeru předchozího zápisu.
    /// </summary>
    public DemizonContext NewContext()
    {
        var options = new DbContextOptionsBuilder<DemizonContext>()
            .UseSqlite(_connection)
            .UseLazyLoadingProxies()
            .AddInterceptors(
                new SqliteBusyTimeoutInterceptor(),
                new AuditSaveChangesInterceptor(CurrentUser))
            .Options;

        var context = new DemizonContext(options);
        _contexts.Add(context);
        return context;
    }

    public void Dispose()
    {
        foreach (var context in _contexts)
            context.Dispose();
        _contexts.Clear();
        _connection.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var context in _contexts)
            await context.DisposeAsync();
        _contexts.Clear();
        await _connection.DisposeAsync();
    }
}
