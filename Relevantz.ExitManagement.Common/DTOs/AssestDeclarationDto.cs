using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class AssetDeclarationDto
{
    [Required(ErrorMessage = "Asset name is required.")]
    [StringLength(100, MinimumLength = 2,
        ErrorMessage = "Asset name must be between 2 and 100 characters.")]
    public string AssetName { get; set; } = default!;

    [Required(ErrorMessage = "Asset code is required.")]
    [StringLength(50, MinimumLength = 2,
        ErrorMessage = "Asset code must be between 2 and 50 characters.")]
    [RegularExpression(@"^[A-Za-z0-9\-_]+$",
        ErrorMessage = "Asset code may only contain letters, numbers, hyphens, and underscores.")]
    public string AssetCode { get; set; } = default!;
}
