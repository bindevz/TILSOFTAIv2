namespace TILSOFTAI.Domain.Configuration;

public sealed class AtomicOptions
{
    public int MaxLimit { get; set; } = 5000;
    public int MaxJoins { get; set; } = 3;
    public int MaxTimeRangeDays { get; set; } = 3660;
}
