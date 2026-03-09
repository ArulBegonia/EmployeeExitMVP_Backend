using System.ComponentModel.DataAnnotations;
using Relevantz.ExitManagement.Common.Enums;

namespace Relevantz.ExitManagement.Common.DTOs;

public class RehireEligibilityRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Employee ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Employee ID must be a positive integer.")]
    public int EmployeeId { get; set; }

    [Required(ErrorMessage = "Rehire decision is required.")]
    [EnumDataType(typeof(RehireDecision),
        ErrorMessage = "Invalid rehire decision value.")]
    public RehireDecision Decision { get; set; }

    [StringLength(1000, ErrorMessage = "Remarks must not exceed 1000 characters.")]
    public string? Remarks { get; set; }

    [Range(1, 120, ErrorMessage = "Block duration must be between 1 and 120 months.")]
    public int? BlockMonths { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (Decision == RehireDecision.NotEligible && !BlockMonths.HasValue)
            yield return new ValidationResult(
                "Block duration (months) is required when decision is Not Eligible.",
                new[] { nameof(BlockMonths) });

        if (Decision != RehireDecision.NotEligible && BlockMonths.HasValue)
            yield return new ValidationResult(
                "Block duration should only be set when decision is Not Eligible.",
                new[] { nameof(BlockMonths) });
    }
}
