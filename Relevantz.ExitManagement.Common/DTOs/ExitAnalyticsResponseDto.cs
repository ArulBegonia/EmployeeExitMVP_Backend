namespace Relevantz.ExitManagement.Common.DTOs;

public class ExitAnalyticsResponseDto
{
    public int TotalExits { get; set; }
    public int CompletedExits { get; set; }
    public int PendingExits { get; set; }

    public double AverageProcessingDays { get; set; }

    public int LowRiskCount { get; set; }
    public int MediumRiskCount { get; set; }
    public int HighRiskCount { get; set; }
    public int CriticalRiskCount { get; set; }

    public string? TopResignationReason { get; set; }
}