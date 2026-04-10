namespace Demizon.Common;

/// <summary>
/// Reprezentuje výsledek operace s hodnotou – náhrada za Task&lt;bool&gt; nebo výjimky pro očekávané chyby.
/// </summary>
public sealed class Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public string? Error { get; }

    private Result(bool isSuccess, T? value, string? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }

    public static Result<T> Ok(T value) => new(true, value, null);
    public static Result<T> Fail(string error) => new(false, default, error);
}

/// <summary>
/// Reprezentuje výsledek operace bez návratové hodnoty.
/// </summary>
public sealed class Result
{
    public bool IsSuccess { get; }
    public string? Error { get; }

    private Result(bool isSuccess, string? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Ok() => new(true, null);
    public static Result Fail(string error) => new(false, error);
}
