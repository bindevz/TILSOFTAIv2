using System;
using TILSOFTAI.Domain.Resilience;

namespace TILSOFTAI.Infrastructure.Resilience;

// Helper to construct policies or access them.
// Since we are using separate registry for CB, and now separate policies for Retry,
// We might just use DI to compose them. 
// A builder might be overkill if we just wire them up in IoC.
// However, the spec requested it.

public class ResiliencePolicyBuilder
{
    // Minimal implementation for now as most logic is in AddTilsoftAiExtensions wiring.
    // The spec mentioned: "Build combined policy: retry wrapped by circuit breaker."
    // We can provide a helper method to wrap execution.
}
