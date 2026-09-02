using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Demizon.Tests.Integration;

/// <summary>
/// <c>AuditSaveChangesInterceptor</c> zapisuje změny entit automaticky.
/// Testy hlídají dvě věci: že se audituje to, co má, a hlavně že do auditu
/// <b>neprosáknou hashe hesel a tokenů</b>.
/// </summary>
public class AuditInterceptorTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new(currentUserLogin: "admin");

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    private async Task<List<AuditLog>> ReadAuditAsync(string entityType)
    {
        await using var db = _fixture.NewContext();
        return await db.AuditLogs.Where(a => a.EntityType == entityType).ToListAsync();
    }

    // ---------------------------------------------------------------- základní chování

    [Fact]
    public async Task Vlozeni_entity_zapise_audit_zaznam_s_akci_Added()
    {
        await using var db = _fixture.NewContext();
        await TestData.SeedMemberAsync(db);

        var entries = await ReadAuditAsync(nameof(Member));

        var entry = Assert.Single(entries);
        Assert.Equal("Added", entry.Action);
        Assert.Equal("admin", entry.UserId);
        Assert.Null(entry.OldValues);
        Assert.NotNull(entry.NewValues);
    }

    [Fact]
    public async Task Zmena_entity_zapise_stare_i_nove_hodnoty()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var tracked = await db.Members.SingleAsync(m => m.Id == member.Id);
        tracked.Name = "Zmeneno";
        await db.SaveChangesAsync();

        var modified = Assert.Single(await ReadAuditAsync(nameof(Member)), a => a.Action == "Modified");
        Assert.Contains("Jan", modified.OldValues);
        Assert.Contains("Zmeneno", modified.NewValues);
    }

    [Fact]
    public async Task Smazani_entity_zapise_akci_Deleted_bez_novych_hodnot()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        // Tvrdé smazání, aby vznikl stav Deleted (soft delete je z pohledu EF Modified).
        db.Members.Remove(await db.Members.SingleAsync(m => m.Id == member.Id));
        await db.SaveChangesAsync();

        var deleted = Assert.Single(await ReadAuditAsync(nameof(Member)), a => a.Action == "Deleted");
        Assert.Null(deleted.NewValues);
    }

    [Fact]
    public async Task Soft_delete_se_audituje_jako_Modified()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var tracked = await db.Members.SingleAsync(m => m.Id == member.Id);
        tracked.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        var modified = Assert.Single(await ReadAuditAsync(nameof(Member)), a => a.Action == "Modified");
        Assert.Contains("DeletedAt", modified.NewValues);
    }

    [Fact]
    public async Task Audit_zaznam_nese_primarni_klic_entity()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);

        var entry = Assert.Single(await ReadAuditAsync(nameof(Member)));
        Assert.Equal(member.Id.ToString(), entry.EntityId);
    }

    [Fact]
    public async Task Audit_zaznam_nese_UTC_cas()
    {
        var before = DateTime.UtcNow.AddSeconds(-5);
        await using var db = _fixture.NewContext();
        await TestData.SeedMemberAsync(db);

        var entry = Assert.Single(await ReadAuditAsync(nameof(Member)));
        Assert.InRange(entry.Timestamp, before, DateTime.UtcNow.AddSeconds(5));
    }

    // ---------------------------------------------------------------- citlivá data

    /// <summary>
    /// Hash hesla je v <c>SensitiveProperties</c>. Kdyby vypadl, byl by v AuditLogu
    /// plaintextově čitelný seznam všech hashů — a AuditLog je součástí záloh.
    /// </summary>
    [Fact]
    public async Task Audit_neobsahuje_hash_hesla()
    {
        await using var db = _fixture.NewContext();
        var member = TestData.Member();
        member.PasswordHash = "AQAAAAIAAYagAAAAEL-TAJNY-HASH-HESLA";
        db.Members.Add(member);
        await db.SaveChangesAsync();

        var entry = Assert.Single(await ReadAuditAsync(nameof(Member)));
        Assert.DoesNotContain("TAJNY-HASH-HESLA", entry.NewValues);
        Assert.DoesNotContain("PasswordHash", entry.NewValues);
    }

    [Fact]
    public async Task Audit_neobsahuje_hash_refresh_tokenu()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        db.RefreshTokens.Add(new RefreshToken
        {
            MemberId = member.Id,
            TokenHash = "AQAAAAIAAYagAAAAEL-TAJNY-HASH-TOKENU",
            TokenPrefix = "abcdefgh",
            ExpiresAt = DateTime.UtcNow.AddDays(30)
        });
        await db.SaveChangesAsync();

        var entry = Assert.Single(await ReadAuditAsync(nameof(RefreshToken)));
        Assert.DoesNotContain("TAJNY-HASH-TOKENU", entry.NewValues);
        Assert.DoesNotContain("TokenHash", entry.NewValues);
    }

    [Fact]
    public async Task Audit_pri_zmene_hesla_neprozradi_ani_stary_hash()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var tracked = await db.Members.SingleAsync(m => m.Id == member.Id);
        tracked.PasswordHash = "NOVY-HASH";
        await db.SaveChangesAsync();

        var modified = Assert.Single(await ReadAuditAsync(nameof(Member)), a => a.Action == "Modified");
        Assert.DoesNotContain("NOVY-HASH", modified.NewValues);
        Assert.DoesNotContain("not-a-real-hash", modified.OldValues);
    }

    // ---------------------------------------------------------------- hraniční případy

    /// <summary>Bez toho by audit auditoval sám sebe a jedno uložení by nikdy neskončilo.</summary>
    [Fact]
    public async Task Audit_neaudituje_sam_sebe()
    {
        await using var db = _fixture.NewContext();
        await TestData.SeedMemberAsync(db);

        Assert.Empty(await ReadAuditAsync(nameof(AuditLog)));
    }

    [Fact]
    public async Task Bez_prihlaseneho_uzivatele_se_audituje_jako_system()
    {
        await using var fixture = new DatabaseFixture(currentUserLogin: null);
        await using var db = fixture.NewContext();
        await TestData.SeedMemberAsync(db);

        await using var verify = fixture.NewContext();
        var entry = Assert.Single(await verify.AuditLogs.Where(a => a.EntityType == nameof(Member)).ToListAsync());
        Assert.Equal("system", entry.UserId);
    }

    [Fact]
    public async Task SaveChanges_bez_zmen_nezapise_nic()
    {
        await using var db = _fixture.NewContext();
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.AuditLogs.ToListAsync());
    }

    [Fact]
    public async Task Nacteni_entity_bez_zmeny_nezapise_audit()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        _ = await db.Members.SingleAsync(m => m.Id == member.Id);
        await db.SaveChangesAsync();

        // Jediný záznam je ten z vložení, čtení nic nepřidalo.
        Assert.Single(await ReadAuditAsync(nameof(Member)));
    }

    [Fact]
    public async Task Hromadne_ulozeni_zapise_zaznam_ke_kazde_entite()
    {
        await using var db = _fixture.NewContext();
        db.Members.Add(TestData.Member(login: "a"));
        db.Members.Add(TestData.Member(login: "b"));
        db.Members.Add(TestData.Member(login: "c"));
        await db.SaveChangesAsync();

        Assert.Equal(3, (await ReadAuditAsync(nameof(Member))).Count);
    }

    /// <summary>
    /// <c>ExecuteUpdateAsync</c> jde přímo do SQL a <c>SaveChanges</c> vůbec nezavolá,
    /// takže se <b>neauditouje</b>. Je to vlastnost, ne chyba — ale je dobré, aby ji
    /// test pojmenoval, než na ni někdo narazí při hledání chybějícího záznamu.
    /// </summary>
    [Fact]
    public async Task ExecuteUpdate_obchazi_audit()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        await db.Members.Where(m => m.Id == member.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.Name, "PresSql"));

        Assert.Single(await ReadAuditAsync(nameof(Member)));
    }

    // ---------------------------------------------------------------- opravené chyby

    /// <summary>
    /// Regresní test: <c>SavingChangesAsync</c> běží před uložením, takže primární klíč
    /// vkládané entity je v tu chvíli jen dočasný placeholder (EF ho generuje jako
    /// záporné číslo). Dřív se do auditu dostal právě ten, takže žádný záznam typu
    /// <c>Added</c> nešel spárovat s řádkem, který popisuje.
    /// </summary>
    [Fact]
    public async Task Audit_vlozene_entity_nese_skutecny_klic_ne_docasny_placeholder()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);

        var entry = Assert.Single(await ReadAuditAsync(nameof(Member)), a => a.Action == "Added");

        Assert.Equal(member.Id.ToString(), entry.EntityId);
        Assert.True(int.Parse(entry.EntityId) > 0,
            $"EntityId má být skutečný klíč, ne dočasná hodnota ({entry.EntityId})");
    }

    /// <summary>
    /// Regresní test: s <c>UseLazyLoadingProxies()</c> je entita načtená z DB instancí
    /// dynamického podtypu, takže <c>Entity.GetType().Name</c> vrátí <c>"MemberProxy"</c>.
    /// Audit tabulka pak měla pro tutéž entitu dva různé názvy podle toho, jestli šlo
    /// o vložení nebo o změnu, a nedalo se v ní filtrovat.
    /// </summary>
    [Fact]
    public async Task Audit_pouziva_nazev_entity_z_modelu_a_ne_nazev_proxy_typu()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var tracked = await db.Members.SingleAsync(m => m.Id == member.Id);
        // Runtime typ je tady proxy, ne Member — o tom ten test je.
        Assert.NotEqual(nameof(Member), tracked.GetType().Name);
        tracked.Name = "Zmeneno";
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        var types = await verify.AuditLogs.Select(a => a.EntityType).Distinct().ToListAsync();

        Assert.Equal([nameof(Member)], types);
        Assert.DoesNotContain("Proxy", string.Join(",", types));
    }

    [Fact]
    public async Task Audit_vlozeni_i_zmeny_stejne_entity_pouziva_stejny_EntityId()
    {
        await using var seed = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(seed);

        await using var db = _fixture.NewContext();
        var tracked = await db.Members.SingleAsync(m => m.Id == member.Id);
        tracked.Name = "Zmeneno";
        await db.SaveChangesAsync();

        var entries = await ReadAuditAsync(nameof(Member));

        Assert.Equal(2, entries.Count);
        Assert.Single(entries.Select(e => e.EntityId).Distinct());
        Assert.Equal(member.Id.ToString(), entries[0].EntityId);
    }

    /// <summary>
    /// Doplňování skutečných klíčů volá vnořené <c>SaveChangesAsync</c>. Musí to fungovat
    /// i uvnitř explicitní transakce — přesně tak pracuje <c>RefreshTokenService.CreateAsync</c>,
    /// která revokaci starých tokenů a vytvoření nového drží atomicky.
    /// </summary>
    [Fact]
    public async Task Doplneni_klice_funguje_i_uvnitr_explicitni_transakce()
    {
        await using var db = _fixture.NewContext();
        await using var transaction = await db.Database.BeginTransactionAsync();

        var member = TestData.Member(login: "v-transakci");
        db.Members.Add(member);
        await db.SaveChangesAsync();
        await transaction.CommitAsync();

        var entry = Assert.Single(await ReadAuditAsync(nameof(Member)));
        Assert.Equal(member.Id.ToString(), entry.EntityId);
    }

    /// <summary>
    /// Rollback transakce musí vzít s sebou i audit řádky — nesmí zůstat záznam
    /// o změně, která se nikdy neuložila.
    /// </summary>
    [Fact]
    public async Task Rollback_transakce_zahodi_i_audit_zaznamy()
    {
        await using var db = _fixture.NewContext();
        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            db.Members.Add(TestData.Member(login: "zahozeny"));
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.AuditLogs.ToListAsync());
        Assert.Empty(await verify.Members.ToListAsync());
    }

    /// <summary>Dvě uložení za sebou na jednom kontextu si nesmí přepsat klíče.</summary>
    [Fact]
    public async Task Dve_ulozeni_na_stejnem_kontextu_maji_kazde_spravny_klic()
    {
        await using var db = _fixture.NewContext();
        var first = TestData.Member(login: "prvni");
        db.Members.Add(first);
        await db.SaveChangesAsync();

        var second = TestData.Member(login: "druhy");
        db.Members.Add(second);
        await db.SaveChangesAsync();

        var entries = await ReadAuditAsync(nameof(Member));
        var added = entries.Where(e => e.Action == "Added").Select(e => e.EntityId).Order().ToList();
        var expected = new[] { first.Id.ToString(), second.Id.ToString() }.Order().ToList();

        Assert.Equal(expected, added);
    }

    /// <summary>
    /// Regresní test k nálezu z code review: když dopsání skutečných klíčů selže,
    /// nesmí po sobě nechat audit řádky ve stavu <c>Modified</c>. Kontext je scoped
    /// na celý Blazor okruh, takže by se neuložené UPDATE přehrály při příštím —
    /// úplně nesouvisejícím — uložení uživatele.
    /// </summary>
    [Fact]
    public async Task Selhani_dopsani_klicu_nenecha_audit_radky_ve_stavu_Modified()
    {
        await using var db = _fixture.NewContext();
        await TestData.SeedMemberAsync(db);

        // Po úspěšném uložení nesmí v change trackeru zůstat nic rozepsaného —
        // ať už dopsání klíčů prošlo, nebo ne.
        Assert.Empty(db.ChangeTracker.Entries<AuditLog>()
            .Where(e => e.State is EntityState.Modified or EntityState.Added));
    }

    /// <summary>
    /// Druhé, nesouvisející uložení na stejném kontextu nesmí přinést UPDATE audit
    /// řádků z prvního uložení.
    /// </summary>
    [Fact]
    public async Task Nasledujici_ulozeni_neprehraje_audit_ucetnictvi_z_predchoziho()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);

        var auditIdsAfterFirst = await ReadAuditAsync(nameof(Member));
        var firstEntityId = Assert.Single(auditIdsAfterFirst).EntityId;

        // Nesouvisející zápis na stejném kontextu.
        db.Events.Add(TestData.Event());
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        var memberAudit = Assert.Single(
            await verify.AuditLogs.Where(a => a.EntityType == nameof(Member)).ToListAsync());
        Assert.Equal(firstEntityId, memberAudit.EntityId);
        Assert.Equal(member.Id.ToString(), memberAudit.EntityId);
    }
}
