using Relevantz.ExitManagement.Common.DTOs;
using Relevantz.ExitManagement.Common.Entities;

namespace Relevantz.ExitManagement.Core.IService;

public interface IExitService
{
    Task<int>  SubmitResignationAsync(int employeeId, SubmitResignationRequestDto request);
    Task       ManagerApproveAsync(int managerId, ManagerApprovalRequestDto request);
    Task       HrApproveAsync(int hrId, HrApprovalRequestDto request);
    Task       UpdateClearanceAsync(int employeeId, UpdateClearanceRequestDto request);
    Task       ApproveSettlementAsync(int approverId, SettlementApprovalRequestDto request);
    Task<ExitAnalyticsResponseDto>   GetExitAnalyticsAsync();
    Task<ExitStatusResponseDto>      GetMyExitStatusAsync(int employeeId);
    Task       UpdateKnowledgeTransferAsync(int managerId, UpdateKtRequestDto request);
    Task<List<NotificationResponseDto>> GetMyNotificationsAsync(int employeeId);
    Task       MarkNotificationAsReadAsync(int notificationId);
    Task       SetRehireEligibilityAsync(int hrId, RehireEligibilityRequestDto request);
    Task<bool> IsEmployeeRehireAllowedAsync(int employeeId);
    Task<Employee?> GetEmployeeByIdForDocumentAsync(int employeeId);
    Task<List<ExitRequestSummaryDto>> GetAllExitRequestsAsync(string? status);
    Task<ExitRequestSummaryDto>       GetExitRequestByIdAsync(int id);
    Task<List<ExitRequestSummaryDto>> GetPendingRequestsForManagerAsync(int managerId);

    // Phase 1
    Task       UpdateKtTaskStatusAsync(int managerId, UpdateKtTaskStatusDto request);
    Task<List<KtTaskResponseDto>>        GetKtTasksAsync(int exitRequestId);
    Task       UpdateClearanceItemAsync(int employeeId, UpdateClearanceItemDto request);
    Task<List<ClearanceItemResponseDto>> GetClearanceItemsAsync(int exitRequestId, string dept);

    // Flaw fixes
    Task<List<AssetDeclarationDto>>      GetAssetsByExitIdAsync(int exitRequestId);
    Task<List<ExitRequestSummaryDto>>    GetAllCompletedExitsAsync();

    // Flaw 3 & 9: KT dashboard — all active exits for manager's team
    Task<List<ExitRequestSummaryDto>>    GetActiveExitsForManagerAsync(int managerId);
}
