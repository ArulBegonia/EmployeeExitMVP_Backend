namespace Relevantz.ExitManagement.Common.Entities;

public class AuditLog
{
    public int Id { get; set; }
    public string Action { get; set; } = default!;
    public string PerformedBy { get; set; } = default!;
    public DateTime Timestamp { get; set; }
}