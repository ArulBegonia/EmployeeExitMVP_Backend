using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.Entities;

public class Notification
{
    public int Id { get; set; }

    [Required]
    public int RecipientEmployeeId { get; set; }

    [Required]
    [StringLength(200)]
    public string Title { get; set; } = null!;

    [Required]
    [StringLength(2000)]
    public string Message { get; set; } = null!;

    public bool IsRead { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
