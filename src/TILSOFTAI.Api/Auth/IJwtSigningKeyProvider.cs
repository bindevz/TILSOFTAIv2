using Microsoft.IdentityModel.Tokens;

namespace TILSOFTAI.Api.Auth;

public interface IJwtSigningKeyProvider
{
    IReadOnlyCollection<SecurityKey> GetKeys();
}
