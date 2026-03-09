namespace Relevantz.ExitManagement.Core.IService;
 
public interface IDocumentService
{
    Task<byte[]> GenerateRelievingLetterAsync(int exitRequestId);
    Task<byte[]> GenerateExperienceLetterAsync(int exitRequestId);
    Task<byte[]> GenerateClearanceCertificateAsync(int exitRequestId);
}
 