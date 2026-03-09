namespace Relevantz.ExitManagement.Common.DTOs;

public class ClearanceItemResponseDto
{
    public int Id { get; set; }
    public string ItemName { get; set; } = default!;
    public string DepartmentName { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string? Remarks { get; set; }
    public DateTime? ReturnedDate { get; set; }
    public decimal? PendingDueAmount { get; set; }
}
