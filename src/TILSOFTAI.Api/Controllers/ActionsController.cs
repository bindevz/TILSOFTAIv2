using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TILSOFTAI.Domain.ExecutionContext;
using TILSOFTAI.Orchestration.Actions;

namespace TILSOFTAI.Api.Controllers;

[ApiController]
[Route("api/actions")]
[Authorize]
public sealed class ActionsController : ControllerBase
{
    private readonly ActionApprovalService _approvalService;
    private readonly IExecutionContextAccessor _contextAccessor;

    public ActionsController(ActionApprovalService approvalService, IExecutionContextAccessor contextAccessor)
    {
        _approvalService = approvalService ?? throw new ArgumentNullException(nameof(approvalService));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    }

    [HttpPost("{actionId}/approve")]
    [Authorize(Roles = "ai_action_approver")]
    public async Task<IActionResult> Approve(string actionId, CancellationToken cancellationToken)
    {
        var result = await _approvalService.ApproveAsync(_contextAccessor.Current, actionId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{actionId}/reject")]
    [Authorize(Roles = "ai_action_approver")]
    public async Task<IActionResult> Reject(string actionId, CancellationToken cancellationToken)
    {
        var result = await _approvalService.RejectAsync(_contextAccessor.Current, actionId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("{actionId}/execute")]
    [Authorize(Roles = "ai_action_approver")]
    public async Task<IActionResult> Execute(string actionId, CancellationToken cancellationToken)
    {
        var result = await _approvalService.ExecuteAsync(_contextAccessor.Current, actionId, cancellationToken);
        return Ok(result);
    }
}
