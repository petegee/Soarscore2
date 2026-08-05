// Result<T> — docs/plans/command-side-steel-thread-plan.md WI-3,
// LADR-0003 "Result type".
//
// Lives in Domain, not Application: WI-4's decide functions (Person.Register
// etc.) return it, and Domain cannot reference Application (LADR-0003
// "Project layout"). Hand-rolled rather than a library (LanguageExt would add
// a second language in the codebase; CSharpFunctionalExtensions is defensible
// but unnecessary at this size).
//
// Total and non-throwing: a decide function or handler always returns one of
// exactly two states and never throws to signal a domain-level failure — the
// only throw in this file guards a caller reading .Value/.Error off the wrong
// branch, which is a programming error, not a domain outcome.

namespace Soarscore.Domain;

/// <summary>
/// One validation failure. LADR-0002 §4: "Defect renders as an API error
/// body — check identity, path into the document, and a message naming the
/// construct." <see cref="Code"/> is that check identity; the sixteen adoption
/// checks (LADR-0003 "Validate()", out of scope for this thread) are its first
/// real producer. <see cref="Result{T}"/> carries a list of these so that
/// thread can return every failure it finds, not just the first.
/// </summary>
public sealed record Defect(string Code, string Path, string Message);

/// <summary>
/// Success carries a <typeparamref name="T"/>; failure carries a stable,
/// machine-readable <see cref="Code"/>, a human <see cref="Message"/>, and the
/// <see cref="Defects"/> a validation failure needs to report more than one
/// problem at once. Never both, never neither.
/// </summary>
public readonly struct Result<T>
{
    private readonly T? _value;

    private Result(bool isSuccess, T? value, string? code, string? message, IReadOnlyList<Defect> defects)
    {
        IsSuccess = isSuccess;
        _value = value;
        Code = code;
        Message = message;
        Defects = defects;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Failure only. Null on success.</summary>
    public string? Code { get; }

    /// <summary>Failure only. Null on success.</summary>
    public string? Message { get; }

    /// <summary>Failure only. Empty (never null) on success.</summary>
    public IReadOnlyList<Defect> Defects { get; }

    /// <summary>Throws if <see cref="IsFailure"/> — read <see cref="IsSuccess"/> first, or use <see cref="Match{TResult}"/>.</summary>
    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException($"Result is a failure ({Code}): {Message}");

    public static Result<T> Success(T value) => new(true, value, null, null, []);

    public static Result<T> Failure(string code, string message, IReadOnlyList<Defect>? defects = null) =>
        new(false, default, code, message, defects ?? []);

    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<Result<T>, TResult> onFailure) =>
        IsSuccess ? onSuccess(_value!) : onFailure(this);
}
