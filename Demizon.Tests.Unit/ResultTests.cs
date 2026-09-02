using Demizon.Common;

namespace Demizon.Tests.Unit;

/// <summary>
/// <see cref="Result"/> a <see cref="Result{T}"/> jsou náhrada za výjimky u očekávaných
/// chyb. Testy hlídají, že se nedá zkonstruovat nekonzistentní stav (úspěch s chybou apod.).
/// </summary>
public class ResultTests
{
    [Fact]
    public void Ok_je_uspech_s_hodnotou_a_bez_chyby()
    {
        var result = Result<int>.Ok(42);

        Assert.True(result.IsSuccess);
        Assert.Equal(42, result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Fail_je_neuspech_s_chybou_a_bez_hodnoty()
    {
        var result = Result<int>.Fail("nenalezeno");

        Assert.False(result.IsSuccess);
        Assert.Equal("nenalezeno", result.Error);
        Assert.Equal(default, result.Value);
    }

    [Fact]
    public void Fail_referencniho_typu_ma_hodnotu_null()
    {
        var result = Result<string>.Fail("chyba");

        Assert.False(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public void Ok_umi_nest_i_null_jako_platnou_hodnotu()
    {
        // Rozlišení „úspěch s null hodnotou“ vs. „selhání“ musí zůstat možné.
        var result = Result<string?>.Ok(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Bezhodnotovy_Ok_je_uspech_bez_chyby()
    {
        var result = Result.Ok();

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
    }

    [Fact]
    public void Bezhodnotovy_Fail_nese_chybu()
    {
        var result = Result.Fail("nelze smazat");

        Assert.False(result.IsSuccess);
        Assert.Equal("nelze smazat", result.Error);
    }

    [Fact]
    public void Konstruktor_je_privatni_takze_nekonzistentni_stav_nelze_vytvorit()
    {
        Assert.Empty(typeof(Result).GetConstructors());
        Assert.Empty(typeof(Result<int>).GetConstructors());
    }
}
