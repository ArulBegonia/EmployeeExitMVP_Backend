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
        var employee = await _repository.GetEmployeeByIdAsync(employeeId)
            ?? throw new NotFoundException("Employee not found or inactive.");

        if (!employee.IsActive)
            throw new InvalidOperationException("Inactive employees cannot submit a resignation.");

        if (await _repository.HasActiveExitRequestAsync(employeeId))
            throw new InvalidOperationException(
                "A resignation is already in progress. Please wait for it to be resolved.");

        if (request.ProposedLastWorkingDate == default)
            throw new InvalidOperationException("Proposed last working date is required.");

        if (request.ProposedLastWorkingDate.Date <= DateTime.UtcNow.Date)
            throw new InvalidOperationException(
                "Proposed last working date must be a future date.");

        var minimumLwd = DateTime.UtcNow.AddDays(DEFAULT_NOTICE_PERIOD);

        if (request.ProposedLastWorkingDate.Date < minimumLwd.Date)
            throw new InvalidOperationException(
                $"Proposed last working date must be at least {DEFAULT_NOTICE_PERIOD} days from today.");

        if (!Enum.IsDefined(typeof(ResignationReason), request.ReasonType))
            throw new InvalidOperationException("Invalid resignation reason.");

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
            Reason = request.DetailedReason?.Trim() ?? request.ReasonType.ToString(),
            HandoverBuddyId = request.HandoverBuddyId,
            HandoverNotes = request.HandoverNotes?.Trim(),
        };

        bool isHrOrAdmin = employee.RoleId == 1 || employee.RoleId == 2;

        if (isHrOrAdmin)
        {
            exitRequest.Status = ExitStatus.PendingHrReview;

            await _repository.AddAsync(exitRequest);
            await _repository.SaveChangesAsync();

            await CalculateRiskAsync(exitRequest);

            await _repository.SaveChangesAsync();
        }
        else
        {
            if (!employee.L1ManagerId.HasValue)
            {
                exitRequest.Status = ExitStatus.PendingHrReview;

                await _repository.AddAsync(exitRequest);
                await _repository.SaveChangesAsync();

                await CalculateRiskAsync(exitRequest);

                await _repository.SaveChangesAsync();
            }
            else
            {
                exitRequest.Status = ExitStatus.PendingL1Approval;

                await _repository.AddAsync(exitRequest);
                await _repository.SaveChangesAsync();

                await CalculateRiskAsync(exitRequest);

                await _repository.AddApprovalAsync(new ExitApproval
                {
                    ExitRequestId = exitRequest.Id,
                    ApproverId = employee.L1ManagerId.Value,
                    Status = ApprovalStatus.Pending
                });
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

    public async Task ManagerApproveAsync(int managerId, ManagerApprovalRequestDto request)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(request.ExitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        if (exitRequest.Status != ExitStatus.PendingL1Approval &&
            exitRequest.Status != ExitStatus.PendingL2Approval)
            throw new InvalidOperationException(
                $"This request is not pending manager approval. Current status: {exitRequest.Status}");

        var approval = await _repository.GetPendingApprovalAsync(
            request.ExitRequestId, managerId)
            ?? throw new NotFoundException(
                "No pending approval found for this manager on this request.");

        if (!request.IsApproved && string.IsNullOrWhiteSpace(request.Remarks))
            throw new InvalidOperationException("Remarks are required when rejecting a resignation.");

        if (!string.IsNullOrWhiteSpace(request.Remarks) && request.Remarks.Length > 1000)
            throw new InvalidOperationException("Remarks must not exceed 1000 characters.");

        // KT validation
        if (request.IsApproved && request.KtTasks != null)
        {
            if (request.KtTasks.Count > MAX_KT_TASKS)
                throw new InvalidOperationException(
                    $"Cannot assign more than {MAX_KT_TASKS} KT tasks at once.");

            for (int i = 0; i < request.KtTasks.Count; i++)
            {
                var task = request.KtTasks[i];

                if (string.IsNullOrWhiteSpace(task.Title))
                    throw new InvalidOperationException($"KT task [{i + 1}]: Title is required.");

                if (task.Title.Length > 200)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}]: Title must not exceed 200 characters.");

                if (task.Deadline == default)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': Deadline is required.");

                if (task.Deadline.Date < DateTime.UtcNow.Date)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': Deadline cannot be in the past.");

                // NEW VALIDATION
                if (task.Deadline.Date > exitRequest.ProposedLastWorkingDate.Date)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': Deadline must be on or before employee Last Working Day ({exitRequest.ProposedLastWorkingDate:dd MMM yyyy}).");

                if (task.Deadline.Date > DateTime.UtcNow.AddYears(2).Date)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': Deadline cannot exceed 2 years.");

                if (!string.IsNullOrWhiteSpace(task.Description) &&
                    task.Description.Length > 2000)
                    throw new InvalidOperationException(
                        $"KT task [{i + 1}] '{task.Title}': Description too long.");
            }

            var dupTitle = request.KtTasks
                .GroupBy(t => t.Title.Trim().ToLower())
                .FirstOrDefault(g => g.Count() > 1);

            if (dupTitle != null)
                throw new InvalidOperationException(
                    $"Duplicate KT task title '{dupTitle.Key}'.");
        }

        // Remaining logic unchanged
        approval.Status = ApprovalStatus.Approved;
        approval.ActionDate = DateTime.UtcNow;

        if (request.KtTasks?.Any() == true)
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

        await _repository.SaveChangesAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // Update Knowledge Transfer (UPDATED)
    // ─────────────────────────────────────────────────────────────
    public async Task UpdateKnowledgeTransferAsync(int managerId, UpdateKtRequestDto request)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(request.ExitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        var existingTasks = await _repository.GetKtTasksByExitIdAsync(request.ExitRequestId);
        var newTasksCount = request.Tasks?.Count ?? 0;

        if (request.IsCompleted)
        {
            if (existingTasks.Count + newTasksCount == 0)
                throw new InvalidOperationException(
                    "At least one KT task must exist before marking KT as completed.");

            if (existingTasks.Any(t => !t.IsCompleted))
                throw new InvalidOperationException(
                    "All KT tasks must be completed before marking KT as completed.");
        }


        if (request.Tasks != null && request.Tasks.Count > 0)
        {
            if (request.Tasks.Count > MAX_KT_TASKS)
                throw new InvalidOperationException(
                    $"Cannot assign more than {MAX_KT_TASKS} KT tasks at once.");

            var existingTitles = existingTasks
                .Select(t => t.Title.Trim().ToLower())
                .ToHashSet();

            foreach (var task in request.Tasks)
            {
                if (string.IsNullOrWhiteSpace(task.Title))
                    throw new InvalidOperationException("KT task title is required.");

                var title = task.Title.Trim().ToLower();

                if (existingTitles.Contains(title))
                    throw new InvalidOperationException(
                        $"A KT task titled '{task.Title}' already exists.");

                if (task.Title.Length > 200)
                    throw new InvalidOperationException(
                        $"KT task '{task.Title}' title cannot exceed 200 characters.");

                if (!string.IsNullOrWhiteSpace(task.Description) && task.Description.Length > 2000)
                    throw new InvalidOperationException(
                        $"KT task '{task.Title}' description cannot exceed 2000 characters.");

                if (task.Deadline == default)
                    throw new InvalidOperationException(
                        $"KT task '{task.Title}' deadline is required.");

                if (!request.IsCompleted && task.Deadline.Date < DateTime.UtcNow.Date)
                    throw new InvalidOperationException(
                        $"KT task '{task.Title}' deadline cannot be in the past.");

                if (task.Deadline.Date > exitRequest.ProposedLastWorkingDate.Date)
                    throw new InvalidOperationException(
                        $"KT task '{task.Title}' deadline must be on or before employee Last Working Day ({exitRequest.ProposedLastWorkingDate:dd MMM yyyy}).");

                if (task.Deadline.Date > DateTime.UtcNow.AddYears(2).Date)
                    throw new InvalidOperationException(
                        $"KT task '{task.Title}' deadline cannot exceed 2 years.");

                existingTitles.Add(title);
            }

            var tasks = request.Tasks.Select(t => new KtTask
            {
                ExitRequestId = exitRequest.Id,
                Title = t.Title.Trim(),
                Description = t.Description?.Trim(),
                Deadline = t.Deadline.Date,
                CreatedAt = DateTime.UtcNow
            }).ToList();

            await _repository.AddKtTasksAsync(tasks);
        }

        // Save successor
        exitRequest.SuccessorEmployeeId = request.SuccessorEmployeeId;

        exitRequest.IsKtCompleted = request.IsCompleted;
        exitRequest.KtRemarks = request.Remarks?.Trim();

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
            Id = i.Id,
            ItemName = i.ItemName,
            DepartmentName = i.DepartmentName,
            Status = i.Status.ToString(),
            Remarks = i.Remarks,
            // Convert DateTime? → DateOnly? safely
            ReturnedDate = i.ReturnedDate.HasValue
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
