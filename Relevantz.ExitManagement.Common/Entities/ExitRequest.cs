using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Relevantz.ExitManagement.Common.Enums;

namespace Relevantz.ExitManagement.Common.Entities;

public class ExitRequest
{
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }

    [Required]
    public DateTime ResignationDate { get; set; }

    [Required]
    public DateTime ProposedLastWorkingDate { get; set; }

    [Required]
    [StringLength(2000)]
    public string Reason { get; set; } = default!;

    public ExitStatus Status { get; set; } = ExitStatus.PendingL1Approval;

    public DateTime? CompletedDate { get; set; }

    public ResignationReason ReasonType { get; set; }

    [StringLength(2000)]
    public string? DetailedReason { get; set; }

    public bool IsKtCompleted { get; set; } = false;

    public int? SuccessorEmployeeId { get; set; }

    [StringLength(2000)]
    public string? KtRemarks { get; set; }

    [Range(0, 365)]
    public int NoticePeriodDays { get; set; }

    [Required]
    public DateTime CalculatedLastWorkingDate { get; set; }

    [Required]
    public DateTime SubmittedDate { get; set; }

    public DateTime? L1ApprovedDate { get; set; }
    public DateTime? L2ApprovedDate { get; set; }
    public DateTime? HrApprovedDate { get; set; }
    public DateTime? ClearanceCompletedDate { get; set; }
    public DateTime? SettlementCompletedDate { get; set; }

    [Range(0, 100)]
    public int RiskScore { get; set; }

    public ExitRiskLevel RiskLevel { get; set; }

    [StringLength(1000)]
    public string? RiskSummary { get; set; }

    // ── Handover buddy ──
    public int? HandoverBuddyId { get; set; }

    [StringLength(2000)]
    public string? HandoverNotes { get; set; }

    public Employee Employee { get; set; } = default!;

    public RehireDecision? RehireDecision { get; set; }

    [StringLength(1000)]
    public string? RehireRemarks { get; set; }

    public DateTime? RehireDecisionDate { get; set; }
    public DateTime? RehireBlockedUntil { get; set; }

    public ICollection<ExitApproval>?    Approvals        { get; set; }
    public ICollection<AssetDeclaration>? AssetDeclarations { get; set; }
    public ICollection<KtTask>?          KtTasks          { get; set; }
}
