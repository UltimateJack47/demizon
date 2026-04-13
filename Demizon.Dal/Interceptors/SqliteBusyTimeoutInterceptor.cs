using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Demizon.Dal.Interceptors;

/// <summary>
/// Nastaví PRAGMA busy_timeout po každém otevření SQLite spojení.
/// PRAGMA journal_mode=WAL je persistentní (uloží se do souboru), ale busy_timeout
/// je connection-level nastavení — musí být aplikováno na každé nové spojení.
/// </summary>
public class SqliteBusyTimeoutInterceptor : DbConnectionInterceptor
{
    private const int BusyTimeoutMs = 5000;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetBusyTimeout(connection);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetBusyTimeoutAsync(connection, cancellationToken);
    }

    private static void SetBusyTimeout(DbConnection connection)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA busy_timeout={BusyTimeoutMs};";
        cmd.ExecuteNonQuery();
    }

    private static async Task SetBusyTimeoutAsync(DbConnection connection, CancellationToken ct)
    {
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"PRAGMA busy_timeout={BusyTimeoutMs};";
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
