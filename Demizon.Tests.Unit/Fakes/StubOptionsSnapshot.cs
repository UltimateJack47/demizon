using Microsoft.Extensions.Options;

namespace Demizon.Tests.Unit.Fakes;

/// <summary>
/// <see cref="IOptionsSnapshot{T}"/> nad pevnou hodnotou. <c>Options.Create</c> z BCL
/// vrací jen <see cref="IOptions{T}"/>, což na konstruktor služeb tohoto projektu nestačí.
/// </summary>
public sealed class StubOptionsSnapshot<T>(T value) : IOptionsSnapshot<T> where T : class
{
    public T Value { get; } = value;

    public T Get(string? name) => Value;
}
