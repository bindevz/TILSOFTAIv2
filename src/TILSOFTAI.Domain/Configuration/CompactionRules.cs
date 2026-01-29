namespace TILSOFTAI.Domain.Configuration;

public sealed class CompactionRules
{
    public string[] RemoveFields { get; set; } = Array.Empty<string>();
    public int MaxArrayLength { get; set; } = 50;
    public int HeadCount { get; set; } = 20;
    public int TailCount { get; set; } = 5;
}
