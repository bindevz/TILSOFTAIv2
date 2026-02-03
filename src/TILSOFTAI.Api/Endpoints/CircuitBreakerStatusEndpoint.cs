using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TILSOFTAI.Infrastructure.Resilience;

namespace TILSOFTAI.Api.Endpoints;

[ApiController]
[Route("health/circuits")]
public class CircuitBreakerStatusEndpoint : ControllerBase
{
    private readonly TILSOFTAI.Infrastructure.Resilience.CircuitBreakerRegistry _registry;

    public CircuitBreakerStatusEndpoint(TILSOFTAI.Infrastructure.Resilience.CircuitBreakerRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public IActionResult GetCircuitStatuses()
    {
        var states = _registry.GetAllStates();
        var result = states.Select(kvp => new
        {
            Name = kvp.Key,
            State = kvp.Value.ToString()
        });

        return Ok(new { circuits = result });
    }
}
