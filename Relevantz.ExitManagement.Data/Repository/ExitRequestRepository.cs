using Microsoft.EntityFrameworkCore;
using Relevantz.ExitManagement.Common.Entities;
using Relevantz.ExitManagement.Common.Enums;
using Relevantz.ExitManagement.Data.DBContexts;
using Relevantz.ExitManagement.Data.IRepository;

namespace Relevantz.ExitManagement.Data.Repository;

public class ExitRequestRepository : IExitRequestRepository
{
    private readonly ExitManagementDbContext _context;

    public ExitRequestRepository(ExitManagementDbContext context)
        => _context = context;

    public async Task AddAsync(ExitRequest exitRequest)
        => await _context.ExitRequests.AddAsync(exitRequest);

    public async Task AddApprovalAsync(ExitApproval approval)
        => await _context.ExitApprovals.AddAsync(approval);

    public async Task<Employee?> GetEmployeeByIdAsync(int employeeId)
        => await _context.Employees
            .FirstOrDefaultAsync(e => e.Id == employeeId && e.IsActive);

    public async Task<ExitApproval?> GetPendingApprovalAsync(
        int exitRequestId, int approverId)
        => await _context.ExitApprovals
            .FirstOrDefaultAsync(a =>
                a.ExitRequestId == exitRequestId &&
                a.ApproverId == approverId &&
                a.Status == ApprovalStatus.Pending);

    public async Task<ExitRequest?> GetExitRequestByIdAsync(int exitRequestId)
        => await _context.ExitRequests
            .FirstOrDefaultAsync(er => er.Id == exitRequestId);

    public async Task SaveChangesAsync()
        => await _context.SaveChangesAsync();

    public async Task AddClearanceItemAsync(ClearanceItem item)
        => await _context.ClearanceItems.AddAsync(item);

    public async Task<ClearanceItem?> GetClearanceItemAsync(
        int exitRequestId, string departmentName)
        => await _context.ClearanceItems
            .FirstOrDefaultAsync(c =>
                c.ExitRequestId == exitRequestId &&
                c.DepartmentName == departmentName);

    public async Task<List<ClearanceItem>> GetClearanceItemsByExitIdAsync(int exitRequestId)
        => await _context.ClearanceItems
            .Where(c => c.ExitRequestId == exitRequestId)
            .ToListAsync();

    public async Task<bool> HasActiveExitRequestAsync(int employeeId)
        => await _context.ExitRequests
            .AnyAsync(er =>
                er.EmployeeId == employeeId &&
                er.Status != ExitStatus.Completed &&
                er.Status != ExitStatus.Rejected);

    public async Task<ExitRequest?> GetLatestExitRequestByEmployeeAsync(int employeeId)
        => await _context.ExitRequests
            .Where(er => er.EmployeeId == employeeId)
            .OrderByDescending(er => er.Id)
            .FirstOrDefaultAsync();

    public async Task AddAuditLogAsync(AuditLog log)
        => await _context.AuditLogs.AddAsync(log);

    public async Task<List<ExitRequest>> GetAllExitRequestsAsync()
        => await _context.ExitRequests.ToListAsync();

    public async Task AddNotificationAsync(Notification notification)
        => await _context.Notifications.AddAsync(notification);

    public async Task<List<Notification>> GetNotificationsByEmployeeAsync(int employeeId)
        => await _context.Notifications
            .Where(n => n.RecipientEmployeeId == employeeId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

    public async Task<Notification?> GetNotificationByIdAsync(int id)
        => await _context.Notifications.FindAsync(id);

    public async Task<Employee?> GetFirstHrAsync()
    {
        var hrRoleId = await _context.Roles
            .Where(r => r.Name == "HR")
            .Select(r => r.Id)
            .FirstOrDefaultAsync();

        return await _context.Employees
            .FirstOrDefaultAsync(e => e.RoleId == hrRoleId && e.IsActive);
    }

    public async Task<Employee?> GetEmployeeByIdForDocumentAsync(int employeeId)
    => await _context.Employees
        .FirstOrDefaultAsync(e => e.Id == employeeId);

    public async Task<List<ExitRequest>> GetAllAsync(string? status)
    {
        var query = _context.ExitRequests
            .Include(e => e.Employee)
            .AsQueryable();
        if (!string.IsNullOrEmpty(status))
            if (Enum.TryParse<ExitStatus>(status, true, out var parsed))
                query = query.Where(e => e.Status == parsed);
        return await query.OrderByDescending(e => e.SubmittedDate).ToListAsync();
    }

    public async Task<ExitRequest?> GetByIdWithEmployeeAsync(int id)
        => await _context.ExitRequests
            .Include(e => e.Employee)
            .FirstOrDefaultAsync(e => e.Id == id);

    // Approvals only (pending L1/L2)
    public async Task<List<ExitRequest>> GetPendingByManagerIdAsync(int managerId)
        => await _context.ExitRequests
            .Include(e => e.Employee)
            .Where(e =>
                (e.Employee.L1ManagerId == managerId &&
                 e.Status == ExitStatus.PendingL1Approval) ||
                (e.Employee.L2ManagerId == managerId &&
                 e.Status == ExitStatus.PendingL2Approval))
            .OrderByDescending(e => e.SubmittedDate)
            .ToListAsync();

    // Flaw 3 & 9: ALL active exits for KT dashboard — any status except Completed/Rejected
    public async Task<List<ExitRequest>> GetActiveExitsByManagerIdAsync(int managerId)
        => await _context.ExitRequests
            .Include(e => e.Employee)
            .Where(e =>
                (e.Employee.L1ManagerId == managerId ||
                 e.Employee.L2ManagerId == managerId) &&
                e.Status != ExitStatus.Completed &&
                e.Status != ExitStatus.Rejected)
            .OrderByDescending(e => e.SubmittedDate)
            .ToListAsync();

    // Phase 1
    public async Task AddAssetDeclarationsAsync(List<AssetDeclaration> assets)
        => await _context.AssetDeclarations.AddRangeAsync(assets);

    public async Task AddKtTasksAsync(List<KtTask> tasks)
        => await _context.KtTasks.AddRangeAsync(tasks);

    public async Task<List<KtTask>> GetKtTasksByExitIdAsync(int exitRequestId)
        => await _context.KtTasks
            .Where(k => k.ExitRequestId == exitRequestId)
            .OrderBy(k => k.Deadline)
            .ToListAsync();

    public async Task<KtTask?> GetKtTaskByIdAsync(int taskId)
        => await _context.KtTasks.FindAsync(taskId);

    public async Task<ClearanceItem?> GetClearanceItemByIdAsync(int itemId)
        => await _context.ClearanceItems.FindAsync(itemId);

    public async Task<List<ClearanceItem>> GetClearanceItemsByDeptAsync(
        int exitRequestId, string dept)
        => await _context.ClearanceItems
            .Where(c => c.ExitRequestId == exitRequestId && c.DepartmentName == dept)
            .ToListAsync();

    // Flaw fixes
    public async Task<List<AssetDeclaration>> GetAssetsByExitIdAsync(int exitRequestId)
        => await _context.AssetDeclarations
            .Where(a => a.ExitRequestId == exitRequestId)
            .ToListAsync();

    public async Task<ExitRequest?> GetLatestCompletedExitByEmployeeAsync(int employeeId)
        => await _context.ExitRequests
            .Where(e => e.EmployeeId == employeeId && e.Status == ExitStatus.Completed)
            .OrderByDescending(e => e.Id)
            .FirstOrDefaultAsync();

    public async Task<List<ExitRequest>> GetAllCompletedExitsAsync()
        => await _context.ExitRequests
            .Include(e => e.Employee)
            .Where(e => e.Status == ExitStatus.Completed)
            .OrderByDescending(e => e.CompletedDate)
            .ToListAsync();
}
