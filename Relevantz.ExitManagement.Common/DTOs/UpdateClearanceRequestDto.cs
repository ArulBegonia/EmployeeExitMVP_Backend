using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class UpdateClearanceRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Exit request ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Exit request ID must be a positive integer.")]
    public int ExitRequestId { get; set; }

    [Required(ErrorMessage = "Department name is required.")]
    [StringLength(100, MinimumLength = 2,
        ErrorMessage = "Department name must be between 2 and 100 characters.")]
    public string DepartmentName { get; set; } = default!;

    [Required(ErrorMessage = "Cleared status is required.")]
    public bool IsCleared { get; set; }

    [StringLength(1000, ErrorMessage = "Remarks must not exceed 1000 characters.")]
    public string? Remarks { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!IsCleared && string.IsNullOrWhiteSpace(Remarks))
            yield return new ValidationResult(
                "Remarks are required when marking clearance as not cleared.",
                new[] { nameof(Remarks) });
    }
}
