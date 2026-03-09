using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Relevantz.ExitManagement.Core.IService;
using Relevantz.ExitManagement.Data.IRepository;
using Relevantz.ExitManagement.Common.Exceptions;

namespace Relevantz.ExitManagement.Core.Service;

public class DocumentService : IDocumentService
{
    private readonly IExitRequestRepository _repository;

    // ── Fix 1: use AppContext.BaseDirectory instead of GetCurrentDirectory ──
    // GetCurrentDirectory() returns the working dir which changes in production
    private readonly string LogoPath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "relevantz_logo.png");

    private readonly string HrSignaturePath =
        Path.Combine(AppContext.BaseDirectory, "Assets", "hr_signature.png");

    public DocumentService(IExitRequestRepository repository)
        => _repository = repository;

    private byte[] GenerateLetterDocument(
        string title,
        string employeeName,
        string effectiveDate,
        string paragraph1,
        string paragraph2)
    {
        // ── Fix 2: Remove duplicate QuestPDF.Settings.License — already set in Program.cs ──
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(50);

                page.Content().Column(column =>
                {
                    column.Spacing(15);

                    // ── Header: Logo + Date ──
                    column.Item().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            if (File.Exists(LogoPath))
                                col.Item().Width(180).Image(LogoPath);
                            else
                                col.Item()
                                    .Text("Relevantz Technologies")
                                    .FontSize(16).Bold()
                                    .FontColor("#27235C");
                        });

                        row.RelativeItem().AlignRight().Column(col =>
                        {
                            col.Item()
                                .Text("Date: " + DateTime.UtcNow.ToString("dd MMM yyyy"))
                                .FontSize(12).Bold()
                                .FontColor("#27235C");
                        });
                    });

                    column.Item().PaddingTop(10);

                    // ── Title ──
                    column.Item()
                        .AlignCenter()
                        .Text(title)
                        .FontSize(20).Bold()
                        .FontColor("#27235C");

                    column.Item()
                        .LineHorizontal(1)
                        .LineColor("#9D247D");

                    column.Item().PaddingTop(10);

                    // ── Greeting ──
                    column.Item()
                        .Text("To Whom It May Concern,")
                        .FontSize(13).Bold();

                    column.Item().PaddingTop(10);

                    // ── Body ──
                    column.Item()
                        .Text(paragraph1)
                        .FontSize(13).LineHeight(1.6f).Justify();

                    column.Item().PaddingTop(8);

                    column.Item()
                        .Text(paragraph2)
                        .FontSize(13).LineHeight(1.6f).Justify();

                    column.Item().PaddingTop(20);

                    // ── Closing ──
                    column.Item()
                        .Text("With Best Regards,")
                        .FontSize(13);

                    column.Item().PaddingTop(15);

                    column.Item().Column(col =>
                    {
                        if (File.Exists(HrSignaturePath))
                            col.Item().Height(50).Image(HrSignaturePath);

                        col.Item()
                            .Text("HR Department")
                            .Bold().FontColor("#27235C");

                        col.Item()
                            .Text("Relevantz Technologies Services India Private Limited");
                    });
                });
            });
        }).GeneratePdf();
    }

    // ── Relieving Letter ────────────────────────────────────────────────────
    public async Task<byte[]> GenerateRelievingLetterAsync(int exitRequestId)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(exitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        var employee = await _repository.GetEmployeeByIdForDocumentAsync(exitRequest.EmployeeId)
            ?? throw new NotFoundException("Employee not found.");

        var name          = $"{employee.FirstName} {employee.LastName}";
        var effectiveDate = exitRequest.ProposedLastWorkingDate.ToString("dd MMM yyyy");

        return GenerateLetterDocument(
            "RELIEVING LETTER",
            name,
            effectiveDate,
            $"This letter serves as formal confirmation that {name} has been relieved " +
            $"from the services of Relevantz Technologies effective {effectiveDate}.",
            $"During the tenure with our organization, {employee.FirstName} contributed " +
            "diligently and fulfilled assigned responsibilities professionally. " +
            "We sincerely appreciate the efforts and dedication shown and wish " +
            "continued success in all future endeavors.");
    }

    // ── Experience Letter ───────────────────────────────────────────────────
    public async Task<byte[]> GenerateExperienceLetterAsync(int exitRequestId)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(exitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        var employee = await _repository.GetEmployeeByIdForDocumentAsync(exitRequest.EmployeeId)
            ?? throw new NotFoundException("Employee not found.");

        var name          = $"{employee.FirstName} {employee.LastName}";
        var effectiveDate = exitRequest.ProposedLastWorkingDate.ToString("dd MMM yyyy");

        return GenerateLetterDocument(
            "EXPERIENCE LETTER",
            name,
            effectiveDate,
            $"This is to certify that {name} was employed with Relevantz Technologies " +
            $"and served the organization sincerely until {effectiveDate}.",
            $"During the period of employment, {employee.FirstName} demonstrated " +
            "professionalism, dedication, and competence in assigned duties. " +
            "We acknowledge the valuable contribution made and extend our best wishes " +
            "for continued professional growth and success.");
    }

    public async Task<byte[]> GenerateClearanceCertificateAsync(int exitRequestId)
    {
        var exitRequest = await _repository.GetExitRequestByIdAsync(exitRequestId)
            ?? throw new NotFoundException("Exit request not found.");

        var employee = await _repository.GetEmployeeByIdForDocumentAsync(exitRequest.EmployeeId)
            ?? throw new NotFoundException("Employee not found.");

        var name = $"{employee.FirstName} {employee.LastName}";

        return GenerateLetterDocument(
            "CLEARANCE CERTIFICATE",
            name,
            string.Empty,
            $"This is to certify that {name} was employed with Relevantz Technologies " +
            "and has completed all formalities related to the exit process.",
            $"{employee.FirstName} has no pending obligations or liabilities with the " +
            "company and is hereby cleared of all responsibilities. " +
            "This clearance certificate is issued for official purposes as required.");
    }
}
