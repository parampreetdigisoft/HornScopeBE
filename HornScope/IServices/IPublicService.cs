using HornScope.Common.Models;
using HornScope.Dtos.chatDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.PublicDto;

namespace HornScope.IServices
{
    public interface IPublicService
    {
        Task<ResultResponseDto<List<PartnerCountryResponseDto>>> GetAllCountries();
        Task<ResultResponseDto<List<PillarResponseDto>>> GetAllPillarAsync();
        Task<CountryCityResponse> GetCountriesAndCountries_WithStaleSupport();
        Task<ResultResponseDto<List<PromotedPillarsResponseDto>>> GetPromotedCountries();
        Task<ResultResponseDto<EmergingTrendsResult>> GetEmergingTrendsAndIssues();
        Task<bool> RefreshEmergingTrendsCacheAsync(int countryCount, CancellationToken cancellationToken = default);
        Task<ResultResponseDto<PillarLiveSignalsResult>> GetPillarLiveSignals();
        Task<ResultResponseDto<OverallHornscopeResponse>> GetOverAllHornscopeScore();

    }
}
