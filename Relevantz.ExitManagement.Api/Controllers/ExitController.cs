using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Relevantz.ExitManagement.Common.DTOs;
using Relevantz.ExitManagement.Common.Enums;
using Relevantz.ExitManagement.Core.IService;

namespace Relevantz.ExitManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExitController : ControllerBase
{
    private readonly IExitService _exitService;
    private readonly IDocumentService _documentService;

    public ExitController(IExitService exitService, IDocumentService documentService)
    {
        _exitService = exitService;
        _documentService = documentService;
    }

    private int CurrentEmpId() =>
        int.Parse(User.FindFirst("empId")!.Value);

    // ── Resignation ───────────────────────────────────────────────────────

    [Authorize(Roles = "EMPLOYEE,MANAGER,HR,ADMIN")]
    [HttpPost("resign")]
    public async Task<IActionResult> SubmitResignation(
        [FromBody] SubmitResignationRequestDto request)
    {
        var id = await _exitService.SubmitResignationAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<int>.SuccessResponse(id, "Resignation submitted successfully."));
    }

    // ── Manager ───────────────────────────────────────────────────────────

    [Authorize(Roles = "MANAGER")]
    [HttpPost("manager-approval")]
    public async Task<IActionResult> ManagerApproval(
        [FromBody] ManagerApprovalRequestDto request)
    {
        await _exitService.ManagerApproveAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<string>.SuccessResponse("Processed", "Manager decision recorded."));
    }

    // ── HR ────────────────────────────────────────────────────────────────

    [Authorize(Roles = "HR")]
    [HttpPost("hr-approval")]
    public async Task<IActionResult> HrApproval([FromBody] HrApprovalRequestDto request)
    {
        await _exitService.HrApproveAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<string>.SuccessResponse("HR Approved", "HR approval recorded."));
    }

    // ── Clearance ─────────────────────────────────────────────────────────

    [Authorize(Roles = "IT,ADMIN")]
    [HttpPost("update-clearance")]
    public async Task<IActionResult> UpdateClearance(
        [FromBody] UpdateClearanceRequestDto request)
    {
        await _exitService.UpdateClearanceAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<string>.SuccessResponse("Updated", "Clearance updated."));
    }

    [Authorize(Roles = "IT,ADMIN")]
    [HttpPost("update-clearance-item")]
    public async Task<IActionResult> UpdateClearanceItem(
        [FromBody] UpdateClearanceItemDto request)
    {
        await _exitService.UpdateClearanceItemAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<string>.SuccessResponse("Updated", "Clearance item updated."));
    }

    [Authorize(Roles = "IT,ADMIN,HR")]
    [HttpGet("clearance-items/{exitRequestId}/{dept}")]
    public async Task<IActionResult> GetClearanceItems(int exitRequestId, string dept)
    {
        var items = await _exitService.GetClearanceItemsAsync(exitRequestId, dept);
        return Ok(ApiResponse<List<ClearanceItemResponseDto>>.SuccessResponse(items));
    }

    [Authorize(Roles = "IT,ADMIN,HR,MANAGER")]
    [HttpGet("declared-assets/{exitRequestId}")]
    public async Task<IActionResult> GetDeclaredAssets(int exitRequestId)
    {
        var assets = await _exitService.GetAssetsByExitIdAsync(exitRequestId);
        return Ok(ApiResponse<List<AssetDeclarationDto>>.SuccessResponse(assets));
    }

    // ── Settlement ────────────────────────────────────────────────────────

    [Authorize(Roles = "HR,ADMIN")]
    [HttpPost("approve-settlement")]
    public async Task<IActionResult> ApproveSettlement(
        [FromBody] SettlementApprovalRequestDto request)
    {
        await _exitService.ApproveSettlementAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<string>.SuccessResponse("Completed", "Exit process completed."));
    }

    // ── Status ────────────────────────────────────────────────────────────

    [Authorize(Roles = "EMPLOYEE,HR,ADMIN,MANAGER,IT")]
    [HttpGet("my-exit-status")]
    public async Task<IActionResult> GetMyExitStatus()
    {
        var result = await _exitService.GetMyExitStatusAsync(CurrentEmpId());
        return Ok(ApiResponse<ExitStatusResponseDto>.SuccessResponse(result));
    }

    // ── Analytics ─────────────────────────────────────────────────────────

    [Authorize(Roles = "ADMIN,HR")]
    [HttpGet("analytics")]
    public async Task<IActionResult> GetAnalytics()
    {
        var result = await _exitService.GetExitAnalyticsAsync();
        return Ok(result);
    }

    // ── Rehire ────────────────────────────────────────────────────────────

    [Authorize(Roles = "HR")]
    [HttpPost("rehire-eligibility")]
    public async Task<IActionResult> SetRehireEligibility(
        [FromBody] RehireEligibilityRequestDto request)
    {
        await _exitService.SetRehireEligibilityAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<string>.SuccessResponse("Updated", "Rehire eligibility updated."));
    }

    [Authorize(Roles = "HR")]
    [HttpGet("completed-exits")]
    public async Task<IActionResult> GetCompletedExits()
    {
        var result = await _exitService.GetAllCompletedExitsAsync();
        return Ok(ApiResponse<List<ExitRequestSummaryDto>>.SuccessResponse(result));
    }

    // ── Documents ─────────────────────────────────────────────────────────

    [Authorize(Roles = "HR")]
    [HttpGet("relieving-letter/{exitRequestId}")]
    public async Task<IActionResult> GetRelievingLetter(int exitRequestId)
    {
        var req = await _exitService.GetExitRequestByIdAsync(exitRequestId);
        if (req.Status != "Completed")
            return BadRequest(new { message = "Relieving letter can only be downloaded after the exit process is fully completed." });
        var pdf = await _documentService.GenerateRelievingLetterAsync(exitRequestId);
        return File(pdf, "application/pdf", "RelievingLetter.pdf");
    }

    [Authorize(Roles = "HR")]
    [HttpGet("experience-letter/{exitRequestId}")]
    public async Task<IActionResult> GetExperienceLetter(int exitRequestId)
    {
        var req = await _exitService.GetExitRequestByIdAsync(exitRequestId);
        if (req.Status != "Completed")
            return BadRequest(new { message = "Experience letter can only be downloaded after the exit process is fully completed." });
        var pdf = await _documentService.GenerateExperienceLetterAsync(exitRequestId);
        return File(pdf, "application/pdf", "ExperienceLetter.pdf");
    }

    [Authorize(Roles = "HR")]
    [HttpGet("clearance-certificate/{exitRequestId}")]
    public async Task<IActionResult> GetClearanceCertificate(int exitRequestId)
    {
        var req = await _exitService.GetExitRequestByIdAsync(exitRequestId);
        if (req.Status != "Completed")
            return BadRequest(new { message = "Clearance certificate can only be downloaded after the exit process is fully completed." });
        var pdf = await _documentService.GenerateClearanceCertificateAsync(exitRequestId);
        return File(pdf, "application/pdf", "ClearanceCertificate.pdf");
    }

    // ── Knowledge Transfer ────────────────────────────────────────────────

    [Authorize(Roles = "MANAGER")]
    [HttpPost("update-knowledge-transfer")]
    public async Task<IActionResult> UpdateKnowledgeTransfer(
        [FromBody] UpdateKtRequestDto request)
    {
        await _exitService.UpdateKnowledgeTransferAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<string>.SuccessResponse("KT Updated", "Knowledge transfer updated."));
    }

    [Authorize(Roles = "MANAGER")]
    [HttpPost("update-kt-task")]
    public async Task<IActionResult> UpdateKtTask(
        [FromBody] UpdateKtTaskStatusDto request)
    {
        await _exitService.UpdateKtTaskStatusAsync(CurrentEmpId(), request);
        return Ok(ApiResponse<string>.SuccessResponse("Updated", "KT task updated."));
    }

    [Authorize(Roles = "MANAGER,HR,ADMIN")]
    [HttpGet("kt-tasks/{exitRequestId}")]
    public async Task<IActionResult> GetKtTasks(int exitRequestId)
    {
        var tasks = await _exitService.GetKtTasksAsync(exitRequestId);
        return Ok(ApiResponse<List<KtTaskResponseDto>>.SuccessResponse(tasks));
    }

    // ── All Requests ──────────────────────────────────────────────────────

    [Authorize(Roles = "HR,ADMIN,MANAGER,IT")]
    [HttpGet("all-requests")]
    public async Task<IActionResult> GetAllRequests([FromQuery] string? status = null)
    {
        var result = await _exitService.GetAllExitRequestsAsync(status);
        return Ok(ApiResponse<List<ExitRequestSummaryDto>>.SuccessResponse(result));
    }

    [Authorize(Roles = "HR,ADMIN,MANAGER,IT")]
    [HttpGet("request/{id}")]
    public async Task<IActionResult> GetRequestById(int id)
    {
        var result = await _exitService.GetExitRequestByIdAsync(id);
        return Ok(ApiResponse<ExitRequestSummaryDto>.SuccessResponse(result));
    }

    [Authorize(Roles = "MANAGER")]
    [HttpGet("pending-for-manager")]
    public async Task<IActionResult> GetPendingForManager()
    {
        var result = await _exitService.GetPendingRequestsForManagerAsync(CurrentEmpId());
        return Ok(ApiResponse<List<ExitRequestSummaryDto>>.SuccessResponse(result));
    }

    [Authorize(Roles = "MANAGER")]
    [HttpGet("active-for-manager")]
    public async Task<IActionResult> GetActiveForManager()
    {
        var result = await _exitService.GetActiveExitsForManagerAsync(CurrentEmpId());
        return Ok(ApiResponse<List<ExitRequestSummaryDto>>.SuccessResponse(result));
    }
}