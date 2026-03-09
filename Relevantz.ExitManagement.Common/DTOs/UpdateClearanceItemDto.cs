using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class UpdateClearanceItemDto : IValidatableObject
{
    [Required(ErrorMessage = "Item ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Item ID must be a positive integer.")]
    public int ItemId { get; set; }

    [Required(ErrorMessage = "Cleared status is required.")]
    public bool IsCleared { get; set; }

    [StringLength(1000, ErrorMessage = "Remarks must not exceed 1000 characters.")]
    public string? Remarks { get; set; }

    public DateTime? ReturnedDate { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Pending due amount must be between 0 and 10,000,000.")]
    public decimal? PendingDueAmount { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (IsCleared && ReturnedDate.HasValue &&
            ReturnedDate.Value.Date > DateTime.UtcNow.Date)
            yield return new ValidationResult(
                "Returned date cannot be in the future.",
                new[] { nameof(ReturnedDate) });

        if (!IsCleared && PendingDueAmount.HasValue && PendingDueAmount.Value < 0)
            yield return new ValidationResult(
                "Pending due amount cannot be negative.",
                new[] { nameof(PendingDueAmount) });

        // ReturnedDate only makes sense when cleared
        if (!IsCleared && ReturnedDate.HasValue)
            yield return new ValidationResult(
                "Returned date should not be set when item is not cleared.",
                new[] { nameof(ReturnedDate) });
    }
}
