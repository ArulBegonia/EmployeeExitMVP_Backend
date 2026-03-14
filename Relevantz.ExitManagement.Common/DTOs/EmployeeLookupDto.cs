namespace Relevantz.ExitManagement.Common.DTOs;

/// <summary>
/// Minimal projection returned for handover buddy live search.
/// Intentionally limited — no salary, role, or contact info exposed.
/// </summary>
public class EmployeeLookupDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string Name         { get; set; } = string.Empty;
    public string? Department  { get; set; }
    public string? Designation { get; set; }
    public bool    IsActive    { get; set; }
}
