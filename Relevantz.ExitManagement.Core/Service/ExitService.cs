using Relevantz.ExitManagement.Common.DTOs;
using Relevantz.ExitManagement.Common.Entities;
using Relevantz.ExitManagement.Common.Enums;
using Relevantz.ExitManagement.Common.Exceptions;
using Relevantz.ExitManagement.Core.IService;
using Relevantz.ExitManagement.Data.IRepository;

namespace Relevantz.ExitManagement.Core.Service;

public class ExitService : IExitService
{
    private readonly IExitRequestRepository _repository;
    private const int DEFAULT_NOTICE_PERIOD = 30;
    private const int MAX_ASSETS = 20;
    private const int MAX_KT_TASKS = 20;

    private static readonly Dictionary<string, List<string>> CLEARANCE_ITEMS = new()
    {
        ["IT"] = new()
        {
            "Laptop / Desktop",
            "Access Card / ID Badge",
            "Email Account Revoked",
            "VPN Access Revoked",
            "Software Licenses Released",
            "Company Mobile Device"
        },
        ["Admin"] = new()
        {
            "Office Keys / Key Card",
            "Parking Pass",
            "Canteen Card",
            "Library Books / Materials",
            "Locker Clearance",
            "Company Credit Card"
        }
    };

    public ExitService(IExitRequestRepository repository)
        => _repository = repository;

    // ── Submit Resignation ──────────────────────────────────────────────────
    public async Task<int> SubmitResignationAsync(
        int employeeId, SubmitResignationRequestDto request)
    {
        // ── Employee guard ──
        var employee = await _repository.GetEmployeeByIdAsync(employeeId)
            ?? throw new NotFoundException("Employee not found or inactive.");

        if (!employee.IsActive)
            throw new InvalidOperationException("Inactive employees cannot submit a resignation.");

        // ── Duplicate active exit guard ──
        if (await _repository.HasActiveExitRequestAsync(employeeId))
            throw new InvalidOperationException(
                "A resignation is already in progress. Please wait for it to be resolved.");

        // ── Date validations ──
        if (request.ProposedLastWorkingDate == default)
            throw new InvalidOperationException("Proposed last working date is required.");

        if (request.ProposedLastWorkingDate.Date <= DateTime.UtcNow.Date)
            throw new InvalidOperationException(
                "Proposed last working date must be a future date.");

        var minimumLwd = DateTime.UtcNow.AddDays(DEFAULT_NOTICE_PERIOD);
        if (request.ProposedLastWorkingDate.Date < minimumLwd.Date)
            throw new InvalidOperationException(
                $"Proposed last working date must be at least {DEFAULT_NOTICE_PERIOD} days " +
                $"from today ({minimumLwd:dd MMM yyyy}).");

        if (request.ProposedLastWorkingDate.Date > DateTime.UtcNow.AddYears(1).Date)
            throw new InvalidOperationException(
                "Proposed last working date cannot be more than 1 year in the future.");

        // ── Reason validation ──
        if (!Enum.IsDefined(typeof(ResignationReason), request.ReasonType))
            throw new InvalidOperationException("Invalid resignation reason.");

        if (!string.IsNullOrWhiteSpace(request.DetailedReason) &&
            request.DetailedReason.Length > 2000)
            throw new InvalidOperationException(
                "Detailed reason must not exceed 2000 characters.");

        // ── Asset validations ──
        if (request.Assets != null)
        {
            if (request.Assets.Count > MAX_ASSETS)
                throw new InvalidOperationException(
                    $"Cannot declare more than {MAX_ASSETS} assets at once.");

            foreach (var asset in request.Assets)
            {
                if (string.IsNullOrWhiteSpace(asset.AssetName))
                    throw new InvalidOperationException("Asset name is required for all assets.");

                if (string.IsNullOrWhiteSpace(asset.AssetCode))
                    throw new InvalidOperationException(
                        $"Serial/Tag number is required for asset '{asset.AssetName}'.");
            }

            var duplicateCode = request.Assets
                .Where(a => !string.IsNullOrWhiteSpace(a.AssetCode))
                .GroupBy(a => a.AssetCode!.Trim().ToLower())
                .FirstOrDefault(g => g.Count() > 1);

            if (duplicateCode != null)
                throw new InvalidOperationException(
                    $"Duplicate Serial/Tag '{duplicateCode.Key}' found. " +
                    "Each asset must have a unique serial number.");
        }

        // ── Handover buddy must not be self ──
        if (request.HandoverBuddyId.HasValue && request.HandoverBuddyId.Value == employeeId)
            throw new InvalidOperationException(
                "You cannot assign yourself as the handover buddy.");

        if (request.HandoverBuddyId.HasValue)
        {
            var buddy = await _repository.GetEmployeeByIdAsync(request.HandoverBuddyId.Value);
            if (buddy is null || !buddy.IsActive)
                throw new NotFoundException(
                    "Handover buddy not found or is inactive.");
        }

        // ── Build exit request ──
        var exitRequest = new ExitRequest
        {
            EmployeeId = employeeId,
            ResignationDate = DateTime.UtcNow,
            SubmittedDate = DateTime.UtcNow,
            NoticePeriodDays = DEFAULT_NOTICE_PERIOD,
            CalculatedLastWorkingDate = minimumLwd,
            ProposedLastWorkingDate = request.ProposedLastWorkingDate,
            ReasonType = request.ReasonType,
            DetailedReason = request.DetailedReason?.Trim(),
            Reason = request.DetailedReason?.Trim()
                                        ?? request.ReasonType.ToString(),
            HandoverBuddyId = request.HandoverBuddyId,
            HandoverNotes = request.HandoverNotes?.Trim(),
        };

        bool isHrOrAdmin = employee.RoleId == 1 || employee.RoleId == 2;

        if (isHrOrAdmin)
        {
            exitRequest.Status = ExitStatus.PendingHrReview;
            await _repository.AddAsync(exitRequest);
            await _repository.SaveChangesAsync();
            await SaveAssetsAsync(exitRequest.Id, request.Assets);
            await CalculateRiskAsync(exitRequest);
            await _repository.SaveChangesAsync();

            var hrUser = await _repository.GetFirstHrAsync();
            if (hrUser != null && hrUser.Id != employeeId)
                await SendNotificationAsync(hrUser.Id,
                    "Admin/HR Resignation Submitted",
                    $"{employee.FirstName} {employee.LastName} has submitted a resignation.");
        }
        else
        {
            if (!employee.L1ManagerId.HasValue)
            {
                exitRequest.Status = ExitStatus.PendingHrReview;
                await _repository.AddAsync(exitRequest);
                await _repository.SaveChangesAsync();
                await SaveAssetsAsync(exitRequest.Id, request.Assets);
                await CalculateRiskAsync(exitRequest);
                await _repository.SaveChangesAsync();

                var hrUser = await _repository.GetFirstHrAsync();
                if (hrUser != null && hrUser.Id != employeeId)
                    await SendNotificationAsync(hrUser.Id,
                        "Resignation Pending HR Review",
                        $"{employee.FirstName} {employee.LastName} submitted a resignation. " +
                        "No L1 manager assigned.");
            }
            else
            {
                exitRequest.Status = ExitStatus.PendingL1Approval;
                await _repository.AddAsync(exitRequest);
                await _repository.SaveChangesAsync();
                await SaveAssetsAsync(exitRequest.Id, request.Assets);
                await CalculateRiskAsync(exitRequest);
                await _repository.SaveChangesAsync();

                await _repository.AddApprovalAsync(new ExitApproval
                {
                    ExitRequestId = exitRequest.Id,
                    ApproverId = employee.L1ManagerId.Value,
                    Status = ApprovalStatus.Pending
                });

                await SendNotificationAsync(employee.L1ManagerId.Value,
                    "New Resignation Submitted",
                    $"{employee.FirstName} {employee.LastName} has submitted a resignation.");
            }
        }

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"Employee {employeeId} submitted resignation → ExitRequest {exitRequest.Id}",
            PerformedBy = employeeId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
        return exitRequest.Id;
    }

    // ── Save Assets ─────────────────────────────────────────────────────────
    private async Task SaveAssetsAsync(int exitRequestId, List<AssetDeclarationDto>? assets)
    {
        if (assets == null || assets.Count == 0) return;
        var entities = assets.Select(a => new AssetDeclaration
        {
            ExitRequestId = exitRequestId,
            AssetName = a.AssetName.Trim(),
            AssetCode = a.AssetCode?.Trim()
        }).ToList();
        await _repository.AddAssetDeclarationsAsync(entities);
    }

    // ── Manager Approval ────────────────────────────────────────────────────
    public async Task ManagerApproveAsync(int managerId, ManagerApprovalRequestDto request)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(request.ExitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        if (exitRequest.Status != ExitStatus.PendingL1Approval &&
            exitRequest.Status != ExitStatus.PendingL2Approval)
            throw new InvalidOperationException(
                $"This request is not pending manager approval. " +
                $"Current status: {exitRequest.Status}");

        var approval = await _repository.GetPendingApprovalAsync(
            request.ExitRequestId, managerId)
            ?? throw new NotFoundException(
                "No pending approval found for this manager on this request.");

        // ── Remarks required on rejection ──
        if (!request.IsApproved && string.IsNullOrWhiteSpace(request.Remarks))
            throw new InvalidOperationException(
                "Remarks are required when rejecting a resignation.");

        if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Length > 1000)
            throw new InvalidOperationException(
                "Remarks must not exceed 1000 characters.");

        // ── KT task validations ──
        if (request.IsApproved && request.KtTasks != null)
        {
            if (request.KtTasks.Count > MAX_KT_TASKS)
                throw new InvalidOperationException(
                    $"Cannot assign more than {MAX_KT_TASKS} KT tasks at once.");

            for (int i = 0; i < request.KtTasks.Count; i++)
            {
                var task = request.KtTasks[i];

                if (string.IsNullOrWhiteSpace(task.Title))
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}]: Title is required.");

                if (task.Title.Length > 200)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}]: Title must not exceed 200 characters.");

                if (task.Deadline == default)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': Deadline is required.");

                if (task.Deadline.Date < DateTime.UtcNow.Date)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': " +
                        $"Deadline '{task.Deadline:dd MMM yyyy}' cannot be in the past.");

                if (task.Deadline.Date > DateTime.UtcNow.AddYears(2).Date)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': " +
                        "Deadline cannot be more than 2 years in the future.");

                if (!string.IsNullOrWhiteSpace(task.Description) &&
                    task.Description.Length > 2000)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': " +
                        "Description must not exceed 2000 characters.");
            }

            // Duplicate title check within this batch
            var dupTitle = request.KtTasks
                .GroupBy(t => t.Title.Trim().ToLower())
                .FirstOrDefault(g => g.Count() > 1);
            if (dupTitle != null)
                throw new InvalidOperationException(
                    $"Duplicate KT task title '{dupTitle.Key}'. " +
                    "Each task must have a unique title.");
        }

        if (!request.IsApproved)
        {
            approval.Status = ApprovalStatus.Rejected;
            approval.Remarks = request.Remarks?.Trim();
            approval.ActionDate = DateTime.UtcNow;
            exitRequest.Status = ExitStatus.Rejected;

            await SendNotificationAsync(exitRequest.EmployeeId,
                "Resignation Rejected",
                $"Your resignation was rejected by your manager. " +
                $"Remarks: {request.Remarks ?? "None"}");

            await _repository.AddAuditLogAsync(new AuditLog
            {
                Action = $"Manager {managerId} rejected ExitRequest {request.ExitRequestId}",
                PerformedBy = managerId.ToString(),
                Timestamp = DateTime.UtcNow
            });
            await _repository.SaveChangesAsync();
            return;
        }

        approval.Status = ApprovalStatus.Approved;
        approval.Remarks = request.Remarks?.Trim();
        approval.ActionDate = DateTime.UtcNow;

        if (request.KtTasks != null && request.KtTasks.Count > 0)
        {
            var tasks = request.KtTasks.Select(t => new KtTask
            {
                ExitRequestId = exitRequest.Id,
                Title = t.Title.Trim(),
                Description = t.Description?.Trim(),
                Deadline = t.Deadline,
                CreatedAt = DateTime.UtcNow
            }).ToList();
            await _repository.AddKtTasksAsync(tasks);
        }

        var employee = await _repository.GetEmployeeByIdAsync(exitRequest.EmployeeId);

        if (exitRequest.Status == ExitStatus.PendingL1Approval)
        {
            exitRequest.L1ApprovedDate = DateTime.UtcNow;

            if (employee?.L2ManagerId is not null)
            {
                exitRequest.Status = ExitStatus.PendingL2Approval;
                await _repository.AddApprovalAsync(new ExitApproval
                {
                    ExitRequestId = exitRequest.Id,
                    ApproverId = employee.L2ManagerId.Value,
                    Status = ApprovalStatus.Pending
                });
                await SendNotificationAsync(employee.L2ManagerId.Value,
                    "Resignation Pending Your Approval",
                    $"ExitRequest #{exitRequest.Id} requires your L2 approval.");
            }
            else
            {
                exitRequest.Status = ExitStatus.PendingHrReview;
                var hrUser = await _repository.GetFirstHrAsync();
                if (hrUser != null)
                    await SendNotificationAsync(hrUser.Id,
                        "Resignation Pending HR Review",
                        $"ExitRequest #{exitRequest.Id} is awaiting HR approval.");
            }
        }
        else if (exitRequest.Status == ExitStatus.PendingL2Approval)
        {
            exitRequest.L2ApprovedDate = DateTime.UtcNow;
            exitRequest.Status = ExitStatus.PendingHrReview;
            var hrUser = await _repository.GetFirstHrAsync();
            if (hrUser != null)
                await SendNotificationAsync(hrUser.Id,
                    "Resignation Pending HR Review",
                    $"ExitRequest #{exitRequest.Id} is awaiting HR approval.");
        }

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"Manager {managerId} approved ExitRequest {request.ExitRequestId}",
            PerformedBy = managerId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
    }

    // ── HR Approval ─────────────────────────────────────────────────────────
    public async Task HrApproveAsync(int hrId, HrApprovalRequestDto request)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(request.ExitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        if (exitRequest.Status != ExitStatus.PendingHrReview)
            throw new InvalidOperationException(
                $"Request is not pending HR review. Current status: {exitRequest.Status}");

        // ── Remarks required on rejection ──
        if (!request.IsApproved && string.IsNullOrWhiteSpace(request.Remarks))
            throw new InvalidOperationException(
                "Remarks are required when rejecting a resignation.");

        if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Length > 1000)
            throw new InvalidOperationException(
                "Remarks must not exceed 1000 characters.");

        if (!request.IsApproved)
        {
            exitRequest.Status = ExitStatus.Rejected;
            await SendNotificationAsync(exitRequest.EmployeeId,
                "Resignation Rejected by HR",
                $"Your resignation was rejected by HR. " +
                $"Remarks: {request.Remarks ?? "None"}");

            await _repository.AddAuditLogAsync(new AuditLog
            {
                Action = $"HR {hrId} rejected ExitRequest {request.ExitRequestId}",
                PerformedBy = hrId.ToString(),
                Timestamp = DateTime.UtcNow
            });
            await _repository.SaveChangesAsync();
            return;
        }

        exitRequest.HrApprovedDate = DateTime.UtcNow;
        exitRequest.Status = ExitStatus.ClearanceInProgress;

        foreach (var (dept, items) in CLEARANCE_ITEMS)
            foreach (var itemName in items)
                await _repository.AddClearanceItemAsync(new ClearanceItem
                {
                    ExitRequestId = exitRequest.Id,
                    DepartmentName = dept,
                    ItemName = itemName,
                    Status = ClearanceStatus.Pending
                });

        await SendNotificationAsync(exitRequest.EmployeeId,
            "HR Approved — Clearance Started",
            "Your resignation has been approved by HR. " +
            "IT and Admin clearance is now in progress.");

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"HR {hrId} approved ExitRequest {request.ExitRequestId} → ClearanceInProgress",
            PerformedBy = hrId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
    }

    // ── Clearance (bulk legacy) ─────────────────────────────────────────────
    public async Task UpdateClearanceAsync(int employeeId, UpdateClearanceRequestDto request)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(request.ExitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        if (exitRequest.Status != ExitStatus.ClearanceInProgress)
            throw new InvalidOperationException(
                $"Exit is not in clearance stage. Current status: {exitRequest.Status}");

        if (string.IsNullOrWhiteSpace(request.DepartmentName))
            throw new InvalidOperationException("Department name is required.");

        // ── Remarks required when not clearing ──
        if (!request.IsCleared && string.IsNullOrWhiteSpace(request.Remarks))
            throw new InvalidOperationException(
                "Remarks are required when marking clearance as not cleared.");

        if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Length > 1000)
            throw new InvalidOperationException(
                "Remarks must not exceed 1000 characters.");

        var deptItems = await _repository.GetClearanceItemsByDeptAsync(
            request.ExitRequestId, request.DepartmentName);

        if (!deptItems.Any())
            throw new NotFoundException(
                $"No clearance items found for department '{request.DepartmentName}'.");

        foreach (var item in deptItems)
        {
            item.Status = request.IsCleared ? ClearanceStatus.Cleared : ClearanceStatus.NotCleared;
            item.Remarks = request.Remarks?.Trim();
        }

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"{request.DepartmentName} bulk clearance for ExitRequest {request.ExitRequestId}",
            PerformedBy = employeeId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
        await CheckAllClearedAndAdvanceAsync(request.ExitRequestId, employeeId);
    }

    // ── Per-item clearance ──────────────────────────────────────────────────
    // ── Per-item clearance ──────────────────────────────────────────────────
    public async Task UpdateClearanceItemAsync(int employeeId, UpdateClearanceItemDto request)
    {
        var item = await _repository.GetClearanceItemByIdAsync(request.ItemId)
            ?? throw new NotFoundException("Clearance item not found.");

        var exitRequest = await _repository.GetExitRequestByIdAsync(item.ExitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        if (exitRequest.Status != ExitStatus.ClearanceInProgress)
            throw new InvalidOperationException(
                $"Exit is not in clearance stage. Current status: {exitRequest.Status}");

        // Validate against LWD — not today
        if (request.IsCleared && request.ReturnedDate.HasValue &&
            request.ProposedLastWorkingDate.HasValue &&
            request.ReturnedDate.Value > request.ProposedLastWorkingDate.Value)
            throw new InvalidOperationException(
                $"Assets must be returned on or before the employee's Last Working Day " +
                $"({request.ProposedLastWorkingDate.Value:dd/MM/yyyy}).");

        if (!request.IsCleared && request.ReturnedDate.HasValue)
            throw new InvalidOperationException(
                "Returned date should only be set when the item is cleared.");

        if (request.PendingDueAmount.HasValue && request.PendingDueAmount.Value < 0)
            throw new InvalidOperationException("Pending due amount cannot be negative.");

        if (request.PendingDueAmount.HasValue && request.PendingDueAmount.Value > 10_000_000)
            throw new InvalidOperationException("Pending due amount exceeds the allowed maximum.");

        if (request.IsCleared && request.PendingDueAmount.HasValue)
            throw new InvalidOperationException(
                "Pending due amount should only be set when the item is not cleared.");

        if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Length > 1000)
            throw new InvalidOperationException("Remarks must not exceed 1000 characters.");

        item.Status = request.IsCleared ? ClearanceStatus.Cleared : ClearanceStatus.NotCleared;
        item.Remarks = request.Remarks?.Trim();
        item.ReturnedDate = request.ReturnedDate.HasValue
            ? DateTime.SpecifyKind(
                request.ReturnedDate.Value.ToDateTime(TimeOnly.MinValue),
                DateTimeKind.Unspecified)   // ← key fix: no UTC conversion
            : null;

        item.PendingDueAmount = request.PendingDueAmount;

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"Clearance item '{item.ItemName}' updated for ExitRequest {item.ExitRequestId}",
            PerformedBy = employeeId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
        await CheckAllClearedAndAdvanceAsync(item.ExitRequestId, employeeId);
    }



    // ── Auto-advance when all items cleared ────────────────────────────────
    private async Task CheckAllClearedAndAdvanceAsync(int exitRequestId, int actorId)
    {
        var all = await _repository.GetClearanceItemsByExitIdAsync(exitRequestId);
        if (all.Count == 0) return;

        if (all.All(c => c.Status == ClearanceStatus.Cleared))
        {
            var er = await _repository.GetExitRequestByIdAsync(exitRequestId);
            if (er != null && er.Status == ExitStatus.ClearanceInProgress)
            {
                er.Status = ExitStatus.SettlementPending;
                er.ClearanceCompletedDate = DateTime.UtcNow;

                await SendNotificationAsync(er.EmployeeId,
                    "Clearance Complete",
                    "All IT and Admin clearance items are done. " +
                    "Your exit is now pending final settlement.");

                var hrUser = await _repository.GetFirstHrAsync();
                if (hrUser != null)
                    await SendNotificationAsync(hrUser.Id,
                        "Clearance Complete — Settlement Required",
                        $"ExitRequest #{exitRequestId} clearance is complete. " +
                        "Approve settlement.");

                await _repository.AddAuditLogAsync(new AuditLog
                {
                    Action = $"All clearance done for ExitRequest {exitRequestId} → SettlementPending",
                    PerformedBy = actorId.ToString(),
                    Timestamp = DateTime.UtcNow
                });
                await _repository.SaveChangesAsync();
            }
        }
    }

    // ── Settlement Approval ─────────────────────────────────────────────────
    public async Task ApproveSettlementAsync(int approverId, SettlementApprovalRequestDto request)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(request.ExitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        if (exitRequest.Status != ExitStatus.SettlementPending)
            throw new InvalidOperationException(
                $"Exit is not ready for settlement. Current status: {exitRequest.Status}");

        if (!exitRequest.IsKtCompleted)
            throw new InvalidOperationException(
                "Knowledge transfer must be completed before settlement can be approved.");

        var allItems = await _repository.GetClearanceItemsByExitIdAsync(request.ExitRequestId);
        if (allItems.Any(c => c.Status != ClearanceStatus.Cleared))
            throw new InvalidOperationException(
                "All clearance items must be cleared before approving settlement.");

        // ── Remarks required on rejection ──
        if (!request.IsApproved && string.IsNullOrWhiteSpace(request.Remarks))
            throw new InvalidOperationException(
                "Remarks are required when rejecting a settlement.");

        if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Length > 1000)
            throw new InvalidOperationException(
                "Remarks must not exceed 1000 characters.");

        if (!request.IsApproved)
        {
            exitRequest.Status = ExitStatus.Rejected;
            await SendNotificationAsync(exitRequest.EmployeeId,
                "Settlement Rejected",
                $"Your settlement was rejected. Remarks: {request.Remarks ?? "None"}");

            await _repository.AddAuditLogAsync(new AuditLog
            {
                Action = $"HR {approverId} rejected settlement for ExitRequest {request.ExitRequestId}",
                PerformedBy = approverId.ToString(),
                Timestamp = DateTime.UtcNow
            });
            await _repository.SaveChangesAsync();
            return;
        }

        exitRequest.Status = ExitStatus.Completed;
        exitRequest.CompletedDate = DateTime.UtcNow;
        exitRequest.SettlementCompletedDate = DateTime.UtcNow;

        var employee = await _repository.GetEmployeeByIdAsync(exitRequest.EmployeeId)
            ?? throw new NotFoundException("Employee not found.");

        employee.IsActive = false;

        await SendNotificationAsync(exitRequest.EmployeeId,
            "Exit Process Completed 🎉",
            "Your exit process has been fully completed. " +
            "Thank you for your contributions.");

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"HR {approverId} approved settlement. ExitRequest {request.ExitRequestId} → Completed",
            PerformedBy = approverId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
    }

    // ── KT Update ───────────────────────────────────────────────────────────
    public async Task UpdateKnowledgeTransferAsync(int managerId, UpdateKtRequestDto request)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(request.ExitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        if (exitRequest.Status == ExitStatus.Completed ||
            exitRequest.Status == ExitStatus.Rejected)
            throw new InvalidOperationException(
                "Cannot update KT after the exit request is closed.");

        if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Length > 2000)
            throw new InvalidOperationException(
                "KT remarks must not exceed 2000 characters.");

        // ── Successor must not be self ──
        if (request.SuccessorEmployeeId.HasValue &&
            request.SuccessorEmployeeId.Value == exitRequest.EmployeeId)
            throw new InvalidOperationException(
                "The exiting employee cannot be their own successor.");

        if (request.SuccessorEmployeeId.HasValue)
        {
            var successor = await _repository.GetEmployeeByIdAsync(
                request.SuccessorEmployeeId.Value);
            if (successor is null || !successor.IsActive)
                throw new NotFoundException(
                    "Successor employee not found or is inactive.");
        }

        // ── KT task validations ──
        if (request.Tasks != null)
        {
            if (request.Tasks.Count > MAX_KT_TASKS)
                throw new InvalidOperationException(
                    $"Cannot assign more than {MAX_KT_TASKS} KT tasks at once.");

            for (int i = 0; i < request.Tasks.Count; i++)
            {
                var task = request.Tasks[i];

                if (string.IsNullOrWhiteSpace(task.Title))
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}]: Title is required.");

                if (task.Title.Length > 200)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}]: Title must not exceed 200 characters.");

                if (task.Deadline == default)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': Deadline is required.");

                if (task.Deadline.Date < DateTime.UtcNow.Date)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': " +
                        $"Deadline '{task.Deadline:dd MMM yyyy}' cannot be in the past.");
            }

            var dupTitle = request.Tasks
                .GroupBy(t => t.Title.Trim().ToLower())
                .FirstOrDefault(g => g.Count() > 1);
            if (dupTitle != null)
                throw new InvalidOperationException(
                    $"Duplicate KT task title '{dupTitle.Key}'. " +
                    "Each task must have a unique title.");
        }

        exitRequest.IsKtCompleted = request.IsCompleted;
        exitRequest.SuccessorEmployeeId = request.SuccessorEmployeeId;
        exitRequest.KtRemarks = request.Remarks?.Trim();

        if (request.Tasks != null && request.Tasks.Count > 0)
        {
            var tasks = request.Tasks.Select(t => new KtTask
            {
                ExitRequestId = exitRequest.Id,
                Title = t.Title.Trim(),
                Description = t.Description?.Trim(),
                Deadline = t.Deadline,
                CreatedAt = DateTime.UtcNow
            }).ToList();
            await _repository.AddKtTasksAsync(tasks);
        }

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"KT updated for ExitRequest {request.ExitRequestId} — " +
                          $"Completed: {request.IsCompleted}",
            PerformedBy = managerId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
    }

    // ── KT Task Status ──────────────────────────────────────────────────────
    public async Task UpdateKtTaskStatusAsync(int managerId, UpdateKtTaskStatusDto request)
    {
        var task = await _repository.GetKtTaskByIdAsync(request.TaskId)
            ?? throw new NotFoundException("KT task not found.");

        var exitRequest = await _repository.GetExitRequestByIdAsync(task.ExitRequestId)
            ?? throw new NotFoundException("Associated exit request not found.");

        if (exitRequest.Status == ExitStatus.Completed ||
            exitRequest.Status == ExitStatus.Rejected)
            throw new InvalidOperationException(
                "Cannot update KT task after exit is closed.");

        if (!string.IsNullOrWhiteSpace(request.CompletionNotes) &&
            request.CompletionNotes.Length > 2000)
            throw new InvalidOperationException(
                "Completion notes must not exceed 2000 characters.");

        task.IsCompleted = request.IsCompleted;
        task.CompletionNotes = request.CompletionNotes?.Trim();

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"KT task '{task.Title}' marked " +
                          $"{(request.IsCompleted ? "complete" : "incomplete")}",
            PerformedBy = managerId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
    }

    // ── Rehire Eligibility ──────────────────────────────────────────────────
    public async Task SetRehireEligibilityAsync(int hrId, RehireEligibilityRequestDto request)
    {
        if (request.EmployeeId <= 0)
            throw new InvalidOperationException("Employee ID must be a positive integer.");

        if (!Enum.IsDefined(typeof(RehireDecision), request.Decision))
            throw new InvalidOperationException("Invalid rehire decision value.");

        if (request.Decision == RehireDecision.NotEligible && !request.BlockMonths.HasValue)
            throw new InvalidOperationException(
                "Block duration (months) is required when decision is Not Eligible.");

        if (request.Decision != RehireDecision.NotEligible && request.BlockMonths.HasValue)
            throw new InvalidOperationException(
                "Block duration should only be set when decision is Not Eligible.");

        if (request.BlockMonths.HasValue &&
            (request.BlockMonths.Value < 1 || request.BlockMonths.Value > 120))
            throw new InvalidOperationException(
                "Block duration must be between 1 and 120 months.");

        if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Length > 1000)
            throw new InvalidOperationException(
                "Remarks must not exceed 1000 characters.");

        var er = await _repository.GetLatestCompletedExitByEmployeeAsync(request.EmployeeId)
            ?? throw new NotFoundException(
                "No completed exit found for this employee.");

        er.RehireDecision = (RehireDecision?)request.Decision;
        er.RehireRemarks = request.Remarks?.Trim();
        er.RehireDecisionDate = DateTime.UtcNow;
        er.RehireBlockedUntil = request.Decision == RehireDecision.NotEligible
                                && request.BlockMonths.HasValue
            ? DateTime.UtcNow.AddMonths(request.BlockMonths.Value)
            : null;

        await _repository.AddAuditLogAsync(new AuditLog
        {
            Action = $"HR {hrId} set rehire → {request.Decision} for employee {request.EmployeeId}",
            PerformedBy = hrId.ToString(),
            Timestamp = DateTime.UtcNow
        });
        await _repository.SaveChangesAsync();
    }

    // ── Notification — ownership guard on mark-read ─────────────────────────
    public async Task MarkNotificationAsReadAsync(int notificationId)
    {
        var n = await _repository.GetNotificationByIdAsync(notificationId)
            ?? throw new NotFoundException("Notification not found.");
        n.IsRead = true;
        await _repository.SaveChangesAsync();
    }

    // ── All remaining methods — unchanged ───────────────────────────────────
    public async Task<List<ExitRequestSummaryDto>> GetActiveExitsForManagerAsync(int managerId)
    {
        var exits = await _repository.GetActiveExitsByManagerIdAsync(managerId);
        return exits.Select(ToSummary).ToList();
    }

    public async Task<List<KtTaskResponseDto>> GetKtTasksAsync(int exitRequestId)
    {
        var tasks = await _repository.GetKtTasksByExitIdAsync(exitRequestId);
        return tasks.Select(k => new KtTaskResponseDto
        {
            Id = k.Id,
            Title = k.Title,
            Description = k.Description,
            Deadline = k.Deadline,
            IsCompleted = k.IsCompleted,
            CompletionNotes = k.CompletionNotes,
            CreatedAt = k.CreatedAt
        }).ToList();
    }

    public async Task<List<ClearanceItemResponseDto>> GetClearanceItemsAsync(
    int exitRequestId, string dept)
{
    var items = await _repository.GetClearanceItemsByDeptAsync(exitRequestId, dept);
    return items.Select(i => new ClearanceItemResponseDto
    {
        Id               = i.Id,
        ItemName         = i.ItemName,
        DepartmentName   = i.DepartmentName,
        Status           = i.Status.ToString(),
        Remarks          = i.Remarks,
        // Convert DateTime? → DateOnly? safely
        ReturnedDate     = i.ReturnedDate.HasValue
                           ? DateOnly.FromDateTime(i.ReturnedDate.Value)
                           : null,
        PendingDueAmount = i.PendingDueAmount
    }).ToList();
}


    public async Task<List<AssetDeclarationDto>> GetAssetsByExitIdAsync(int exitRequestId)
    {
        var assets = await _repository.GetAssetsByExitIdAsync(exitRequestId);
        return assets.Select(a => new AssetDeclarationDto
        {
            AssetName = a.AssetName,
            AssetCode = a.AssetCode
        }).ToList();
    }

    public async Task<ExitStatusResponseDto> GetMyExitStatusAsync(int employeeId)
    {
        var exitRequest = await _repository.GetLatestExitRequestByEmployeeAsync(employeeId)
            ?? throw new NotFoundException("No exit request found.");

        var ktTasks = await _repository.GetKtTasksByExitIdAsync(exitRequest.Id);

        return new ExitStatusResponseDto
        {
            ExitRequestId = exitRequest.Id,
            Status = exitRequest.Status.ToString(),
            ProposedLastWorkingDate = exitRequest.ProposedLastWorkingDate,
            CompletedDate = exitRequest.CompletedDate,
            KtTasks = ktTasks.Select(k => new KtTaskResponseDto
            {
                Id = k.Id,
                Title = k.Title,
                Description = k.Description,
                Deadline = k.Deadline,
                IsCompleted = k.IsCompleted,
                CompletionNotes = k.CompletionNotes,
                CreatedAt = k.CreatedAt
            }).ToList()
        };
    }

    public async Task<bool> IsEmployeeRehireAllowedAsync(int employeeId)
    {
        var last = await _repository.GetLatestExitRequestByEmployeeAsync(employeeId);
        if (last is null) return true;
        if (last.RehireDecision == RehireDecision.NotEligible
            && last.RehireBlockedUntil.HasValue
            && last.RehireBlockedUntil > DateTime.UtcNow)
            return false;
        return last.RehireDecision != RehireDecision.NotEligible;
    }

    public async Task<List<ExitRequestSummaryDto>> GetAllCompletedExitsAsync()
    {
        var exits = await _repository.GetAllCompletedExitsAsync();
        return exits.Select(ToSummary).ToList();
    }

    public async Task<ExitAnalyticsResponseDto> GetExitAnalyticsAsync()
    {
        var exits = await _repository.GetAllExitRequestsAsync();
        if (!exits.Any()) return new ExitAnalyticsResponseDto();

        var completed = exits.Where(e => e.Status == ExitStatus.Completed).ToList();
        var pending = exits.Where(e =>
            e.Status != ExitStatus.Completed &&
            e.Status != ExitStatus.Rejected).ToList();

        double avgDays = completed.Any()
            ? completed
                .Where(e => e.SubmittedDate != default && e.SettlementCompletedDate.HasValue)
                .DefaultIfEmpty()
                .Average(e => e == null ? 0 :
                    (e.SettlementCompletedDate!.Value - e.SubmittedDate).TotalDays)
            : 0;

        var riskGroups = exits
            .GroupBy(e => e.RiskLevel)
            .ToDictionary(g => g.Key, g => g.Count());

        var topReason = exits
            .GroupBy(e => e.ReasonType.ToString())
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key;

        return new ExitAnalyticsResponseDto
        {
            TotalExits = exits.Count,
            CompletedExits = completed.Count,
            PendingExits = pending.Count,
            AverageProcessingDays = Math.Round(avgDays, 2),
            LowRiskCount = riskGroups.GetValueOrDefault(ExitRiskLevel.Low),
            MediumRiskCount = riskGroups.GetValueOrDefault(ExitRiskLevel.Medium),
            HighRiskCount = riskGroups.GetValueOrDefault(ExitRiskLevel.High),
            CriticalRiskCount = riskGroups.GetValueOrDefault(ExitRiskLevel.Critical),
            TopResignationReason = topReason
        };
    }

    public async Task<List<NotificationResponseDto>> GetMyNotificationsAsync(int employeeId)
    {
        var ns = await _repository.GetNotificationsByEmployeeAsync(employeeId);
        return ns.Select(n => new NotificationResponseDto
        {
            Id = n.Id,
            Title = n.Title,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        }).ToList();
    }

    public async Task<Employee?> GetEmployeeByIdForDocumentAsync(int employeeId)
        => await _repository.GetEmployeeByIdAsync(employeeId);

    public async Task<List<ExitRequestSummaryDto>> GetAllExitRequestsAsync(string? status)
    {
        var exits = await _repository.GetAllAsync(status);
        return exits.Select(ToSummary).ToList();
    }

    public async Task<ExitRequestSummaryDto> GetExitRequestByIdAsync(int id)
    {
        var e = await _repository.GetByIdWithEmployeeAsync(id)
            ?? throw new NotFoundException("Exit request not found.");
        return ToSummary(e);
    }

    public async Task<List<ExitRequestSummaryDto>> GetPendingRequestsForManagerAsync(
        int managerId)
    {
        var exits = await _repository.GetPendingByManagerIdAsync(managerId);
        return exits.Select(ToSummary).ToList();
    }

    // ── Risk Calculation ────────────────────────────────────────────────────
    private async Task CalculateRiskAsync(ExitRequest exitRequest)
    {
        var employee = await _repository.GetEmployeeByIdAsync(exitRequest.EmployeeId);
        if (employee is null) return;

        int score = 0;
        var reasons = new List<string>();

        if (employee.RoleId == 3) { score += 30; reasons.Add("Manager role"); }
        if (employee.RoleId == 1) { score += 40; reasons.Add("Admin role"); }
        if (employee.L2ManagerId != null) { score += 15; reasons.Add("Multi-level hierarchy"); }
        if (!exitRequest.SuccessorEmployeeId.HasValue) { score += 20; reasons.Add("No successor"); }

        var noticeDays = (exitRequest.ProposedLastWorkingDate - DateTime.UtcNow).TotalDays;
        if (noticeDays < 30) { score += 20; reasons.Add("Short notice"); }

        exitRequest.RiskScore = score;
        exitRequest.RiskLevel = score <= 25 ? ExitRiskLevel.Low
                                : score <= 50 ? ExitRiskLevel.Medium
                                : score <= 75 ? ExitRiskLevel.High
                                              : ExitRiskLevel.Critical;
        exitRequest.RiskSummary = string.Join(", ", reasons);
    }

    // ── Notification Helper ─────────────────────────────────────────────────
    private async Task SendNotificationAsync(int recipientId, string title, string message)
        => await _repository.AddNotificationAsync(new Notification
        {
            RecipientEmployeeId = recipientId,
            Title = title,
            Message = message,
            CreatedAt = DateTime.UtcNow
        });

    // ── ToSummary ───────────────────────────────────────────────────────────
    private static ExitRequestSummaryDto ToSummary(ExitRequest e) => new()
    {
        Id = e.Id,
        EmployeeId = e.Employee.Id,
        EmployeeName = e.Employee.FirstName + " " + e.Employee.LastName,
        EmployeeCode = e.Employee.EmployeeCode,
        Department = e.Employee.Department ?? string.Empty,
        Status = e.Status.ToString(),
        RiskLevel = e.RiskLevel.ToString(),
        RiskScore = e.RiskScore,
        ProposedLastWorkingDate = e.ProposedLastWorkingDate,
        SubmittedAt = e.SubmittedDate,
        CompletedDate = e.CompletedDate,
        ResignationReason = e.Reason ?? string.Empty,
        IsKtCompleted = e.IsKtCompleted,
        RehireDecision = e.RehireDecision.HasValue
                                    ? e.RehireDecision.Value.ToString()
                                    : null,
        RehireRemarks = e.RehireRemarks,
        RehireDecisionDate = e.RehireDecisionDate,
        RehireBlockedUntil = e.RehireBlockedUntil,
    };
}
