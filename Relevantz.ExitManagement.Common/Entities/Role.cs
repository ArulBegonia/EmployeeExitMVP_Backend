using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.Entities;

public class Role
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    public string Name { get; set; } = default!;
}
