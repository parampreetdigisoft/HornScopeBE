using HornScope.Common.Models;
using HornScope.Dtos.dashboard;
using HornScope.Models;

namespace HornScope.IServices
{
    public interface ISignalDashboardService
    {
        Task<ResultResponseDto<DashboardModeResponseDto>> GetPeaceStressTestDashboard(int countryID, int userId, UserRole userRole, int year);
        Task<ResultResponseDto<DashboardModeResponseDto>> GetEarlyWarningDashboard(int countryID, int userId, UserRole userRole, int year);
        Task<ResultResponseDto<DashboardModeResponseDto>> GetResilienceScorecard(int countryID, int userId, UserRole userRole, int year);
    }
}
