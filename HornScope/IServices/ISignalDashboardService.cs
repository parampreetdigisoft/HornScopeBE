using HornScope.Common.Models;
using HornScope.Dtos.dashboard;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface ISignalDashboardService
    {
        Task<ResultResponseDto<DashboardModeResponseDto>> GetAmbitionDeliveryIndexDashboard(int climateProgramID, int userId, UserRole userRole);
        Task<ResultResponseDto<DashboardModeResponseDto>> GetDiplomaticRiskDashboard(int climateProgramID, int userId, UserRole userRole);
        Task<ResultResponseDto<DashboardModeResponseDto>> GetReadinessScorecardDashboard(int climateProgramID, int userId, UserRole userRole);
    }
}
