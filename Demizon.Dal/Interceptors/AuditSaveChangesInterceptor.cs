using System.Text.Json;
using Demizon.Common.Services;
using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Demizon.Dal.Interceptors;

/// <summary>
/// Automaticky zaznamenává změny entit do AuditLog tabulky.
/// Scoped lifetime – injektuje ICurrentUserAccessor (implementovaný v MVC vrstvě).
/// </summary>
public class AuditSaveChangesInterceptor(ICurrentUserAccessor currentUserAccessor) : SaveChangesInterceptor
{
    // Vlastnosti obsahující citlivá data – vyloučeny z audit logu
    private static readonly HashSet<string> SensitiveProperties = new(StringComparer.OrdinalIgnoreCase)
    {
        "PasswordHash", "TokenHash"
    };

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is DemizonContext context)
        {
            var userId = currentUserAccessor.GetCurrentUserLogin() ?? "system";
            var auditEntries = new List<AuditLog>();

            foreach (var entry in context.ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog
                    || entry.State is EntityState.Detached or EntityState.Unchanged)
                    continue;

                var auditLog = new AuditLog
                {
                    EntityType = entry.Entity.GetType().Name,
                    EntityId = entry.Properties
                        .FirstOrDefault(p => p.Metadata.IsPrimaryKey())
                        ?.CurrentValue?.ToString() ?? "0",
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

                auditEntries.Add(auditLog);
            }

            if (auditEntries.Count > 0)
            {
                await context.AuditLogs.AddRangeAsync(auditEntries, cancellationToken);
            }
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
