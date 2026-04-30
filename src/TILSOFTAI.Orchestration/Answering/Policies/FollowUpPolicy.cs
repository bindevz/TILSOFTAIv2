namespace TILSOFTAI.Orchestration.Answering;

public sealed record FollowUpPolicy
{
    public bool IncludeMissingFields { get; init; } = true;
}
