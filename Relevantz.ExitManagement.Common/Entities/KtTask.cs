using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.Entities;

public class KtTask
{
    public int Id { get; set; }

    [Required]
    public int ExitRequestId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = default!;

    [StringLength(2000)]
    public string? Description { get; set; }

    [Required]
    public DateTime Deadline { get; set; }

    public bool IsCompleted { get; set; } = false;

    [StringLength(2000)]
    public string? CompletionNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ExitRequest ExitRequest { get; set; } = default!;
}
