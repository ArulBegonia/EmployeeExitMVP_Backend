namespace Relevantz.ExitManagement.Common.DTOs;

public class ExitStatusResponseDto
{
    public int ExitRequestId { get; set; }
    public string Status { get; set; } = default!;
    public DateTime ProposedLastWorkingDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public List<KtTaskResponseDto>? KtTasks { get; set; }
    public List<AssetDeclarationDto>? Assets { get; set; }
}
