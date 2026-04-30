namespace TILSOFTAI.Orchestration.Answering;

public sealed record NoDataPolicy
{
    public bool IncludeUsedFilters { get; init; } = true;
}
