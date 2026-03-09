using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class SettlementApprovalRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Exit request ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Exit request ID must be a positive integer.")]
    public int ExitRequestId { get; set; }

    [Required(ErrorMessage = "Approval decision is required.")]
    public bool IsApproved { get; set; }

    [StringLength(1000, ErrorMessage = "Remarks must not exceed 1000 characters.")]
    public string? Remarks { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!IsApproved && string.IsNullOrWhiteSpace(Remarks))
            yield return new ValidationResult(
                "Remarks are required when rejecting a settlement.",
                new[] { nameof(Remarks) });
    }
}
