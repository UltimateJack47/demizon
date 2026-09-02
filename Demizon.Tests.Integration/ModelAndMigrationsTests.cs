using Demizon.Dal;
using Demizon.Dal.Entities;
using Demizon.Tests.Integration.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Demizon.Tests.Integration;

/// <summary>
/// Hlídá konzistenci mezi EF modelem, migracemi a chováním schématu.
/// </summary>
public class ModelAndMigrationsTests : IAsyncDisposable
{
    private readonly DatabaseFixture _fixture = new();

    public ValueTask DisposeAsync() => _fixture.DisposeAsync();

    /// <summary>
    /// Nejcennější test v tomto souboru: pokud někdo změní entitu a zapomene vygenerovat
    /// migraci, projde mu to buildem i všemi ostatními testy (ty jedou nad
    /// <c>EnsureCreated</c>, tedy nad aktuálním modelem) — a rozbije se to až při
    /// nasazení. Tenhle test to zachytí hned.
    /// </summary>
    [Fact]
    public void Model_odpovida_poslednimu_snapshotu_migraci()
    {
        using var db = _fixture.NewContext();

        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot;
        Assert.NotNull(snapshot);

        var initializer = db.GetService<IModelRuntimeInitializer>();
        var snapshotModel = initializer.Initialize(
            ((IMutableModel)snapshot.Model).FinalizeModel(), designTime: true, validationLogger: null);
        var currentModel = db.GetService<IDesignTimeModel>().Model;

        var differ = db.GetService<IMigrationsModelDiffer>();
        var differences = differ.GetDifferences(
            snapshotModel.GetRelationalModel(),
            currentModel.GetRelationalModel());

        Assert.True(differences.Count == 0,
            "EF model se rozešel se snapshotem migrací. Spusť: dotnet ef migrations add <Nazev> "
            + $"--project Demizon.Dal --startup-project Demizon.Mvc. Rozdílů: {differences.Count}.");
    }

    /// <summary>
    /// Migrace musí projít od nuly. Ostatní testy staví schéma přes <c>EnsureCreated</c>,
    /// takže by rozbitou migraci samy nikdy neodhalily.
    /// </summary>
    [Fact]
    public async Task Vsechny_migrace_projdou_na_prazdne_databazi()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<DemizonContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new DemizonContext(options);
        await db.Database.MigrateAsync();

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.NotEmpty(await db.Database.GetAppliedMigrationsAsync());
        // Schéma musí být použitelné, ne jen vytvořené.
        Assert.Empty(await db.Members.ToListAsync());
    }

    // ---------------------------------------------------------------- chování schématu

    [Fact]
    public async Task Klic_nastaveni_je_unikatni()
    {
        await using var db = _fixture.NewContext();
        // DevelopedBy je nasazený přes HasData, takže druhý záznam se stejným klíčem
        // musí narazit na unique index.
        db.Settings.Add(new Setting { Key = SettingKey.DevelopedBy, Value = "duplikat" });

        await Assert.ThrowsAnyAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Seed_data_nastaveni_jsou_v_databazi()
    {
        await using var db = _fixture.NewContext();

        var setting = await db.Settings.SingleAsync(s => s.Key == SettingKey.DevelopedBy);
        Assert.Equal("Jack", setting.Value);
    }

    [Fact]
    public async Task Enumy_se_ukladaji_jako_text_ne_jako_cislo()
    {
        await using var db = _fixture.NewContext();
        var member = TestData.Member(role: UserRole.Admin);
        db.Members.Add(member);
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        var connection = verify.Database.GetDbConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT Role, Gender FROM Members WHERE Id = $id";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$id";
        parameter.Value = member.Id;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("Admin", reader.GetString(0));
        Assert.Equal("Male", reader.GetString(1));
    }

    [Fact]
    public async Task Vychozi_hodnoty_z_modelu_se_uplatni()
    {
        await using var db = _fixture.NewContext();
        var dance = new Dance { Name = "Test" };
        db.Dances.Add(dance);
        var file = new Dal.Entities.File
        {
            Path = "db-stored",
            FileExtension = ".jpg",
            ContentType = "image/jpeg",
            FileSize = 1
        };
        db.Files.Add(file);
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        var storedFile = await verify.Files.SingleAsync(f => f.Id == file.Id);
        Assert.False(storedFile.IsPublic);
        Assert.Equal(FileKind.Image, storedFile.Kind);
    }

    [Fact]
    public async Task Globalni_filtr_soft_delete_je_na_modelu_Member()
    {
        await using var db = _fixture.NewContext();
        var entityType = db.Model.FindEntityType(typeof(Member));

        Assert.NotNull(entityType);
        Assert.NotEmpty(entityType.GetDeclaredQueryFilters());
    }

    [Fact]
    public async Task Smazani_akce_kaskadove_smaze_navazanou_dochazku()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);
        var ev = TestData.Event();
        db.Events.Add(ev);
        await db.SaveChangesAsync();

        db.Attendances.Add(TestData.ActionAttendance(member.Id, ev.Id,
            new DateTime(2026, 6, 1, 18, 0, 0, DateTimeKind.Utc), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        await using var delete = _fixture.NewContext();
        delete.Events.Remove(await delete.Events.SingleAsync(e => e.Id == ev.Id));
        await delete.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        Assert.Empty(await verify.Attendances.Where(a => a.EventId == ev.Id).ToListAsync());
    }

    [Fact]
    public async Task Zkouska_se_uklada_bez_navazane_akce()
    {
        await using var db = _fixture.NewContext();
        var member = await TestData.SeedMemberAsync(db);

        db.Attendances.Add(TestData.RehearsalAttendance(member.Id,
            new DateTime(2026, 5, 1, 18, 0, 0, DateTimeKind.Utc), AttendanceStatus.Yes));
        await db.SaveChangesAsync();

        await using var verify = _fixture.NewContext();
        var stored = await verify.Attendances.SingleAsync();
        Assert.Null(stored.EventId);
        Assert.Null(stored.Event);
    }
}
