using Demizon.Common;

namespace Demizon.Tests.Integration.Infrastructure;

/// <summary>
/// Asserty nad <see cref="Result"/>. Existují proto, že <c>Assert.True(r.IsSuccess)</c>
/// při selhání napíše jen „Expected: True“ a zahodí <c>Error</c> — tedy jedinou
/// informaci, která říká proč.
/// </summary>
public static class ResultAssert
{
    public static void Ok(Result result) =>
        Assert.True(result.IsSuccess, $"Očekáván úspěch, přišlo: {result.Error}");

    /// <summary>Vrací <c>Value</c>, aby šel test napsat jako <c>var id = ResultAssert.Ok(...)</c>.</summary>
    public static T Ok<T>(Result<T> result)
    {
        Assert.True(result.IsSuccess, $"Očekáván úspěch, přišlo: {result.Error}");
        return result.Value!;
    }

    public static void Failed(Result result)
    {
        Assert.False(result.IsSuccess, "Očekáván neúspěch, ale operace prošla.");
        Assert.False(string.IsNullOrWhiteSpace(result.Error), "Neúspěch musí nést text chyby.");
    }

    public static void Failed<T>(Result<T> result)
    {
        Assert.False(result.IsSuccess, "Očekáván neúspěch, ale operace prošla.");
        Assert.False(string.IsNullOrWhiteSpace(result.Error), "Neúspěch musí nést text chyby.");
    }

    public static void Failed(Result result, ResultErrorKind expectedKind)
    {
        Failed(result);
        Assert.Equal(expectedKind, result.ErrorKind);
    }

    public static void Failed<T>(Result<T> result, ResultErrorKind expectedKind)
    {
        Failed(result);
        Assert.Equal(expectedKind, result.ErrorKind);
    }
}
