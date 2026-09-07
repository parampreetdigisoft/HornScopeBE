using HornScope.Common.Models;
using HornScope.Dtos.chatDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.PublicDto;

namespace HornScope.IServices
{
    public interface IPublicService
    {
        Task<ResultResponseDto<List<PartnerProgramResponseDto>>> GetAllPrograms();
        Task<ResultResponseDto<PartnerProgramFilterResponse>> GetPartnerProgramsFilterRecord();
        Task<ResultResponseDto<List<PillarResponseDto>>> GetAllPillarAsync();
        Task<PaginationResponse<PartnerProgramResponseDto>> GetPartnerPrograms(PartnerProgramRequestDto r);
        Task<ProgramResponse> GetProgramsAndPrograms_WithStaleSupport();
        Task<ResultResponseDto<List<PromotedPillarsResponseDto>>> GetPromotedPrograms();
        Task<ResultResponseDto<EmergingTrendsResult>> GetEmergingTrendsAndIssues();
        /// <summary>
        /// Fetches emerging trends from AI, enriches programs, and caches on success only.
        /// </summary>
        Task<bool> RefreshEmergingTrendsCacheAsync(int programCount, CancellationToken cancellationToken = default);
        Task<ResultResponseDto<PillarLiveSignalsResult>> GetPillarLiveSignals();
        Task<ResultResponseDto<ROSEWPublicDashboardDto>> GetResilienceScorecard();

    }
}
