

using HornScope.Dtos.AiDto;
using HornScope.Models;
using static HornScope.Services.AIComputationService;

namespace HornScope.Common.Interface
{
    public interface IPdfGeneratorService
    {
        Task<byte[]> GenerateProgramDetailsPdf(AiProgramSummeryDto program, List<AiProgramPillarResponse> pillars, List<KpiChartItem> kpis, List<PeerProgramHistoryReportDto> peerPrograms, UserRole userRole);
        Task<byte[]> GeneratePillarDetailsPdf(AiProgramPillarResponse programDetails, UserRole userRole);
        Task<byte[]> GenerateAllProgramsDetailsPdf(List<AiProgramSummeryDto> programs, Dictionary<int, List<AiProgramPillarResponse>> pillars, List<KpiChartItem> kpis, UserRole userRole);
    }
}
