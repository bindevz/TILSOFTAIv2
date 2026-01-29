namespace TILSOFTAI.Domain.ExecutionContext;

public interface IExecutionContextAccessor
{
    TilsoftExecutionContext Current { get; }
}
