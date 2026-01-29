using System.Threading;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Infrastructure.ExecutionContext;

public sealed class ExecutionContextAccessor : IExecutionContextAccessor
{
    private static readonly AsyncLocal<TilsoftExecutionContext?> CurrentContext = new();

    public TilsoftExecutionContext Current => CurrentContext.Value ??= new TilsoftExecutionContext();

    public void Set(TilsoftExecutionContext context)
    {
        CurrentContext.Value = context ?? throw new ArgumentNullException(nameof(context));
    }
}
