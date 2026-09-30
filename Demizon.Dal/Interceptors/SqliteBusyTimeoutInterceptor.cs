using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Demizon.Dal.Interceptors;

/// <summary>
/// Nastaví connection-level PRAGMA po každém otevření SQLite spojení.
/// PRAGMA journal_mode=WAL je persistentní (uloží se do souboru), ale tyto
/// jsou connection-level — musí se aplikovat na každé nové spojení.
/// </summary>
public class SqliteBusyTimeoutInterceptor : DbConnectionInterceptor
{
    private const int BusyTimeoutMs = 5000;

    /// <summary>
    /// Strop pro velikost WAL souboru (32 MB). Bez tohoto nastavení je default -1,
    /// což znamená, že SQLite WAL po checkpointu jen recykluje, ale nikdy nezkrátí —
    /// jeden hromadný zápis ho nafoukne na stovky MB a tam už zůstane.
    /// Na 10GB disku je to reálné riziko.
    /// </summary>
    private const int JournalSizeLimitBytes = 32 * 1024 * 1024;

    /// <summary>
    /// Počet stránek WAL, po kterých se spustí automatický checkpoint (default 1000).
    /// Nižší hodnota drží WAL menší za cenu častějšího zápisu do hlavního souboru.
    /// </summary>
    private const int WalAutoCheckpointPages = 512;

    /// <summary>
    /// <c>synchronous=NORMAL</c> místo výchozího <c>FULL</c>: ve WAL režimu se fsync dělá
    /// až při checkpointu, ne po každém commitu. Databáze se ani při výpadku proudu
    /// nepoškodí (commity ve WAL mají kontrolní součty), přijít se dá jen o poslední
    /// transakce od checkpointu — a jen při pádu celého stroje, ne appky či kontejneru.
    /// V benchmarku (2026-09-30, mru.sk na SQLite) FULL 154 req/s, NORMAL 325 req/s:
    /// s FULL čekalo všechno na disk. SQLite ve WAL režimu NORMAL samo doporučuje.
    /// </summary>
    private const string Synchronous = "NORMAL";

    /// <summary>
    /// <c>auto_vacuum=INCREMENTAL</c> is database-level. Setting it on an existing DB
    /// that was created with <c>NONE</c> (EF Core default) has <b>no effect</b> until a
    /// one-time <c>VACUUM</c> rewrites the file. That VACUUM temporarily needs ~2× the
    /// DB size free on disk — do it offline / with enough headroom, e.g.:
    /// <code>sqlite3 /data/demizon.sqlite "PRAGMA auto_vacuum=INCREMENTAL; VACUUM;"</code>
    /// After that, <c>PRAGMA incremental_vacuum(N)</c> (see DiskMaintenanceService)
    /// can reclaim free pages without a full rewrite.
    /// </summary>
    private static readonly string PragmaBatch =
        $"PRAGMA busy_timeout={BusyTimeoutMs};" +
        $"PRAGMA journal_size_limit={JournalSizeLimitBytes};" +
        $"PRAGMA wal_autocheckpoint={WalAutoCheckpointPages};" +
        $"PRAGMA synchronous={Synchronous};" +
        "PRAGMA auto_vacuum=INCREMENTAL;";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        ApplyPragmas(connection);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await ApplyPragmasAsync(connection, cancellationToken);
    }

    private static void ApplyPragmas(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaBatch;
        cmd.ExecuteNonQuery();
    }

    private static async Task ApplyPragmasAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = PragmaBatch;
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
