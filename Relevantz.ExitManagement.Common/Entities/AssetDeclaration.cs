using System.ComponentModel.DataAnnotations;

namespace Relevantz.ExitManagement.Common.Entities;

public class AssetDeclaration
{
    public int Id { get; set; }

    [Required]
    public int ExitRequestId { get; set; }

    [Required]
    [StringLength(100)]
    public string AssetName { get; set; } = default!;

    [StringLength(50)]
    public string? AssetCode { get; set; }

    public bool IsReturned { get; set; } = false;

    public ExitRequest ExitRequest { get; set; } = default!;
}
