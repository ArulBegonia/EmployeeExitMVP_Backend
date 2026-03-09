using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class KtTaskDto : IValidatableObject
{
    [Required(ErrorMessage = "Task title is required.")]
    [StringLength(200, MinimumLength = 3,
        ErrorMessage = "Title must be between 3 and 200 characters.")]
    public string Title { get; set; } = default!;

    [StringLength(2000, ErrorMessage = "Description must not exceed 2000 characters.")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Task deadline is required.")]
    public DateTime Deadline { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (Deadline != default && Deadline.Date < DateTime.UtcNow.Date)
            yield return new ValidationResult(
                "Deadline cannot be in the past.",
                new[] { nameof(Deadline) });

        // Deadline should not be too far into future (e.g. 2 years max)
        if (Deadline != default && Deadline.Date > DateTime.UtcNow.Date.AddYears(2))
            yield return new ValidationResult(
                "Deadline cannot be more than 2 years in the future.",
                new[] { nameof(Deadline) });
    }
}
