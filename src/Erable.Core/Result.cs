namespace Erable.Core;

/// <summary>
/// An expected business failure (a rule says no), as opposed to a bug. <see cref="Code"/> is stable: the UI
/// translates it and the API returns it in its <c>ProblemDetails</c>, e.g. <c>tree.move.crossRoot</c>.
/// </summary>
/// <param name="Code">Stable dotted identifier of the error.</param>
/// <param name="Message">English explanation for logs and developers (never shown as is to users).</param>
/// <param name="Extensions">
/// Optional machine-readable details, returned as extensions of the <c>ProblemDetails</c> (e.g. the IDs of a category
/// loop, the pages blocking a type change). Values are JSON-serializable.
/// </param>
public sealed record DomainError(string Code, string Message, IReadOnlyDictionary<string, object?>? Extensions = null);

/// <summary>Outcome of a business operation: a value or a <see cref="DomainError"/>, no exception for business cases.</summary>
public readonly struct Result<T>
{
    private readonly T? _value;
    private readonly DomainError? _error;

    private Result(T value)
    {
        _value = value;
        _error = null;
    }

    private Result(DomainError error)
    {
        _value = default;
        _error = error;
    }

    public bool IsSuccess => _error is null;

    public T Value => _error is null
        ? _value!
        : throw new InvalidOperationException($"No value: the operation failed with '{_error.Code}'.");

    public DomainError Error => _error ?? throw new InvalidOperationException("No error: the operation succeeded.");

#pragma warning disable CA2225 // Named alternates: Result.Success / Result.Failure.
    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>(error);
    }
#pragma warning restore CA2225

    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({_error!.Code})";
}

/// <summary>Factory methods of <see cref="Result{T}"/>, for when the implicit conversions do not apply.</summary>
public static class Result
{
    public static Result<T> Success<T>(T value) => value;

    public static Result<T> Failure<T>(DomainError error) => error;
}
