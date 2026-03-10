using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Relevantz.ExitManagement.Common.Enums;

namespace Relevantz.ExitManagement.Common.Entities;

public class ClearanceItem
{
    public int Id { get; set; }

    [Required]
    public int ExitRequestId { get; set; }

    [Required]
    [StringLength(100)]
    public string DepartmentName { get; set; } = default!;

    [Required]
    [StringLength(200)]
    public string ItemName { get; set; } = default!;

    public ClearanceStatus Status { get; set; } = ClearanceStatus.Pending;

    [StringLength(1000)]
    public string? Remarks { get; set; }

    // ✅ Keep as DateTime? to match datetime(6) DB column
    // Store as Unspecified Kind (no UTC conversion) via ExitService
    public DateTime? ReturnedDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    [Range(0, 10_000_000)]
    public decimal? PendingDueAmount { get; set; }

    public ExitRequest ExitRequest { get; set; } = default!;
}
