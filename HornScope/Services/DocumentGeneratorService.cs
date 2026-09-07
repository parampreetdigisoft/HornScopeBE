
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Models;
using HornScope.Common.Interface;
using HornScope.Dtos.AiDto;
using HornScope.IServices;
using HornScope.Models;
using static HornScope.Services.AIComputationService;


namespace HornScope.Services
{
    /// <summary>
    /// Facade that delegates to <see cref="PdfGeneratorService"/> or
    /// <see cref="DocxGeneratorService"/> based on the requested <see cref="DocumentFormat"/>.
    ///
    /// Register as: services.AddScoped&lt;IDocumentGeneratorService, DocumentGeneratorService&gt;()
    /// </summary>
    public sealed class DocumentGeneratorService : IDocumentGeneratorService
    {
        private readonly Common.Interface.IPdfGeneratorService _pdf;
        private readonly IDocxGeneratorService _docx;

        public DocumentGeneratorService(
            Common.Interface.IPdfGeneratorService pdf,
            IDocxGeneratorService docx)
        {
            _pdf = pdf;
            _docx = docx;
        }

        public Task<byte[]> GenerateProgramDetails(
            AiProgramSummeryDto program,
            List<AiProgramPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerProgramHistoryReportDto> peerProgram,
            UserRole userRole,
        HornScope.IServices.DocumentFormat format = HornScope.IServices.DocumentFormat.Pdf)
        {
             var result = format == HornScope.IServices.DocumentFormat.Docx
                ? _docx.GenerateProgramDetailsDocx(program, pillars, kpis, peerProgram, userRole)
                : _pdf.GenerateProgramDetailsPdf(program, pillars, kpis, peerProgram, userRole);

            return result;
        }

        public Task<byte[]> GeneratePillarDetails(
            AiProgramPillarResponse pillarData,
            UserRole userRole,
            HornScope.IServices.DocumentFormat format = HornScope.IServices.DocumentFormat.Pdf)
            => format == HornScope.IServices.DocumentFormat.Docx
                ? _docx.GeneratePillarDetailsDocx(pillarData, userRole)
                : _pdf.GeneratePillarDetailsPdf(pillarData, userRole);

        public Task<byte[]> GenerateAllProgramsDetails(
            List<AiProgramSummeryDto> programs,
            Dictionary<int, List<AiProgramPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole,
            HornScope.IServices.DocumentFormat format = HornScope.IServices.DocumentFormat.Pdf)
            => format == HornScope.IServices.DocumentFormat.Docx
                ? _docx.GenerateAllProgramsDetailsDocx(programs, pillarsDict, kpis, userRole)
                : _pdf.GenerateAllProgramsDetailsPdf(programs, pillarsDict, kpis, userRole);
    }
}
