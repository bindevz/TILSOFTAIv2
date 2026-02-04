using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using TILSOFTAI.Domain.Configuration;

namespace TILSOFTAI.Api.Controllers;

[ApiController]
[Route("v1/models")]
[AllowAnonymous]
public sealed class ModelsController : ControllerBase
{
    private readonly LlmOptions _llmOptions;
    private readonly ILogger<ModelsController> _logger;

    public ModelsController(
        IOptions<LlmOptions> llmOptions,
        ILogger<ModelsController> logger)
    {
        _llmOptions = llmOptions?.Value ?? throw new ArgumentNullException(nameof(llmOptions));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Lists available models compatible with OpenAI API format.
    /// This endpoint is designed to work with Open WebUI and other OpenAI-compatible clients.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ModelsListResponse), StatusCodes.Status200OK)]
    public IActionResult List()
    {
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var modelId = string.IsNullOrWhiteSpace(_llmOptions.Model) ? "gpt-oss:20b" : _llmOptions.Model;

        var response = new ModelsListResponse
        {
            Object = "list",
            Data =
            [
                new ModelInfo
                {
                    Id = modelId,
                    Object = "model",
                    Created = created,
                    OwnedBy = "tilsoft"
                }
            ]
        };

        _logger.LogDebug("Returning model list with {Count} model(s)", response.Data.Count);
        return Ok(response);
    }

    /// <summary>
    /// Retrieves a specific model by ID (OpenAI API compatibility).
    /// </summary>
    [HttpGet("{modelId}")]
    [ProducesResponseType(typeof(ModelInfo), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult Retrieve(string modelId)
    {
        var configuredModelId = string.IsNullOrWhiteSpace(_llmOptions.Model) ? "gpt-oss:20b" : _llmOptions.Model;
        
        // For simplicity, we only have one model configured
        // Return 404 if requesting a different model
        if (!string.Equals(modelId, configuredModelId, StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { error = new { message = $"Model '{modelId}' not found", type = "invalid_request_error" } });
        }

        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var model = new ModelInfo
        {
            Id = configuredModelId,
            Object = "model",
            Created = created,
            OwnedBy = "tilsoft"
        };

        return Ok(model);
    }
}

/// <summary>
/// Response for GET /v1/models
/// </summary>
public sealed class ModelsListResponse
{
    public string Object { get; set; } = "list";
    public List<ModelInfo> Data { get; set; } = [];
}

/// <summary>
/// Model information in OpenAI API format
/// </summary>
public sealed class ModelInfo
{
    public string Id { get; set; } = string.Empty;
    public string Object { get; set; } = "model";
    public long Created { get; set; }
    public string OwnedBy { get; set; } = string.Empty;
}
