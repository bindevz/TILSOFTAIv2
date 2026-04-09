using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TILSOFTAI.Approvals;
using TILSOFTAI.Domain.ExecutionContext;

namespace TILSOFTAI.Api.Controllers;

[ApiController]
[Route("api/actions")]
[Authorize]
public sealed class ActionsController : ControllerBase
{
    private readonly IApprovalEngine _approvalEngine;
    private readonly IExecutionContextAccessor _contextAccessor;

    public ActionsController(IApprovalEngine approvalEngine, IExecutionContextAccessor contextAccessor)
    {
        _approvalEngine = approvalEngine ?? throw new ArgumentNullException(nameof(approvalEngine));
        _contextAccessor = contextAccessor ?? throw new ArgumentNullException(nameof(contextAccessor));
    }

    [HttpPost("{actionId}/approve")]
    [Authorize(Roles = "ai_action_approver")]
    public async Task<IActionResult> Approve(string actionId, CancellationToken cancellationToken)
    {
        var result = await _approvalEngine.ApproveAsync(
            actionId,
            ApprovalContext.FromExecutionContext(_contextAccessor.Current),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{actionId}/reject")]
    [Authorize(Roles = "ai_action_approver")]
    public async Task<IActionResult> Reject(string actionId, CancellationToken cancellationToken)
    {
        var result = await _approvalEngine.RejectAsync(
            actionId,
            ApprovalContext.FromExecutionContext(_contextAccessor.Current),
            cancellationToken);
        return Ok(result);
    }

    [HttpPost("{actionId}/execute")]
    [Authorize(Roles = "ai_action_approver")]
    public async Task<IActionResult> Execute(string actionId, CancellationToken cancellationToken)
    {
        var result = await _approvalEngine.ExecuteAsync(
            actionId,
            ApprovalContext.FromExecutionContext(_contextAccessor.Current),
            cancellationToken);
        return Ok(result);
    }
}
