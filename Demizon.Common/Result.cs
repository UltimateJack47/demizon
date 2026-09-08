namespace Demizon.Common;

/// <summary>
/// Druh selhání. Text chyby je pro uživatele, tohle je pro volajícího —
/// bez toho by controller nerozlišil 404 od 500 a musel by hádat z textu.
/// </summary>
public enum ResultErrorKind
{
    /// <summary>Operace se nepovedla na naší straně (výjimka, zápis do DB). → HTTP 500.</summary>
    Failure = 0,

    /// <summary>Entita neexistuje. → HTTP 404.</summary>
    NotFound = 1,

    /// <summary>
    /// Odmítnuto pravidlem, se kterým uživatel může něco udělat — kvóta, limit
    /// velikosti, validace. → HTTP 4xx, a text se mu má zobrazit.
    /// </summary>
    Rejected = 2,
}

/// <summary>
/// Reprezentuje výsledek operace s hodnotou – náhrada za Task&lt;bool&gt; nebo výjimky pro očekávané chyby.
/// </summary>
/// <remarks>
/// Záměrně <b>bez</b> implicitní konverze na <c>bool</c>: právě ta by vrátila
/// problém, kvůli kterému typ vznikl — že se výsledek dá nepozorovaně ignorovat.
/// Kompilátor zahození nenahlásí (C# nemá <c>[[nodiscard]]</c>), takže to musí
/// hlídat review a testy.
/// </remarks>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }
    public ResultErrorKind ErrorKind { get; }

    private Result(bool isSuccess, T? value, string? error, ResultErrorKind errorKind)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
        ErrorKind = errorKind;
    }

    public static Result<T> Ok(T value) => new(true, value, null, ResultErrorKind.Failure);

    public static Result<T> Fail(string error, ResultErrorKind kind = ResultErrorKind.Failure) =>
        new(false, default, error, kind);

    public static Result<T> NotFound(string error) => Fail(error, ResultErrorKind.NotFound);

    public static Result<T> Rejected(string error) => Fail(error, ResultErrorKind.Rejected);
}

/// <summary>
/// Reprezentuje výsledek operace bez návratové hodnoty.
/// </summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }
    public ResultErrorKind ErrorKind { get; }

    private Result(bool isSuccess, string? error, ResultErrorKind errorKind)
    {
        IsSuccess = isSuccess;
        Error = error;
        ErrorKind = errorKind;
    }

    public static Result Ok() => new(true, null, ResultErrorKind.Failure);

    public static Result Fail(string error, ResultErrorKind kind = ResultErrorKind.Failure) =>
        new(false, error, kind);

    public static Result NotFound(string error) => Fail(error, ResultErrorKind.NotFound);

    public static Result Rejected(string error) => Fail(error, ResultErrorKind.Rejected);
}
