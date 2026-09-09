using Demizon.Common;
using Microsoft.AspNetCore.Mvc;

namespace Demizon.Mvc.Extensions;

/// <summary>
/// Překlad <see cref="ResultErrorKind"/> na HTTP odpověď. Jedno místo, aby se
/// stejné rozhodování neopakovalo v každé akci — a aby bylo vidět, že mobilní
/// klient dostane vždy stejný tvar těla (<c>{ error }</c>).
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>
    /// Volat jen na neúspěšném výsledku. Úspěch nemá jednu správnou odpověď —
    /// někde je to 204, jinde 201 s tělem — takže si ji akce řeší sama.
    /// </summary>
    public static ActionResult ToErrorResponse(this Result result) =>
        Map(result.IsSuccess, result.ErrorKind, result.Error);

    /// <inheritdoc cref="ToErrorResponse(Result)"/>
    public static ActionResult ToErrorResponse<T>(this Result<T> result) =>
        Map(result.IsSuccess, result.ErrorKind, result.Error);

    private static ActionResult Map(bool isSuccess, ResultErrorKind kind, string? error)
    {
        // Nahlas, ne tiše: zavolat tohle na úspěchu je chyba volajícího a dřív
        // by z ní bylo HTTP 500 s `{ error: null }`, což se hledá špatně.
        if (isSuccess)
        {
            throw new InvalidOperationException(
                "ToErrorResponse se volá jen na neúspěšném výsledku.");
        }

        return MapKind(kind, error);
    }

    private static ActionResult MapKind(ResultErrorKind kind, string? error) => kind switch
    {
        ResultErrorKind.NotFound => new NotFoundObjectResult(new { error }),
        // 400, ne 409: mobilní klienti už dnes umí u 400 zobrazit text ze
        // serveru, a právě u zamítnutí (kvóta, limit) je ten text to podstatné.
        ResultErrorKind.Rejected => new BadRequestObjectResult(new { error }),
        _ => new ObjectResult(new { error }) { StatusCode = StatusCodes.Status500InternalServerError },
    };
}
