

using HornScope.Dtos.AiDto;
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
        Task<byte[]> GenerateProgramDetailsDocx(
            AiProgramSummeryDto program,
            List<AiProgramPillarResponse> pillars,
            List<KpiChartItem> kpis,
            List<PeerProgramHistoryReportDto> peerPrograms,
            UserRole userRole);

        Task<byte[]> GeneratePillarDetailsDocx(
            AiProgramPillarResponse pillarData,
            UserRole userRole);

        Task<byte[]> GenerateAllProgramsDetailsDocx(
            List<AiProgramSummeryDto> programs,
            Dictionary<int, List<AiProgramPillarResponse>> pillarsDict,
            List<KpiChartItem> kpis,
            UserRole userRole);
    }
}
