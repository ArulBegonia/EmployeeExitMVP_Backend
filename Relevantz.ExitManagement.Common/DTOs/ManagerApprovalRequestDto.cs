using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class ManagerApprovalRequestDto : IValidatableObject
{
    [Required(ErrorMessage = "Exit request ID is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Exit request ID must be a positive integer.")]
    public int ExitRequestId { get; set; }

    [Required(ErrorMessage = "Approval decision is required.")]
    public bool IsApproved { get; set; }

    [StringLength(1000, ErrorMessage = "Remarks must not exceed 1000 characters.")]
    public string? Remarks { get; set; }

    [MaxLength(20, ErrorMessage = "Cannot assign more than 20 KT tasks at once.")]
    public List<KtTaskDto>? KtTasks { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext ctx)
    {
        if (!IsApproved && string.IsNullOrWhiteSpace(Remarks))
            yield return new ValidationResult(
                "Remarks are required when rejecting a request.",
                new[] { nameof(Remarks) });

        if (IsApproved && KtTasks != null)
        {
            for (int i = 0; i < KtTasks.Count; i++)
            {
                var task = KtTasks[i];

                if (string.IsNullOrWhiteSpace(task.Title))
                    yield return new ValidationResult(
                        $"KT task [{i + 1}]: Title is required.",
                        new[] { nameof(KtTasks) });

                if (task.Deadline == default)
                    yield return new ValidationResult(
                        $"KT task [{i + 1}]: Deadline is required.",
                        new[] { nameof(KtTasks) });

                if (task.Deadline != default && task.Deadline < DateTime.UtcNow.Date)
                    yield return new ValidationResult(
                        $"KT task [{i + 1}]: Deadline '{task.Deadline:dd MMM yyyy}' cannot be in the past.",
                        new[] { nameof(KtTasks) });
            }
        }
    }
}
