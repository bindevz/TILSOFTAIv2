using System;
using System.Threading;
using System.Threading.Tasks;

namespace TILSOFTAI.Domain.Resilience;

public interface IRetryPolicy
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> action, CancellationToken cancellationToken);
    
    // Spec requested Func<int, ...> but simple ExecuteAsync usually implies wrapping.
    // Spec diff intent says: "ExecuteAsync<T>(Func<int, CancellationToken, Task<T>> action, CancellationToken ct)"
    // where int is attempt number.
    // This allows the action to know which attempt it is.
    Task<T> ExecuteAsync<T>(Func<int, CancellationToken, Task<T>> action, CancellationToken cancellationToken);
}
