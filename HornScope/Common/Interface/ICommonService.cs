using HornScope.Common.Models.views;
using HornScope.Dtos.AssessmentDto;
using HornScope.Dtos.PillarDto;
using HornScope.Dtos.ProgramDto;
using HornScope.Models;

namespace HornScope.Common.Interface
{
    public interface ICommonService
    {
        Task<List<EvaluationProgramProgressResultDto>> GetProgramProgressAsync(int userId,int role, int climateProgramID = 0);
        Task<List<EvaluationProgramProgressHistoryResultDto>> GetProgramProgressHistoryAsync(int userId, int role);
        Task<List<GetProgramsProgressAdminDto>> GetProgramProgressForAdmin(int userId, int role);
        Task<List<ProgramRankingResultDto>> GetProgramRankings(int climateProgramID);
        Task<List<GetPillarDTO>> GetPillars();
        void ClearPillarCache();
        Task<List<GetAssessmentResponseDto>> GetUserDetailsAssignedToProgram(int climateProgramID = 0);
        Task<List<GetDashboardModeResult>> GetDashboardModeResults(int userId, int role, int dashboardModeID, int climateProgramID = 0);
    }
}
