using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.DTOs;

public class LoginRequestDto
{
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [StringLength(256, ErrorMessage = "Email must not exceed 256 characters.")]
    public string Email { get; set; } = default!;

    [Required(ErrorMessage = "Password is required.")]
    [StringLength(128, MinimumLength = 6,
        ErrorMessage = "Password must be between 6 and 128 characters.")]
    public string Password { get; set; } = default!;
}
