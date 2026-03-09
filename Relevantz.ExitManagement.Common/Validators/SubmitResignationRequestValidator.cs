using FluentValidation;
using Relevantz.ExitManagement.Common.DTOs;
using Relevantz.ExitManagement.Common.Enums;

namespace Relevantz.ExitManagement.Common.Validators
{
    public class SubmitResignationRequestValidator : AbstractValidator<SubmitResignationRequestDto>
    {
        public SubmitResignationRequestValidator()
        {
            RuleFor(x => x.ProposedLastWorkingDate)
                .GreaterThan(DateTime.UtcNow.Date)
                .WithMessage("Last working date must be in the future.");

            RuleFor(x => x.ReasonType)
                .IsInEnum()
                .WithMessage("Invalid resignation reason.");

            RuleFor(x => x.DetailedReason)
                .MaximumLength(500);

            RuleFor(x => x.DetailedReason)
                .NotEmpty()
                .When(x => x.ReasonType == ResignationReason.Other)
                .WithMessage("Detailed reason is required when selecting 'Other'.");
        }
    }
}