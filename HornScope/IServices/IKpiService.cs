using HornScope.Common.Models;
using HornScope.Dtos.ClientDto;
using HornScope.Dtos.CommonDto;
using HornScope.Dtos.kpiDto;
using HornScope.Enums;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface IKpiService
    {
        Task<PaginationResponse<GetAnalyticalLayerResultDto>> GetAnalyticalLayerResults(GetAnalyticalLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending);
        Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpi(int userId, UserRole role);
        Task<ResultResponseDto<List<AnalyticalLayer>>> GetAllKpiPillarMapping(int userId, UserRole role);
        Task<ResultResponseDto<List<AnalyticalLayerPillarMappingDTO>>> GetKPIDetailsByLayerID(int layerID);
        Task<ResultResponseDto<CompareProgramResponseDto>> ComparePrograms(CompareProgramsRequestDto c, int userId, UserRole role, bool applyPagination = true);
        Task<Tuple<string, byte[]>> ExportComparePrograms(CompareProgramsRequestDto request, int userId, UserRole role);
        Task<ResultResponseDto<GetMutiplekpiLayerResultsDto>> GetMutiplekpiLayerResults(GetMutiplekpiLayerRequestDto request, int userId, UserRole role, TieredAccessPlan userPlan = TieredAccessPlan.Pending);
        Task<ResultResponseDto<SummarizeKpiResponseDto>> SummarizeKpiPerformance(SummarizeKpiRequestDto request, int userId, UserRole role);

    }
}
