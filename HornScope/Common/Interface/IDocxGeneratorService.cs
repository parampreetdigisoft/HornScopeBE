

using HornScope.Dtos.AiDto;
using HornScope.Dtos.CountryDto;
using HornScope.Models;
using static HornScope.Services.AIComputationService;

namespace HornScope.Common.Interface
{
    /// <summary>
    /// Low-level Word document generation contract.
    /// Consumed by <see cref="DocumentGeneratorService"/>;
    /// controllers should depend on <see cref="IDocumentGeneratorService"/> instead.
    /// </summary>
    public interface IDocxGeneratorService
    {
        Task<byte[]> GenerateCountryDetailsDocx(
            AiCountrySummeryDto country,
            List<AiCountryPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerCountryHistoryReportDto> peerCountries,
            UserRole userRole);

        Task<byte[]> GeneratePillarDetailsDocx(
            AiCountryPillarResponse pillarData,
            UserRole userRole);

        Task<byte[]> GenerateSelectedPillarsDetailsDocx(
            List<AiCountryPillarResponse> pillars,
            List<CountryPillarRankingResultDto> pillarRankings,
            UserRole userRole);

        Task<byte[]> GenerateAllCountriesDetailsDocx(
            List<AiCountrySummeryDto> countries,
            Dictionary<int, List<AiCountryPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole);
    }
}
