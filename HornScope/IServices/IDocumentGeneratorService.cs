
using HornScope.Dtos.AiDto;
using HornScope.Models;
using static HornScope.Services.AIComputationService;

namespace HornScope.IServices
{
    /// <summary>
    /// Output format for document generation.
    /// PDF is the default; Docx produces an editable Word document.
    /// </summary>
    public enum DocumentFormat
    {
        Pdf,
        Docx
    }

    /// <summary>
    /// Unified document-generation service.
    /// Replaces direct calls to IPdfGeneratorService.
    /// Pass <see cref="DocumentFormat.Docx"/> to get a Word document instead of a PDF.
    /// </summary>
    public interface IDocumentGeneratorService
    {
        /// <summary>Full program report: dashboard, summary, pillars, peer comparison, trends, KPI dashboard.</summary>
        Task<byte[]> GenerateProgramDetails(
            AiProgramSummeryDto programDetails,
            List<AiProgramPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerProgramHistoryReportDto> peerPrograms,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);

        /// <summary>Single pillar detail report.</summary>
        Task<byte[]> GeneratePillarDetails(
            AiProgramPillarResponse pillarData,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);

        /// <summary>Combined report covering every program in the list.</summary>
        Task<byte[]> GenerateAllProgramsDetails(
            List<AiProgramSummeryDto> programs,
            Dictionary<int, List<AiProgramPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole,
            DocumentFormat format = DocumentFormat.Pdf);
    }
}
