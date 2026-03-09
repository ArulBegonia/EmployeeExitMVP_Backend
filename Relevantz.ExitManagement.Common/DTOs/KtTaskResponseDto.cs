namespace Relevantz.ExitManagement.Common.DTOs;

public class KtTaskResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime Deadline { get; set; }
    public bool IsCompleted { get; set; }
    public string? CompletionNotes { get; set; }
    public DateTime CreatedAt { get; set; }
}
