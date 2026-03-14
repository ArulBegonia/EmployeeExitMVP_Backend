using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class UpdateKtRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Exit request ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Exit request ID must be a positive integer.")]
    public int ExitRequestId { get; set; }

    [Required(ErrorMessage = "Completion status is required.")]
    public bool IsCompleted { get; set; }

    public int? SuccessorEmployeeId { get; set; }

    [StringLength(1000, ErrorMessage = "Remarks must not exceed 1000 characters.")]
    public string? Remarks { get; set; }

    [MaxLength(20, ErrorMessage = "Cannot assign more than 20 KT tasks at once.")]
    public List<KtTaskDto>? Tasks { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (SuccessorEmployeeId.HasValue && SuccessorEmployeeId.Value <= 0)
        {
            yield return new ValidationResult(
                "Successor employee ID must be greater than 0.",
                new[] { nameof(SuccessorEmployeeId) });
        }
    }
}