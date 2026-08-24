namespace ModularMonolith.Framework.Results;

public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    protected Result(bool isSuccess, Error error)
    {
        if (isSuccess && error != Error.None)
            throw new InvalidOperationException("Cannot create a successful Result with an error.");
        if (!isSuccess && error == Error.None)
            throw new InvalidOperationException("Cannot create a failed Result without an error.");
        IsSuccess = isSuccess;
        Error = error;
    }

    public static Result Success() => new(true, Error.None);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(string code, string message) => new(false, new Error(code, message));
    public static Result<T> Success<T>(T value) => new(value, true, Error.None);
    public static Result<T> Failure<T>(Error error) => new(default, false, error);
}

public class Result<T> : Result
{
    public T? Value { get; }
    internal Result(T? value, bool isSuccess, Error error) : base(isSuccess, error) => Value = value;
}

public sealed record Error(string Code, string Message)
{
    public static readonly Error None = new(string.Empty, string.Empty);
    public static readonly Error NotFound = new("NOT_FOUND", "Resource was not found.");
    public static readonly Error Conflict = new("CONFLICT", "Resource already exists.");
    public static readonly Error Validation = new("VALIDATION", "Validation failed.");
    public static readonly Error Unauthorized = new("UNAUTHORIZED", "Unauthorized.");
    public static readonly Error Forbidden = new("FORBIDDEN", "Forbidden.");
    public static readonly Error Internal = new("INTERNAL", "Internal server error.");
    public static readonly Error ConcurrencyConflict = new("CONCURRENCY_CONFLICT", "The resource was modified by another transaction. Please retry.");
}
