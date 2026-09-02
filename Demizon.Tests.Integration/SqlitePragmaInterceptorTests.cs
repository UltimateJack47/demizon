using Demizon.Dal;
using Demizon.Dal.Interceptors;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Tests.Integration;

/// <summary>
/// <see cref="SqliteBusyTimeoutInterceptor"/> aplikuje tři connection-level PRAGMA.
/// </summary>
/// <remarks>
/// Jsou to opatření proti dvěma reálným provozním problémům na cílovém 1 GB / 10 GB
/// stroji: <c>busy_timeout</c> proti „database is locked“ při souběhu dvou hostů
/// a <c>journal_size_limit</c> + <c>wal_autocheckpoint</c> proti WAL souboru, který
/// se po hromadném zápisu nafoukne na stovky MB a už nikdy nezkrátí.
/// <para>
/// Tyto testy jedou nad <b>souborovou</b> databází, ne in-memory — WAL a s ním
/// i <c>journal_size_limit</c> mají v in-memory databázi jiné chování.
/// </para>
/// </remarks>
public sealed class SqlitePragmaInterceptorTests : IDisposable
{
    private const int ExpectedBusyTimeoutMs = 5000;
    private const int ExpectedJournalSizeLimitBytes = 32 * 1024 * 1024;
    private const int ExpectedWalAutoCheckpointPages = 512;

    private readonly string _databasePath = Path.Combine(
        Path.GetTempPath(), $"demizon-pragma-{Guid.NewGuid():N}.sqlite");

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (var suffix in new[] { "", "-wal", "-shm" })
        {
            var path = _databasePath + suffix;
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private DemizonContext NewContext(bool withInterceptor = true)
    {
        var builder = new DbContextOptionsBuilder<DemizonContext>()
            .UseSqlite($"Data Source={_databasePath}");

        if (withInterceptor)
            builder.AddInterceptors(new SqliteBusyTimeoutInterceptor());

        return new DemizonContext(builder.Options);
    }

    private static long ReadPragma(DemizonContext db, string pragma)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragma};";
        return Convert.ToInt64(command.ExecuteScalar());
    }

    [Fact]
    public void Interceptor_nastavi_busy_timeout()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();

        Assert.Equal(ExpectedBusyTimeoutMs, ReadPragma(db, "busy_timeout"));
    }

    [Fact]
    public void Interceptor_nastavi_journal_size_limit_na_32MB()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();

        Assert.Equal(ExpectedJournalSizeLimitBytes, ReadPragma(db, "journal_size_limit"));
    }

    [Fact]
    public void Interceptor_nastavi_wal_autocheckpoint()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();

        Assert.Equal(ExpectedWalAutoCheckpointPages, ReadPragma(db, "wal_autocheckpoint"));
    }

    /// <summary>
    /// Bez interceptoru platí defaulty. Test dokládá, že hodnoty výše skutečně nastavuje
    /// interceptor, a ne že by je SQLite měla samo.
    /// </summary>
    [Fact]
    public void Bez_interceptoru_plati_vychozi_hodnoty()
    {
        using var db = NewContext(withInterceptor: false);
        db.Database.EnsureCreated();

        Assert.Equal(0, ReadPragma(db, "busy_timeout"));
        Assert.Equal(-1, ReadPragma(db, "journal_size_limit"));
        Assert.Equal(1000, ReadPragma(db, "wal_autocheckpoint"));
    }

    /// <summary>
    /// Klíčová vlastnost: jsou to connection-level PRAGMA, ne persistentní. Musí se
    /// aplikovat na <b>každé</b> nové spojení, jinak by je poolované spojení „zapomnělo“.
    /// </summary>
    [Fact]
    public void Pragmy_se_aplikuji_na_kazde_nove_spojeni()
    {
        using (var first = NewContext())
        {
            first.Database.EnsureCreated();
            Assert.Equal(ExpectedBusyTimeoutMs, ReadPragma(first, "busy_timeout"));
        }

        using var second = NewContext();
        Assert.Equal(ExpectedBusyTimeoutMs, ReadPragma(second, "busy_timeout"));
        Assert.Equal(ExpectedJournalSizeLimitBytes, ReadPragma(second, "journal_size_limit"));
        Assert.Equal(ExpectedWalAutoCheckpointPages, ReadPragma(second, "wal_autocheckpoint"));
    }

    [Fact]
    public void Pragmy_nerozbiji_bezny_zapis_a_cteni()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();

        db.Members.Add(Infrastructure.TestData.Member(login: "pragma-test"));
        db.SaveChanges();

        Assert.NotNull(db.Members.SingleOrDefault(m => m.Login == "pragma-test"));
    }

    /// <summary>
    /// WAL mode zapíná <c>EnableWalMode()</c> při startu aplikace a je persistentní
    /// (uloží se do souboru DB). Interceptorem nastavené limity na něj musí navazovat.
    /// </summary>
    [Fact]
    public void Journal_size_limit_plati_i_ve_WAL_modu()
    {
        using var db = NewContext();
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");

        Assert.Equal("wal", ReadPragmaText(db, "journal_mode"));
        Assert.Equal(ExpectedJournalSizeLimitBytes, ReadPragma(db, "journal_size_limit"));
    }

    private static string ReadPragmaText(DemizonContext db, string pragma)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
            connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {pragma};";
        return command.ExecuteScalar()?.ToString() ?? string.Empty;
    }
}
