
using AssessmentPlatform.Dtos.AiDto;
using AssessmentPlatform.Models;
using HornScope.Common.Interface;
using HornScope.Dtos.AiDto;
using HornScope.Dtos.CountryDto;
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

        public Task<byte[]> GenerateCountryDetails(
            AiCountrySummeryDto country,
            List<AiCountryPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCountryHistoryReportDto> peercountry,
            UserRole userRole,
        HornScope.IServices.DocumentFormat format = HornScope.IServices.DocumentFormat.Pdf)
        {
             var result = format == HornScope.IServices.DocumentFormat.Docx
                ? _docx.GenerateCountryDetailsDocx(country, pillars, kpis, peercountry, userRole)
                : _pdf.GenerateCountryDetailsPdf(country, pillars, kpis, peercountry, userRole);

            return result;
        }

        public Task<byte[]> GeneratePillarDetails(
            AiCountryPillarResponse pillarData,
            UserRole userRole,
            HornScope.IServices.DocumentFormat format = HornScope.IServices.DocumentFormat.Pdf)
            => format == HornScope.IServices.DocumentFormat.Docx
                ? _docx.GeneratePillarDetailsDocx(pillarData, userRole)
                : _pdf.GeneratePillarDetailsPdf(pillarData, userRole);

        public Task<byte[]> GenerateSelectedPillarDetails(
            List<AiCountryPillarResponse> pillars,
            List<CountryPillarRankingResultDto> pillarRankings,
            UserRole userRole,
            HornScope.IServices.DocumentFormat format = HornScope.IServices.DocumentFormat.Pdf)
            => format == HornScope.IServices.DocumentFormat.Docx
                ? _docx.GenerateSelectedPillarsDetailsDocx(pillars, pillarRankings, userRole)
                : _pdf.GenerateSelectedPillarsDetailsPdf(pillars, pillarRankings, userRole);

        public Task<byte[]> GenerateAllCountriesDetails(
            List<AiCountrySummeryDto> countries,
            Dictionary<int, List<AiCountryPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole,
            HornScope.IServices.DocumentFormat format = HornScope.IServices.DocumentFormat.Pdf)
            => format == HornScope.IServices.DocumentFormat.Docx
                ? _docx.GenerateAllCountriesDetailsDocx(countries, pillarsDict, kpis, userRole)
                : _pdf.GenerateAllCountriesDetailsPdf(countries, pillarsDict, kpis, userRole);
    }
}
