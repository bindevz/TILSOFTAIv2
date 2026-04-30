namespace TILSOFTAI.Orchestration.Answering.Narration;

public sealed class AnswerNarrationPolicy
{
    public bool Enabled { get; set; } = true;
    public bool UseAi { get; set; } = true;
    public bool FallbackOnInvalidOutput { get; set; } = true;
    public int MaxRowsForNarration { get; set; } = 20;
    public int MaxOutputCharacters { get; set; } = 1200;
}
