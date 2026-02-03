using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TILSOFTAI.Domain.Resilience;

namespace TILSOFTAI.Infrastructure.Resilience;

public class CircuitBreakerDelegatingHandler : DelegatingHandler
{
    private readonly ICircuitBreakerPolicy _policy;

    public CircuitBreakerDelegatingHandler(ICircuitBreakerPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return await _policy.ExecuteAsync(async ct =>
        {
            var response = await base.SendAsync(request, ct);
            
            // Optionally: treat 5xx as failures for the circuit breaker? 
            // In a simple policy handle<Exception>, we only break on exceptions.
            // If we want to break on 500s, we'd need to throw or configure the policy to handle results.
            // For now, adhering to standard "Exceptions" trip the breaker.
            return response;
        }, cancellationToken);
    }
}
