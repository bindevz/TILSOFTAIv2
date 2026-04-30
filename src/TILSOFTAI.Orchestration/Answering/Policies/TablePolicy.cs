namespace TILSOFTAI.Orchestration.Answering;

public sealed record TablePolicy
{
    public bool Enabled { get; init; } = true;
    public int MaxDisplayedRows { get; init; } = 20;
    public bool IncludeRowCount { get; init; } = true;
    public bool IncludeTruncationNotice { get; init; } = true;

    public void Validate()
    {
        if (Enabled && MaxDisplayedRows <= 0)
        {
            throw new InvalidOperationException("Answer policy table.maxDisplayedRows must be greater than zero when table.enabled is true.");
        }
    }
}
