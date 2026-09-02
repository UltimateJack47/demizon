using Demizon.Dal.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Demizon.Tests.Integration.Infrastructure;

/// <summary>
/// Shodí <b>jen</b> to vnořené uložení, kterým <c>AuditSaveChangesInterceptor</c>
/// dopisuje skutečné primární klíče. Bez toho by se jeho <c>catch</c> blok v testech
/// nikdy nespustil a oprava by zůstala nepokrytá.
/// </summary>
/// <remarks>
/// Rozlišovacím znakem je stav audit řádků. Při <b>původním</b> uložení jsou
/// <see cref="EntityState.Added"/> (audit je právě zakládá), při dopisování klíčů
/// <see cref="EntityState.Modified"/> (mění se jim jen <c>EntityId</c>). Interceptor
/// je proto nutné registrovat <b>za</b> auditní, aby už audit řádky v change trackeru
/// viděl.
/// </remarks>
public sealed class FailAuditFixupInterceptor : SaveChangesInterceptor
{
    /// <summary>Zpráva výjimky, aby ji test mohl v logu i chování rozpoznat.</summary>
    public const string FailureMessage = "simulované selhání dopsání klíčů";

    public int FailureCount { get; private set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        var isKeyFixup = eventData.Context?.ChangeTracker
            .Entries<AuditLog>()
            .Any(e => e.State == EntityState.Modified) == true;

        if (isKeyFixup)
        {
            FailureCount++;
            throw new InvalidOperationException(FailureMessage);
        }

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
