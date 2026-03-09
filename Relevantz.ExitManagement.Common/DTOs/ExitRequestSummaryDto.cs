namespace Relevantz.ExitManagement.Common.DTOs;

public class ExitRequestSummaryDto
{
    public int      Id                      { get; set; }
    public int      EmployeeId              { get; set; }   // ← needed by HRRehire.jsx
    public string   EmployeeName            { get; set; } = string.Empty;
    public string   EmployeeCode            { get; set; } = string.Empty;
    public string   Department              { get; set; } = string.Empty;
    public string   Status                  { get; set; } = string.Empty;
    public string   RiskLevel               { get; set; } = string.Empty;
    public int      RiskScore               { get; set; }
    public DateTime ProposedLastWorkingDate { get; set; }
    public DateTime SubmittedAt             { get; set; }
    public string   ResignationReason       { get; set; } = string.Empty;
    public bool     IsKtCompleted           { get; set; }
    public string?  RehireDecision          { get; set; }
    public string?  RehireRemarks           { get; set; }
    public DateTime? RehireDecisionDate     { get; set; }
    public DateTime? RehireBlockedUntil     { get; set; }
    public DateTime? CompletedDate          { get; set; }   // ← needed by HRRehire table
}
