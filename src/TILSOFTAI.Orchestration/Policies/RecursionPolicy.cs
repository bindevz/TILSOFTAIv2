using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Orchestration.Policies;

public sealed class RecursionPolicy
{
    public const string ErrorCode = "ERR_RECURSION_LIMIT";

    private readonly ChatOptions _chatOptions;
    private readonly AsyncLocal<int> _depth = new();

    public RecursionPolicy(IOptions<ChatOptions> chatOptions)
    {
        _chatOptions = chatOptions?.Value ?? throw new ArgumentNullException(nameof(chatOptions));
    }

    public void Reset()
    {
        _depth.Value = 0;
    }

    public bool TryAdvance(out string errorCode)
    {
        var next = _depth.Value + 1;
        if (next > _chatOptions.MaxRecursiveDepth)
        {
            errorCode = ErrorCode;
            return false;
        }

        _depth.Value = next;
        errorCode = string.Empty;
        return true;
    }
}
