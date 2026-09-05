using System.Text.Json;
using Demizon.Common.Services;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Demizon.Dal.Interceptors;

/// <summary>
/// Automaticky zaznamenává změny entit do AuditLog tabulky.
/// Scoped lifetime – injektuje ICurrentUserAccessor (implementovaný v MVC vrstvě).
/// </summary>
/// <remarks>
/// Audituje se jen <b>asynchronní</b> cesta. Produkční kód synchronní <c>SaveChanges()</c>
/// nikde nepoužívá; kdyby ho někdo doplnil, změny by se do auditu nedostaly.
/// </remarks>
public class AuditSaveChangesInterceptor(
    ICurrentUserAccessor currentUserAccessor,
    ILogger<AuditSaveChangesInterceptor>? logger = null) : SaveChangesInterceptor
{
    // Vlastnosti obsahující citlivá data – vyloučeny z audit logu
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "TokenHash"
    };

    /// <summary>
    /// High-volume / low-value entity types. Auditing them filled ~90 % of AuditLog
    /// growth on Stardust (especially RefreshToken with 60 min JWT expiry) and after
    /// the temporary-PK fixup each insert also triggers an extra UPDATE.
    /// </summary>
    private static readonly HashSet<string> ExcludedEntityTypes = new(StringComparer.Ordinal)
    {
        nameof(RefreshToken),
        nameof(SentNotification),
        nameof(DeviceToken),
        "File",
    };

    /// <summary>
    /// Audit řádky vložených entit, kterým se skutečný primární klíč doplní teprve po uložení.
    /// </summary>
    private readonly List<(AuditLog Audit, EntityEntry Entry)> _pendingKeys = [];

    /// <summary>Brání rekurzi při dopisování klíčů, které samo volá <c>SaveChangesAsync</c>.</summary>
    private bool _isResolvingKeys;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (!_isResolvingKeys && eventData.Context is DemizonContext context)
        {
            var userId = currentUserAccessor.GetCurrentUserLogin() ?? "system";
            var auditEntries = new List<AuditLog>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog
                    || entry.State is EntityState.Detached or EntityState.Unchanged)
                    continue;

                var entityTypeName = entry.Metadata.ClrType.Name;
                if (ExcludedEntityTypes.Contains(entityTypeName))
                    continue;

                var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());

                var auditLog = new AuditLog
                {
                    // Metadata.ClrType, ne Entity.GetType(): s UseLazyLoadingProxies() jsou
                    // entity načtené z DB instancemi dynamických podtypů, takže runtime typ
                    // se jmenuje např. "MemberProxy". Model zná skutečný název entity.
                    EntityType = entityTypeName,
                    EntityId = primaryKey?.CurrentValue?.ToString() ?? "0",
                    Action = entry.State.ToString(),
                    UserId = userId,
                    Timestamp = DateTime.UtcNow,
                    OldValues = entry.State == EntityState.Modified
                        ? JsonSerializer.Serialize(
                            entry.Properties
                                .Where(p => p.IsModified && !SensitiveProperties.Contains(p.Metadata.Name))
                                .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue?.ToString()))
                        : null,
                    NewValues = entry.State != EntityState.Deleted
                        ? JsonSerializer.Serialize(
                            entry.Properties
                                .Where(p => (p.IsModified || entry.State == EntityState.Added)
                                            && !SensitiveProperties.Contains(p.Metadata.Name))
                                .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString()))
                        : null
                };

                // U vkládaných entit je klíč zatím jen dočasný placeholder, který EF generuje
                // jako záporné číslo. Skutečnou hodnotu zná až databáze, takže se doplní
                // v SavedChangesAsync – jinak by audit nešel spárovat s řádkem, který popisuje.
                if (primaryKey is { IsTemporary: true })
                    _pendingKeys.Add((auditLog, entry));

                auditEntries.Add(auditLog);
            }

            if (auditEntries.Count > 0)
            {
                await context.AuditLogs.AddRangeAsync(auditEntries, cancellationToken);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (!_isResolvingKeys && _pendingKeys.Count > 0 && eventData.Context is DemizonContext context)
        {
            var pending = _pendingKeys.ToList();
            _pendingKeys.Clear();

            foreach (var (audit, entry) in pending)
            {
                var primaryKey = entry.Properties.FirstOrDefault(p => p.Metadata.IsPrimaryKey());
                audit.EntityId = primaryKey?.CurrentValue?.ToString() ?? "0";
            }

            // Vnořené uložení nese jen AuditLog řádky, které se samy neauditují,
            // takže rekurze skončí hned. _isResolvingKeys je pojistka pro čitelnost.
            _isResolvingKeys = true;
            try
            {
                // CancellationToken.None záměrně: data volajícího jsou v tuhle chvíli
                // už commitnutá, takže zrušení při shutdownu nesmí shodit dopsání klíčů.
                await context.SaveChangesAsync(CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Klíčové: tohle uložení běží AŽ PO commitu volajícího, takže výjimka
                // odsud by z úspěšně uloženého zápisu udělala zdánlivě neúspěšný —
                // volající by se domníval, že se nic neuložilo, a zkusil to znovu.
                // Zastaralé EntityId v auditu je výrazně menší škoda než duplicitní řádek.
                logger?.LogWarning(ex,
                    "Nepodařilo se doplnit skutečné primární klíče do {Count} audit záznamů. "
                    + "Data jsou uložená, audit u nich nese dočasný klíč.", pending.Count);

                // Bez odpojení by ty řádky zůstaly Modified. Kontext je scoped na celý
                // Blazor okruh, takže by se neuložené UPDATE přehrály při příštím —
                // úplně nesouvisejícím — SaveChanges uživatele. Kdyby příčinou bylo
                // SQLITE_BUSY a zopakovalo se, spadl by uživateli jeho vlastní zápis
                // kvůli zbytku audit účetnictví. Kompromis „menší škoda“ tím padá.
                foreach (var (audit, _) in pending)
                    context.Entry(audit).State = EntityState.Detached;
            }
            finally
            {
                _isResolvingKeys = false;
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData,
        CancellationToken cancellationToken = default)
    {
        _pendingKeys.Clear();
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
}
