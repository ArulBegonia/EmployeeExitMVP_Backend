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

    // ✅ DateOnly avoids UTC timezone shifting (2026-03-25 stays 2026-03-25)
    public DateOnly? ReturnedDate { get; set; }

    [Range(0, 10_000_000, ErrorMessage = "Pending due amount must be between 0 and 10,000,000.")]
    public decimal? PendingDueAmount { get; set; }

    // ✅ LWD sent from client to validate return date against it
    public DateOnly? ProposedLastWorkingDate { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (IsCleared && ReturnedDate.HasValue)
        {
            // ✅ Return date must be on or before LWD — future dates within LWD are allowed
            if (ProposedLastWorkingDate.HasValue &&
                ReturnedDate.Value > ProposedLastWorkingDate.Value)
                yield return new ValidationResult(
                    $"Assets must be returned on or before the employee's Last Working Day ({ProposedLastWorkingDate.Value:dd/MM/yyyy}).",
                    new[] { nameof(ReturnedDate) });
        }

        if (!IsCleared && PendingDueAmount.HasValue && PendingDueAmount.Value < 0)
            yield return new ValidationResult(
                "Pending due amount cannot be negative.",
                new[] { nameof(PendingDueAmount) });

        if (!IsCleared && ReturnedDate.HasValue)
            yield return new ValidationResult(
                "Returned date should not be set when item is not cleared.",
                new[] { nameof(ReturnedDate) });
    }
}
