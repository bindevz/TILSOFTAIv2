namespace TILSOFTAI.Domain.Configuration;

public sealed class StreamingOptions
{
    public int ChannelCapacity { get; set; } = 256;
    public bool DropDeltaWhenFull { get; set; } = true;
    public int DeltaFlushIntervalMs { get; set; } = 40;
    public int MaxDeltaBufferChars { get; set; } = 512;
}
