using System.ComponentModel.DataAnnotations;
using Relevantz.ExitManagement.Common.Enums;

namespace Relevantz.ExitManagement.Common.DTOs;

public class SubmitResignationRequestDto
{
    [Required]
    public DateTime ProposedLastWorkingDate { get; set; }

    [Required]
    public ResignationReason ReasonType { get; set; }

    public string? DetailedReason { get; set; }

    public int?    HandoverBuddyId { get; set; }
    public string? HandoverNotes   { get; set; }

    public List<AssetDeclarationDto>? Assets { get; set; }
}
