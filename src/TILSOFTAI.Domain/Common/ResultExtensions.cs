namespace TILSOFTAI.Domain.Common;

/// <summary>
/// Extension methods for Result types.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Converts a nullable value to a Result.
    /// </summary>
    public static Result<T> ToResult<T>(this T? value, Error errorIfNull)
        where T : class
    {
        return value is not null
            ? Result<T>.Success(value)
            : Result<T>.Failure(errorIfNull);
    }
    
    /// <summary>
    /// Combines two results into a tuple result.
    /// </summary>
    public static Result<(T1, T2)> Combine<T1, T2>(
        this Result<T1> first,
        Result<T2> second)
    {
        if (!first.IsSuccess) return Result<(T1, T2)>.Failure(first.Error!);
        if (!second.IsSuccess) return Result<(T1, T2)>.Failure(second.Error!);
        return Result<(T1, T2)>.Success((first.Value!, second.Value!));
    }
}
