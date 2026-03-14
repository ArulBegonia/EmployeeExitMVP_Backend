using System.ComponentModel.DataAnnotations;
using Relevantz.ExitManagement.Common.Enums;

namespace Relevantz.ExitManagement.Common.DTOs;

public class SubmitResignationRequestDto
{
    // ── Existing fields (unchanged) ───────────────────────────────────────

    [Required]
    public DateTime ProposedLastWorkingDate { get; set; }

    [Required]
    public ResignationReason ReasonType { get; set; }

    [MaxLength(2000)]
    public string? DetailedReason { get; set; }

    public int? HandoverBuddyId { get; set; }

    [MaxLength(2000)]
    public string? HandoverNotes { get; set; }

    // ── New: Resignation type ─────────────────────────────────────────────

    /// <summary>
    /// Voluntary | Retirement | ContractEnd | Mutual
    /// </summary>
    [MaxLength(50)]
    public string? ResignationType { get; set; } = "Voluntary";

    // ── New: Notice period waiver ─────────────────────────────────────────

    /// <summary>
    /// Employee is requesting an early exit before the mandatory notice period.
    /// Subject to manager/HR approval — does NOT bypass validation automatically.
    /// </summary>
    public bool NoticePeriodWaiver { get; set; } = false;

    /// <summary>
    /// Required when NoticePeriodWaiver is true.
    /// </summary>
    [MaxLength(1000)]
    public string? WaiverReason { get; set; }

    /// <summary>
    /// The earliest date the employee wishes to be relieved if waiver is approved.
    /// Must be before ProposedLastWorkingDate.
    /// </summary>
    public DateTime? RequestedEarlyExitDate { get; set; }

    // ── New: LWD negotiation ──────────────────────────────────────────────

    /// <summary>
    /// Employee indicates willingness to negotiate last working date with manager.
    /// </summary>
    public bool LastWorkingDateFlexible { get; set; } = false;

    // ── New: Handover enrichment ──────────────────────────────────────────

    /// <summary>
    /// Resolved display name of the handover buddy (stored for HR records,
    /// actual FK lookup remains via HandoverBuddyId).
    /// </summary>
    [MaxLength(200)]
    public string? HandoverBuddyName { get; set; }

    /// <summary>
    /// Whether the employee is willing to train their replacement during notice period.
    /// </summary>
    public bool WillingToTrain { get; set; } = false;

    /// <summary>
    /// Active projects and recurring responsibilities to be handed over.
    /// </summary>
    [MaxLength(1500)]
    public string? CurrentProjects { get; set; }

    /// <summary>
    /// Urgent items needing immediate attention: credentials, escalations, deadlines.
    /// </summary>
    [MaxLength(1000)]
    public string? ImmediateHandoverNeeds { get; set; }
}
