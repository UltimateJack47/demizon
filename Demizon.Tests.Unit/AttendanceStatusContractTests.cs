using Demizon.Dal.Entities;
using Demizon.Mvc.Controllers.Api;
using Demizon.Mvc.Mapping;

namespace Demizon.Tests.Unit;

/// <summary>
/// Kontrakt stavu docházky je lowercase string <c>"yes"|"maybe"|"no"</c> (viz AGENTS.md).
/// Parsuje se v <c>AttendancesController.ParseStatus</c>.
/// </summary>
/// <remarks>
/// Nebezpečí je tady tiché: <c>ParseStatus</c> má fallback na <see cref="AttendanceStatus.No"/>,
/// takže překlep nebo změna casingu na straně klienta neskončí chybou, ale zapsanou
/// <em>neúčastí</em>. Proto se testuje i round-trip s mapováním do DTO.
/// </remarks>
public class AttendanceStatusContractTests
{
    [Theory]
    [InlineData("yes", AttendanceStatus.Yes)]
    [InlineData("maybe", AttendanceStatus.Maybe)]
    [InlineData("no", AttendanceStatus.No)]
    public void ParseStatus_zna_tri_hodnoty_kontraktu(string input, AttendanceStatus expected)
    {
        Assert.Equal(expected, AttendancesController.ParseStatus(input));
    }

    [Theory]
    [InlineData("YES")]
    [InlineData("Yes")]
    [InlineData("MaYbE")]
    public void ParseStatus_je_case_insensitive(string input)
    {
        Assert.NotEqual(AttendanceStatus.No, AttendancesController.ParseStatus(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ano")]
    [InlineData("true")]
    [InlineData("1")]
    [InlineData("probably")]
    public void ParseStatus_neznamou_hodnotu_bere_jako_neucast(string? input)
    {
        // Záměrný fallback, ne chyba — endpoint nikdy nespadne na neznámém stavu.
        Assert.Equal(AttendanceStatus.No, AttendancesController.ParseStatus(input));
    }

    /// <summary>
    /// Nejdůležitější test celé trojice: co API vrátí, to musí umět i přijmout zpět.
    /// Kdyby se rozešel serializační a parsovací konec, klient by si stav přepsal na "no".
    /// </summary>
    [Theory]
    [InlineData(AttendanceStatus.Yes)]
    [InlineData(AttendanceStatus.Maybe)]
    [InlineData(AttendanceStatus.No)]
    public void Serializace_a_parsovani_stavu_jsou_navzajem_inverzni(AttendanceStatus status)
    {
        var wireValue = new Attendance { Id = 1, Status = status }.ToDto().Status;

        Assert.Equal(status, AttendancesController.ParseStatus(wireValue));
    }

    [Fact]
    public void Vsechny_hodnoty_enumu_jsou_pokryte_kontraktem()
    {
        // Kdyby někdo přidal čtvrtý stav (třeba "Late"), tento test spadne a připomene,
        // že ho je nutné doplnit i do ParseStatus — jinak by se tiše parsoval jako No.
        foreach (var status in Enum.GetValues<AttendanceStatus>())
        {
            var wireValue = new Attendance { Id = 1, Status = status }.ToDto().Status;

            Assert.Equal(status, AttendancesController.ParseStatus(wireValue));
        }
    }
}
