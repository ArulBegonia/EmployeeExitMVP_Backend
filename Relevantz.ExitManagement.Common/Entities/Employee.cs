using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Relevantz.ExitManagement.Common.Entities;

public class Employee
{
    public int Id { get; set; }

    [Required]
    [StringLength(20)]
    public string EmployeeCode { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = default!;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = default!;

    [Required]
    [EmailAddress]
    [StringLength(256)]
    public string Email { get; set; } = default!;

    [Required]
    [StringLength(256)]
    public string Password { get; set; } = default!;

    [Required]
    public int RoleId { get; set; }

    public int? L1ManagerId { get; set; }
    public int? L2ManagerId { get; set; }

    public bool IsActive { get; set; } = true;

    [StringLength(100)]
    public string? Department { get; set; }

    public ICollection<ExitRequest>? ExitRequests { get; set; }
}
