using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class UpdateKtRequestDto
{
    [Required(ErrorMessage = "Exit request ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Exit request ID must be a positive integer.")]
    public int ExitRequestId { get; set; }

    [Required(ErrorMessage = "Completion status is required.")]
    public bool IsCompleted { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Successor employee ID must be a positive integer.")]
    public int? SuccessorEmployeeId { get; set; }

    [StringLength(1000, ErrorMessage = "Remarks must not exceed 1000 characters.")]
    public string? Remarks { get; set; }

    [MaxLength(20, ErrorMessage = "Cannot assign more than 20 KT tasks at once.")]
    public List<KtTaskDto>? Tasks { get; set; }
}
