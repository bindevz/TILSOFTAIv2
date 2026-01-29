using System;
using System.Threading;
using System.Threading.Tasks;

namespace TILSOFTAI.Orchestration.Caching;

public interface IEmbeddingClient
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}
