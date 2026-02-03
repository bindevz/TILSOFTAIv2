using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TILSOFTAI.Domain.Resilience;

namespace TILSOFTAI.Infrastructure.Resilience;

public class RetryDelegatingHandler : DelegatingHandler
{
    private readonly TILSOFTAI.Domain.Resilience.IRetryPolicy _policy;

    public RetryDelegatingHandler(TILSOFTAI.Domain.Resilience.IRetryPolicy policy)
    {
        _policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // We use the policy to execute the base SendAsync
        return await _policy.ExecuteAsync<HttpResponseMessage>(async (ct) =>
        {
            var response = await base.SendAsync(request, ct);
            
            // Check for transient status codes that didn't throw exceptions
            // If the response is a transient failure, we want to retry.
            // By default Polly implementation above only catches Exceptions.
            // We need to ensure transient HTTP Status codes throw or are handled.
            // Since our TransientExceptionClassifier handles HttpRequestException status codes,
            // we should ensure non-success transient codes throw so Polly catches them.
            
            if (!response.IsSuccessStatusCode)
            {
                // We throw so the Policy.Handle<Exception> catches it.
                // We only throw if it's transient, otherwise we let it pass through (permanent error).
                // Or we throw always and let Classifier decide?
                // Better: Throw HttpRequestException, let Classifier decide.
                // But wait, if we throw, we lose the response content which might be needed?
                // Usually for 5xx/429 we don't care about content as much as retrying.
                // But strictly speaking, standard HttpClient.EnsureSuccessStatusCode() throws.
                
                // Let's manually check if it's transient code, and if so, throw to trigger retry.
                if ((int)response.StatusCode == 408 || 
                    (int)response.StatusCode == 429 || 
                    (int)response.StatusCode >= 500)
                {
                     // Respect Retry-After header for 429
                     if ((int)response.StatusCode == 429 && response.Headers.RetryAfter != null)
                     {
                         var delay = response.Headers.RetryAfter.Delta;
                         if (response.Headers.RetryAfter.Date.HasValue)
                         {
                             delay = response.Headers.RetryAfter.Date.Value - DateTimeOffset.UtcNow;
                         }

                         if (delay.HasValue && delay.Value > TimeSpan.Zero)
                         {
                             await Task.Delay(delay.Value, ct);
                         }
                     }

                     response.EnsureSuccessStatusCode(); // This throws HttpRequestException
                }
            }
            
            return response;
        }, cancellationToken);
    }
}
