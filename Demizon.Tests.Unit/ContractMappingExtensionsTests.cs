using Demizon.Contracts.Attendances;
using Demizon.Dal.Entities;
using Demizon.Mvc.Mapping;

namespace Demizon.Tests.Unit;

/// <summary>
/// Hranice kontraktu: API vrací <c>Demizon.Contracts.*</c> DTO, nikdy EF entity.
/// Tyto testy hlídají, že se při mapování nic neztratí ani neprosákne.
/// </summary>
public class ContractMappingExtensionsTests
{
    // ---------------------------------------------------------------- Event

    [Fact]
    public void Event_ToDto_prenese_vsechna_pole()
    {
        var entity = new Event
        {
            Id = 42,
            Name = "Krojovaný ples",
            DateFrom = new DateTime(2026, 2, 14, 19, 0, 0, DateTimeKind.Utc),
            DateTo = new DateTime(2026, 2, 15, 2, 0, 0, DateTimeKind.Utc),
            Place = "Kulturní dům",
            IsCancelled = true,
            IsPublic = true,
            NotifyBeforeDays = 7,
            Recurrence = RecurrenceType.Weekly
        };

        var dto = entity.ToDto();

        Assert.Equal(42, dto.Id);
        Assert.Equal("Krojovaný ples", dto.Name);
        Assert.Equal(entity.DateFrom, dto.DateFrom);
        Assert.Equal(entity.DateTo, dto.DateTo);
        Assert.Equal("Kulturní dům", dto.Place);
        Assert.True(dto.IsCancelled);
        Assert.True(dto.IsPublic);
        Assert.Equal(7, dto.NotifyBeforeDays);
        Assert.Equal("Weekly", dto.Recurrence);
        Assert.Null(dto.MyAttendance);
    }

    [Fact]
    public void Event_ToDto_pripoji_predanou_dochazku()
    {
        var attendance = new AttendanceDto(1, "yes", null, null, DateTime.UtcNow);

        var dto = new Event { Id = 1, Name = "Zkouška" }.ToDto(attendance);

        Assert.Same(attendance, dto.MyAttendance);
    }

    [Theory]
    [InlineData(RecurrenceType.None, "None")]
    [InlineData(RecurrenceType.Weekly, "Weekly")]
    [InlineData(RecurrenceType.Monthly, "Monthly")]
    public void Event_ToDto_serializuje_opakovani_jako_nazev_enumu(RecurrenceType recurrence, string expected)
    {
        var dto = new Event { Name = "x", Recurrence = recurrence }.ToDto();

        Assert.Equal(expected, dto.Recurrence);
    }

    // ---------------------------------------------------------------- Attendance

    /// <summary>
    /// Kontrakt stavu docházky je <b>malými písmeny</b> (<c>"yes"</c>/<c>"maybe"</c>/<c>"no"</c>).
    /// Mobilní klient na tom staví, takže velké písmeno by rozbilo parsování na druhé straně.
    /// </summary>
    [Theory]
    [InlineData(AttendanceStatus.Yes, "yes")]
    [InlineData(AttendanceStatus.No, "no")]
    [InlineData(AttendanceStatus.Maybe, "maybe")]
    public void Attendance_ToDto_serializuje_stav_malymi_pismeny(AttendanceStatus status, string expected)
    {
        var dto = new Attendance { Id = 1, Status = status }.ToDto();

        Assert.Equal(expected, dto.Status);
    }

    [Fact]
    public void Attendance_ToDto_prenese_komentar_roli_a_cas()
    {
        var lastUpdated = new DateTime(2026, 5, 1, 12, 30, 0, DateTimeKind.Utc);
        var entity = new Attendance
        {
            Id = 7,
            Status = AttendanceStatus.Maybe,
            Comment = "Přijdu později",
            ActivityRole = AttendanceActivityRole.Musician,
            LastUpdated = lastUpdated
        };

        var dto = entity.ToDto();

        Assert.Equal(7, dto.Id);
        Assert.Equal("maybe", dto.Status);
        Assert.Equal("Přijdu později", dto.Comment);
        Assert.Equal("Musician", dto.ActivityRole);
        Assert.Equal(lastUpdated, dto.LastUpdated);
    }

    [Fact]
    public void Attendance_ToDto_necha_nevyplnenou_roli_null()
    {
        var dto = new Attendance { Id = 1, ActivityRole = null }.ToDto();

        Assert.Null(dto.ActivityRole);
    }

    // ---------------------------------------------------------------- Member

    [Fact]
    public void Member_ToProfileDto_neprosakuje_hash_hesla()
    {
        var entity = new Member
        {
            Id = 3,
            Name = "Anna",
            Surname = "Dvořáková",
            Login = "anna",
            Email = "anna@demizon.test",
            PasswordHash = "SUPER-TAJNY-HASH",
            Role = UserRole.Admin,
            Gender = Gender.Female
        };

        var dto = entity.ToProfileDto();

        Assert.Equal(3, dto.Id);
        Assert.Equal("Anna", dto.Name);
        Assert.Equal("Dvořáková", dto.Surname);
        Assert.Equal("anna", dto.Login);
        Assert.Equal("anna@demizon.test", dto.Email);
        Assert.Equal("Admin", dto.Role);
        Assert.Equal("Female", dto.Gender);

        // MemberProfileDto je record — kdyby někdo přidal pole s hashem, chytí to tady.
        Assert.DoesNotContain("SUPER-TAJNY-HASH", dto.ToString());
    }

    [Fact]
    public void Member_ToProfileDto_zvlada_chybejici_email()
    {
        var dto = new Member { Id = 1, Name = "A", Surname = "B", Login = "ab", Email = null }.ToProfileDto();

        Assert.Null(dto.Email);
    }

    // ---------------------------------------------------------------- Dance

    [Fact]
    public void Dance_ToDto_vraci_jen_viditelna_videa()
    {
        var dance = new Dance
        {
            Id = 5,
            Name = "Sedlácká",
            Region = "Morava",
            Description = "veřejný popis",
            InternalDescription = "interní poznámka",
            Lyrics = "text písně",
            Videos =
            [
                new VideoLink { Id = 1, Name = "Viditelné", Url = "https://example.test/1", IsVisible = true },
                new VideoLink { Id = 2, Name = "Skryté", Url = "https://example.test/2", IsVisible = false },
                new VideoLink { Id = 3, Name = "Druhé viditelné", Url = "https://example.test/3", IsVisible = true }
            ]
        };

        var dto = dance.ToDto();

        Assert.Equal(5, dto.Id);
        Assert.Equal("Sedlácká", dto.Name);
        Assert.Equal("Morava", dto.Region);
        Assert.Equal("veřejný popis", dto.Description);
        Assert.Equal("interní poznámka", dto.InternalDescription);
        Assert.Equal("text písně", dto.Lyrics);
        Assert.Equal([1, 3], dto.Videos.Select(v => v.Id));
    }

    [Fact]
    public void Dance_ToDto_bez_videi_vraci_prazdny_seznam()
    {
        var dto = new Dance { Id = 1, Name = "x" }.ToDto();

        Assert.NotNull(dto.Videos);
        Assert.Empty(dto.Videos);
    }

    // ---------------------------------------------------------------- VideoLink

    [Fact]
    public void VideoLink_ToDto_prenese_vsechna_pole()
    {
        var entity = new VideoLink
        {
            Id = 9,
            Name = "Vystoupení 2025",
            Url = "https://youtu.be/abc",
            Year = 2025,
            IsVisible = true,
            IsInternal = true
        };

        var dto = entity.ToDto();

        Assert.Equal(9, dto.Id);
        Assert.Equal("Vystoupení 2025", dto.Name);
        Assert.Equal("https://youtu.be/abc", dto.Url);
        Assert.Equal(2025, dto.Year);
        Assert.True(dto.IsVisible);
        Assert.True(dto.IsInternal);
    }

    // ---------------------------------------------------------------- File

    /// <summary>
    /// <c>ToDocumentDto</c> posílá klientovi jen jméno souboru, ne celou cestu na disku —
    /// úložná cesta je interní detail.
    /// </summary>
    [Theory]
    [InlineData("files/documents/abc/noty.pdf", "noty.pdf")]
    [InlineData("noty.pdf", "noty.pdf")]
    [InlineData("db-stored", "db-stored")]
    public void File_ToDocumentDto_vraci_jen_jmeno_souboru(string path, string expectedName)
    {
        var dto = new Dal.Entities.File
        {
            Id = 1,
            Path = path,
            ContentType = "application/pdf",
            FileSize = 2048
        }.ToDocumentDto();

        Assert.Equal(expectedName, dto.FileName);
        Assert.Equal("application/pdf", dto.ContentType);
        Assert.Equal(2048, dto.FileSize);
    }
}
