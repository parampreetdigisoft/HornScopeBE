

using HornScope.Dtos.AiDto;
using HornScope.Dtos.CountryDto;
using HornScope.Models;
using static HornScope.Services.AIComputationService;

namespace HornScope.Common.Interface
{
    public interface IPdfGeneratorService
    {
        Task<byte[]> GenerateCountryDetailsPdf(AiCountrySummeryDto country, List<AiCountryPillarResponse> pillars, List<KpiChartItem> kpis, List<PeerCountryHistoryReportDto> peercountry, UserRole userRole);
        Task<byte[]> GeneratePillarDetailsPdf(AiCountryPillarResponse countryDetails, UserRole userRole);
        Task<byte[]> GenerateSelectedPillarsDetailsPdf(List<AiCountryPillarResponse> pillars, List<CountryPillarRankingResultDto> pillarRankings, UserRole userRole);
        Task<byte[]> GenerateAllCountriesDetailsPdf(List<AiCountrySummeryDto> countries, Dictionary<int, List<AiCountryPillarResponse>> pillars, List<KpiChartItem> kpis, UserRole userRole);
    }
}
