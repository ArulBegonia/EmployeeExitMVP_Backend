namespace Relevantz.ExitManagement.Common.DTOs;

public class ClearanceItemResponseDto
{
    public int Id { get; set; }
    public string ItemName { get; set; } = default!;
    public string DepartmentName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Remarks { get; set; }

    // ✅ DateOnly? serializes as "2026-03-30" — no T00:00:00 suffix, no timezone risk
    public DateOnly? ReturnedDate { get; set; }

    public decimal? PendingDueAmount { get; set; }
}
