using Relevantz.ExitManagement.Common.Entities;

namespace Relevantz.ExitManagement.Data.IRepository;

public interface IExitRequestRepository
{
    Task AddAsync(ExitRequest exitRequest);
    Task AddApprovalAsync(ExitApproval approval);
    Task AddClearanceItemAsync(ClearanceItem item);
    Task AddAuditLogAsync(AuditLog log);
    Task AddNotificationAsync(Notification notification);
    Task AddAssetDeclarationsAsync(List<AssetDeclaration> assets);
    Task AddKtTasksAsync(List<KtTask> tasks);
    Task SaveChangesAsync();

    Task<bool>           HasActiveExitRequestAsync(int employeeId);
    Task<Employee?>      GetEmployeeByIdAsync(int employeeId);
    Task<Employee?>      GetEmployeeByIdForDocumentAsync(int employeeId);  // ← was missing from interface
    Task<Employee?>      GetFirstHrAsync();

    Task<ExitRequest?>   GetExitRequestByIdAsync(int exitRequestId);
    Task<ExitRequest?>   GetLatestExitRequestByEmployeeAsync(int employeeId);
    Task<ExitRequest?>   GetLatestCompletedExitByEmployeeAsync(int employeeId);
    Task<ExitRequest?>   GetByIdWithEmployeeAsync(int id);
    Task<List<ExitRequest>> GetAllAsync(string? status);
    Task<List<ExitRequest>> GetAllExitRequestsAsync();
    Task<List<ExitRequest>> GetAllCompletedExitsAsync();
    Task<List<ExitRequest>> GetPendingByManagerIdAsync(int managerId);
    Task<List<ExitRequest>> GetActiveExitsByManagerIdAsync(int managerId);

    Task<ExitApproval?>  GetPendingApprovalAsync(int exitRequestId, int approverId);

    Task<ClearanceItem?> GetClearanceItemAsync(int exitRequestId, string departmentName);
    Task<ClearanceItem?> GetClearanceItemByIdAsync(int itemId);
    Task<List<ClearanceItem>> GetClearanceItemsByExitIdAsync(int exitRequestId);
    Task<List<ClearanceItem>> GetClearanceItemsByDeptAsync(int exitRequestId, string dept);

    Task<KtTask?>        GetKtTaskByIdAsync(int taskId);
    Task<List<KtTask>>   GetKtTasksByExitIdAsync(int exitRequestId);

    Task<List<AssetDeclaration>> GetAssetsByExitIdAsync(int exitRequestId);

    Task<Notification?>  GetNotificationByIdAsync(int id);
    Task<List<Notification>> GetNotificationsByEmployeeAsync(int employeeId);
}
