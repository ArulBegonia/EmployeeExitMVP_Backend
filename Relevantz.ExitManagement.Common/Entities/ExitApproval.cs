using System.ComponentModel.DataAnnotations;
using Relevantz.ExitManagement.Common.Enums;

namespace Relevantz.ExitManagement.Common.Entities;

public class ExitApproval
{
    public int Id { get; set; }

    [Required]
    public int ExitRequestId { get; set; }

    [Required]
    public int ApproverId { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    [StringLength(1000)]
    public string? Remarks { get; set; }

    public DateTime? ActionDate { get; set; }

    public ExitRequest ExitRequest { get; set; } = default!;
}
