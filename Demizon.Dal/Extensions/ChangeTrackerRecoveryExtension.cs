using Microsoft.EntityFrameworkCore;

namespace Demizon.Dal.Extensions;

public static class ChangeTrackerRecoveryExtension
{
    /// <summary>
    /// Zahodí <b>všechny</b> rozpracované změny v change trackeru po neúspěšném uložení.
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
    /// <b>Proč se maže celý tracker, a ne jen jedna entita.</b> Cílit na konkrétní
    /// entitu nestačí ze tří důvodů, které se ukázaly až v provozu:
    /// </para>
    /// <list type="number">
    /// <item><description>
    /// <b>Grafy.</b> <c>AddAsync(member)</c> u člena s fotkou nastraží jako
    /// <c>Added</c> i tu fotku. Odpojení samotného člena ji nechá v trackeru.
    /// </description></item>
    /// <item><description>
    /// <b>Update přes jinou instanci.</b> <c>AttendanceService.CreateOrUpdateAsync</c>
    /// kopíruje hodnoty do <em>načtené</em> entity, takže trackovaná je ona, ne ta
    /// předaná — a <c>Entry()</c> na předané by byl no-op.
    /// </description></item>
    /// <item><description>
    /// <b>Audit.</b> <c>AuditSaveChangesInterceptor</c> přidává <c>AuditLog</c> řádky
    /// v <c>SavingChangesAsync</c>. Když uložení pak selže, zůstanou <c>Added</c>
    /// a vložily by se s příštím uložením jako osiřelé záznamy o změně,
    /// která se nikdy nestala.
    /// </description></item>
    /// </list>
    /// <para>
    /// Volající služby ukládají vždy hned po své vlastní změně, takže všechno
    /// rozpracované v momentě selhání <em>je</em> ta selhaná operace.
    /// </para>
    /// </remarks>
    public static void DiscardPendingChanges(this DemizonContext context)
    {
        // ToList: stav se v cyklu mění, takže se nedá iterovat živá kolekce.
        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            switch (entry.State)
            {
                // Nikdy nevložená entita nemá v trackeru co dělat.
                case EntityState.Added:
                    entry.State = EntityState.Detached;
                    break;

                // Vrátí i hodnoty v paměti, ne jen stav. Bez toho by entita zůstala
                // s nezapsanými hodnotami označená jako čistá, takže by ji další čtení
                // ze stejného kontextu vydalo změněnou — třeba člena jako smazaného,
                // přesto že soft delete selhal.
                case EntityState.Modified:
                    entry.CurrentValues.SetValues(entry.OriginalValues);
                    entry.State = EntityState.Unchanged;
                    break;

                // Řádek v databázi zůstal, jak byl — zahodí se jen zamýšlené smazání.
                case EntityState.Deleted:
                    entry.State = EntityState.Unchanged;
                    break;
            }
        }
    }
}
