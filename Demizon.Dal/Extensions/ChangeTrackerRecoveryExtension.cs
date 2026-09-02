using Microsoft.EntityFrameworkCore;

namespace Demizon.Dal.Extensions;

public static class ChangeTrackerRecoveryExtension
{
    /// <summary>
    /// Zapomene rozpracovanou změnu entity po <b>neúspěšném</b> uložení.
    /// </summary>
    /// <remarks>
    /// EF Core při výjimce ze <c>SaveChangesAsync</c> change tracker <b>nevrací</b> —
    /// entita zůstane <see cref="EntityState.Added"/> (resp. <c>Modified</c>/<c>Deleted</c>).
    /// A protože <c>AddDbContext</c> je v Blazor Serveru scoped na <b>celý okruh</b>,
    /// přežije ta rozpracovaná změna zbytek uživatelovy session a přehraje se při
    /// každém dalším <c>SaveChangesAsync</c> — i úplně nesouvisejícím.
    /// <para>
    /// Konkrétní důsledky, kvůli kterým to vzniklo: neúspěšné vytvoření člena šlo
    /// zopakovat jen tak, že se uložil <em>dvakrát</em>; a při nahrávání více fotek
    /// se ta, u které zápis selhal, tiše vložila spolu s následující, takže se
    /// nahlásila jako neúspěšná, přesto že v databázi skončila.
    /// </para>
    /// <para>
    /// Volá se ze služeb, které výjimku ze zápisu spolykají a vrátí <c>false</c> —
    /// bez tohohle úklidu je takový <c>false</c> pro volajícího nepoužitelný, protože
    /// nezaručuje, že se změna neuloží později.
    /// </para>
    /// </remarks>
    public static void DiscardPendingChange(this DemizonContext context, object? entity)
    {
        if (entity is null)
            return;

        var entry = context.Entry(entity);

        switch (entry.State)
        {
            // Nikdy nevložená entita nemá v trackeru co dělat.
            case EntityState.Added:
                entry.State = EntityState.Detached;
                break;

            // Řádek v databázi zůstal, jak byl — zahodí se jen zamýšlená změna.
            case EntityState.Modified:
            case EntityState.Deleted:
                entry.State = EntityState.Unchanged;
                break;
        }
    }
}
