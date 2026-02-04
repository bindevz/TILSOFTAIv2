namespace TILSOFTAI.Domain.Common;

/// <summary>
/// Represents the result of an operation that does not return a value.
/// </summary>
public readonly struct Result
{
    public bool IsSuccess { get; }
    public Error? Error { get; }
    
    private Result(bool isSuccess, Error? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }
    
    public static Result Success() => new(true, null);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(string code, string message) 
        => new(false, new Error(code, message));
    
    public Result OnFailure(Action<Error> action)
    {
        if (!IsSuccess && Error is not null) action(Error);
        return this;
    }
}

/// <summary>
/// Represents the result of an operation that returns a value.
/// </summary>
public readonly struct Result<T>
{
    public bool IsSuccess { get; }
    public T? Value { get; }
    public Error? Error { get; }
    
    private Result(bool isSuccess, T? value, Error? error)
    {
        IsSuccess = isSuccess;
        Value = value;
        Error = error;
    }
    
    public static Result<T> Success(T value) => new(true, value, null);
    public static Result<T> Failure(Error error) => new(false, default, error);
    public static Result<T> Failure(string code, string message)
        => new(false, default, new Error(code, message));
    
    public static implicit operator Result<T>(T value) => Success(value);
    
    public TResult Match<TResult>(
        Func<T, TResult> onSuccess,
        Func<Error, TResult> onFailure)
    {
        return IsSuccess && Value is not null
            ? onSuccess(Value)
            : onFailure(Error ?? Error.Unknown);
    }
    
    public async Task<TResult> MatchAsync<TResult>(
        Func<T, Task<TResult>> onSuccess,
        Func<Error, Task<TResult>> onFailure)
    {
        return IsSuccess && Value is not null
            ? await onSuccess(Value)
            : await onFailure(Error ?? Error.Unknown);
    }
    
    public Result<TNew> Map<TNew>(Func<T, TNew> mapper)
    {
        return IsSuccess && Value is not null
            ? Result<TNew>.Success(mapper(Value))
            : Result<TNew>.Failure(Error ?? Error.Unknown);
    }
    
    public async Task<Result<TNew>> MapAsync<TNew>(Func<T, Task<TNew>> mapper)
    {
        return IsSuccess && Value is not null
            ? Result<TNew>.Success(await mapper(Value))
            : Result<TNew>.Failure(Error ?? Error.Unknown);
    }
}
