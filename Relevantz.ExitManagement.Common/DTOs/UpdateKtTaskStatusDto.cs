using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class UpdateKtTaskStatusDto
{
    [Required(ErrorMessage = "Task ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Task ID must be a positive integer.")]
    public int TaskId { get; set; }

    [Required(ErrorMessage = "Completion status is required.")]
    public bool IsCompleted { get; set; }

    [StringLength(2000, ErrorMessage = "Completion notes must not exceed 2000 characters.")]
    public string? CompletionNotes { get; set; }
}
