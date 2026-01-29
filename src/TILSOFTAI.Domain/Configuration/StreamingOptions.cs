namespace TILSOFTAI.Domain.Configuration;

public sealed class StreamingOptions
{
    public int ChannelCapacity { get; set; } = 256;
    public bool DropDeltaWhenFull { get; set; } = true;
}
