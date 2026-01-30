using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Tokens;

namespace TILSOFTAI.Api.Auth;

internal sealed class JsonWebKeySetRetriever : IConfigurationRetriever<JsonWebKeySet>
{
    public async Task<JsonWebKeySet> GetConfigurationAsync(
        string address,
        IDocumentRetriever retriever,
        CancellationToken cancel)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            throw new ArgumentException("JWKS address must be provided.", nameof(address));
        }

        if (retriever is null)
        {
            throw new ArgumentNullException(nameof(retriever));
        }

        var json = await retriever.GetDocumentAsync(address, cancel);
        return new JsonWebKeySet(json);
    }
}
